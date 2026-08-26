using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

public interface IRecipeService
{
    Task<List<Recipe>> GetAllAsync();
    Task<Recipe?> GetByIdAsync(Guid id);
    Task<Recipe> CreateAsync(Recipe recipe);
    Task<Recipe> UpdateAsync(Recipe recipe);
    Task<bool> DeleteAsync(Guid id);
    Task<List<RecipeMatch>> GetRecipesByAvailableProductsAsync(List<Product> products);
}
