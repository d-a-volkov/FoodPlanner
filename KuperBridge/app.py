"""kuper-bridge: HTTP-прокси к неофициальному API Купера через curl_cffi.

Вход в аккаунт — по cookie из авторизованного браузера web.kuper.ru.
Cookie хранится только в файле состояния (внутренний том) и не логируется.

Состояние сессии держится между перезапусками в JSON-файле
(путь — переменная окружения KUPER_STATE, по умолчанию ./kuper_state.json).
"""

from __future__ import annotations

import json
import os
import threading
import time
from typing import Dict, List, Optional, Tuple

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

import matcher

from concurrent.futures import ThreadPoolExecutor

STATE_FILE = os.environ.get("KUPER_STATE", "./kuper_state.json")
IMPERSONATE = os.environ.get("KUPER_IMPERSONATE", "chrome131")
DEFAULT_LAT = 55.7558
DEFAULT_LON = 37.6173
# /api/v2/stores отдаёт магазины только в зоне доставки точки, поэтому
# одного центра города недостаточно: обходим соседние точки и объединяем.
STORES_PER_PAGE = 100
STORES_MAX_PAGES = 3
STORES_OFFSET = 0.045  # ~5 км
STORES_MAX_WORKERS = 9
#: Поиск товаров: страницы выдачи и число альтернатив в ответе.
PRODUCTS_PER_PAGE = 24
PRODUCTS_PAGES = 2
ALTERNATIVES_LIMIT = 10
#: Страница корзины Купера, куда ведёт кнопка после добавления.
KUPER_CART_URL = "https://web.kuper.ru/cart"
#: Порог совпадения для уточняющего поиска по запросу пользователя.
REFINE_MIN_OVERLAP = 0.01
#: Как часто обновлять историю покупок автоматически (секунды).
HISTORY_TTL_SECONDS = 600

app = FastAPI(title="kuper-bridge", version="0.4.4")


def _brief(exc: Exception) -> str:
    """Короткое читаемое сообщение об ошибке для показа в UI."""
    text = str(exc).strip() or exc.__class__.__name__
    return text[:300]


def _patch_order_parsing() -> None:
    """Заказы с address=null роняют kuper_api (AttributeError) - пропускаем их.

    История покупок нужна только для подсказок, поэтому битый заказ
    не должен ломать подбор товаров.
    """
    original = Order.de_json

    @classmethod
    def safe_de_json(cls, data, client=None, **kwargs):  # type: ignore[no-untyped-def]
        try:
            if not isinstance(data, dict) or not data.get("id"):
                return None
            if not data.get("address"):
                data = {**data, "address": {}}
            return original(data, client)
        except Exception:
            return None

    Order.de_json = safe_de_json  # type: ignore[assignment]


_patch_order_parsing()


class SessionSetup(BaseModel):
    cookie: Optional[str] = None
    lat: float = DEFAULT_LAT
    lon: float = DEFAULT_LON


class StoreSelect(BaseModel):
    store_id: int


class ResolveItem(BaseModel):
    name: str = Field(..., min_length=1)
    amount: float = 1.0
    unit: str = "pieces"


class PreferItem(BaseModel):
    """Позиция списка предпочтений из истории покупок (id + имя)."""

    product_id: int = Field(..., gt=0)
    name: str = Field(..., min_length=1, max_length=300)
    times_bought: int = Field(0, ge=0)


class ResolveRequest(BaseModel):
    items: List[ResolveItem] = Field(..., min_items=1, max_items=100)
    #: Список предпочтений; None — взять сохранённую историю бриджа.
    prefer: Optional[List[PreferItem]] = Field(None, max_length=5000)


class CartItem(BaseModel):
    # Идентификаторы товаров Купера длинные (например, 30703909781),
    # обычный Python int подходит; проверяем только положительность.
    product_id: int = Field(..., gt=0)
    quantity: int = Field(1, ge=1, le=99)


class CartRequest(BaseModel):
    items: List[CartItem] = Field(..., min_items=1, max_items=200)


class SearchRequest(BaseModel):
    """Уточняющий поиск по одному товару."""

    query: str = Field(..., min_length=1, max_length=120)
    store_id: Optional[int] = None
    #: Список предпочтений; None — взять сохранённую историю бриджа.
    prefer: Optional[List[PreferItem]] = Field(None, max_length=5000)


class StoresRequest(BaseModel):
    """Запрос списка магазинов в точке или городе."""

    lat: float = DEFAULT_LAT
    lon: float = DEFAULT_LON
    wide: bool = True


class Session:
    """Состояние сессии Купера."""

    def __init__(self) -> None:
        self.client: Optional[Client] = None
        self.profile: dict = {}
        self.stores: List[dict] = []
        self.active_store_id: Optional[int] = None
        self.lat: float = DEFAULT_LAT
        self.lon: float = DEFAULT_LON
        self.history_ids: set = set()
        self.history_names: Dict[int, str] = {}
        self.history_items: List[dict] = []
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
        self.lat = lat
        self.lon = lon
        self.active_store_id = data.get("active_store_id")
        profile = data.get("profile")
        if isinstance(profile, dict) and profile:
            self.profile = profile
        stores = data.get("stores")
        if isinstance(stores, list):
            self.stores = [s for s in stores if isinstance(s, dict) and s.get("store_id")]
        # история переживает перезапуски: иначе подбор после рестарта
        # начинается с нуля до первого обновления
        history_ids = data.get("history_ids")
        if isinstance(history_ids, list):
            for x in history_ids:
                if not isinstance(x, (int, float)):
                    continue
                try:
                    self.history_ids.add(int(x))
                except (TypeError, ValueError):
                    continue
        history_names = data.get("history_names")
        if isinstance(history_names, dict):
            for k, v in history_names.items():
                if not isinstance(v, str) or not v:
                    continue
                try:
                    self.history_names[int(k)] = v
                except (TypeError, ValueError):
                    continue
        history_items = data.get("history_items")
        if isinstance(history_items, list):
            self.history_items = [
                item for item in history_items
                if isinstance(item, dict) and item.get("product_id")
            ]
        refreshed = data.get("history_refreshed_at")
        if isinstance(refreshed, (int, float)):
            self.history_refreshed_at = float(refreshed)

    def save(self) -> None:
        with self._lock:
            cookie = self._cookie_storage
            payload = {
                "cookie": cookie,
                "profile": self.profile,
                "lat": self.lat,
                "lon": self.lon,
                "active_store_id": self.active_store_id,
                "stores": self.stores,
                "history_ids": sorted(self.history_ids),
                "history_names": {str(k): v for k, v in self.history_names.items()},
                "history_items": self.history_items,
                "history_refreshed_at": self.history_refreshed_at,
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


def _order_data(order) -> dict:
    """Данные заказа/корзины.

    ``to_json`` берётся первым: ``to_dict`` у Order опирается на ``__dict__``
    и теряет вложенные ``shipments``, из-за чего корзина выглядела пустой.
    """
    if order is None:
        return {}
    for attr in ("to_json", "to_dict"):
        method = getattr(order, attr, None)
        if not callable(method):
            continue
        try:
            value = method()
        except Exception:  # noqa: BLE001 - приоритет у следующего способа
            continue
        if isinstance(value, str):
            try:
                value = json.loads(value)
            except ValueError:
                continue
        if isinstance(value, dict):
            return value
    return {}


def _serialize_product(product) -> dict:
    data = _order_data(product)
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
    if not isinstance(retailer, dict):
        retailer = getattr(retailer, "__dict__", {}) or {}
    if not isinstance(location, dict):
        location = getattr(location, "__dict__", {}) or {}
    address = data.get("address") or location.get("address") or {}
    if not isinstance(address, dict):
        address = getattr(address, "__dict__", {}) or {}
    address_text = (
        data.get("address_name")
        or address.get("name")
        or address.get("full_name")
        or ""
    )
    city = data.get("city") or location.get("city") or address.get("city") or ""
    return {
        "store_id": int(data.get("id", 0)),
        "name": data.get("name", ""),
        "retailer_slug": retailer.get("slug"),
        "retailer_name": retailer.get("name"),
        "address": address_text,
        "city": city if isinstance(city, str) else str(city),
        "lat": location.get("lat") or location.get("latitude"),
        "lon": location.get("lon") or location.get("longitude"),
        "min_order_amount": data.get("min_order_amount"),
        "delivery_min": data.get("estimate_minutes_min"),
        "delivery_max": data.get("estimate_minutes_max"),
    }


def _probe_points(lat: float, lon: float, wide: bool) -> List[tuple]:
    """Точки для обхода: сам город + 8 соседних (если wide)."""
    points = [(lat, lon, STORES_MAX_PAGES)]
    if not wide:
        return points
    for dlat in (-STORES_OFFSET, 0.0, STORES_OFFSET):
        for dlon in (-STORES_OFFSET, 0.0, STORES_OFFSET):
            if dlat or dlon:
                points.append((lat + dlat, lon + dlon, 1))
    return points


def _stores_at(client: Client, lat: float, lon: float, pages: int) -> List[dict]:
    out: List[dict] = []
    for page in range(1, pages + 1):
        try:
            res = client.stores(lat, lon, per_page=STORES_PER_PAGE, page=page)
        except KuperError:
            break
        except Exception:
            break
        if not res:
            break
        out.extend(_serialize_store(s) for s in res)
        if len(res) < STORES_PER_PAGE:
            break
    return out


def fetch_stores(client: Client, lat: float, lon: float, wide: bool = True) -> List[dict]:
    """Объединяет магазины из зон доставки нескольких точек."""
    points = _probe_points(lat, lon, wide)

    def work(point: tuple) -> List[dict]:
        return _stores_at(client, point[0], point[1], point[2])

    found: Dict[int, dict] = {}
    with ThreadPoolExecutor(max_workers=min(STORES_MAX_WORKERS, len(points))) as pool:
        for batch in pool.map(work, points):
            for store in batch:
                sid = store.get("store_id")
                if sid and sid not in found:
                    found[sid] = store
    return sorted(found.values(), key=lambda s: (s.get("name") or "").lower())


def _require_session() -> Client:
    if not _state.client:
        raise HTTPException(409, "Сессия не настроена: вставьте cookie из браузера Купера.")
    return _state.client


def _require_store() -> int:
    if not _state.active_store_id:
        raise HTTPException(409, "Магазин не выбран.")
    return _state.active_store_id


def _map_error(exc: Exception) -> HTTPException:
    if isinstance(exc, Unauthorized):
        return HTTPException(401, "Сессия не авторизована. Вставьте свежую cookie из браузера Купера.")
    if isinstance(exc, AntiBotChallenge):
        return HTTPException(423, "Kuper заблокировал IP анти-ботом (VPN/дата-центр). Нужен чистый IP.")
    if isinstance(exc, (NotFound, BadRequest, UnprocessableEntity)):
        return HTTPException(400, str(exc))
    if isinstance(exc, NetworkError):
        return HTTPException(502, str(exc))
    return HTTPException(502, f"Ошибка Kuper: {exc}")


def _raw_orders(client: Client) -> List[dict]:
    """Сырые заказы для истории покупок.

    ``/api/v2/orders/previous`` отдаёт ``{"order": {...}}``, а библиотечный
    ``orders(previous=True)`` ищет ключ ``orders`` и превращает ответ в
    ``[None]``. Разбор kuper_api здесь непригоден: у заказов нет ``id``,
    а товары лежат во вложенном ``line_item.product``. Читаем JSON напрямую.
    """
    raws: List[dict] = []
    for path in ("/api/v2/orders", "/api/v2/orders/previous"):
        try:
            data = client._request.get(path)
        except Exception:
            continue
        if isinstance(data, dict):
            for key in ("orders", "order"):
                value = data.get(key)
                if isinstance(value, list):
                    raws.extend(item for item in value if isinstance(item, dict))
                elif isinstance(value, dict):
                    raws.append(value)
        elif isinstance(data, list):
            raws.extend(item for item in data if isinstance(item, dict))

    orders: Dict[str, dict] = {}
    for raw in raws:
        number = raw.get("number")
        # Текущую корзину покупкой считать нельзя.
        if not number or raw.get("state") == "cart":
            continue
        existing = orders.get(number)
        # Предпочитаем ответ, в котором уже есть товары.
        if existing is None or (
            not _has_line_items(existing) and _has_line_items(raw)
        ):
            orders[number] = raw
    return list(orders.values())


def _has_line_items(order: dict) -> bool:
    return any(
        isinstance(shipment, dict) and shipment.get("line_items")
        for shipment in order.get("shipments") or []
    )


def _order_shipments(client: Client, order: dict) -> List[dict]:
    """Отгрузки заказа с товарами.

    Список ``/api/v2/orders`` приходит без ``line_items`` — по номеру
    заказа догружаем подробный ответ.
    """
    shipments = [
        shipment for shipment in order.get("shipments") or []
        if isinstance(shipment, dict)
    ]
    if not _has_line_items(order):
        number = order.get("number")
        if not number:
            return shipments
        try:
            data = client._request.get(f"/api/v2/orders/{number}")
        except Exception:
            return shipments
        detailed = data.get("order") if isinstance(data, dict) else None
        if isinstance(detailed, dict):
            shipments = [
                shipment for shipment in detailed.get("shipments") or []
                if isinstance(shipment, dict)
            ]
    return shipments


def _aggregate_history(
    orders: List[Tuple[dict, List[dict]]],
) -> Tuple[set, Dict[int, str], List[dict]]:
    """Агрегация сырых заказов в историю покупок (чистая функция)."""
    ids: set = set()
    names: Dict[int, str] = {}
    aggregated: Dict[int, dict] = {}
    for _order, shipments in orders:
        for shipment in shipments:
            window = shipment.get("delivery_window")
            when = window.get("starts_at") if isinstance(window, dict) else None
            for item in shipment.get("line_items") or []:
                if not isinstance(item, dict):
                    continue
                product = item.get("product")
                product = product if isinstance(product, dict) else {}
                pid_raw = product.get("id", item.get("offer_id"))
                try:
                    pid = int(pid_raw)
                except (TypeError, ValueError):
                    continue
                nm = product.get("name") or item.get("name") or ""
                ids.add(pid)
                if nm:
                    names[pid] = nm
                try:
                    quantity = int(item.get("quantity") or 0)
                except (TypeError, ValueError):
                    quantity = 0
                count = quantity if quantity > 0 else 1
                price = item.get("price", product.get("price"))
                price = price if isinstance(price, (int, float)) else None
                entry = aggregated.get(pid)
                if entry is None:
                    aggregated[pid] = {
                        "product_id": pid,
                        "name": nm,
                        "sku": product.get("sku") or item.get("sku"),
                        "human_volume": product.get("human_volume"),
                        "times_bought": count,
                        "last_bought_at": when if isinstance(when, str) else None,
                        "last_price": price,
                    }
                    continue
                entry["times_bought"] += count
                if nm:
                    entry["name"] = nm
                if product.get("sku") or item.get("sku"):
                    entry["sku"] = product.get("sku") or item.get("sku")
                if product.get("human_volume"):
                    entry["human_volume"] = product.get("human_volume")
                # Цена и дата — от самой свежей покупки; без даты берём
                # последнюю увиденную цену.
                known = entry.get("last_bought_at")
                if isinstance(when, str) and (not known or when > known):
                    entry["last_bought_at"] = when
                    if price is not None:
                        entry["last_price"] = price
                elif not known and price is not None:
                    entry["last_price"] = price
    return ids, names, sorted(
        aggregated.values(), key=lambda e: (-e["times_bought"], e["name"])
    )


def _refresh_history(client: Client) -> None:
    """История покупок — вспомогательные подсказки, ошибки не критичны."""
    try:
        shipments = [
            (order, _order_shipments(client, order))
            for order in _raw_orders(client)
        ]
        ids, names, items = _aggregate_history(shipments)
    except Exception:
        return
    _state.history_ids = ids
    _state.history_names = names
    _state.history_items = items
    _state.history_refreshed_at = time.time()
    try:
        _state.save()
    except Exception:
        pass


def _history_payload() -> dict:
    """Полезная нагрузка истории для /history и /history/refresh."""
    return {
        "refreshed_at": _state.history_refreshed_at,
        "count": len(_state.history_items),
        "products": _state.history_items,
    }


def _ensure_history(client: Client) -> None:
    """История обновляется по требованию, но не чаще HISTORY_TTL_SECONDS."""
    refreshed = _state.history_refreshed_at
    if refreshed and (time.time() - refreshed) < HISTORY_TTL_SECONDS:
        return
    _refresh_history(client)


# -- API -------------------------------------------------------------------


def _ensure_profile() -> dict:
    """Профиль аккаунта; подтягиваем при первом обращении, если его нет в состоянии."""
    if _state.profile:
        return _state.profile
    client = _require_session()
    try:
        profile = client.profile()
    except Exception:
        return {}
    if not profile:
        return {}
    data = profile.to_dict()
    _state.profile = {
        "id": data.get("id"),
        "fullname": data.get("fullname")
        or f"{data.get('first_name', '')} {data.get('last_name', '')}".strip(),
        "phone": str(data.get("phone", "") or ""),
        "email": data.get("email") or "",
    }
    try:
        _state.save()
    except Exception:
        pass
    return _state.profile


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
            "stores_count": len(_state.stores),
            "lat": _state.lat,
            "lon": _state.lon,
            "history_items": len(_state.history_ids),
            "history_refreshed_at": _state.history_refreshed_at,
        },
    }


@app.post("/session")
def setup_session(payload: SessionSetup):
    cookie = (payload.cookie or "").strip()
    if not cookie:
        raise HTTPException(400, "Вставьте cookie из авторизованного браузера Купера.")
    return _finalize_session(cookie, payload.lat, payload.lon)


def _finalize_session(cookie: str, lat: float, lon: float) -> dict:
    client = Client(cookie=cookie, impersonate=IMPERSONATE)
    try:
        profile = client.profile()
    except KuperError as exc:
        raise _map_error(exc)
    if not profile:
        raise HTTPException(401, "Не удалось получить профиль: сессия не авторизована.")
    stores = fetch_stores(client, lat, lon, wide=True)
    _state.client = client
    pdata = profile.to_dict()
    _state.profile = {
        "id": pdata.get("id"),
        "fullname": pdata.get("fullname") or f"{pdata.get('first_name', '')} {pdata.get('last_name', '')}".strip(),
        "phone": str(pdata.get("phone", "") or ""),
        "email": pdata.get("email") or "",
    }
    _state.lat = lat
    _state.lon = lon
    _state.stores = stores
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
        "profile": _ensure_profile(),
        "stores": _state.stores,
        "store_id": _state.active_store_id,
        "lat": _state.lat,
        "lon": _state.lon,
    }


@app.delete("/session")
def delete_session():
    _state.client = None
    _state.profile = {}
    _state.stores = []
    _state.active_store_id = None
    _state.history_ids = set()
    _state.history_names = {}
    _state.history_items = []
    _state.history_refreshed_at = None
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
        # разрешаем выбор из свежего списка магазинов по сохранённым координатам
        client = _state.client
        try:
            stores = fetch_stores(client, _state.lat, _state.lon, wide=True)
        except KuperError as exc:
            raise _map_error(exc)
        _state.stores = stores
        if not any(s["store_id"] == payload.store_id for s in _state.stores):
            raise HTTPException(400, "Магазин не найден в выдаче.")
    _state.active_store_id = payload.store_id
    _state.save()
    return {"ok": True, "store_id": payload.store_id}


@app.post("/stores")
def refresh_stores(payload: StoresRequest):
    """Перезагрузить список магазинов в другой точке/городе."""
    client = _require_session()
    _state.lat = payload.lat
    _state.lon = payload.lon
    try:
        stores = fetch_stores(client, payload.lat, payload.lon, wide=payload.wide)
    except KuperError as exc:
        raise _map_error(exc)
    _state.stores = stores
    if _state.active_store_id and not any(
        s["store_id"] == _state.active_store_id for s in _state.stores
    ):
        _state.active_store_id = None
    _state.save()
    return {
        "stores": _state.stores,
        "store_id": _state.active_store_id,
        "lat": _state.lat,
        "lon": _state.lon,
    }


@app.get("/history")
def get_history():
    """Полная история покупок (с обновлением не чаще раза в 10 минут)."""
    client = _require_session()
    _ensure_history(client)
    return _history_payload()


@app.post("/history/refresh")
def refresh_history():
    client = _require_session()
    _refresh_history(client)
    return {"ok": True, **_history_payload()}


def _search_candidates(client, store_id: int, raw_query: str) -> List[dict]:
    """Кандидаты из каталога магазина по произвольному запросу.

    Выдача сортируется по релевантности (popularity) и дополняется следующей
    страницей, чтобы альтернативы не обрывались на первых 24.
    """
    query = matcher.normalize_name(raw_query) or raw_query.strip()
    candidates: List[dict] = []
    for page in range(1, PRODUCTS_PAGES + 1):
        try:
            products = client.products(
                store_id, query=query, page=page, per_page=PRODUCTS_PER_PAGE, sort="popularity"
            )
        except NotFound:
            break
        except (KuperError, Unauthorized, AntiBotChallenge) as exc:
            raise _map_error(exc)
        if not products:
            break
        candidates.extend(_serialize_product(p) for p in products)
        if len(products) < PRODUCTS_PER_PAGE:
            break
    return candidates


def _prefer_items(payload: BaseModel) -> List[dict]:
    """Предпочтения из запроса; если их нет — сохранённая история бриджа."""
    if payload.prefer is not None:
        return [
            {"product_id": p.product_id, "name": p.name, "times_bought": p.times_bought}
            for p in payload.prefer
        ]
    if _state.history_items:
        return list(_state.history_items)
    return [
        {"product_id": pid, "name": nm, "times_bought": 0}
        for pid, nm in _state.history_names.items()
    ]


def _candidates_with_preferences(
    client, store_id: int, raw_query: str, prefer: List[dict]
):
    """Кандидаты каталога плюс точечный поиск сильных предпочтений.

    Двухфазный подбор: если запрос совпал с локальным списком предпочтений
    (фаза 1), а нужного товара в обычной выдаче нет — ищем его отдельным
    запросом по названию предпочтения (фаза 2). Возвращает
    (кандидаты, совпадения).
    """
    candidates = _search_candidates(client, store_id, raw_query)
    matched = matcher.match_preferences(raw_query, prefer)
    if not matched:
        return candidates, matched
    known = {c.get("product_id") for c in candidates}
    if any(m["product_id"] in known for m in matched):
        return candidates, matched
    for m in matched[: matcher.PREFER_LOOKUP_LIMIT]:
        try:
            extra = _search_candidates(client, store_id, m["name"])
        except HTTPException:
            continue
        for c in extra:
            if c.get("product_id") is not None and c["product_id"] not in known:
                candidates.append(c)
                known.add(c["product_id"])
    return candidates, matched


@app.post("/resolve")
def resolve_items(payload: ResolveRequest):
    client = _require_session()
    store_id = _require_store()
    prefer = _prefer_items(payload)

    results = []
    for item in payload.items:
        normalized = matcher.normalize_name(item.name)
        candidates, matched = _candidates_with_preferences(client, store_id, item.name, prefer)
        decision = matcher.pick_best(
            item.name,
            candidates,
            previously_bought_ids={p["product_id"] for p in prefer},
            previously_bought_names={p["product_id"]: p.get("name", "") for p in prefer},
            preferred_ids={m["product_id"] for m in matched} if matched else set(),
            alternatives_limit=ALTERNATIVES_LIMIT,
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


def _refine_decision(query: str, candidates: List[dict], prefer: List[dict]) -> dict:
    return matcher.pick_best(
        query,
        candidates,
        previously_bought_ids={p["product_id"] for p in prefer},
        previously_bought_names={p["product_id"]: p.get("name", "") for p in prefer},
        preferred_ids={
            m["product_id"] for m in matcher.match_preferences(query, prefer)
        },
        alternatives_limit=ALTERNATIVES_LIMIT,
        # Запрос написан пользователем и может быть фильтром («без сахара»),
        # поэтому любое совпадение уже подходит: ранжируем, а не отсекаем.
        min_overlap=REFINE_MIN_OVERLAP,
    )


@app.post("/search")
def search_products(payload: SearchRequest):
    """Уточняющий поиск по одному товару: «кофе растворимый 250 г»."""
    client = _require_session()
    store_id = payload.store_id or _require_store()
    query = payload.query.strip()
    prefer = _prefer_items(payload)

    candidates, _ = _candidates_with_preferences(client, store_id, query, prefer)
    decision = _refine_decision(query, candidates, prefer)
    fallback_token = None
    if not decision["product"]:
        # Уточнение бывает слишком узким для каталога («огурец засоленный»):
        # ищем по главному слову запроса и ранжируем выдачу по всему запросу.
        tokens = (matcher.normalize_name(query) or query).split()
        if tokens and tokens[0] != matcher.normalize_name(query):
            fallback_token = tokens[0]
            fb_candidates, _ = _candidates_with_preferences(
                client, store_id, fallback_token, prefer
            )
            decision = _refine_decision(query, fb_candidates, prefer)

    return {
        "query": query,
        "normalized": matcher.normalize_name(query),
        "fallback_token": fallback_token,
        **decision,
    }


def _cart_number(client, store_id: int) -> Optional[str]:
    """Номер активной корзины; создаём её, если её нет.

    Купер не создаёт корзину сам: без неё ``/api/multiretailer_order``
    возвращает последний заказ, в том числе уже завершённый, и товары
    добавляются в него вместо корзины — сайт такую корзину не показывает.
    """
    try:
        cart = client.cart()
    except Exception:
        cart = None
    data = _order_data(cart) if cart else {}
    number = data.get("number")
    # Состояние cart — единственная корзина; complete/shipped — это заказ.
    if number and data.get("state") == "cart":
        return number
    try:
        created = client._request.post("/api/v2/orders", {"store_id": store_id})
    except Exception as exc:
        raise HTTPException(502, f"Не удалось создать корзину Купера: {_brief(exc)}")
    number = ((created or {}).get("order") or {}).get("number")
    if not number:
        raise HTTPException(
            502,
            "Не удалось создать корзину Купера. Откройте https://web.kuper.ru/cart "
            "и обновите страницу, затем повторите.",
        )
    return number


@app.post("/cart")
def add_to_cart(payload: CartRequest):
    client = _require_session()
    store_id = _require_store()
    order_number = _cart_number(client, store_id)

    added = []
    failed = []
    for item in payload.items:
        try:
            client.line_item_add(order_number, store_id, item.product_id, item.quantity)
            added.append({"product_id": item.product_id, "quantity": item.quantity})
        except Exception as exc:
            failed.append(
                {
                    "product_id": item.product_id,
                    "quantity": item.quantity,
                    "error": _brief(exc),
                }
            )
    if not added and failed:
        detail = failed[0]["error"]
        raise HTTPException(502, f"Купер не принял ни один товар: {detail}")
    return {
        "order_number": order_number,
        "added": added,
        "failed": failed,
        "cart_url": KUPER_CART_URL,
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
    data = _order_data(cart)
    # Завершённый заказ Купер отдаёт на месте корзины — показывать его нельзя.
    if data.get("state") not in (None, "cart"):
        return {"order_number": None, "state": data.get("state"), "total": None, "items": []}
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