using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

public interface IProductService
{
    Task<List<Product>> GetAllAsync();
    Task<List<Product>> GetByCategoryAsync(ProductCategory category);
    Task<List<Product>> GetByStockStatusAsync(StockStatus status);
    Task<Product?> GetByIdAsync(Guid id);
    Task<Product> CreateAsync(Product product);
    Task<Product> UpdateAsync(Product product);
    Task<bool> DeleteAsync(Guid id);
    Task<List<Product>> SearchAsync(string query);
    Task RecategorizeAllAsync();
}