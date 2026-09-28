namespace FoodPlanner.Core.Models;

/// <summary>
/// Категория в том виде, в котором её видит клиент: встроенные (из enum)
/// и пользовательские в одном списке с общей нумерацией.
/// </summary>
public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public int ProductCount { get; set; }
}
