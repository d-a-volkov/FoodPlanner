namespace FoodPlanner.Core.Models;

/// <summary>
/// Результат синхронизации списка покупок по наличию продуктов.
/// </summary>
public class ShoppingListSyncResult
{
    public ShoppingList List { get; set; } = new();

    /// <summary>Пункты списка, отмеченные как купленные (продукт в наличии).</summary>
    public int MarkedPurchased { get; set; }

    /// <summary>Продуктам выставлен статус «В наличии» по отметкам «куплено».</summary>
    public int StatusFixed { get; set; }

    /// <summary>Закончившихся продуктов добавлено в список.</summary>
    public int Added { get; set; }
}