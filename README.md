# FoodPlanner — Учет продуктов и рецептов

Система учета наличия продуктов, формирования списков покупок и создания рецептов
на основе правил гарвардской тарелки.

## Технологии

| Компонент | Технология |
|---|---|
| Бэкенд | ASP.NET Core 10, REST API, Swagger |
| Фронтенд | Vue 3, Vite, Pinia, Chart.js |
| Хранение данных | JSON-файлы |
| Telegram-бот | Telegram.Bot (C#) |
| VK-бот | VkNet (C#) |
| Импорт данных | ClosedXML (Excel) |

## Структура проекта

```
FoodPlanner/
├── FoodPlanner.Core/          # Модели, интерфейсы, перечисления
├── FoodPlanner.Data/          # JSON-хранилище, импорт Excel
├── FoodPlanner.Services/      # Бизнес-логика
├── FoodPlanner.Api/           # REST API + раздача веб-интерфейса
├── FoodPlanner.Web/           # Vue 3 приложение
├── FoodPlanner.Bot.Telegram/  # Telegram-бот
├── FoodPlanner.Bot.VK/        # VK-бот
├── FoodPlanner.Seed/          # Утилита импорта данных
└── Список основных продуктов.xlsx  # Исходные данные
```

## Быстрый старт

### Требования

- .NET 10 SDK
- Node.js 18+ и npm
- Telegram-токен (для Telegram-бота)
- VK-токен сообщества (для VK-бота)

### 1. Сборка решения

```bash
cd D:\MyProject\OpenCode\FoodPlanner
dotnet build
```

### 2. Импорт данных из Excel

Данные из вашей таблицы импортируются в JSON-формат:

```bash
dotnet run --project FoodPlanner.Seed
```

Результат: файлы `products.json`, `recipes.json`, `shoppinglists.json`
в папке `FoodPlanner.Api/Data/`.

### 3. Запуск API

```bash
dotnet run --project FoodPlanner.Api
```

- API: `https://localhost:7215`
- Swagger: `https://localhost:7215/swagger`

### 4. Запуск веб-интерфейса

**В режиме разработки:**

```bash
cd FoodPlanner.Web
npm install
npm run dev
```

Откроется `http://localhost:5173` с прокси-запросами к API.

**В продакшн-режиме:**

```bash
cd FoodPlanner.Web
npm run build
```

Собранные файлы попадут в `dist/`. Запустите API — он автоматически раздаст
статику из `dist/`.

### 5. Запуск Telegram-бота

1. Получите токен у [@BotFather](https://t.me/BotFather) в Telegram
2. Впишите токен в `FoodPlanner.Bot.Telegram/appsettings.json`:

```json
{
  "Telegram": {
    "BotToken": "ВАШ_ТОКЕН",
    "ApiBaseUrl": "https://localhost:7215"
  }
}
```

3. Запустите бота:

```bash
dotnet run --project FoodPlanner.Bot.Telegram
```

### 6. Запуск VK-бота

1. Создайте сообщество ВКонтакте
2. Включите Callback API, получите токен
3. Впишите токен в `FoodPlanner.Bot.VK/appsettings.json`:

```json
{
  "VK": {
    "AccessToken": "ВАШ_ТОКЕН",
    "ConfirmationCode": "КОД_ПОДТВЕРЖДЕНИЯ",
    "ApiBaseUrl": "https://localhost:7215"
  }
}
```

4. Запустите бота:

```bash
dotnet run --project FoodPlanner.Bot.VK
```

---

## REST API

Все эндпоинты доступны через Swagger UI:
`https://localhost:7215/swagger`

### Продукты

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/products` | Все продукты |
| `GET` | `/api/products/{id}` | Продукт по ID |
| `GET` | `/api/products/zone/{zone}` | Продукты по зоне хранения |
| `GET` | `/api/products/category/{category}` | Продукты по категории |
| `GET` | `/api/products/status/{status}` | Продукты по статусу наличия |
| `GET` | `/api/products/search?q=...` | Поиск продуктов |
| `POST` | `/api/products` | Добавить продукт |
| `PUT` | `/api/products/{id}` | Обновить продукт |
| `DELETE` | `/api/products/{id}` | Удалить продукт |

**Зоны хранения** (zone): `Fridge`, `VegetableAndFruitShelf`, `DairyShelf`,
`CannedGoodsShelf`, `FridgeDoor`, `Freezer`, `BakingShelf`,
`GrainsAndPastaShelf`, `SpicesAndSeasoningsShelf`, `CoffeeAndTea`, `HouseholdSupplies`

**Статусы** (status): `InStock` (0), `OutOfStock` (1), `LowStock` (2)

### Рецепты

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/recipes` | Все рецепты |
| `GET` | `/api/recipes/{id}` | Рецепт по ID |
| `POST` | `/api/recipes` | Создать рецепт |
| `PUT` | `/api/recipes/{id}` | Обновить рецепт |
| `DELETE` | `/api/recipes/{id}` | Удалить рецепт |
| `POST` | `/api/recipes/match` | Подбор рецептов по продуктам |

### Гарвардская тарелка

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/harvardplate/analysis` | Анализ текущего баланса рациона |
| `GET` | `/api/harvardplate/recipes` | Рецепты на основе имеющихся продуктов |

### Списки покупок

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/shoppinglists` | Все списки |
| `GET` | `/api/shoppinglists/{id}` | Список по ID |
| `POST` | `/api/shoppinglists/from-recipe/{recipeId}` | Создать из рецепта |
| `POST` | `/api/shoppinglists/merge?list1=...&list2=...` | Объединить два списка |
| `PUT` | `/api/shoppinglists/{listId}/items/{itemId}/toggle` | Переключить статус покупки |
| `DELETE` | `/api/shoppinglists/{id}` | Удалить список |

---

## Веб-интерфейс (Vue 3)

### Страницы

| Страница | Роут | Описание |
|---|---|---|
| Продукты | `/products` | Таблица продуктов по зонам хранения, поиск, фильтры, добавление/редактирование/удаление |
| Гарвардская тарелка | `/harvard-plate` | Круговые диаграммы, прогресс-бары, рекомендации, подбор рецептов |
| Рецепты | `/recipes` | Карточки рецептов, CRUD, теги, ингредиенты, кнопка «В список покупок» |
| Списки покупок | `/shopping-list` | Боковая панель со списками, чекбоксы куплено/нет, прогресс-бар |

### Управление продуктами

- **Добавление**: кнопка «+ Добавить продукт»
- **Редактирование**: кнопка ✏️ в таблице
- **Удаление**: кнопка 🗑 в таблице
- **Изменение статуса**: клик по иконке статуса (✅/❌/⚠️)
- **Поиск**: текстовое поле вверху страницы
- **Фильтры**: по зоне хранения и статусу наличия

---

## Telegram-бот — Команды

| Команда | Описание |
|---|---|
| `/start` | Приветствие |
| `/help` | Список команд |
| `/products` | Все продукты по зонам |
| `/outofstock` | Отсутствующие продукты |
| `/plate` | Анализ гарвардской тарелки |
| `/recipes` | Доступные рецепты |
| `/recipe <id>` | Детали рецепта |
| `/shopping` | Формирование списка покупок |

---

## VK-бот — Команды

| Команда | Описание |
|---|---|
| `/start`, `начать` | Приветствие |
| `/help`, `помощь` | Список команд |
| `/products`, `продукты` | Все продукты |
| `/outofstock`, `нетвналичии` | Отсутствующие продукты |
| `/plate`, `тарелка` | Анализ гарвардской тарелки |
| `/recipes`, `рецепты` | Доступные рецепты |
| `/shopping`, `покупки` | Список покупок |

---

## Модели данных

### Product

```json
{
  "id": "-guid-",
  "name": "Картофель",
  "storageZone": 1,
  "category": 0,
  "stockStatus": 0,
  "hasReserve": false,
  "defaultUnit": 0,
  "caloriesPer100g": 77,
  "proteinPer100g": 2.0,
  "fatPer100g": 0.1,
  "carbsPer100g": 17.0,
  "quantityInStock": 0
}
```

### Recipe

```json
{
  "id": "-guid-",
  "name": "Гречка с овощами",
  "description": "Полезный гарнир",
  "mealType": "Обед",
  "servings": 2,
  "preparationTimeMinutes": 30,
  "tags": ["гарнир", "вегетарианское"],
  "ingredients": [
    { "productId": "...", "amount": 200, "unit": 0 }
  ],
  "steps": ["Отварить гречку", "Обжарить овощи", "Смешать"]
}
```

### ShoppingList

```json
{
  "id": "-guid-",
  "name": "Для рецепта: Гречка с овощами",
  "createdDate": "2026-08-26T00:00:00Z",
  "items": [
    {
      "id": "-guid-",
      "productId": "...",
      "productName": "Морковь",
      "amount": 2,
      "unit": 2,
      "isPurchased": false,
      "sourceRecipeName": "Гречка с овощами"
    }
  ]
}
```

---

## Перезалив данных из Excel

Если Excel-таблица обновлена, повторно запустите импорт:

```bash
dotnet run --project FoodPlanner.Seed -- "путь_к_файлу.xlsx"
```

По умолчанию используется файл `Список основных продуктов.xlsx`
в корне проекта.

---

## Конфигурация

### FoodPlanner.Api/appsettings.json

```json
{
  "DataPath": "D:\\MyProject\\OpenCode\\FoodPlanner\\FoodPlanner.Api\\Data",
  "FrontendPath": "D:\\MyProject\\OpenCode\\FoodPlanner\\FoodPlanner.Web\\dist"
}
```

- `DataPath` — папка с JSON-файлами данных
- `FrontendPath` — папка `dist/` собранного Vue-приложения

---

## Возможности для развития

- [ ] Автоматическое заполнение БЖУ продуктов из открытых API
- [ ] Экспорт данных в Excel/CSV
- [ ] Планирование меню на неделю
- [ ] Уведомления о заканчивающихся продуктах
- [ ] Распознавание голосовых команд в Telegram-боте
- [ ] Мобильное приложение (MAUI / Flutter) через тот же API
