using System.Text.RegularExpressions;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Services.ExternalRecipe;

public static class IngredientNameMatcher
{
    private const double AcceptThreshold = 0.9;

    public static Product? Match(string ingredientName, IEnumerable<Product> products)
    {
        var normalized = Normalize(ingredientName);
        if (normalized.Length < 2) return null;

        Product? best = null;
        var bestScore = 0.0;

        foreach (var product in products)
        {
            var score = Score(normalized, Normalize(product.Name));
            if (score > bestScore)
            {
                bestScore = score;
                best = product;
            }
        }

        return bestScore >= AcceptThreshold ? best : null;
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value.ToLowerInvariant(), @"\s+", " ").Trim();
    }

    private static double Score(string ingredient, string product)
    {
        if (ingredient == product) return 1.0;

        if (ingredient.Length >= 3 && product.Length >= 3 &&
            (ingredient.Contains(product) || product.Contains(ingredient)))
        {
            return 0.95;
        }

        var ingredientTokens = ingredient.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var productTokens = product.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (productTokens.Length >= 1 && productTokens.All(ingredientTokens.Contains)) return 0.9;
        if (ingredientTokens.Length >= 1 && ingredientTokens.All(productTokens.Contains)) return 0.9;

        var prefixLength = 0;
        var maxLength = Math.Min(ingredient.Length, product.Length);
        while (prefixLength < maxLength && ingredient[prefixLength] == product[prefixLength])
            prefixLength++;

        if (prefixLength >= 4)
            return 0.5 + 0.4 * prefixLength / Math.Max(ingredient.Length, product.Length);

        return 0;
    }
}