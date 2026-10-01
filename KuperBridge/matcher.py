"""Нормализация названий продуктов и ранжирование кандидатов для корзины Kuper.

Чистые функции без зависимостей от сети — можно покрывать юнит-тестами.
"""

from __future__ import annotations

import math
import re
from typing import Dict, List, Optional

#: Слова-добавки к названию (размер/упаковка/сорт), не влияющие на смысл.
NOISE_WORDS = {
    "шт", "штук", "штука", "уп", "упак", "упаковка", "пач", "пачка", "пакет",
    "коробка", "банка", "бутылка", "пленки", "кг", "г", "мл", "л", "гр",
    "грамм", "килограмм", "литр", "миллилитр", "граммов", "килограммов",
    "литров", "миллилитров", "полулитровый", "упакованный",
    "свежий", "свежая", "свежее", "свежие", "молочный", "молочная",
    "сливочный", "сливочная", "натуральный", "натуральная", "отборный",
    "отборная", "премиум", "климат", "контроль", "идеальный", "100",
}

#: Алиасы: общее название -> краткая форма (как в каталоге Купера).
ALIASES = {
    "творог": "тв", "творожок": "тв", "творожный": "тв",
    "молоко": "мол", "молочный": "мол",
    "куриное": "кур", "куриный": "кур", "куриная": "кур", "куриные": "кур",
    "филе": "фил",
    "грудки": "груд", "грудинка": "груд",
    "сыр": "сыр", "сырный": "сыр",
    "сметана": "смет",
    "кефир": "кеф",
    "йогурт": "йог",
    "ряженка": "ряж",
    "хлеб": "хлеб",
    "батон": "бат",
    "яйцо": "яиц", "яйца": "яиц", "яичный": "яиц",
    "сахар": "сах",
    "соль": "сол",
    "масло": "масл",
    "подсолнечное": "подсол",
    "оливковое": "олив",
    "сливочное": "слив",
    "колбаса": "колб",
    "сосиски": "сосиски", "сосисок": "сосиски",
    "говядина": "гов",
    "свинина": "свин",
    "фарш": "фарш",
    "огурцы": "огур", "огурец": "огур",
    "помидоры": "помид", "томаты": "помид", "томат": "помид",
    "картофель": "карт", "картошка": "карт",
    "лук": "лук",
    "морковь": "морк", "морковка": "морк",
    "капуста": "капуст",
    "макароны": "макар",
    "гречка": "греч", "гречневая": "греч",
    "рис": "рис",
    "мука": "мук",
}

#: Токены, которые при совпадении считаются смысловыми (убираем из расчёта).
NOISE_TOKENS = set(ALIASES) | NOISE_WORDS

_NON_ALNUM = re.compile(r"[^0-9а-яёa-z]+", re.IGNORECASE)
_TOKENIZE = re.compile(r"\d+|[а-яёa-z]+", re.IGNORECASE)
_BIG_NUMBER = re.compile(r"^\d{3,}$")
_MEASURE_SUFFIX = re.compile(
    r"^(.*?)[\s\*]*(?:х|x)?[\s\*]*(\d{1,3}\s*(?:кг|г|гр|мл|л|шт|штук)\s*|х\d+|№\d+)?$"
)
_UNITS = re.compile(r"(^|[\s*])(?:шт|штук|уп|упак|пач|пакет|кг|г|гр|мл|л)\.?\s*$")


def normalize_name(name: str) -> str:
    """Нормализовать название: нижний регистр, без мер веса и служебных слов."""
    if not name:
        return ""
    text = _NON_ALNUM.sub(" ", name.lower().strip())
    words = text.split()
    kept: List[str] = []
    for w in words:
        for chunk in _TOKENIZE.findall(w):
            if chunk in NOISE_WORDS or _BIG_NUMBER.match(chunk):
                continue
            if chunk.isdigit():
                # Проценты жирности и мелкие числа оставляем как есть.
                kept.append(chunk)
                continue
            base = chunk.rstrip("0123456789")
            if not base or base in NOISE_WORDS:
                continue
            kept.append(ALIASES.get(base, base))
    return " ".join(kept)


def tokens(name: str) -> set:
    """Слово-токены нормализованного названия."""
    return set(normalize_name(name).split())


def _tok_covers(a: str, b: str) -> bool:
    """Совпадение токена: равенство либо один — префикс другого."""
    return a == b or a.startswith(b) or b.startswith(a)


def token_overlap(item_name: str, product_name: str) -> float:
    """Доля токенов искомого названия, найденных в названии товара (0..1)."""
    it = tokens(item_name)
    if not it:
        return 0.0
    pt = tokens(product_name)
    if not pt and product_name:
        return 0.0
    hit = {ti for ti in it if any(_tok_covers(ti, pi) for pi in pt)}
    if not hit:
        return 0.0
    # Совпадение всего искомого — полное совпадение.
    if len(hit) == len(it):
        return 1.0
    return len(hit) / len(it)


def quantity_for(amount: float, unit: str) -> int:
    """Пересчёт количества из списка покупок в число упаковок.

    Штучные товары округляются вверх с минимумом 1; весовые/объёмные
    считаются одной упаковкой (проверяется в корзине Купера вручную).
    """
    try:
        value = float(amount)
    except (TypeError, ValueError):
        value = 1.0
    unit = (unit or "").lower()
    if unit in ("pieces", "штуки", "шт", "штук"):
        return max(1, int(math.ceil(value)))
    return 1


def unit_note(unit: str) -> str:
    """Комментарий к количеству, отображаемый в UI."""
    unit = (unit or "").lower()
    if unit in ("pieces", "штуки", "шт", "штук"):
        return ""
    return "вес/объём: берётся 1 упаковка, проверьте в корзине Купера"


def score_candidate(
    item_name: str,
    product_name: str,
    *,
    previously_bought: bool = False,
    in_stock: bool = True,
) -> float:
    """Оценка кандидата 0..100: совпадение названия + бонусы."""
    overlap = token_overlap(item_name, product_name)
    if overlap <= 0:
        score = 0.0
    elif overlap >= 1.0:
        score = 70.0
    elif overlap >= 0.5:
        score = 55.0
    else:
        score = 30.0 + overlap * 20.0
    if previously_bought:
        score += 25.0
    if in_stock:
        score += 5.0
    return score


def pick_best(
    item_name: str,
    candidates: List[dict],
    *,
    previously_bought_ids: Optional[set] = None,
    previously_bought_names: Optional[Dict[int, str]] = None,
) -> dict:
    """Выбор лучшего кандидата и топ-3 альтернатив.

    Args:
        item_name: исходное название пункта списка.
        candidates: список товаров (словари с ключами ``name``, ``price``,
            ``available``, ``product_id``).
        previously_bought_ids: известные ``product_id`` из истории покупок.
        previously_bought_names: словарь ``product_id -> название`` из истории.

    Returns:
        Словарь с ключами ``product``, ``score``, ``is_previous_buy``,
        ``is_replacement``, ``reason``, ``alternatives``.
    """
    it_norm = normalize_name(item_name)
    prev_ids = previously_bought_ids or set()
    prev_names = previously_bought_names or {}

    ranked: List[tuple] = []
    for cand in candidates:
        pid = cand.get("product_id")
        is_prev = pid in prev_ids
        prev_name = prev_names.get(pid) or (prev_ids and pid is not None and False) or ""
        score = score_candidate(
            it_norm,
            cand.get("name", ""),
            previously_bought=is_prev,
            in_stock=bool(cand.get("available", True)),
        )
        # Дешёвый товар получает небольшое преимущество при равенстве.
        score += 0.0
        ranked.append((score, pid, cand, is_prev, prev_name))

    ranked.sort(key=lambda r: (-r[0], float(r[2].get("price") or 0) if r[2].get("price") else 0))
    if not ranked:
        return {
            "product": None,
            "score": 0.0,
            "is_previous_buy": False,
            "is_replacement": False,
            "reason": "ничего не найдено",
            "alternatives": [],
        }

    best_score, _, best_cand, best_prev, best_prev_name = ranked[0]
    is_prev = bool(best_prev and best_cand)
    # Замена: не куплено ранее и совпадение названия неполное.
    nomatch_products = token_overlap(it_norm, best_cand.get("name", "")) < 1.0
    is_replacement = bool(not is_prev and nomatch_products and best_cand.get("name"))
    if not is_replacement and best_cand.get("name"):
        reason = "найден товар с точным совпадением названия"
    elif is_prev:
        reason = "покупали ранее"
    else:
        reason = "замена по цене и качеству"

    alternatives = [
        {"product": c, "score": s}
        for s, _, c, _, _ in ranked[1:4]
    ]

    return {
        "product": best_cand,
        "score": round(best_score, 1),
        "is_previous_buy": is_prev,
        "is_replacement": is_replacement,
        "reason": reason,
        "alternatives": alternatives,
    }