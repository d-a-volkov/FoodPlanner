namespace FoodPlanner.Core.Models;

/// <summary>
/// Категория, созданная пользователем. Встроенные категории живут в enum
/// ProductCategory (0..19) и сюда не попадают, поэтому id пользовательских
/// категорий начинается с 20 и не конфликтует с enum.
/// </summary>
public class CustomCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}
