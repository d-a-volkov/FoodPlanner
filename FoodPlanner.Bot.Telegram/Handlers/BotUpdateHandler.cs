using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FoodPlanner.Bot.Telegram.Handlers;

public class BotUpdateHandler
{
    private readonly FoodPlannerApiClient _api;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<BotUpdateHandler> _logger;

    public BotUpdateHandler(FoodPlannerApiClient api, ITelegramBotClient bot, ILogger<BotUpdateHandler> logger)
    {
        _api = api;
        _bot = bot;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is { Text: { } text })
        {
            _logger.LogInformation("Получено сообщение от {User}: {Text}",
                update.Message.From?.Username ?? update.Message.From?.Id.ToString(), text);

            try
            {
                await HandleCommandAsync(update.Message.Chat.Id, text, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки команды");
                await _bot.SendMessage(update.Message.Chat.Id,
                    "Произошла ошибка при обработке команды. Попробуйте позже.",
                    cancellationToken: ct);
            }
        }
    }

    private async Task HandleCommandAsync(long chatId, string text, CancellationToken ct)
    {
        var command = text.Split(' ')[0].ToLowerInvariant();
        var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (command)
        {
            case "/start":
                await SendWelcomeAsync(chatId, ct);
                break;
            case "/help":
                await SendHelpAsync(chatId, ct);
                break;
            case "/products":
                await SendProductsAsync(chatId, ct);
                break;
            case "/outofstock":
                await SendOutOfStockAsync(chatId, ct);
                break;
            case "/plate":
                await SendHarvardPlateAsync(chatId, ct);
                break;
            case "/recipes":
                await SendRecipesAsync(chatId, ct);
                break;
            case "/recipe":
                if (args.Length > 1 && Guid.TryParse(args[1], out var recipeId))
                    await SendRecipeDetailsAsync(chatId, recipeId, ct);
                else
                    await _bot.SendMessage(chatId, "Использование: /recipe <id_рецепта>",
                        cancellationToken: ct);
                break;
            case "/shopping":
                await SendShoppingListFromRecipesAsync(chatId, ct);
                break;
            default:
                await _bot.SendMessage(chatId,
                    "Неизвестная команда. Введите /help для списка команд.",
                    cancellationToken: ct);
                break;
        }
    }

    private async Task SendWelcomeAsync(long chatId, CancellationToken ct)
    {
        var welcome = """
            Добро пожаловать в FoodPlanner Bot!

            Этот бот поможет вам управлять продуктами, следить за балансом рациона по гарвардской тарелке и формировать списки покупок.

            Введите /help для списка всех команд.
            """;

        await _bot.SendMessage(chatId, welcome, cancellationToken: ct);
    }

    private async Task SendHelpAsync(long chatId, CancellationToken ct)
    {
        var help = """
            Доступные команды:

            /products — Все продукты и их наличие
            /outofstock — Продукты, которых нет в наличии
            /plate — Анализ гарвардской тарелки
            /recipes — Доступные рецепты
            /recipe <id> — Детали рецепта
            /shopping — Сформировать список покупок
            /help — Помощь
            """;

        await _bot.SendMessage(chatId, help, cancellationToken: ct);
    }

    private async Task SendProductsAsync(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId, "Загрузка продуктов...", cancellationToken: ct);

        var products = await _api.GetProductsAsync();
        var grouped = products
            .GroupBy(p => p.StorageZone)
            .OrderBy(g => g.Key);

        var lines = new List<string>();
        foreach (var zone in grouped)
        {
            var zoneName = GetZoneName(zone.Key);
            lines.Add($"\n<b>{zoneName}:</b>");
            foreach (var p in zone)
            {
                var status = p.StockStatus switch
                {
                    StockStatus.InStock => "✅",
                    StockStatus.OutOfStock => "❌",
                    StockStatus.LowStock => "⚠️",
                    StockStatus.NotUsed => "🚫",
                    _ => "❓"
                };
                var reserve = p.HasReserve ? " 🔄" : "";
                lines.Add($"  {status}{reserve} {p.Name}");
            }
        }

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000] + "\n...";

        await _bot.SendMessage(chatId, message, ParseMode.Html, cancellationToken: ct);
    }

    private async Task SendOutOfStockAsync(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId, "Загрузка...", cancellationToken: ct);

        var products = await _api.GetProductsAsync();
        var outOfStock = products
            .Where(p => p.StockStatus == StockStatus.OutOfStock)
            .OrderBy(p => p.StorageZone)
            .ThenBy(p => p.Name)
            .ToList();

        if (outOfStock.Count == 0)
        {
            await _bot.SendMessage(chatId, "Все продукты в наличии! 🎉",
                cancellationToken: ct);
            return;
        }

        var lines = new List<string> { "<b>Продукты, которых нет:</b>" };
        foreach (var p in outOfStock)
        {
            var reserve = p.HasReserve ? " (есть запас)" : "";
            lines.Add($"  ❌ {p.Name}{reserve}");
        }

        await _bot.SendMessage(chatId, string.Join("\n", lines), ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task SendHarvardPlateAsync(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId, "Анализ гарвардской тарелки...", cancellationToken: ct);

        var analysis = await _api.GetHarvardPlateAnalysisAsync();

        var lines = new List<string>
        {
            $"<b>🍽 Гарвардская тарелка</b>",
            $"Баланс рациона: <b>{analysis.OverallScore}%</b>",
            ""
        };

        foreach (var ratio in analysis.Ratios)
        {
            var bar = GetProgressBar(ratio.CurrentPercentage, ratio.RecommendedPercentage);
            lines.Add($"<b>{ratio.DisplayName}</b>");
            lines.Add($"  {bar} {ratio.CurrentPercentage}% / {ratio.RecommendedPercentage}%");
        }

        if (analysis.Recommendations.Count > 0)
        {
            lines.Add("");
            lines.Add("<b>📋 Рекомендации:</b>");
            foreach (var rec in analysis.Recommendations)
            {
                lines.Add($"  • {rec}");
            }
        }

        if (analysis.SuggestedProducts.Count > 0)
        {
            lines.Add("");
            lines.Add("<b>🛒 Рекомендуемые продукты:</b>");
            lines.Add(string.Join(", ", analysis.SuggestedProducts.Select(p => p.Name)));
        }

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        await _bot.SendMessage(chatId, message, ParseMode.Html, cancellationToken: ct);
    }

    private async Task SendRecipesAsync(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId, "Поиск рецептов...", cancellationToken: ct);

        var matches = await _api.GetMatchingRecipesAsync();

        if (matches.Count == 0)
        {
            await _bot.SendMessage(chatId, "Рецептов пока нет.",
                cancellationToken: ct);
            return;
        }

        var lines = new List<string> { "<b>📖 Рецепты:</b>" };
        foreach (var match in matches.Take(15))
        {
            var icon = match.AvailabilityPercentage >= 100 ? "🟢" :
                       match.AvailabilityPercentage >= 50 ? "🟡" : "🔴";
            var missing = match.MissingIngredients.Count > 0
                ? $" (нет: {string.Join(", ", match.MissingIngredients.Select(IngredientLabel))})"
                : " — всё есть!";
            lines.Add($"{icon} <b>{match.Recipe.Name}</b> — {match.AvailabilityPercentage}%{missing}");
        }

        if (matches.Count > 15)
            lines.Add($"\n...и ещё {matches.Count - 15} рецептов");

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        await _bot.SendMessage(chatId, message, ParseMode.Html, cancellationToken: ct);
    }

    private async Task SendRecipeDetailsAsync(long chatId, Guid recipeId, CancellationToken ct)
    {
        var recipes = await _api.GetRecipesAsync();
        var recipe = recipes.FirstOrDefault(r => r.Id == recipeId);
        if (recipe == null)
        {
            await _bot.SendMessage(chatId, "Рецепт не найден.", cancellationToken: ct);
            return;
        }

        var lines = new List<string>
        {
            $"<b>📖 {recipe.Name}</b>",
            ""
        };

        if (!string.IsNullOrEmpty(recipe.Description))
            lines.Add($"{recipe.Description}\n");

        if (!string.IsNullOrEmpty(recipe.MealType))
            lines.Add($"🕐 Тип: {recipe.MealType}");
        if (recipe.PreparationTimeMinutes > 0)
            lines.Add($"⏱ Время: {recipe.PreparationTimeMinutes} мин");
        if (recipe.Servings > 0)
            lines.Add($"👥 Порции: {recipe.Servings}");

        if (recipe.Ingredients.Count > 0)
        {
            lines.Add("");
            lines.Add("<b>Ингредиенты:</b>");
            foreach (var ing in recipe.Ingredients)
                lines.Add($"  • {IngredientLabel(ing)} — {ing.Amount} ед.");
        }

        if (recipe.Steps.Count > 0)
        {
            lines.Add("");
            lines.Add("<b>Приготовление:</b>");
            for (int i = 0; i < recipe.Steps.Count; i++)
                lines.Add($"  {i + 1}. {recipe.Steps[i]}");
        }

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        await _bot.SendMessage(chatId, message, ParseMode.Html, cancellationToken: ct);
    }

    private async Task SendShoppingListFromRecipesAsync(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId, "Формирование списка покупок...", cancellationToken: ct);

        var matches = await _api.GetMatchingRecipesAsync();
        var readyRecipes = matches
            .Where(m => m.AvailabilityPercentage >= 80)
            .ToList();

        if (readyRecipes.Count == 0)
        {
            await _bot.SendMessage(chatId,
                "Нет рецептов, для которых достаточно продуктов. Добавьте продукты или попробуйте другие рецепты.",
                cancellationToken: ct);
            return;
        }

        var lines = new List<string> { "<b>🛒 Рекомендуемые списки покупок:</b>" };
        foreach (var match in readyRecipes.Take(5))
        {
            var missing = match.MissingIngredients
                .Select(i => $"{IngredientLabel(i)} ({i.Amount} ед.)")
                .ToList();
            if (missing.Count > 0)
            {
                lines.Add($"\n<b>{match.Recipe.Name}</b> ({match.AvailabilityPercentage}%)");
                lines.Add($"  Нужно купить: {string.Join(", ", missing)}");
            }
            else
            {
                lines.Add($"\n<b>{match.Recipe.Name}</b> — всё есть! ✅");
            }
        }

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        await _bot.SendMessage(chatId, message, ParseMode.Html, cancellationToken: ct);
    }

    private static string GetZoneName(StorageZone zone) => zone switch
    {
        StorageZone.Fridge => "🧊 Холодильник",
        StorageZone.VegetableAndFruitShelf => "🥕 Овощи и фрукты",
        StorageZone.DairyShelf => "🥛 Молочные продукты",
        StorageZone.CannedGoodsShelf => "🥫 Консервы",
        StorageZone.FridgeDoor => "🚪 Дверца",
        StorageZone.Freezer => "❄️ Морозилка",
        StorageZone.BakingShelf => "🧁 Выпечка",
        StorageZone.GrainsAndPastaShelf => "🌾 Крупы и макароны",
        StorageZone.SpicesAndSeasoningsShelf => "🧂 Специи",
        StorageZone.CoffeeAndTea => "☕ Кофе и чай",
        StorageZone.HouseholdSupplies => "🧹 Хоз. товары",
        _ => zone.ToString()
    };

    private static string IngredientLabel(RecipeIngredient ingredient)
        => ingredient.ProductName
           ?? (ingredient.ProductId.HasValue
               ? ingredient.ProductId.Value.ToString("N")[..8]
               : "—");

    private static string GetProgressBar(double current, double recommended)
    {
        var filled = Math.Min((int)(current / 5), 20);
        var empty = Math.Max(0, 20 - filled);
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }
}
