using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

public interface ICategoryService
{
    /// <summary>Встроенные и пользовательские категории в одном списке.</summary>
    Task<List<CategoryDto>> GetAllAsync();

    Task<CategoryDto> CreateAsync(string name);

    Task<CategoryDto> RenameAsync(int categoryId, string name);

    /// <summary>Удаляет только пользовательскую и только пустую категорию.</summary>
    Task DeleteAsync(int categoryId);

    /// <summary>Существует ли категория с таким id (встроенная или пользовательская).</summary>
    Task<bool> ExistsAsync(int categoryId);
}
