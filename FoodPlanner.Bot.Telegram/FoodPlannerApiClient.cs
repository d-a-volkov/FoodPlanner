using System.Text.Json;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Bot.Telegram;

public class FoodPlannerApiClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public FoodPlannerApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        var response = await _http.GetAsync("/api/products");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Product>>(json, JsonOptions) ?? [];
    }

    public async Task<HarvardPlateAnalysis> GetHarvardPlateAnalysisAsync()
    {
        var response = await _http.GetAsync("/api/harvardplate/analysis");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<HarvardPlateAnalysis>(json, JsonOptions)!;
    }

    public async Task<List<RecipeMatch>> GetMatchingRecipesAsync()
    {
        var response = await _http.GetAsync("/api/harvardplate/recipes");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<RecipeMatch>>(json, JsonOptions) ?? [];
    }

    public async Task<List<Recipe>> GetRecipesAsync()
    {
        var response = await _http.GetAsync("/api/recipes");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Recipe>>(json, JsonOptions) ?? [];
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(product, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await _http.PostAsync("/api/products", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Product>(json, JsonOptions)!;
    }

    public async Task<bool> ToggleProductStockAsync(Product product)
    {
        var updated = new Product
        {
            Id = product.Id,
            Name = product.Name,
            StorageZone = product.StorageZone,
            Category = product.Category,
            StockStatus = product.StockStatus == StockStatus.InStock ? StockStatus.OutOfStock : StockStatus.InStock,
            HasReserve = product.HasReserve,
            DefaultUnit = product.DefaultUnit,
            CaloriesPer100g = product.CaloriesPer100g,
            ProteinPer100g = product.ProteinPer100g,
            FatPer100g = product.FatPer100g,
            CarbsPer100g = product.CarbsPer100g,
            QuantityInStock = product.QuantityInStock
        };
        var content = new StringContent(
            JsonSerializer.Serialize(updated, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await _http.PutAsync($"/api/products/{product.Id}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<ShoppingList> CreateShoppingListFromRecipeAsync(Guid recipeId)
    {
        var response = await _http.PostAsync($"/api/shoppinglists/from-recipe/{recipeId}", null);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ShoppingList>(json, JsonOptions)!;
    }

    public void Dispose() => _http.Dispose();
}
