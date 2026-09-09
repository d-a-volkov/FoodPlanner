using System.Text.Json;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;
using Microsoft.Extensions.Logging;
using VkNet.Abstractions;
using VkNet.Model;

namespace FoodPlanner.Bot.VK.Handlers;

public class BotUpdateHandler
{
    private readonly FoodPlannerApiClient _api;
    private readonly IVkApi _vkApi;
    private readonly ILogger<BotUpdateHandler> _logger;

    public BotUpdateHandler(FoodPlannerApiClient api, IVkApi vkApi, ILogger<BotUpdateHandler> logger)
    {
        _api = api;
        _vkApi = vkApi;
        _logger = logger;
    }

    public string HandleCallback(JsonElement update)
    {
        try
        {
            var type = update.GetProperty("type").GetString();
            if (type == "confirmation")
            {
                var groupId = update.GetProperty("group_id").GetInt64();
                return GetConfirmationCode();
            }

            if (type == "message_new")
            {
                var message = update.GetProperty("object").GetProperty("message");
                var text = message.GetProperty("text").GetString() ?? "";
                var peerId = message.GetProperty("peer_id").GetInt64();
                var fromId = message.GetProperty("from_id").GetInt64();

                _logger.LogInformation("VK сообщение от {User}: {Text}", fromId, text);

                var command = text.Split(' ')[0].ToLowerInvariant();
                var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (command)
                {
                    case "/start":
                    case "начать":
                        SendVkMessage(peerId, GetWelcomeText());
                        break;
                    case "/help":
                    case "помощь":
                        SendVkMessage(peerId, GetHelpText());
                        break;
                    case "/products":
                    case "продукты":
                        SendProductsAsync(peerId).Wait();
                        break;
                    case "/outofstock":
                    case "нетвналичии":
                        SendOutOfStockAsync(peerId).Wait();
                        break;
                    case "/plate":
                    case "тарелка":
                        SendHarvardPlateAsync(peerId).Wait();
                        break;
                    case "/recipes":
                    case "рецепты":
                        SendRecipesAsync(peerId).Wait();
                        break;
                    case "/shopping":
                    case "покупки":
                        SendShoppingListAsync(peerId).Wait();
                        break;
                    default:
                        SendVkMessage(peerId, "Неизвестная команда. Введите /help для списка команд.");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки VK callback");
        }

        return "ok";
    }

    private void SendVkMessage(long peerId, string text)
    {
        _vkApi.Messages.Send(new MessagesSendParams
        {
            PeerId = peerId,
            Message = text,
            RandomId = Random.Shared.Next()
        });
    }

    private string GetConfirmationCode() => ""; // Заполняется из конфигурации

    private string GetWelcomeText() =>
        """
        Добро пожаловать в FoodPlanner Bot!

        Этот бот поможет вам управлять продуктами и следить за балансом рациона.

        Введите /help для списка всех команд.
        """;

    private string GetHelpText() =>
        """
        Доступные команды:

        /products — Все продукты и их наличие
        /outofstock — Продукты, которых нет в наличии
        /plate — Анализ гарвардской тарелки
        /recipes — Доступные рецепты
        /shopping — Формирование списка покупок
        /help — Помощь
        """;

    private async Task SendProductsAsync(long peerId)
    {
        SendVkMessage(peerId, "Загрузка продуктов...");

        var products = await _api.GetProductsAsync();
        var grouped = products
            .GroupBy(p => p.StorageZone)
            .OrderBy(g => g.Key);

        var lines = new List<string>();
        foreach (var zone in grouped)
        {
            var zoneName = GetZoneName(zone.Key);
            lines.Add($"\n{zoneName}:");
            foreach (var p in zone)
            {
                var status = p.StockStatus switch
                {
                    StockStatus.InStock => "+",
                    StockStatus.OutOfStock => "-",
                    StockStatus.LowStock => "!",
                    StockStatus.NotUsed => "x",
                    _ => "?"
                };
                var reserve = p.HasReserve ? " (запас)" : "";
                lines.Add($"  [{status}] {p.Name}{reserve}");
            }
        }

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        SendVkMessage(peerId, message);
    }

    private async Task SendOutOfStockAsync(long peerId)
    {
        SendVkMessage(peerId, "Загрузка...");

        var products = await _api.GetProductsAsync();
        var outOfStock = products
            .Where(p => p.StockStatus == StockStatus.OutOfStock)
            .OrderBy(p => p.Name)
            .ToList();

        if (outOfStock.Count == 0)
        {
            SendVkMessage(peerId, "Все продукты в наличии!");
            return;
        }

        var lines = new List<string> { "Продукты, которых нет:" };
        foreach (var p in outOfStock)
        {
            var reserve = p.HasReserve ? " (есть запас)" : "";
            lines.Add($"  - {p.Name}{reserve}");
        }

        SendVkMessage(peerId, string.Join("\n", lines));
    }

    private async Task SendHarvardPlateAsync(long peerId)
    {
        SendVkMessage(peerId, "Анализ гарвардской тарелки...");

        var analysis = await _api.GetHarvardPlateAnalysisAsync();

        var lines = new List<string>
        {
            "Гарвардская тарелка",
            $"Баланс рациона: {analysis.OverallScore}%",
            ""
        };

        foreach (var ratio in analysis.Ratios)
        {
            var bar = GetProgressBar(ratio.CurrentPercentage, ratio.RecommendedPercentage);
            lines.Add($"{ratio.DisplayName}");
            lines.Add($"  {bar} {ratio.CurrentPercentage}% / {ratio.RecommendedPercentage}%");
        }

        if (analysis.Recommendations.Count > 0)
        {
            lines.Add("");
            lines.Add("Рекомендации:");
            foreach (var rec in analysis.Recommendations)
            {
                lines.Add($"  * {rec}");
            }
        }

        if (analysis.SuggestedProducts.Count > 0)
        {
            lines.Add("");
            lines.Add("Рекомендуемые продукты:");
            lines.Add(string.Join(", ", analysis.SuggestedProducts.Select(p => p.Name)));
        }

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        SendVkMessage(peerId, message);
    }

    private async Task SendRecipesAsync(long peerId)
    {
        SendVkMessage(peerId, "Поиск рецептов...");

        var matches = await _api.GetMatchingRecipesAsync();

        if (matches.Count == 0)
        {
            SendVkMessage(peerId, "Рецептов пока нет.");
            return;
        }

        var lines = new List<string> { "Рецепты:" };
        foreach (var match in matches.Take(15))
        {
            var icon = match.AvailabilityPercentage >= 100 ? "[OK]" :
                       match.AvailabilityPercentage >= 50 ? "[~]" : "[!]";
            var missing = match.MissingIngredients.Count > 0
                ? $" (нет: {string.Join(", ", match.MissingIngredients.Select(IngredientLabel))})"
                : " — всё есть!";
            lines.Add($"{icon} {match.Recipe.Name} — {match.AvailabilityPercentage}%{missing}");
        }

        if (matches.Count > 15)
            lines.Add($"\n...и ещё {matches.Count - 15} рецептов");

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        SendVkMessage(peerId, message);
    }

    private async Task SendShoppingListAsync(long peerId)
    {
        SendVkMessage(peerId, "Формирование списка покупок...");

        var matches = await _api.GetMatchingRecipesAsync();
        var readyRecipes = matches
            .Where(m => m.AvailabilityPercentage >= 80)
            .ToList();

        if (readyRecipes.Count == 0)
        {
            SendVkMessage(peerId,
                "Нет рецептов, для которых достаточно продуктов. Добавьте продукты.");
            return;
        }

        var lines = new List<string> { "Рекомендуемые списки покупок:" };
        foreach (var match in readyRecipes.Take(5))
        {
            var missing = match.MissingIngredients
                .Select(i => $"{IngredientLabel(i)} ({i.Amount} ед.)")
                .ToList();
            if (missing.Count > 0)
            {
                lines.Add($"");
                lines.Add($"{match.Recipe.Name} ({match.AvailabilityPercentage}%)");
                lines.Add($"  Нужно купить: {string.Join(", ", missing)}");
            }
            else
            {
                lines.Add($"");
                lines.Add($"{match.Recipe.Name} — всё есть!");
            }
        }

        var message = string.Join("\n", lines);
        if (message.Length > 4000)
            message = message[..4000];

        SendVkMessage(peerId, message);
    }

    private static string GetZoneName(StorageZone zone) => zone switch
    {
        StorageZone.Fridge => "Холодильник",
        StorageZone.VegetableAndFruitShelf => "Овощи и фрукты",
        StorageZone.DairyShelf => "Молочные продукты",
        StorageZone.CannedGoodsShelf => "Консервы",
        StorageZone.FridgeDoor => "Дверца",
        StorageZone.Freezer => "Морозилка",
        StorageZone.BakingShelf => "Выпечка",
        StorageZone.GrainsAndPastaShelf => "Крупы и макароны",
        StorageZone.SpicesAndSeasoningsShelf => "Специи",
        StorageZone.CoffeeAndTea => "Кофе и чай",
        StorageZone.HouseholdSupplies => "Хоз. товары",
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
        return $"[{new string('#', filled)}{new string('-', empty)}]";
    }
}
