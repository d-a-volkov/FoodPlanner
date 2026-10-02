"""Нормализация названий продуктов и ранжирование кандидатов для корзины Kuper.

Чистые функции без зависимостей от сети — можно покрывать юнит-тестами.
"""

from __future__ import annotations

import math
import re
from typing import Dict, List, Optional, Sequence

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

#: Сокращения: полная форма -> краткая (как в каталоге Купера).
STEM_ALIASES = {
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

#: Группы синонимов: разные слова означают один товар.
#: Используются только как допустимое совпадение, точное слово всегда важнее.
SYNONYM_GROUPS: Sequence[Sequence[str]] = (
    ("кофе", "кофейный", "кофеин", "молотый", "эспрессо", "американо",
     "капучино", "латте", "раф", "капучино"),
    ("апельсин", "апельсины", "мандарин", "цитрус"),
    ("лимон", "лимоны", "лайм"),
    ("вода", "питьевая", "минеральная", "газированная"),
    ("сок", "нектар", "напиток"),
    ("чай", "чайный", "чаинный"),
    ("хлеб", "батон", "буханка", "лепешка", "батончик"),
    ("печенье", "печеньки", "бисквит", "сухарик", "сухари"),
    ("конфета", "конфеты", "карамель", "леденец", "карамелька"),
    ("шоколад", "шоколадка", "шоколадный"),
    ("масло", "сливочное", "подсолнечное", "оливковое", "растительное"),
    ("творог", "творожок", "творожная", "творожный"),
    ("молоко", "молочко", "молочник", "молочный"),
    ("сосиски", "сосиска", "сосиски"),
    ("колбаса", "колбаски", "колбас"),
    ("курица", "куриное", "куриный", "куриная", "цыпленок", "цыплятина"),
    ("индейка", "индейка"),
    ("говядина", "говяжья", "говядина"),
    ("свинина", "свиная"),
    ("овощи", "овощной"),
    ("фрукт", "фруктовый", "фрукты"),
    ("ягоды", "ягодный", "ягода"),
    ("рыба", "рыбный"),
    ("сахар", "сахарный"),
    ("соль", "соленый", "солёный"),
    ("чай", "чайный"),
)

#: Слово -> идентификатор группы синонимов.
_SYNONYM_INDEX: Dict[str, str] = {}
for _i, _group in enumerate(SYNONYM_GROUPS):
    _key = f"syn{_i}"
    for _w in _group:
        _SYNONYM_INDEX.setdefault(_w, _key)
#: Сокращения из каталога привязываем к группе по краткой форме, чтобы
#: «огурец»/«огурцы»/«огур» и «яйцо»/«яйца»/«яиц» были одним товаром.
for _full, _short in STEM_ALIASES.items():
    _grp = _SYNONYM_INDEX.get(_full) or f"ali{_short}"
    _SYNONYM_INDEX.setdefault(_full, _grp)
    _SYNONYM_INDEX.setdefault(_short, _grp)

_NON_ALNUM = re.compile(r"[^0-9а-яёa-z]+", re.IGNORECASE)
_TOKENIZE = re.compile(r"\d+|[а-яёa-z]+", re.IGNORECASE)

#: Русские окончания, отсекаемые при стемминге (от длинных к коротким).
_ENDINGS = (
    "иями", "ями", "ами", "иях", "ях", "ией", "ей", "ий", "ый", "ое", "ая",
    "ые", "ых", "ов", "ев", "ью", "ия", "ие", "ам", "ям", "ах", "ях",
    "ом", "ем", "ку", "ю", "я", "а", "ы", "и", "о", "у", "е", "ь", "й",
)

#: Минимальное совпадение токенов, чтобы принять товар как подходящий.
MIN_OVERLAP = 0.5
#: Сколько альтернатив возвращать по умолчанию.
DEFAULT_ALTERNATIVES = 10


def _stem(token: str) -> str:
    """Грубая основа русского слова: «апельсины» -> «апельсин»."""
    if len(token) <= 4 or token.isdigit():
        return token
    for ending in _ENDINGS:
        if token.endswith(ending) and len(token) - len(ending) >= 4:
            return token[: -len(ending)]
    return token


def _key(token: str) -> str:
    """Ключ сравнения: синоним или основа."""
    if token in _SYNONYM_INDEX:
        return _SYNONYM_INDEX[token]
    return _stem(token)


def normalize_name(name: str) -> str:
    """Нормализовать название для поиска и сравнения.

    Убираются регистр, знаки препинания, числа (объём/жирность/цена),
    единицы измерения и служебные слова. Слово остаётся в исходной форме:
    сокращения и синонимы применяются только при сравнении, иначе запрос
    в Купер уходит слишком коротким и находит не то.
    """
    if not name:
        return ""
    text = _NON_ALNUM.sub(" ", name.lower().strip())
    kept: List[str] = []
    for word in text.split():
        for chunk in _TOKENIZE.findall(word):
            base = chunk.rstrip("0123456789")
            if not base or base in NOISE_WORDS:
                continue
            kept.append(base)
    return " ".join(kept)


def tokens(name: str) -> set:
    """Слово-токены нормализованного названия."""
    return set(normalize_name(name).split())


def stems(name: str) -> set:
    """Основы слов — используются для оценки точности совпадения."""
    return {_stem(t) for t in tokens(name)}


def token_overlap(item_name: str, product_name: str) -> float:
    """Доля токенов искомого названия, найденных в названии товара (0..1)."""
    it = tokens(item_name)
    if not it:
        return 0.0
    pt = tokens(product_name)
    if not pt and product_name:
        return 0.0
    item_keys = {_key(t) for t in it}
    prod_keys = {_key(t) for t in pt}
    hit = {ti for ti in item_keys if any(_tok_covers(ti, pi) for pi in prod_keys)}
    if not hit:
        return 0.0
    if len(hit) == len(item_keys):
        return 1.0
    return len(hit) / len(item_keys)


def _tok_covers(a: str, b: str) -> bool:
    """Совпадение основ: равенство либо один — префикс другого."""
    if a == b:
        return True
    if len(a) >= 4 and len(b) >= 4:
        return a.startswith(b) or b.startswith(a)
    return False


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
    rank: int = 0,
    total: int = 1,
) -> float:
    """Оценка кандидата 0..100: совпадение названия + бонусы.

    ``rank``/``total`` — позиция в выдаче Купера, отсортированной по
    релевантности: первый результат получает небольшое преимущество.
    """
    overlap = token_overlap(item_name, product_name)
    if overlap <= 0:
        return 0.0

    if overlap >= 1.0:
        score = 80.0
    elif overlap >= 0.5:
        score = 55.0 + (overlap - 0.5) * 40.0
    else:
        score = 20.0 + overlap * 20.0

    # Совпадение по тому же слову (а не по синониму) — важнее.
    if stems(item_name) & stems(product_name):
        score += 6.0

    # Релевантность по выдаче Купера (мягкий сигнал, чтобы не перебивать смысл).
    if total > 1:
        score += max(0.0, 6.0 * (1.0 - (rank / total)))

    if previously_bought:
        score += 8.0
    if in_stock:
        score += 3.0
    return score


def pick_best(
    item_name: str,
    candidates: List[dict],
    *,
    previously_bought_ids: Optional[set] = None,
    previously_bought_names: Optional[Dict[int, str]] = None,
    alternatives_limit: int = DEFAULT_ALTERNATIVES,
    min_overlap: float = MIN_OVERLAP,
) -> dict:
    """Выбор лучшего кандидата и списка альтернатив.

    Если ни один товар не достигает ``min_overlap``, основной выбор пустой
    (``product is None``) — товар подбирается вручную из альтернатив.
    Такой товар никогда не подставляется молча: нулевое совпадение = 0 баллов.
    """
    prev_ids = previously_bought_ids or set()
    prev_names = previously_bought_names or {}
    # Отсутствующий товар нельзя подставлять: если в наличии есть хоть один
    # кандидат, отсутствующие отбрасываются полностью.
    in_stock = [c for c in candidates if c.get("available", True)]
    pool = in_stock or list(candidates)
    total = len(pool)

    ranked: List[tuple] = []
    for index, cand in enumerate(pool):
        pid = cand.get("product_id")
        is_prev = pid in prev_ids
        prev_name = prev_names.get(pid, "")
        score = score_candidate(
            item_name,
            cand.get("name", ""),
            previously_bought=is_prev,
            in_stock=bool(cand.get("available", True)),
            rank=index,
            total=total,
        )
        price = cand.get("price")
        ranked.append((score, -index, float(price) if isinstance(price, (int, float)) else 1e9, cand, is_prev, prev_name))

    # Сортировка: сначала балл, потом позиция выдачи Купера, потом цена.
    ranked.sort(key=lambda r: (-r[0], -r[1], r[2]))

    if not ranked:
        return {
            "product": None,
            "score": 0.0,
            "is_previous_buy": False,
            "is_replacement": False,
            "reason": "ничего не найдено",
            "alternatives": [],
        }

    # Товар с названием, совпадающим с запросом, предпочтительнее любого
    # расширенного названия («Кофе» вместо «Кофе арабика 250 г»).
    wanted = normalize_name(item_name)
    exact_rows = [r for r in ranked if wanted and normalize_name(r[3].get("name", "")) == wanted]
    if exact_rows:
        ranked = exact_rows + [r for r in ranked if r not in exact_rows]

    best_score, _, _, best_cand, best_prev, _ = ranked[0]
    best_overlap = token_overlap(item_name, best_cand.get("name", ""))

    def _alts(rows: Sequence[tuple]) -> List[dict]:
        return [
            {
                "product": c,
                "score": round(s, 1),
                "overlap": round(token_overlap(item_name, c.get("name", "")), 2),
            }
            for s, _, _, c, _, _ in rows[:alternatives_limit]
        ]

    if best_overlap < min_overlap:
        # Подходящего товара нет: показываем самые близкие для ручного выбора.
        return {
            "product": None,
            "score": round(best_score, 1),
            "is_previous_buy": False,
            "is_replacement": False,
            "reason": "ничего не найдено",
            "alternatives": _alts(ranked),
        }

    alternatives = _alts([r for r in ranked[1:] if token_overlap(item_name, r[3].get("name", "")) > 0])

    is_prev = bool(best_prev)
    is_replacement = bool(not is_prev and best_overlap < 1.0)
    if best_overlap >= 1.0 and is_prev:
        reason = "найдено точное совпадение, покупали ранее"
    elif best_overlap >= 1.0:
        reason = "найдено точное совпадение названия"
    elif is_prev:
        reason = "похожее совпадение, покупали ранее"
    else:
        reason = "подобрано по похожему названию"

    return {
        "product": best_cand,
        "score": round(best_score, 1),
        "is_previous_buy": is_prev,
        "is_replacement": is_replacement,
        "reason": reason,
        "alternatives": alternatives,
    }