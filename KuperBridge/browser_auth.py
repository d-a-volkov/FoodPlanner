"""Вход в Купер по телефону и SMS-коду через официальную страницу логина.

JS-челлендж ServicePipe и TLS-детекцию реального браузера обходит headless
Chromium (Playwright). Поток:

1. POST /session ``{"phone": "+7..."}``  → открываем web.kuper.ru/login,
   вводим телефон и жмём «Получить код в СМС». Ответ: ``code_sent``.
2. POST /session ``{"phone": "+7...", "code": "1234"}`` → вводим код,
   дожидаемся установки cookie ``_Instamart_session`` и возвращаем её.
   Дальше сессией работает обычный HTTP-клиент (curl_cffi), как и раньше.

Между шагами браузер держится открытым (требование флоу SMS), поэтому
допускается только один незавершённый вход. Повторный запрос кода закрывает
старый браузер и перезапускает вход заново. Селекторы вынесены в переменные
окружения ``KUPER_SELECTOR_*`` — их можно поправить без пересборки образа.
"""

from __future__ import annotations

import os
import re
import threading
import time
from typing import Dict, List, Optional

from fastapi import HTTPException

LOGIN_URL = os.environ.get("KUPER_LOGIN_URL", "https://web.kuper.ru/login")
HEADLESS = os.environ.get("KUPER_HEADLESS", "1").strip().lower() not in ("0", "false", "no", "")
CODE_TIMEOUT = float(os.environ.get("KUPER_CODE_TIMEOUT", "180"))
PAGE_TIMEOUT = float(os.environ.get("KUPER_PAGE_TIMEOUT", "90"))

PHONE_SELECTOR = os.environ.get(
    "KUPER_SELECTOR_PHONE",
    "input[type='tel'], input[name*='phone' i], input[placeholder*='телефон' i]",
)
CODE_SELECTOR = os.environ.get("KUPER_SELECTOR_CODE", "input:visible")
SEND_BTN_RE = os.environ.get("KUPER_SEND_BTN", "Получить код|Отправить код")
SUBMIT_BTN_RE = os.environ.get("KUPER_SUBMIT_BTN", "Войти|Продолжить|Далее|Подтвердить")


class _Pending:
    """Незавершённый вход: браузер ждёт код из СМС."""

    __slots__ = ("pw", "context", "page", "phone", "started_at")

    def __init__(self, pw, context, page, phone: str, started_at: float) -> None:
        self.pw = pw
        self.context = context
        self.page = page
        self.phone = phone
        self.started_at = started_at


_pending: Optional[_Pending] = None
_pending_lock = threading.Lock()


# -- управление незавершённым входом ---------------------------------------


def _close_pending() -> None:
    global _pending
    with _pending_lock:
        p = _pending
        _pending = None
    if p is None:
        return
    for closer in (p.context.close, p.pw.stop):
        try:
            closer()
        except Exception:
            pass


def _discard_browser(pw) -> None:
    """Гасит Playwright, если сессия не была сохранена в _pending.

    Без этого неудачная попытка оставляет запущенный event loop в потоке
    threadpool: следующий запрос, попавший в тот же поток, падает с
    "Sync API inside the asyncio loop", а процессы Chromium копятся.
    """
    if pw is None:
        return
    try:
        pw.stop()
    except Exception:
        pass


def _watchdog() -> None:
    while True:
        with _pending_lock:
            p = _pending
            if p is None:
                return
            expired = time.time() - p.started_at > CODE_TIMEOUT
        if expired:
            _close_pending()
            return
        time.sleep(5)


# -- работа со страницей ----------------------------------------------------


def _phone_input(page):
    loc = page.locator(PHONE_SELECTOR)
    for i in range(loc.count()):
        el = loc.nth(i)
        if el.is_visible() and el.is_enabled():
            return el
    loc = page.locator("input:visible")
    for i in range(loc.count()):
        el = loc.nth(i)
        if el.is_enabled():
            return el
    raise HTTPException(500, "Не удалось найти поле ввода телефона на странице входа Купера.")


def _first_visible(locator):
    for i in range(locator.count()):
        el = locator.nth(i)
        if el.is_visible():
            return el
    raise HTTPException(500, "Элемент не найден на странице входа Купера.")


def _fill_code(page, code: str, phone: str) -> None:
    """Заполнить код: отдельные OTP-поля по цифрам или одно поле целиком."""
    digits = list(code)
    boxes = page.locator("input[maxlength='1']")
    visible_boxes = [b for b in boxes.all() if b.is_visible()]
    if len(visible_boxes) >= len(digits):
        for i, box in enumerate(visible_boxes[: len(digits)]):
            box.fill(digits[i])
        page.wait_for_timeout(300)
        return

    candidates = page.locator(CODE_SELECTOR)
    chosen = None
    for i in range(candidates.count()):
        el = candidates.nth(i)
        if not el.is_visible():
            continue
        try:
            maxlen = int(el.get_attribute("maxlength") or 0)
        except (TypeError, ValueError):
            maxlen = 0
        auto = el.get_attribute("autocomplete") or ""
        value = (el.input_value() or "").strip().replace("+", "")
        if value == phone.replace("+", ""):
            continue
        if "one-time-code" in auto:
            chosen = el
            break
        if maxlen >= len(code):
            chosen = el
            break
    if chosen is None:
        for i in range(candidates.count() - 1, -1, -1):
            el = candidates.nth(i)
            if not el.is_visible():
                continue
            if (el.get_attribute("type") or "").lower() in ("tel", "text", "number"):
                chosen = el
                break
    if chosen is None:
        raise HTTPException(401, "Не удалось найти поле для кода из СМС на странице входа.")
    chosen.fill(code)
    page.wait_for_timeout(300)


def _submit(page) -> None:
    btn = page.get_by_role("button", name=re.compile(SUBMIT_BTN_RE, re.I))
    if btn.count():
        btn.first.click(timeout=15000)
    else:
        page.keyboard.press("Enter")


def _cookie_string(cookies: List[Dict[str, str]]) -> str:
    seen: set = set()
    parts: List[str] = []
    for c in cookies:
        name = c.get("name", "")
        if not name or name in seen:
            continue
        seen.add(name)
        parts.append(f"{name}={c.get('value', '')}")
    return "; ".join(parts)


def _wait_session(page, context) -> str:
    deadline = time.time() + 60
    while time.time() < deadline:
        try:
            body = page.inner_text("body")[:400].replace("\n", " ")
        except Exception:
            body = ""
        if re.search(r"неверн|неправильн|не подош|попробуйте ещё|код истек", body, re.I):
            raise HTTPException(401, "Неверный или устаревший код из СМС. Запросите новый.")
        cookies = context.cookies()
        if any(c.get("name") == "_Instamart_session" for c in cookies):
            return _cookie_string(cookies)
        page.wait_for_timeout(700)
    raise HTTPException(408, "Превышено ожидание входа в Купер (60 с).")


def _finish_login(p: _Pending, code: str) -> str:
    page, context = p.page, p.context
    _fill_code(page, code, p.phone)
    _submit(page)
    return _wait_session(page, context)


# -- публичный API -----------------------------------------------------------


def request_code(phone: str) -> dict:
    """Шаг 1: запросить SMS-код. Открывает браузер и держит его до ввода кода."""
    global _pending
    phone = phone.strip()
    if not re.fullmatch(r"\+?[0-9]{10,15}", phone):
        raise HTTPException(400, "Некорректный номер телефона. Пример: +79991234567")
    with _pending_lock:
        busy = _pending is not None
    if busy:
        # повторная отправка кода: закрываем старый браузер и начинаем заново
        _close_pending()

    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        raise HTTPException(
            500,
            "В kuper-bridge не установлен Playwright. "
            "Используйте образ mcr.microsoft.com/playwright/python.",
        )

    pw = None
    try:
        pw = sync_playwright().start()
        browser = pw.chromium.launch(
            headless=HEADLESS,
            args=["--no-sandbox", "--disable-blink-features=AutomationControlled"],
        )
        context = browser.new_context(locale="ru-RU", timezone_id="Europe/Moscow")
        page = context.new_page()
    except Exception as exc:
        if pw is not None:
            try:
                pw.stop()
            except Exception:
                pass
        raise HTTPException(500, f"Не удалось запустить браузер для входа в Купер: {exc}")

    try:
        page.goto(LOGIN_URL, wait_until="domcontentloaded", timeout=60000)
        phone_input = _phone_input(page)
        phone_input.wait_for(state="visible", timeout=PAGE_TIMEOUT * 1000)
        phone_input.fill(phone)
        send_btn = page.get_by_role("button", name=re.compile(SEND_BTN_RE, re.I))
        send_btn.first.wait_for(state="visible", timeout=15000)
        send_btn.first.click()
        page.wait_for_timeout(1500)
    except HTTPException as exc:
        _discard_browser(pw)
        raise exc
    except Exception as exc:
        _discard_browser(pw)
        raise HTTPException(500, f"Не удалось отправить SMS-код Купера: {exc}")

    with _pending_lock:
        _pending = _Pending(pw, context, page, phone, time.time())
    threading.Thread(target=_watchdog, daemon=True).start()
    return {"status": "code_sent", "phone": phone}


def submit_code(code: str) -> str:
    """Шаг 2: подтвердить код из СМС и получить строку cookie сессии Купера."""
    code = code.strip()
    if not code.isdigit() or not (4 <= len(code) <= 8):
        raise HTTPException(400, "Код из СМС должен содержать от 4 до 8 цифр.")
    with _pending_lock:
        p = _pending
    if p is None:
        raise HTTPException(410, "Запрос кода не найден или истёк. Запросите код заново.")

    error: Optional[HTTPException] = None
    cookie = ""
    try:
        cookie = _finish_login(p, code)
    except HTTPException as exc:
        error = exc
    except Exception as exc:
        error = HTTPException(500, f"Ошибка подтверждения кода: {exc}")
    finally:
        _close_pending()
    if error is not None:
        raise error
    return cookie