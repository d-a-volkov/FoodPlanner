namespace FoodPlanner.Core.Models;

public class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RecipeIngredient> Ingredients { get; set; } = [];
    public List<string> Steps { get; set; } = [];
    public string MealType { get; set; } = string.Empty;
    public int Servings { get; set; } = 1;
    public int PreparationTimeMinutes { get; set; }
    public List<string> Tags { get; set; } = [];
}
