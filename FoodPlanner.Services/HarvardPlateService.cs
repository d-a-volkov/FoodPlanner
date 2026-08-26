using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Services.Services;

public class HarvardPlateService : IHarvardPlateService
{
    private readonly IProductService _productService;
    private readonly IRecipeService _recipeService;

    private static readonly List<HarvardPlateRatio> RecommendedRatios =
    [
        new() { Category = ProductCategory.Vegetables, DisplayName = "Овощи", RecommendedPercentage = 35 },
        new() { Category = ProductCategory.Fruits, DisplayName = "Фрукты", RecommendedPercentage = 15 },
        new() { Category = ProductCategory.WholeGrains, DisplayName = "Цельнозерновые", RecommendedPercentage = 25 },
        new() { Category = ProductCategory.Proteins, DisplayName = "Белок", RecommendedPercentage = 25 },
    ];

    private static readonly List<string> RecommendedProducts = new()
    {
        "Овощи: брокколи, шпинат, помидоры, огурцы, морковь, перец, капуста",
        "Фрукты: ягоды, яблоки, бананы, цитрусовые",
        "Цельнозерновые: гречка, овсянка, бурый рис, цельнозерновой хлеб",
        "Белок: рыба, птица, бобовые, яйца, орехи",
        "Молочные: нежирный йогурт, творог",
        "Здоровые жиры: оливковое масло, авокадо, орехи"
    };

    public HarvardPlateService(IProductService productService, IRecipeService recipeService)
    {
        _productService = productService;
        _recipeService = recipeService;
    }

    public async Task<HarvardPlateAnalysis> AnalyzeCurrentStockAsync()
    {
        var products = await _productService.GetAllAsync();
        var inStockProducts = products
            .Where(p => p.StockStatus == StockStatus.InStock || p.HasReserve)
            .ToList();

        var categoryGroups = inStockProducts
            .GroupBy(p => p.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        int totalCount = inStockProducts.Count;
        if (totalCount == 0) totalCount = 1;

        var ratios = RecommendedRatios.Select(r =>
        {
            var currentCount = categoryGroups.GetValueOrDefault(r.Category, 0);
            var currentPct = (double)currentCount / totalCount * 100;

            return new HarvardPlateRatio
            {
                Category = r.Category,
                DisplayName = r.DisplayName,
                RecommendedPercentage = r.RecommendedPercentage,
                CurrentPercentage = Math.Round(currentPct, 1),
                CurrentGrams = currentCount,
                RecommendedGrams = Math.Round(r.RecommendedPercentage / 100 * totalCount, 0)
            };
        }).ToList();

        var recommendations = GenerateRecommendations(ratios, inStockProducts);
        double score = CalculateScore(ratios);

        return new HarvardPlateAnalysis
        {
            Ratios = ratios,
            Recommendations = recommendations,
            SuggestedProducts = GetMissingProducts(ratios, products),
            OverallScore = score
        };
    }

    public async Task<List<RecipeMatch>> FindRecipesByAvailableProductsAsync()
    {
        var products = await _productService.GetAllAsync();
        return await FindRecipesByAvailableProductsAsync(products);
    }

    public async Task<List<RecipeMatch>> FindRecipesByAvailableProductsAsync(List<Product> products)
    {
        var availableProducts = products
            .Where(p => p.StockStatus == StockStatus.InStock || p.HasReserve)
            .ToList();

        return await _recipeService.GetRecipesByAvailableProductsAsync(availableProducts);
    }

    private List<string> GenerateRecommendations(
        List<HarvardPlateRatio> ratios,
        List<Product> products)
    {
        var recommendations = new List<string>();

        foreach (var ratio in ratios)
        {
            var diff = ratio.RecommendedPercentage - ratio.CurrentPercentage;
            if (diff > 5)
            {
                recommendations.Add(
                    $"Не хватает продуктов категории \"{ratio.DisplayName}\". " +
                    $"Рекомендуется {ratio.RecommendedPercentage}%, сейчас {ratio.CurrentPercentage}%.");
            }
            else if (diff < -10)
            {
                recommendations.Add(
                    $"Категория \"{ratio.DisplayName}\" представлена в избытке ({ratio.CurrentPercentage}%). " +
                    $"Рекомендуется разнообразить рацион другими продуктами.");
            }
        }

        var proteinProducts = products.Where(p => p.Category == ProductCategory.Proteins).ToList();
        bool hasFish = proteinProducts.Any(p => p.Name.Contains("рыб", StringComparison.OrdinalIgnoreCase));
        bool hasPoultry = proteinProducts.Any(p =>
            p.Name.Contains("куриц", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("индейк", StringComparison.OrdinalIgnoreCase));

        if (!hasFish)
            recommendations.Add("Рекомендуется добавить рыбу в рацион (2-3 раза в неделю).");
        if (!hasPoultry)
            recommendations.Add("Рекомендуется добавить птицу (курицу/индейку) как источник белка.");

        bool hasHealthyFats = products.Any(p => p.Category == ProductCategory.HealthyFats);
        if (!hasHealthyFats)
            recommendations.Add("Добавьте полезные жиры: оливковое масло, орехи, авокадо.");

        return recommendations;
    }

    private double CalculateScore(List<HarvardPlateRatio> ratios)
    {
        double totalDeviation = ratios.Sum(r => Math.Abs(r.CurrentPercentage - r.RecommendedPercentage));
        double maxDeviation = 200;
        return Math.Round(Math.Max(0, 100 - totalDeviation / maxDeviation * 100), 1);
    }

    private List<Product> GetMissingProducts(List<HarvardPlateRatio> ratios, List<Product> allProducts)
    {
        var missing = new List<Product>();
        var inStock = allProducts
            .Where(p => p.StockStatus == StockStatus.InStock || p.HasReserve)
            .ToList();

        foreach (var ratio in ratios)
        {
            if (ratio.CurrentPercentage < ratio.RecommendedPercentage - 5)
            {
                var candidates = allProducts
                    .Where(p => p.Category == ratio.Category &&
                                !inStock.Any(ip => ip.Id == p.Id))
                    .Take(3);
                missing.AddRange(candidates);
            }
        }

        return missing;
    }
}
