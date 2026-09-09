using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

public interface IExternalRecipeService
{
    Task<List<ExternalRecipeResult>> SearchAsync(string query, ExternalRecipeSource? source = null, int maxResults = 10);
    Task<ExternalRecipeResult?> GetDetailsAsync(ExternalRecipeSource source, string url);
    Task<List<ExternalRecipeResult>> SearchByAvailableProductsAsync(ExternalRecipeSource? source = null, int maxResults = 10, double minAvailability = 0);
    Task<Recipe> ImportAsync(ExternalRecipeResult result);
}