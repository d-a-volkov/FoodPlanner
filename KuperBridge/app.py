"""kuper-bridge: HTTP-прокси к неофициальному API Купера через curl_cffi.

Вход в аккаунт — по телефону и SMS-коду через официальную страницу логина
(headless Chromium, см. browser_auth). Либо классическая cookie из браузера.
Cookie хранится только в файле состояния (внутренний том) и не логируется.

Состояние сессии держится между перезапусками в JSON-файле
(путь — переменная окружения KUPER_STATE, по умолчанию ./kuper_state.json).
"""

from __future__ import annotations

import json
import os
import threading
import time
from typing import Dict, List, Optional

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from kuper_api import Client
from kuper_api.exceptions import (
    AntiBotChallenge,
    BadRequest,
    KuperError,
    NetworkError,
    NotFound,
    Unauthorized,
    UnprocessableEntity,
)
from kuper_api.order.order import Order

import browser_auth
import matcher

STATE_FILE = os.environ.get("KUPER_STATE", "./kuper_state.json")
IMPERSONATE = os.environ.get("KUPER_IMPERSONATE", "chrome131")
DEFAULT_LAT = 55.7558
DEFAULT_LON = 37.6173

app = FastAPI(title="kuper-bridge", version="0.2.0")


class SessionSetup(BaseModel):
    cookie: Optional[str] = None
    phone: Optional[str] = None
    code: Optional[str] = None
    lat: float = DEFAULT_LAT
    lon: float = DEFAULT_LON


class StoreSelect(BaseModel):
    store_id: int


class ResolveItem(BaseModel):
    name: str = Field(..., min_length=1)
    amount: float = 1.0
    unit: str = "pieces"


class ResolveRequest(BaseModel):
    items: List[ResolveItem] = Field(..., min_items=1, max_items=100)


class CartItem(BaseModel):
    product_id: int
    quantity: int = Field(1, ge=1, le=99)


class CartRequest(BaseModel):
    items: List[CartItem] = Field(..., min_items=1, max_items=200)


class Session:
    """Состояние сессии Купера."""

    def __init__(self) -> None:
        self.client: Optional[Client] = None
        self.profile: dict = {}
        self.stores: List[dict] = []
        self.active_store_id: Optional[int] = None
        self.history_ids: set = set()
        self.history_names: Dict[int, str] = {}
        self.history_refreshed_at: Optional[float] = None
        self._lock = threading.Lock()

    # -- персистентность ---------------------------------------------------
    def load(self) -> None:
        try:
            with open(STATE_FILE, encoding="utf-8") as fh:
                data = json.load(fh)
        except (FileNotFoundError, json.JSONDecodeError):
            return
        cookie = data.get("cookie", "")
        if not cookie:
            return
        self.client = Client(cookie=cookie, impersonate=IMPERSONATE)
        lat = data.get("lat", DEFAULT_LAT)
        lon = data.get("lon", DEFAULT_LON)
        self.active_store_id = data.get("active_store_id")
        # историю подтягиваем по требованию (/history/refresh или /resolve)

    def save(self) -> None:
        with self._lock:
            cookie = self._cookie_storage
            payload = {
                "cookie": cookie,
                "lat": DEFAULT_LAT,
                "lon": DEFAULT_LON,
                "active_store_id": self.active_store_id,
                "saved_at": time.time(),
            }
        try:
            os.makedirs(os.path.dirname(STATE_FILE) or ".", exist_ok=True)
            with open(STATE_FILE, "w", encoding="utf-8") as fh:
                json.dump(payload, fh, ensure_ascii=False)
        except OSError as exc:
            raise HTTPException(500, f"Не удалось сохранить состояние: {exc}")

    @property
    def _cookie_storage(self) -> str:
        return self.client._request.cookie if self.client else ""


_state = Session()


# -- вспомогательные -------------------------------------------------------


def _serialize_product(product) -> dict:
    data = product.to_dict() if hasattr(product, "to_dict") else dict(product)
    images = data.get("images") or []
    image_url = ""
    for img in images:
        if isinstance(img, dict):
            image_url = img.get("url_thumb") or img.get("url") or ""
            break
        image_url = getattr(img, "url_thumb", None) or getattr(img, "url", "") or ""
        if image_url:
            break
    return {
        "product_id": int(data.get("id", 0)),
        "name": data.get("name", ""),
        "sku": data.get("sku"),
        "price": float(data.get("price") or 0),
        "original_price": float(data.get("original_price") or 0) or None,
        "discount": float(data.get("discount") or 0) or None,
        "human_volume": data.get("human_volume"),
        "price_type": data.get("price_type"),
        "store_id": data.get("store_id"),
        "permalink": data.get("permalink"),
        "image_url": image_url,
        "available": bool(getattr(product, "available", True)),
    }


def _serialize_store(store) -> dict:
    data = store.to_dict()
    retailer = data.get("retailer") or {}
    location = data.get("location") or {}
    return {
        "store_id": int(data.get("id", 0)),
        "name": data.get("name", ""),
        "retailer_slug": retailer.get("slug") if isinstance(retailer, dict) else getattr(retailer, "slug", None),
        "retailer_name": retailer.get("name") if isinstance(retailer, dict) else getattr(retailer, "name", None),
        "min_order_amount": data.get("min_order_amount"),
        "delivery_min": data.get("estimate_minutes_min"),
        "delivery_max": data.get("estimate_minutes_max"),
    }


def _require_session() -> Client:
    if not _state.client:
        raise HTTPException(409, "Сессия не настроена: войдите в Купер по телефону.")
    return _state.client


def _require_store() -> int:
    if not _state.active_store_id:
        raise HTTPException(409, "Магазин не выбран.")
    return _state.active_store_id


def _map_error(exc: Exception) -> HTTPException:
    if isinstance(exc, Unauthorized):
        return HTTPException(401, "Сессия не авторизована. Войдите в Купер заново по телефону.")
    if isinstance(exc, AntiBotChallenge):
        return HTTPException(423, "Kuper заблокировал IP анти-ботом (VPN/дата-центр). Нужен чистый IP.")
    if isinstance(exc, (NotFound, BadRequest, UnprocessableEntity)):
        return HTTPException(400, str(exc))
    if isinstance(exc, NetworkError):
        return HTTPException(502, str(exc))
    return HTTPException(502, f"Ошибка Kuper: {exc}")


def _refresh_history(client: Client) -> None:
    try:
        orders: List[Order] = client.orders(previous=True)
    except KuperError:
        return
    ids: set = set()
    names: Dict[int, str] = {}
    for order in orders:
        for shipment in order.shipments or []:
            for item in shipment.line_items or []:
                oid = getattr(item, "offer_id", None)
                nm = getattr(item, "name", "")
                if oid:
                    ids.add(int(oid))
                    names[int(oid)] = nm
    _state.history_ids = ids
    _state.history_names = names
    _state.history_refreshed_at = time.time()
    try:
        _state.save()
    except Exception:
        pass


# -- API -------------------------------------------------------------------


@app.get("/health")
def health():
    has_cookie = bool(getattr(_state, "client", None))
    return {
        "ok": True,
        "service": "kuper-bridge",
        "session": {
            "has_cookie": has_cookie,
            "profile": bool(_state.profile),
            "store_selected": bool(_state.active_store_id),
            "store_id": _state.active_store_id,
            "history_items": len(_state.history_ids),
            "history_refreshed_at": _state.history_refreshed_at,
        },
    }


@app.post("/session")
def setup_session(payload: SessionSetup):
    if payload.phone and not payload.code:
        return browser_auth.request_code(payload.phone)
    if payload.phone and payload.code:
        cookie = browser_auth.submit_code(payload.code)
        return _finalize_session(cookie, payload.lat, payload.lon)
    if payload.cookie:
        return _finalize_session(payload.cookie.strip(), payload.lat, payload.lon)
    raise HTTPException(400, "Укажите cookie, либо телефон и код из СМС.")


def _finalize_session(cookie: str, lat: float, lon: float) -> dict:
    if not cookie:
        raise HTTPException(400, "Пустой cookie.")
    client = Client(cookie=cookie, impersonate=IMPERSONATE)
    try:
        profile = client.profile()
    except KuperError as exc:
        raise _map_error(exc)
    if not profile:
        raise HTTPException(401, "Не удалось получить профиль: сессия не авторизована.")
    stores = client.stores(lat, lon, per_page=50)
    _state.client = client
    pdata = profile.to_dict()
    _state.profile = {
        "id": pdata.get("id"),
        "fullname": pdata.get("fullname") or f"{pdata.get('first_name', '')} {pdata.get('last_name', '')}".strip(),
        "phone": str(pdata.get("phone", "") or ""),
        "email": pdata.get("email") or "",
    }
    _state.stores = [_serialize_store(s) for s in stores]
    _state.save()
    try:
        _refresh_history(client)
    except Exception:
        pass
    return {"profile": _state.profile, "stores": _state.stores}


@app.get("/session")
def get_session():
    if not _state.client:
        return {"connected": False, "stores": [], "profile": None}
    return {
        "connected": True,
        "profile": _state.profile,
        "stores": _state.stores,
        "store_id": _state.active_store_id,
    }


@app.delete("/session")
def delete_session():
    _state.client = None
    _state.profile = {}
    _state.stores = []
    _state.active_store_id = None
    _state.history_ids = set()
    _state.history_names = {}
    try:
        if os.path.exists(STATE_FILE):
            os.remove(STATE_FILE)
    except OSError:
        pass
    return {"ok": True}


@app.post("/session/store")
def select_store(payload: StoreSelect):
    _require_session()
    if not any(s["store_id"] == payload.store_id for s in _state.stores):
        # разрешаем выбор из свежего списка магазинов
        client = _state.client
        try:
            stores = client.stores(DEFAULT_LAT, DEFAULT_LON, per_page=50)
            _state.stores = [_serialize_store(s) for s in stores]
        except KuperError as exc:
            raise _map_error(exc)
        if not any(s["store_id"] == payload.store_id for s in _state.stores):
            raise HTTPException(400, "Магазин не найден в выдаче.")
    _state.active_store_id = payload.store_id
    _state.save()
    return {"ok": True, "store_id": payload.store_id}


@app.post("/history/refresh")
def refresh_history():
    client = _require_session()
    try:
        _refresh_history(client)
    except KuperError as exc:
        raise _map_error(exc)
    return {"ok": True, "items": len(_state.history_ids), "names": list(_state.history_names.values())[:20]}


@app.post("/resolve")
def resolve_items(payload: ResolveRequest):
    client = _require_session()
    store_id = _require_store()
    try:
        _refresh_history(client)
    except KuperError:
        pass

    results = []
    for item in payload.items:
        normalized = matcher.normalize_name(item.name)
        query = normalized if normalized else item.name.strip()
        try:
            products = client.products(store_id, query=query, per_page=12, sort="unit_price_asc")
        except NotFound:
            products = []
        except (KuperError, Unauthorized, AntiBotChallenge) as exc:
            raise _map_error(exc)
        candidates = [_serialize_product(p) for p in products]
        decision = matcher.pick_best(
            item.name,
            candidates,
            previously_bought_ids=_state.history_ids,
            previously_bought_names=_state.history_names,
        )
        results.append(
            {
                "source": {
                    "name": item.name,
                    "amount": item.amount,
                    "unit": item.unit,
                    "normalized": normalized,
                    "quantity": matcher.quantity_for(item.amount, item.unit),
                    "quantity_note": matcher.unit_note(item.unit),
                },
                **decision,
            }
        )
    return {"items": results}


@app.post("/cart")
def add_to_cart(payload: CartRequest):
    client = _require_session()
    store_id = _require_store()
    try:
        cart: Optional[Order] = client.cart()
    except KuperError as exc:
        raise _map_error(exc)
    order_number = getattr(cart, "number", None) if cart else None
    if not order_number:
        raise HTTPException(
            409,
            "Не удалось получить корзину Купера. Откройте https://web.kuper.ru, "
            "положите один любой товар в корзину и попробуйте снова.",
        )

    added = []
    failed = []
    for item in payload.items:
        try:
            client.line_item_add(order_number, store_id, item.product_id, item.quantity)
            added.append({"product_id": item.product_id, "quantity": item.quantity})
        except KuperError as exc:
            failed.append({"product_id": item.product_id, "quantity": item.quantity, "error": str(exc)})
    return {
        "order_number": order_number,
        "added": added,
        "failed": failed,
        "cart_url": "https://web.kuper.ru",
    }


@app.get("/cart")
def get_cart_summary():
    client = _require_session()
    try:
        cart = client.cart()
    except KuperError as exc:
        raise _map_error(exc)
    if not cart:
        return {"order_number": None, "items": []}
    data = cart.to_dict()
    line_items = []
    for shipment in data.get("shipments") or []:
        for it in shipment.get("line_items") or []:
            line_items.append(
                {
                    "product_id": it.get("offer_id"),
                    "name": it.get("name"),
                    "quantity": it.get("quantity"),
                    "price": it.get("price"),
                    "total": it.get("total"),
                }
            )
    return {
        "order_number": data.get("number"),
        "state": data.get("state"),
        "total": data.get("total"),
        "items": line_items,
    }


_state.load()