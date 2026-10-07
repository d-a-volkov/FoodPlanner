namespace FoodPlanner.Core.Models;

/// <summary>Запрос переименования списка покупок.</summary>
public class ShoppingListRenameRequest
{
    public string Name { get; set; } = string.Empty;
}