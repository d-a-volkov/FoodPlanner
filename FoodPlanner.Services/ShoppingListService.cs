using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Services.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly IJsonStorageService<ShoppingList> _storage;
    private readonly IProductService _productService;

    public ShoppingListService(
        IJsonStorageService<ShoppingList> storage,
        IProductService productService)
    {
        _storage = storage;
        _productService = productService;
    }

    public async Task<List<ShoppingList>> GetAllAsync()
        => await _storage.GetAllAsync();

    public async Task<ShoppingList?> GetByIdAsync(Guid id)
        => await _storage.GetByIdAsync(id);

    public async Task<ShoppingList> CreateFromRecipeAsync(
        Recipe recipe, List<Product> availableProducts)
    {
        var availableProductIds = availableProducts
            .Where(p => p.StockStatus == StockStatus.InStock || p.HasReserve)
            .Select(p => p.Id)
            .ToHashSet();

        var items = new List<ShoppingItem>();

        foreach (var ingredient in recipe.Ingredients)
        {
            if (!availableProductIds.Contains(ingredient.ProductId))
            {
                var product = await _productService.GetByIdAsync(ingredient.ProductId);
                items.Add(new ShoppingItem
                {
                    ProductId = ingredient.ProductId,
                    ProductName = product?.Name ?? "Неизвестный продукт",
                    Amount = ingredient.Amount,
                    Unit = ingredient.Unit,
                    IsPurchased = false,
                    SourceRecipeName = recipe.Name
                });
            }
        }

        var shoppingList = new ShoppingList
        {
            Name = $"Для рецепта: {recipe.Name}",
            Items = items,
            CreatedDate = DateTime.UtcNow
        };

        return await _storage.CreateAsync(shoppingList);
    }

    public async Task<ShoppingList> MergeListsAsync(Guid list1Id, Guid list2Id)
    {
        var allLists = await _storage.GetAllAsync();
        var list1 = allLists.FirstOrDefault(l => l.Id == list1Id)
            ?? throw new KeyNotFoundException($"Список {list1Id} не найден");
        var list2 = allLists.FirstOrDefault(l => l.Id == list2Id)
            ?? throw new KeyNotFoundException($"Список {list2Id} не найден");

        var mergedItems = new List<ShoppingItem>(list1.Items);

        foreach (var item in list2.Items)
        {
            var existing = mergedItems.FirstOrDefault(i =>
                i.ProductId == item.ProductId && i.Unit == item.Unit);

            if (existing != null)
            {
                existing.Amount += item.Amount;
            }
            else
            {
                mergedItems.Add(new ShoppingItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Amount = item.Amount,
                    Unit = item.Unit,
                    IsPurchased = false,
                    SourceRecipeName = item.SourceRecipeName
                });
            }
        }

        var merged = new ShoppingList
        {
            Name = $"{list1.Name} + {list2.Name}",
            Items = mergedItems,
            CreatedDate = DateTime.UtcNow
        };

        await _storage.DeleteAsync(list1Id);
        await _storage.DeleteAsync(list2Id);
        return await _storage.CreateAsync(merged);
    }

    public async Task<ShoppingItem> TogglePurchasedAsync(Guid listId, Guid itemId)
    {
        var lists = await _storage.GetAllAsync();
        var list = lists.FirstOrDefault(l => l.Id == listId)
            ?? throw new KeyNotFoundException($"Список {listId} не найден");

        var item = list.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException($"Элемент {itemId} не найден");

        item.IsPurchased = !item.IsPurchased;
        await _storage.SaveAllAsync(lists);
        return item;
    }

    public async Task<bool> DeleteAsync(Guid id)
        => await _storage.DeleteAsync(id);
}
