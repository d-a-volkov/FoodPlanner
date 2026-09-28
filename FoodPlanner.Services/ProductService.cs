using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using FoodPlanner.Core.Services;

namespace FoodPlanner.Services.Services;

public class ProductService : IProductService
{
    private readonly IJsonStorageService<Product> _storage;
    private readonly ICategoryService _categories;

    public ProductService(IJsonStorageService<Product> storage, ICategoryService categories)
    {
        _storage = storage;
        _categories = categories;
    }

    public async Task<List<Product>> GetAllAsync()
        => await _storage.GetAllAsync();

    public async Task<List<Product>> GetByCategoryAsync(ProductCategory category)
    {
        var products = await _storage.GetAllAsync();
        return products.Where(p => p.Category == category).ToList();
    }

    public async Task<List<Product>> GetByStockStatusAsync(StockStatus status)
    {
        var products = await _storage.GetAllAsync();
        return products.Where(p => p.StockStatus == status).ToList();
    }

    public async Task<Product?> GetByIdAsync(Guid id)
        => await _storage.GetByIdAsync(id);

    public async Task<Product> CreateAsync(Product product)
    {
        product.Id = Guid.NewGuid();

        // Категорию из формы нельзя затирать: пользователь подтверждает
        // предложенную детектором и имеет право её исправить.
        // Детектор - только запасной вариант, если категория не передана
        // или ссылается на несуществующую (в т.ч. удалённую пользовательскую).
        if (!await _categories.ExistsAsync((int)product.Category))
            product.Category = ProductCategoryDetector.Detect(product.Name);

        return await _storage.CreateAsync(product);
    }

    public async Task<Product> UpdateAsync(Product product)
        => await _storage.UpdateAsync(product);

    public async Task<bool> DeleteAsync(Guid id)
        => await _storage.DeleteAsync(id);

    public async Task<List<Product>> SearchAsync(string query)
    {
        var products = await _storage.GetAllAsync();
        return products
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task RecategorizeAllAsync()
    {
        var products = await _storage.GetAllAsync();
        if (products.Count == 0) return;

        // Только встроенные: детектор ничего не знает про пользовательские
        // категории и сбросил бы их в "Прочее".
        foreach (var product in products)
            if (Enum.IsDefined(product.Category))
                product.Category = ProductCategoryDetector.Detect(product.Name);

        await _storage.SaveAllAsync(products);
    }
}