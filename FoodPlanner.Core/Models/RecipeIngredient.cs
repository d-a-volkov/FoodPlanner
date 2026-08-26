using FoodPlanner.Core.Enums;

namespace FoodPlanner.Core.Models;

public class RecipeIngredient
{
    public Guid ProductId { get; set; }
    public double Amount { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Grams;
}
