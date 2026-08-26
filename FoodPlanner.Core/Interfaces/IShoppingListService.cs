using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

public interface IShoppingListService
{
    Task<List<ShoppingList>> GetAllAsync();
    Task<ShoppingList?> GetByIdAsync(Guid id);
    Task<ShoppingList> CreateFromRecipeAsync(Recipe recipe, List<Product> availableProducts);
    Task<ShoppingList> MergeListsAsync(Guid list1Id, Guid list2Id);
    Task<ShoppingItem> TogglePurchasedAsync(Guid listId, Guid itemId);
    Task<bool> DeleteAsync(Guid id);
}
