using FoodPlanner.Core.Enums;

namespace FoodPlanner.Core.Models;

public class ExternalRecipeResult
{
    public ExternalRecipeSource Source { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string? MealType { get; set; }
    public int Servings { get; set; }
    public int? PreparationTimeMinutes { get; set; }
    public List<ExternalIngredient> Ingredients { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public double? AvailabilityPercentage { get; set; }
    public List<ExternalIngredient> MissingIngredients { get; set; } = new();
    public int InitialOrder { get; set; }
}