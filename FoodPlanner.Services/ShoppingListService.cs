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
            .Where(p => p.StockStatus != StockStatus.NotUsed &&
                        (p.StockStatus == StockStatus.InStock || p.HasReserve))
            .Select(p => p.Id)
            .ToHashSet();

        var items = new List<ShoppingItem>();

        foreach (var ingredient in recipe.Ingredients)
        {
            if (!ingredient.ProductId.HasValue) continue;

            var product = await _productService.GetByIdAsync(ingredient.ProductId.Value);
            if (product == null) continue;
            if (product.StockStatus == StockStatus.NotUsed) continue;

            if (!availableProductIds.Contains(ingredient.ProductId.Value))
            {
                items.Add(new ShoppingItem
                {
                    ProductId = ingredient.ProductId.Value,
                    ProductName = product.Name,
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

    public async Task<ShoppingList> CreateFromOutOfStockAsync(string? name = null, bool includeLowStock = false, List<int>? categories = null)
    {
        var products = await _productService.GetAllAsync();
        var categoryFilter = categories is { Count: > 0 };

        var notable = products
            .Where(p => (p.StockStatus == StockStatus.OutOfStock ||
                        (includeLowStock && p.StockStatus == StockStatus.LowStock)) &&
                        (!categoryFilter || categories!.Contains((int)p.Category)))
            .OrderBy(p => p.Name)
            .ToList();

        var categorySuffix = categoryFilter ? " в выбранных категориях" : "";
        if (notable.Count == 0)
            throw new InvalidOperationException(
                includeLowStock
                    ? $"Отсутствующих и «мало» продуктов{categorySuffix} нет — список создавать не нужно."
                    : $"Отсутствующих продуктов{categorySuffix} нет — список создавать не нужно.");

        var items = notable.Select(product => new ShoppingItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Amount = product.StockStatus == StockStatus.LowStock ? 0 : 1,
            Unit = product.DefaultUnit,
            IsPurchased = false,
            SourceRecipeName = null
        }).ToList();

        var shoppingList = new ShoppingList
        {
            Name = name ?? $"Список покупок {(includeLowStock ? "(отсутствующие и мало)" : "(отсутствующие)")} {DateTime.Today:dd.MM.yyyy}",
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

    public async Task<ShoppingList> RenameAsync(Guid listId, string name)
    {
        var nameValue = name?.Trim();
        if (string.IsNullOrWhiteSpace(nameValue))
            throw new ArgumentException("Название не может быть пустым");

        var lists = await _storage.GetAllAsync();
        var list = lists.FirstOrDefault(l => l.Id == listId)
            ?? throw new KeyNotFoundException($"Список {listId} не найден");

        list.Name = nameValue;
        await _storage.SaveAllAsync(lists);
        return list;
    }

    public async Task<ShoppingListSyncResult> SyncStockAsync(Guid listId, bool includeLowStock)
    {
        var lists = await _storage.GetAllAsync();
        var list = lists.FirstOrDefault(l => l.Id == listId)
            ?? throw new KeyNotFoundException($"Список {listId} не найден");

        var products = await _productService.GetAllAsync();
        var byId = products.ToDictionary(p => p.Id);
        var inStockIds = products
            .Where(p => p.StockStatus == StockStatus.InStock || p.HasReserve)
            .Select(p => p.Id)
            .ToHashSet();
        var endedIds = products
            .Where(p => p.StockStatus == StockStatus.OutOfStock ||
                        (includeLowStock && p.StockStatus == StockStatus.LowStock))
            .Select(p => p.Id)
            .ToHashSet();

        var result = new ShoppingListSyncResult();

        foreach (var item in list.Items.Where(i => !i.IsPurchased && inStockIds.Contains(i.ProductId)))
        {
            item.IsPurchased = true;
            result.MarkedPurchased++;
        }

        var fixedProducts = new List<Product>();
        foreach (var item in list.Items.Where(i => i.IsPurchased))
        {
            if (byId.TryGetValue(item.ProductId, out var product) &&
                (product.StockStatus == StockStatus.OutOfStock ||
                 (includeLowStock && product.StockStatus == StockStatus.LowStock)))
            {
                product.StockStatus = StockStatus.InStock;
                fixedProducts.Add(product);
                result.StatusFixed++;
            }
        }

        foreach (var productId in endedIds)
        {
            if (list.Items.Any(i => i.ProductId == productId)) continue;
            if (!byId.TryGetValue(productId, out var product)) continue;

            list.Items.Add(new ShoppingItem
            {
                ProductId = productId,
                ProductName = product.Name,
                Amount = product.StockStatus == StockStatus.LowStock ? 0 : 1,
                Unit = product.DefaultUnit,
                IsPurchased = false,
                SourceRecipeName = null
            });
            result.Added++;
        }

        foreach (var product in fixedProducts)
            await _productService.UpdateAsync(product);
        await _storage.SaveAllAsync(lists);

        result.List = list;
        return result;
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

        if (item.IsPurchased)
        {
            var product = await _productService.GetByIdAsync(item.ProductId);
            if (product != null &&
                (product.StockStatus == StockStatus.OutOfStock || product.StockStatus == StockStatus.LowStock))
            {
                product.StockStatus = StockStatus.InStock;
                await _productService.UpdateAsync(product);
            }
        }

        return item;
    }

    public async Task<bool> DeleteItemAsync(Guid listId, Guid itemId)
    {
        var lists = await _storage.GetAllAsync();
        var list = lists.FirstOrDefault(l => l.Id == listId);
        if (list == null) return false;

        var item = list.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return false;

        list.Items.Remove(item);
        await _storage.SaveAllAsync(lists);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
        => await _storage.DeleteAsync(id);
}
