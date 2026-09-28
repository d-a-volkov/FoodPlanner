using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Exceptions;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Services.Services;

/// <summary>
/// Встроенные категории заданы enum ProductCategory и удалить их нельзя:
/// на них завязаны детектор, фильтры и списки покупок. Пользовательские
/// категории хранятся в categories.json и получают id начиная с 20.
/// </summary>
public class CategoryService : ICategoryService
{
    public const int BuiltInCount = 20;
    public const int MaxNameLength = 60;

    private readonly IJsonStorageService<CustomCategory> _storage;
    private readonly IJsonStorageService<Product> _products;

    public CategoryService(
        IJsonStorageService<CustomCategory> storage,
        IJsonStorageService<Product> products)
    {
        _storage = storage;
        _products = products;
    }

    private static bool IsBuiltIn(int id) => id >= 0 && id < BuiltInCount;

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        var custom = await _storage.GetAllAsync();
        var products = await _products.GetAllAsync();

        var result = Enum.GetValues<ProductCategory>()
            .Select(c => new CategoryDto
            {
                Id = (int)c,
                Name = c.GetDisplayName(),
                IsCustom = false,
                ProductCount = products.Count(p => (int)p.Category == (int)c)
            })
            .ToList();

        foreach (var c in custom.OrderBy(c => c.CategoryId))
        {
            result.Add(new CategoryDto
            {
                Id = c.CategoryId,
                Name = c.Name,
                IsCustom = true,
                ProductCount = products.Count(p => (int)p.Category == c.CategoryId)
            });
        }

        return result;
    }

    public async Task<CategoryDto> CreateAsync(string name)
    {
        var cleanName = ValidateName(name);
        var custom = await _storage.GetAllAsync();

        EnsureUniqueName(cleanName, custom, null);
        EnsureNotShadowsBuiltIn(cleanName);

        // id не переиспользуем, чтобы старые ссылки не «оживали» после удаления
        var all = Enum.GetValues<ProductCategory>().Select(c => (int)c).Concat(custom.Select(c => c.CategoryId));
        var nextId = all.DefaultIfEmpty(BuiltInCount - 1).Max() + 1;

        var created = new CustomCategory { CategoryId = nextId, Name = cleanName };
        await _storage.CreateAsync(created);

        return new CategoryDto { Id = nextId, Name = cleanName, IsCustom = true, ProductCount = 0 };
    }

    public async Task<CategoryDto> RenameAsync(int categoryId, string name)
    {
        if (IsBuiltIn(categoryId))
            throw new CategoryException("Встроенную категорию переименовать нельзя.");

        var cleanName = ValidateName(name);
        var custom = await _storage.GetAllAsync();
        var target = custom.FirstOrDefault(c => c.CategoryId == categoryId)
            ?? throw new CategoryException("Категория не найдена.", 404);

        EnsureUniqueName(cleanName, custom, categoryId);
        EnsureNotShadowsBuiltIn(cleanName);

        target.Name = cleanName;
        await _storage.SaveAllAsync(custom);

        var products = await _products.GetAllAsync();
        return new CategoryDto
        {
            Id = categoryId,
            Name = cleanName,
            IsCustom = true,
            ProductCount = products.Count(p => (int)p.Category == categoryId)
        };
    }

    public async Task DeleteAsync(int categoryId)
    {
        if (IsBuiltIn(categoryId))
            throw new CategoryException("Встроенную категорию удалить нельзя.");

        var custom = await _storage.GetAllAsync();
        var target = custom.FirstOrDefault(c => c.CategoryId == categoryId)
            ?? throw new CategoryException("Категория не найдена.", 404);

        var used = (await _products.GetAllAsync()).Count(p => (int)p.Category == categoryId);
        if (used > 0)
            throw new CategoryException(
                $"В категории «{target.Name}» ещё {used} шт. Сначала перенесите их в другую категорию.", 409);

        custom.Remove(target);
        await _storage.SaveAllAsync(custom);
    }

    public async Task<bool> ExistsAsync(int categoryId)
    {
        if (IsBuiltIn(categoryId)) return true;
        return (await _storage.GetAllAsync()).Any(c => c.CategoryId == categoryId);
    }

    private static string ValidateName(string? name)
    {
        var clean = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(clean))
            throw new CategoryException("Название категории не может быть пустым.");
        if (clean.Length > MaxNameLength)
            throw new CategoryException($"Название длиннее {MaxNameLength} символов.");
        return clean;
    }

    private static void EnsureUniqueName(string name, List<CustomCategory> custom, int? exceptId)
    {
        var clash = custom.Any(c => c.CategoryId != exceptId
            && string.Equals(c.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (clash)
            throw new CategoryException($"Категория «{name}» уже существует.", 409);
    }

    // пользовательская категория с тем же названием, что у встроенной, запутала бы фильтры
    private static void EnsureNotShadowsBuiltIn(string name)
    {
        var clash = Enum.GetValues<ProductCategory>().Any(c =>
            string.Equals(c.GetDisplayName().Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (clash)
            throw new CategoryException($"Категория «{name}» уже есть среди встроенных.", 409);
    }
}
