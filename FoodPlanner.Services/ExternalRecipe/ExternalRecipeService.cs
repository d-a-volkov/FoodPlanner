using System.Text;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FoodPlanner.Services.ExternalRecipe;

public class ExternalRecipeService : IExternalRecipeService
{
    private const string HttpClientName = "ExternalRecipes";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IProductService _productService;
    private readonly IRecipeService _recipeService;
    private readonly HundredMenuParser _hundredMenuParser = new();
    private readonly FoodRuParser _foodRuParser = new();

    public ExternalRecipeService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IProductService productService,
        IRecipeService recipeService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _productService = productService;
        _recipeService = recipeService;
    }

    public async Task<List<ExternalRecipeResult>> SearchAsync(
        string query, ExternalRecipeSource? source = null, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Запрос не может быть пустым", nameof(query));

        var results = new List<ExternalRecipeResult>();
        foreach (var recipeSource in GetSources(source))
        {
            var searchResults = await SearchSourceSafeAsync(recipeSource, query, maxResults);
            results.AddRange(searchResults);
            if (results.Count >= maxResults) break;
        }

        return results.Take(maxResults).ToList();
    }

    public async Task<ExternalRecipeResult?> GetDetailsAsync(ExternalRecipeSource source, string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var cacheKey = $"ext_recipes_detail_{source}_{url}";
        try
        {
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;

                var html = await DownloadAsync(url);
                var result = source == ExternalRecipeSource.HundredMenu
                    ? _hundredMenuParser.ParseDetails(html, url)
                    : _foodRuParser.ParseDetails(html, url);

                if (result != null)
                    await EnrichWithAvailabilityAsync(result);

                return result;
            });
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<ExternalRecipeResult>> SearchByAvailableProductsAsync(
        ExternalRecipeSource? source = null, int maxResults = 10, double minAvailability = 0)
    {
        var products = await _productService.GetAllAsync();
        var availableNames = products
            .Where(p => p.StockStatus != StockStatus.NotUsed &&
                        (p.StockStatus == StockStatus.InStock || p.HasReserve))
            .Select(p => p.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(3)
            .ToList();

        if (availableNames.Count == 0) return new List<ExternalRecipeResult>();

        var query = string.Join(" ", availableNames);
        var searchResults = await SearchAsync(query, source, maxResults);

        var enriched = new List<ExternalRecipeResult>();
        var order = 0;
        foreach (var searchResult in searchResults)
        {
            searchResult.InitialOrder = order++;
            var details = await GetDetailsAsync(searchResult.Source, searchResult.Url);
            if (details == null || details.Ingredients.Count == 0) continue;

            searchResult.Ingredients = details.Ingredients;
            searchResult.Steps = details.Steps;
            searchResult.Servings = details.Servings;
            searchResult.Description ??= details.Description;
            searchResult.AvailabilityPercentage = details.AvailabilityPercentage;
            searchResult.MissingIngredients = details.MissingIngredients;

            enriched.Add(searchResult);
        }

        return enriched
            .Where(r => r.AvailabilityPercentage >= minAvailability)
            .OrderByDescending(r => r.AvailabilityPercentage)
            .ThenBy(r => r.InitialOrder)
            .Take(maxResults)
            .ToList();
    }

    public async Task<Recipe> ImportAsync(ExternalRecipeResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (string.IsNullOrWhiteSpace(result.Title))
            throw new InvalidOperationException("Рецепт не содержит названия.");

        if (result.Ingredients.Count == 0 && !string.IsNullOrWhiteSpace(result.Url))
        {
            var details = await GetDetailsAsync(result.Source, result.Url);
            if (details != null)
            {
                result.Ingredients = details.Ingredients;
                result.Steps = details.Steps;
                result.Servings = details.Servings;
                result.Description ??= details.Description;
                result.PreparationTimeMinutes ??= details.PreparationTimeMinutes;
            }
        }

        var products = await _productService.GetAllAsync();
        var candidates = products.Where(p => p.StockStatus != StockStatus.NotUsed).ToList();

        var ingredients = result.Ingredients.Select(ingredient =>
        {
            var matched = IngredientNameMatcher.Match(ingredient.Name, candidates);
            return new RecipeIngredient
            {
                ProductId = matched?.Id,
                ProductName = matched?.Name ?? ingredient.Name,
                Amount = ingredient.Amount,
                Unit = ingredient.Unit
            };
        }).ToList();

        var sourceName = result.Source == ExternalRecipeSource.HundredMenu ? "1000.menu" : "food.ru";

        var tags = new List<string>(result.Tags)
        {
            "Внешний рецепт",
            $"Источник: {sourceName}"
        };
        if (!string.IsNullOrWhiteSpace(result.Url))
            tags.Add(result.Url);

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = result.Title.Trim(),
            Description = result.Description ?? string.Empty,
            MealType = result.MealType ?? string.Empty,
            Servings = result.Servings > 0 ? result.Servings : 1,
            PreparationTimeMinutes = result.PreparationTimeMinutes ?? 0,
            Ingredients = ingredients,
            Steps = result.Steps,
            Tags = tags
        };

        return await _recipeService.CreateAsync(recipe);
    }

    private async Task<List<ExternalRecipeResult>> SearchSourceSafeAsync(
        ExternalRecipeSource source, string query, int maxResults)
    {
        try
        {
            return await SearchSourceAsync(source, query, maxResults);
        }
        catch
        {
            return new List<ExternalRecipeResult>();
        }
    }

    private async Task<List<ExternalRecipeResult>> SearchSourceAsync(
        ExternalRecipeSource source, string query, int maxResults)
    {
        var cacheKey = $"ext_recipes_search_{source}_{query}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            var url = source == ExternalRecipeSource.HundredMenu
                ? $"https://1000.menu/cooking/search?str={Uri.EscapeDataString(query)}"
                : $"https://food.ru/search?material=recipe&query={Uri.EscapeDataString(query)}";

            var html = await DownloadAsync(url);
            var results = source == ExternalRecipeSource.HundredMenu
                ? _hundredMenuParser.ParseSearch(html)
                : _foodRuParser.ParseSearch(html);

            return results.Take(maxResults).ToList();
        }) ?? new List<ExternalRecipeResult>();
    }

    private async Task EnrichWithAvailabilityAsync(ExternalRecipeResult result)
    {
        var products = await _productService.GetAllAsync();
        var available = products
            .Where(p => p.StockStatus != StockStatus.NotUsed &&
                        (p.StockStatus == StockStatus.InStock || p.HasReserve))
            .ToList();

        if (result.Ingredients.Count == 0)
        {
            result.AvailabilityPercentage = null;
            return;
        }

        var missing = new List<ExternalIngredient>();
        var matchedCount = 0;
        foreach (var ingredient in result.Ingredients)
        {
            if (IngredientNameMatcher.Match(ingredient.Name, available) != null)
                matchedCount++;
            else
                missing.Add(ingredient);
        }

        result.AvailabilityPercentage = Math.Round((double)matchedCount / result.Ingredients.Count * 100, 1);
        result.MissingIngredients = missing;
    }

    private async Task<string> DownloadAsync(string url)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var encoding = response.Content.Headers.ContentType?.CharSet;

        if (!string.IsNullOrWhiteSpace(encoding) &&
            encoding.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static IEnumerable<ExternalRecipeSource> GetSources(ExternalRecipeSource? source)
        => source.HasValue
            ? new[] { source.Value }
            : new[] { ExternalRecipeSource.HundredMenu, ExternalRecipeSource.FoodRu };
}