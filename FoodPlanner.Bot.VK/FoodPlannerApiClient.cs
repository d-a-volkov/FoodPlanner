using System.Text.Json;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Bot.VK;

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

    public void Dispose() => _http.Dispose();
}
