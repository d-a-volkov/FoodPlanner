using FoodPlanner.Core.Enums;

namespace FoodPlanner.Core.Models;

public class ShoppingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Amount { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Grams;
    public bool IsPurchased { get; set; }
    public string? SourceRecipeName { get; set; }
}
