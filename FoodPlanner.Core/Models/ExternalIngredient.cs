using FoodPlanner.Core.Enums;

namespace FoodPlanner.Core.Models;

public class ExternalIngredient
{
    public string Name { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public double Amount { get; set; }
    public MeasurementUnit Unit { get; set; }
}