"""Тесты сбора истории покупок из сырых ответов Купера.

Проблемы, которые они закрепляют:

* ``/api/v2/orders/previous`` отдаёт ``{"order": {...}}``, а библиотечный
  ``orders(previous=True)`` ищет ключ ``orders`` и превращает ответ в
  ``[None]``;
* список ``/api/v2/orders`` приходит без ``line_items`` — товары есть только
  в подробном ответе по номеру заказа;
* товары лежат во вложенном ``line_item.product`` (``id``/``name``/``sku``),
  а плоских ``offer_id``/``name`` у элемента нет.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from app import _aggregate_history, _order_shipments, _raw_orders  # noqa: E402


SHIPMENT_WITH_ITEMS = {
    "id": 111,
    "delivery_window": {"starts_at": "2026-10-02T15:00:00.000+03:00"},
    "line_items": [
        {
            "quantity": 2,
            "price": 107.96,
            "product": {
                "id": 33111720755,
                "name": "Ряженка Каждый День 2,5% БЗМЖ 450 мл",
                "sku": 26477297,
                "human_volume": "450 мл",
            },
        },
        {
            "quantity": 1,
            "price": 36.99,
            "product": {
                "id": 1658495814,
                "name": "Сухари АШАН Красная птица панировочные 200 г",
                "sku": 326550,
                "human_volume": "200 г",
            },
        },
    ],
}
SHIPMENT_WITHOUT_ITEMS = {"id": 222, "delivery_window": None}

ORDER_WITH_ITEMS = {
    "number": "R909228551",
    "state": "complete",
    "item_count": 37,
    "shipments": [SHIPMENT_WITH_ITEMS],
}
ORDER_LIST_ITEM = {
    "number": "R909228551",
    "state": "complete",
    "item_count": 37,
    "shipments": [SHIPMENT_WITHOUT_ITEMS],
}
ORDER_OTHER = {
    "number": "R021138592",
    "state": "complete",
    "item_count": 25,
    "shipments": [SHIPMENT_WITHOUT_ITEMS],
}
ORDER_CART = {"number": "R359344185", "state": "cart", "shipments": []}


class _FakeRequest:
    def __init__(self, payloads):
        self.payloads = payloads
        self.calls = []

    def get(self, path, *args, **kwargs):
        self.calls.append(path)
        if path not in self.payloads:
            raise TimeoutError(f"нет ответа на {path}")
        return self.payloads[path]


class _FakeClient:
    def __init__(self, payloads):
        self._request = _FakeRequest(payloads)


class RawOrdersTests(unittest.TestCase):
    def test_collects_both_endpoint_shapes(self):
        client = _FakeClient({
            "/api/v2/orders": {"orders": [ORDER_LIST_ITEM, ORDER_CART], "meta": {}},
            "/api/v2/orders/previous": {"order": ORDER_OTHER},
        })
        numbers = [o["number"] for o in _raw_orders(client)]
        self.assertEqual(sorted(numbers), ["R021138592", "R909228551"])

    def test_skips_active_cart(self):
        client = _FakeClient({
            "/api/v2/orders": {"orders": [ORDER_CART]},
            "/api/v2/orders/previous": {"order": None},
        })
        self.assertEqual(_raw_orders(client), [])

    def test_prefers_payload_with_line_items(self):
        client = _FakeClient({
            "/api/v2/orders": {"orders": [ORDER_LIST_ITEM]},
            "/api/v2/orders/previous": {"order": ORDER_WITH_ITEMS},
        })
        orders = _raw_orders(client)
        self.assertEqual(len(orders), 1, "дубликат по номеру заказа")
        self.assertTrue(orders[0]["shipments"][0]["line_items"])

    def test_broken_endpoint_does_not_break_the_other(self):
        client = _FakeClient({
            "/api/v2/orders": {"orders": [ORDER_LIST_ITEM]},
        })
        orders = _raw_orders(client)
        self.assertEqual([o["number"] for o in orders], ["R909228551"])


class OrderShipmentsTests(unittest.TestCase):
    def test_fetches_detail_when_list_has_no_line_items(self):
        client = _FakeClient({
            "/api/v2/orders/R909228551": {"order": ORDER_WITH_ITEMS},
        })
        shipments = _order_shipments(client, ORDER_LIST_ITEM)
        self.assertIn("/api/v2/orders/R909228551", client._request.calls)
        self.assertTrue(shipments[0]["line_items"])

    def test_no_extra_request_when_items_present(self):
        client = _FakeClient({})
        shipments = _order_shipments(client, ORDER_WITH_ITEMS)
        self.assertEqual(client._request.calls, [])
        self.assertTrue(shipments[0]["line_items"])

    def test_detail_failure_falls_back_to_list_shipments(self):
        client = _FakeClient({})
        shipments = _order_shipments(client, ORDER_LIST_ITEM)
        self.assertEqual(len(shipments), 1)
        self.assertNotIn("line_items", shipments[0])


class AggregateHistoryTests(unittest.TestCase):
    def test_reads_nested_product_fields(self):
        ids, names, items = _aggregate_history([(ORDER_WITH_ITEMS, [SHIPMENT_WITH_ITEMS])])
        self.assertIn(33111720755, ids)
        self.assertIn(1658495814, ids)
        self.assertEqual(names[33111720755], "Ряженка Каждый День 2,5% БЗМЖ 450 мл")
        entry = next(e for e in items if e["product_id"] == 1658495814)
        self.assertEqual(entry["sku"], 326550)
        self.assertEqual(entry["human_volume"], "200 г")
        self.assertEqual(entry["times_bought"], 1)
        self.assertEqual(entry["last_price"], 36.99)
        self.assertEqual(entry["last_bought_at"], "2026-10-02T15:00:00.000+03:00")

    def test_sums_quantities_across_orders(self):
        older = {
            "delivery_window": {"starts_at": "2025-09-04T13:00:00.000+03:00"},
            "line_items": [{
                "quantity": 3,
                "price": 90.0,
                "product": {"id": 1658495814, "name": "Сухари АШАН"},
            }],
        }
        _, _, items = _aggregate_history([
            (ORDER_WITH_ITEMS, [SHIPMENT_WITH_ITEMS]),
            (ORDER_OTHER, [older]),
        ])
        entry = next(e for e in items if e["product_id"] == 1658495814)
        self.assertEqual(entry["times_bought"], 4)
        # Дата и цена берутся от самой свежей покупки.
        self.assertEqual(entry["last_bought_at"], "2026-10-02T15:00:00.000+03:00")
        self.assertEqual(entry["last_price"], 36.99)

    def test_skips_items_without_product_id(self):
        shipment = {"line_items": [{"quantity": 1, "product": {"name": "без id"}}]}
        ids, names, items = _aggregate_history([(ORDER_OTHER, [shipment])])
        self.assertEqual(ids, set())
        self.assertEqual(items, [])

    def test_without_delivery_window_keeps_last_price(self):
        shipment = {"line_items": [{"quantity": 1, "price": 55.0,
                                    "product": {"id": 7, "name": "Масло"}}]}
        _, _, items = _aggregate_history([(ORDER_OTHER, [shipment])])
        self.assertEqual(items[0]["last_bought_at"], None)
        self.assertEqual(items[0]["last_price"], 55.0)


class EndToEndTests(unittest.TestCase):
    def test_history_from_real_shapes(self):
        client = _FakeClient({
            "/api/v2/orders": {"orders": [ORDER_LIST_ITEM, ORDER_OTHER], "meta": {}},
            "/api/v2/orders/previous": {"order": ORDER_WITH_ITEMS},
            "/api/v2/orders/R021138592": {
                "order": {
                    "number": "R021138592",
                    "state": "complete",
                    "shipments": [{
                        "delivery_window": {"starts_at": "2025-09-04T13:00:00.000+03:00"},
                        "line_items": [{
                            "quantity": 1,
                            "price": 169.99,
                            "product": {"id": 1658489553,
                                        "name": "Масло несоленое Брест-Литовск 180 г",
                                        "sku": 115227},
                        }],
                    }],
                },
            },
        })
        shipments = [(o, _order_shipments(client, o)) for o in _raw_orders(client)]
        ids, names, items = _aggregate_history(shipments)
        self.assertEqual(len(items), 3)
        self.assertIn(33111720755, ids)
        self.assertIn(1658489553, ids)
        self.assertEqual(
            sorted(client._request.calls),
            ["/api/v2/orders", "/api/v2/orders/R021138592", "/api/v2/orders/previous"],
        )


if __name__ == "__main__":
    unittest.main()
