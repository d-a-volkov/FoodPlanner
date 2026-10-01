"""Быстрые проверки matcher (без сети)."""

from matcher import (
    normalize_name,
    token_overlap,
    quantity_for,
    score_candidate,
    pick_best,
)

assert normalize_name("Молоко 3,2% 900 мл") == "мол 3 2"
assert normalize_name("Творог 5% уп 200г") == "тв 5"
assert normalize_name("Яйца куриные С1 10шт") == "яиц кур с 1 10"
assert normalize_name("Хлеб бородинский") == "хлеб бородинский"
assert normalize_name("") == ""

assert token_overlap("молоко пастеризованное", "Молоко пастеризованное 900 мл") == 1.0
assert token_overlap("молоко", "Молоко ультрапастеризованное") == 1.0
assert token_overlap("молоко", "кефир") == 0.0

assert quantity_for(4, "pieces") == 4
assert quantity_for(0.5, "pieces") == 1
assert quantity_for(500, "grams") == 1
assert quantity_for(1, "g") == 1

assert score_candidate("молоко", "Молоко 3,2%", previously_bought=True) == 100.0
assert score_candidate("молоко", "кефир", previously_bought=False, in_stock=False) == 0.0

candidates = [
    {"product_id": 1, "name": "Молоко 3,2% 900 мл", "price": 79.0, "available": True},
    {"product_id": 2, "name": "Молоко 1,5% 1 л", "price": 65.0, "available": True},
    {"product_id": 3, "name": "Кефир 1%", "price": 50.0, "available": True},
]
best = pick_best("Молоко 3,2%", candidates)
assert best["product"]["product_id"] == 1, best
assert best["is_previous_buy"] is False

best2 = pick_best("Молоко", candidates, previously_bought_ids={2}, previously_bought_names={2: "Молоко 1,5% 1 л"})
assert best2["product"]["product_id"] == 2, best2
assert best2["is_previous_buy"] is True, best2

best3 = pick_best("Сыр российский", [{"product_id": 4, "name": "Сыр Гауда 45%", "price": 300, "available": True}])
assert best3["is_replacement"] is True, best3
assert len(best3["alternatives"]) == 0

print("matcher tests OK")