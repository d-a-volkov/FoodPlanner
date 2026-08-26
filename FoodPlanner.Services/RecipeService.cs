using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Services.Services;

public class RecipeService : IRecipeService
{
    private readonly IJsonStorageService<Recipe> _storage;

    public RecipeService(IJsonStorageService<Recipe> storage)
    {
        _storage = storage;
    }

    public async Task<List<Recipe>> GetAllAsync()
        => await _storage.GetAllAsync();

    public async Task<Recipe?> GetByIdAsync(Guid id)
        => await _storage.GetByIdAsync(id);

    public async Task<Recipe> CreateAsync(Recipe recipe)
    {
        recipe.Id = Guid.NewGuid();
        return await _storage.CreateAsync(recipe);
    }

    public async Task<Recipe> UpdateAsync(Recipe recipe)
        => await _storage.UpdateAsync(recipe);

    public async Task<bool> DeleteAsync(Guid id)
        => await _storage.DeleteAsync(id);

    public async Task<List<RecipeMatch>> GetRecipesByAvailableProductsAsync(List<Product> availableProducts)
    {
        var recipes = await _storage.GetAllAsync();
        var matches = new List<RecipeMatch>();

        foreach (var recipe in recipes)
        {
            var availableProductIds = availableProducts.Select(p => p.Id).ToHashSet();
            var matchedIngredients = recipe.Ingredients
                .Count(i => availableProductIds.Contains(i.ProductId));
            double availability = recipe.Ingredients.Count > 0
                ? (double)matchedIngredients / recipe.Ingredients.Count * 100
                : 0;

            var missingIngredients = recipe.Ingredients
                .Where(i => !availableProductIds.Contains(i.ProductId))
                .ToList();

            matches.Add(new RecipeMatch
            {
                Recipe = recipe,
                AvailabilityPercentage = Math.Round(availability, 1),
                MissingIngredients = missingIngredients
            });
        }

        return matches.OrderByDescending(m => m.AvailabilityPercentage).ToList();
    }
}
