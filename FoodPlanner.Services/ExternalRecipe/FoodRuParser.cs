using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;
using HtmlAgilityPack;

namespace FoodPlanner.Services.ExternalRecipe;

public class FoodRuParser
{
    public List<ExternalRecipeResult> ParseSearch(string html)
    {
        var results = new List<ExternalRecipeResult>();
        if (TryGetNextData(html, out var nextData))
        {
            try
            {
                using var doc = JsonDocument.Parse(nextData);
                var collection = FindElement(doc.RootElement, IsMaterialsCollection);
                if (collection != null &&
                    collection.Value.TryGetProperty("materials", out var materials) &&
                    materials.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in materials.EnumerateArray())
                    {
                        var recipe = ParseSearchItem(item);
                        if (recipe != null) results.Add(recipe);
                    }
                }
            }
            catch (JsonException)
            {
                return results;
            }
        }

        if (results.Count == 0)
            results.AddRange(ParseSearchFromHtmlCards(html));

        return results;
    }

    public ExternalRecipeResult? ParseDetails(string html, string url)
    {
        if (!TryGetNextData(html, out var nextData)) return null;

        try
        {
            using var doc = JsonDocument.Parse(nextData);
            var recipeElement = FindElement(doc.RootElement, e =>
                e.ValueKind == JsonValueKind.Object &&
                (e.TryGetProperty("url_part", out _) || e.TryGetProperty("main_title", out _) || e.TryGetProperty("id", out _)) &&
                e.TryGetProperty("ingredients", out var ingredients) &&
                ingredients.ValueKind == JsonValueKind.Array);

            if (recipeElement == null) return null;
            var el = recipeElement.Value;

            var result = new ExternalRecipeResult
            {
                Source = ExternalRecipeSource.FoodRu,
                Url = url,
                Title = GetString(el, "main_title") ?? GetString(el, "title") ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(result.Title)) return null;

            result.Description = GetString(el, "description") ?? ExtractSlateDescription(el);

            var coverPath = GetString(el, "cover_path");
            if (!string.IsNullOrWhiteSpace(coverPath))
                result.ImageUrl = BuildImageUrl(coverPath);

            var totalTime = GetInt32(el, "total_cooking_time")
                ?? GetInt32(el, "active_cooking_time");
            result.PreparationTimeMinutes = totalTime;

            result.Servings = GetInt32(el, "servings") ?? GetInt32(el, "portions") ?? 0;

            if (el.TryGetProperty("ingredients", out var ingredientArray) &&
                ingredientArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ingredientArray.EnumerateArray())
                    ParseIngredient(item, result);
            }

            result.Steps.AddRange(ParseSteps(el));

            if (el.TryGetProperty("breadcrumbs", out var breadcrumbs) &&
                breadcrumbs.ValueKind == JsonValueKind.Array)
            {
                foreach (var crumb in breadcrumbs.EnumerateArray())
                {
                    var crumbTitle = GetString(crumb, "title");
                    if (!string.IsNullOrWhiteSpace(crumbTitle)) result.Tags.Add(crumbTitle);
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ExternalRecipeResult? ParseSearchItem(JsonElement item)
    {
        var urlPart = GetString(item, "url_part");
        var id = GetLong(item, "id");
        if (string.IsNullOrWhiteSpace(urlPart) && id == null) return null;

        var url = id != null
            ? $"https://food.ru/recipes/{id}-{urlPart}"
            : $"https://food.ru/recipes/{urlPart}";

        var title = GetString(item, "main_title") ?? GetString(item, "title") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title)) return null;

        var result = new ExternalRecipeResult
        {
            Source = ExternalRecipeSource.FoodRu,
            Url = url,
            Title = title,
            PreparationTimeMinutes = GetInt32(item, "total_cooking_time") ?? GetInt32(item, "active_cooking_time")
        };

        var coverPath = GetString(item, "cover_path");
        if (!string.IsNullOrWhiteSpace(coverPath))
            result.ImageUrl = BuildImageUrl(coverPath);

        result.Description = ExtractSlateDescription(item);

        if (item.TryGetProperty("ingredients", out var ingredients) &&
            ingredients.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ingredients.EnumerateArray())
            {
                if (ing.ValueKind == JsonValueKind.String)
                {
                    var name = ing.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        result.Ingredients.Add(new ExternalIngredient { Name = name, RawText = name });
                }
            }
        }

        if (item.TryGetProperty("breadcrumbs", out var breadcrumbs) &&
            breadcrumbs.ValueKind == JsonValueKind.Array)
        {
            foreach (var crumb in breadcrumbs.EnumerateArray())
            {
                var crumbTitle = GetString(crumb, "title");
                if (!string.IsNullOrWhiteSpace(crumbTitle)) result.Tags.Add(crumbTitle);
            }
        }

        return result;
    }

    private static void ParseIngredient(JsonElement item, ExternalRecipeResult result)
    {
        string? name = null;
        double? amount = null;
        string? unit = null;

        if (item.ValueKind == JsonValueKind.String)
        {
            name = item.GetString();
        }
        else if (item.ValueKind == JsonValueKind.Object)
        {
            name = GetString(item, "title")
                ?? GetString(item, "name")
                ?? GetString(item, "product_name")
                ?? GetString(item, "main_title");
            amount = GetDouble(item, "count") ?? GetDouble(item, "amount");
            unit = GetString(item, "unit")
                ?? GetString(item, "unit_title")
                ?? GetString(item, "unit_type_title")
                ?? GetString(item, "measure");
        }

        if (string.IsNullOrWhiteSpace(name)) return;

        var raw = unit == null
            ? name
            : $"{amount:0.##} {unit} {name}".Trim();

        var parsed = MeasurementUnitParser.Parse($"{amount:0.##} {unit}".Trim());

        result.Ingredients.Add(new ExternalIngredient
        {
            Name = name,
            RawText = raw,
            Amount = parsed.Amount ?? 0,
            Unit = parsed.Unit ?? default
        });
    }

    private static List<string> ParseSteps(JsonElement recipeElement)
    {
        var steps = new List<string>();

        if (recipeElement.TryGetProperty("cooking_steps", out var cookingSteps) &&
            cookingSteps.ValueKind == JsonValueKind.Array)
        {
            CollectStepTexts(cookingSteps, steps);
        }

        if (steps.Count == 0 && recipeElement.TryGetProperty("steps", out var stepsArray) &&
            stepsArray.ValueKind == JsonValueKind.Array)
        {
            CollectStepTexts(stepsArray, steps);
        }

        if (steps.Count == 0 && recipeElement.TryGetProperty("instructions", out var instructions) &&
            instructions.ValueKind == JsonValueKind.Array)
        {
            CollectStepTexts(instructions, steps);
        }

        return steps;
    }

    private static void CollectStepTexts(JsonElement array, List<string> steps)
    {
        foreach (var item in array.EnumerateArray())
        {
            string? text = null;
            if (item.ValueKind == JsonValueKind.String)
            {
                text = item.GetString();
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                text = GetString(item, "instruction")
                    ?? GetString(item, "text")
                    ?? GetString(item, "text_raw")
                    ?? GetString(item, "title");
            }

            if (!string.IsNullOrWhiteSpace(text))
                steps.Add(CleanText(text));
        }
    }

    private static string? ExtractSlateDescription(JsonElement element)
    {
        if (!element.TryGetProperty("subtitle", out var subtitle) ||
            subtitle.ValueKind != JsonValueKind.Object ||
            !subtitle.TryGetProperty("children", out var children) ||
            children.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var node in children.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object ||
                !node.TryGetProperty("children", out var blocks) ||
                blocks.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var block in blocks.EnumerateArray())
            {
                var content = GetString(block, "content");
                var text = ExtractSlateSegment(content);
                if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
            }
        }

        var joined = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? ExtractSlateSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment)) return null;

        var match = Regex.Match(segment, @"content=(.*?);\s*(?:bold|color|size|align|version|type|href|prefer)=");
        if (match.Success) return CleanText(match.Groups[1].Value);

        var index = segment.IndexOf("content=", StringComparison.Ordinal);
        if (index < 0) return null;

        var rest = segment[(index + 8)..];
        var end = rest.IndexOf('}');
        if (end > 0) rest = rest[..end];
        return CleanText(rest);
    }

    private static string BuildImageUrl(string coverPath)
    {
        var s3Uri = $"s3://media/{coverPath}";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(s3Uri))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"https://cdn.food.ru/unsigned/fit/320/240/ce/0/{base64}.webp";
    }

    private static bool TryGetNextData(string html, out string json)
    {
        json = string.Empty;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scriptNode = doc.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");
        if (scriptNode == null) return false;

        json = scriptNode.InnerText;
        return !string.IsNullOrWhiteSpace(json);
    }

    private static IEnumerable<ExternalRecipeResult> ParseSearchFromHtmlCards(string html)
    {
        var results = new List<ExternalRecipeResult>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = doc.DocumentNode.SelectNodes("//a[contains(@href,'/recipes/')]");
        if (links == null) return results;

        var seen = new HashSet<string>();
        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrEmpty(href) || !href.StartsWith("/recipes/", StringComparison.Ordinal)) continue;
            if (!seen.Add(href)) continue;

            var title = CleanText(link.SelectSingleNode(".//h3")?.InnerText ?? link.InnerText);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var imageUrl = link.SelectSingleNode(".//img") is { } img
                ? img.GetAttributeValue("src", null)
                : null;

            results.Add(new ExternalRecipeResult
            {
                Source = ExternalRecipeSource.FoodRu,
                Url = "https://food.ru" + href,
                Title = title,
                ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl
            });
        }

        return results;
    }

    private static JsonElement? FindElement(JsonElement element, Func<JsonElement, bool> predicate)
    {
        if (predicate(element)) return element;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var match = FindElement(property.Value, predicate);
                if (match != null) return match;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var match = FindElement(item, predicate);
                if (match != null) return match;
            }
        }

        return null;
    }

    private static bool IsMaterialsCollection(JsonElement element)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty("materials", out var materials) &&
           materials.ValueKind == JsonValueKind.Array;

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(property, out var value)) return null;

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in value.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var text = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(text) && prop.Name is "text" or "content" or "title")
                        return text;
                }
            }
        }

        return null;
    }

    private static long? GetLong(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static int? GetInt32(JsonElement element, string property)
        => GetLong(element, property) is { } value ? (int)value : null;

    private static double? GetDouble(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString()?.Replace(',', '.'), out var parsed))
            return parsed;
        return null;
    }

    private static string CleanText(string? text)
        => Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
}