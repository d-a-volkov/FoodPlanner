import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from matcher import normalize_name, pick_best, token_overlap


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


if __name__ == "__main__":
    unittest.main()
