import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from matcher import match_preferences, normalize_name, pick_best, token_overlap


def cand(pid, name, price=100):
    return {"product_id": pid, "name": name, "price": price, "available": True}


class NormalizeNameTests(unittest.TestCase):
    def test_lowercase_and_trims(self):
        self.assertEqual(normalize_name("  Кофе  "), "кофе")

    def test_strips_punctuation_and_numbers(self):
        self.assertEqual(normalize_name("Молоко 3,2%, 930мл"), "молоко")

    def test_strips_noise_words(self):
        self.assertNotIn("натуральный", normalize_name("Молоко натуральное"))

    def test_empty_input(self):
        self.assertEqual(normalize_name("   "), "")


class TokenOverlapTests(unittest.TestCase):
    def test_exact_match(self):
        self.assertEqual(token_overlap("кофе", "Кофе"), 1.0)

    def test_plural_and_case_insensitive(self):
        self.assertGreater(token_overlap("апельсины", "Апельсины отборные"), 0.9)

    def test_unrelated(self):
        self.assertEqual(token_overlap("кофе", "Морковь свежая"), 0.0)

    def test_synonyms_are_equivalent(self):
        self.assertGreater(token_overlap("хлеб", "Батон пшеничный"), 0.0)

    def test_singular_plural_and_abbreviation(self):
        for item, product in [
            ("огурец", "Огурцы грунтовые"),
            ("яйца", "Яйцо куриное Желток Солнца С1 коричневое 10 шт"),
            ("молоко", "Молоко пастеризованное 2,5% 930 мл"),
            ("помидоры", "Томаты розовые"),
            ("картошка", "Картофель мытый"),
        ]:
            with self.subTest(item=item, product=product):
                self.assertEqual(token_overlap(item, product), 1.0)


class PickBestTests(unittest.TestCase):
    def test_picks_name_match_over_cheaper_noise(self):
        candidates = [
            cand(1, "Морковь свежая 500 г", 20),
            cand(2, "Молоко 3,2% 930 мл", 100),
        ]
        decision = pick_best("молоко", candidates)
        self.assertEqual(decision["product"]["product_id"], 2)
        # Морковь в альтернативы не попадает: совпадения слов нет.
        alt_ids = [a["product"]["product_id"] for a in decision["alternatives"]]
        self.assertNotIn(1, alt_ids)

    def test_exact_name_wins(self):
        candidates = [cand(1, "Кофе арабика 250 г", 300), cand(2, "Кофе", 150)]
        decision = pick_best("кофе", candidates)
        self.assertEqual(decision["product"]["product_id"], 2)

    def test_exact_name_wins_over_paid_become_previous(self):
        candidates = [cand(1, "Кофе арабика 250 г", 300), cand(2, "Кофе", 150)]
        decision = pick_best("кофе", candidates, previously_bought_ids={1})
        self.assertEqual(decision["product"]["product_id"], 2)

    def test_no_overlap_returns_no_product(self):
        decision = pick_best("кофе", [cand(1, "Морковь свежая 500 г", 20)])
        self.assertIsNone(decision["product"])
        self.assertEqual(decision["reason"], "ничего не найдено")
        self.assertTrue(decision["alternatives"], "должны быть показаны варианты для ручного выбора")

    def test_alternatives_exclude_chosen_but_keep_limit(self):
        candidates = [cand(i, f"Кофе вариант {i}", 100 + i) for i in range(20)]
        decision = pick_best("кофе", candidates, alternatives_limit=10)
        chosen = decision["product"]["product_id"]
        alt_ids = [a["product"]["product_id"] for a in decision["alternatives"]]
        self.assertEqual(len(alt_ids), 10)
        self.assertNotIn(chosen, alt_ids)

    def test_previous_buy_is_marked(self):
        candidates = [cand(7, "Кофе Tchibo Gold", 250), cand(8, "Кофе арабика", 300)]
        decision = pick_best("кофе", candidates, previously_bought_ids={7})
        self.assertEqual(decision["product"]["product_id"], 7)
        self.assertTrue(decision["is_previous_buy"])

    def test_out_of_stock_is_not_chosen(self):
        candidates = [
            {**cand(1, "Кофе", 100), "available": False},
            cand(2, "Кофе растворимый 95 г", 110),
        ]
        decision = pick_best("кофе", candidates)
        self.assertEqual(decision["product"]["product_id"], 2)

    def test_empty_candidates(self):
        decision = pick_best("кофе", [])
        self.assertIsNone(decision["product"])
        self.assertEqual(decision["alternatives"], [])

    def test_gibberish_returns_no_product(self):
        decision = pick_best("кофе", [cand(1, "Морковь", 20), cand(2, "Гречка", 30)])
        self.assertIsNone(decision["product"])

    def test_out_of_stock_used_only_if_nothing_in_stock(self):
        decision = pick_best("кофе", [{**cand(1, "Кофе", 100), "available": False}])
        self.assertEqual(decision["product"]["product_id"], 1)

    def test_query_keeps_readable_words_for_search(self):
        # Запрос в Купер должен оставаться читаемым, а не «мол 3 2».
        self.assertEqual(normalize_name("Молоко 3,2%, 930мл"), "молоко")
        self.assertEqual(normalize_name("Апельсины 1,5 кг"), "апельсины")

    def test_refine_query_allows_filter_words(self):
        # Пользовательский запрос-фильтр («без сахара»): подходит товар,
        # совпавший только по основному слову, поэтому порог снижается.
        candidates = [cand(1, "Хлеб Бородинский 400 г", 60)]
        self.assertIsNone(
            pick_best("хлеб без сахара", candidates)["product"],
            "строгий порог отбрасывает запрос с фильтром",
        )
        relaxed = pick_best("хлеб без сахара", candidates, min_overlap=0.01)
        self.assertIsNotNone(relaxed["product"])
        self.assertEqual(relaxed["product"]["product_id"], 1)

    def test_refine_query_ignores_unrelated_candidates(self):
        # Ноль совпадений не подходит даже в уточняющем поиске.
        candidates = [cand(1, "Хлеб Бородинский 400 г", 60)]
        self.assertIsNone(pick_best("яйцо с1", candidates, min_overlap=0.01)["product"])


class MatchPreferencesTests(unittest.TestCase):
    def test_ignores_unrelated(self):
        prefs = [{"product_id": 1, "name": "Морковь свежая", "times_bought": 2}]
        self.assertEqual(match_preferences("молоко", prefs), [])

    def test_sorts_by_overlap_then_times_bought(self):
        prefs = [
            {"product_id": 10, "name": "Кофе растворимый 95 г", "times_bought": 1},
            {"product_id": 11, "name": "Кофе арабика молотый 250 г", "times_bought": 5},
            {"product_id": 12, "name": "Кофе", "times_bought": 2},
        ]
        matched = match_preferences("кофе", prefs)
        # Все совпадают полностью, дальше побеждает число покупок.
        self.assertEqual([m["product_id"] for m in matched], [11, 12, 10])

    def test_filters_below_overlap_threshold(self):
        prefs = [{"product_id": 5, "name": "Сыр Российский 200 г", "times_bought": 3}]
        self.assertEqual(match_preferences("сыр твёрдый гауда", prefs), [])

    def test_empty_input(self):
        self.assertEqual(match_preferences("", []), [])
        self.assertEqual(match_preferences("молоко", None), [])


class PreferencePickTests(unittest.TestCase):
    def test_preferred_beats_exact_name(self):
        # Правило «точное название из каталога» уступает предпочтению.
        candidates = [cand(1, "Кофе", 150), cand(2, "Кофе арабика 250 г", 300)]
        decision = pick_best("кофе", candidates, preferred_ids={2})
        self.assertEqual(decision["product"]["product_id"], 2)
        self.assertTrue(decision["is_preferred"])

    def test_unavailable_preferred_is_skipped(self):
        candidates = [
            {**cand(2, "Кофе арабика 250 г", 300), "available": False},
            cand(1, "Кофе", 150),
        ]
        decision = pick_best("кофе", candidates, preferred_ids={2})
        self.assertEqual(decision["product"]["product_id"], 1)
        self.assertFalse(decision["is_preferred"])

    def test_weak_overlap_preferred_does_not_win(self):
        candidates = [cand(2, "Сыр Российский 200 г", 200), cand(1, "Сыр твёрдый Гауда", 450)]
        decision = pick_best("сыр твёрдый гауда", candidates, preferred_ids={2})
        self.assertEqual(decision["product"]["product_id"], 1)
        self.assertFalse(decision["is_preferred"])

    def test_marks_previous_buy_and_preferred_together(self):
        decision = pick_best(
            "кофе", [cand(7, "Кофе Tchibo Gold", 250)],
            previously_bought_ids={7}, preferred_ids={7},
        )
        self.assertTrue(decision["is_previous_buy"])
        self.assertTrue(decision["is_preferred"])
        self.assertIn("предпочтение", decision["reason"])

    def test_no_preference_keeps_old_flags(self):
        decision = pick_best("кофе", [cand(1, "Кофе", 150)])
        self.assertFalse(decision["is_preferred"])
        self.assertFalse(decision["is_previous_buy"])

    def test_empty_pick_keeps_preferred_flag(self):
        decision = pick_best("кофе", [])
        self.assertFalse(decision["is_preferred"])


if __name__ == "__main__":
    unittest.main()
