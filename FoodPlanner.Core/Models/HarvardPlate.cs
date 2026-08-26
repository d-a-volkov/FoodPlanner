using FoodPlanner.Core.Enums;

namespace FoodPlanner.Core.Models;

public class HarvardPlateRatio
{
    public ProductCategory Category { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public double RecommendedPercentage { get; set; }
    public double CurrentPercentage { get; set; }
    public double CurrentGrams { get; set; }
    public double RecommendedGrams { get; set; }
}

public class HarvardPlateAnalysis
{
    public List<HarvardPlateRatio> Ratios { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public List<Product> SuggestedProducts { get; set; } = [];
    public double OverallScore { get; set; }
}

public class RecipeMatch
{
    public Recipe Recipe { get; set; } = null!;
    public double AvailabilityPercentage { get; set; }
    public List<RecipeIngredient> MissingIngredients { get; set; } = [];
}
