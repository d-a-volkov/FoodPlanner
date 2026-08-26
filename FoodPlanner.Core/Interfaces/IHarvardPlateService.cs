using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

public interface IHarvardPlateService
{
    Task<HarvardPlateAnalysis> AnalyzeCurrentStockAsync();
    Task<List<RecipeMatch>> FindRecipesByAvailableProductsAsync();
    Task<List<RecipeMatch>> FindRecipesByAvailableProductsAsync(List<Product> products);
}
