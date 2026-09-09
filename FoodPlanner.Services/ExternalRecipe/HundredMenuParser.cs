using System.Net;
using System.Text.RegularExpressions;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;
using HtmlAgilityPack;

namespace FoodPlanner.Services.ExternalRecipe;

public class HundredMenuParser
{
    private const string BaseUrl = "https://1000.menu";

    public List<ExternalRecipeResult> ParseSearch(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<ExternalRecipeResult>();
        var cards = doc.DocumentNode.SelectNodes("//div[contains(@class,'cn-item')]");
        if (cards == null) return results;

        foreach (var card in cards)
        {
            var link = card.SelectSingleNode(".//a[contains(@href,'/cooking/')]");
            if (link == null) continue;

            var href = link.GetAttributeValue("href", string.Empty);
            var url = BuildAbsoluteUrl(href);
            if (!url.Contains("1000.menu", StringComparison.OrdinalIgnoreCase)) continue;

            var title = ReadCardTitle(card, link);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var imageUrl = ReadImageUrl(card.SelectSingleNode(".//img"));

            results.Add(new ExternalRecipeResult
            {
                Source = ExternalRecipeSource.HundredMenu,
                Url = url,
                Title = CleanText(title),
                ImageUrl = imageUrl
            });
        }

        return results;
    }

    public ExternalRecipeResult? ParseDetails(string html, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var titleNode = doc.DocumentNode.SelectSingleNode("//h1[@itemprop='name']");
        if (titleNode == null) return null;

        var result = new ExternalRecipeResult
        {
            Source = ExternalRecipeSource.HundredMenu,
            Url = url,
            Title = CleanText(titleNode.InnerText)
        };

        var descriptionNode = doc.DocumentNode
            .SelectSingleNode("//div[@itemprop='description']//span[contains(@class,'description-text')]");
        if (descriptionNode != null)
            result.Description = CleanText(descriptionNode.InnerText);

        var yieldMeta = doc.DocumentNode.SelectSingleNode("//meta[@itemprop='recipeYield']");
        if (yieldMeta != null && int.TryParse(yieldMeta.GetAttributeValue("content", string.Empty), out var servings))
            result.Servings = servings;

        var timeMeta = doc.DocumentNode.SelectSingleNode("//meta[@itemprop='totalTime']");
        result.PreparationTimeMinutes = ParseIsoDuration(timeMeta?.GetAttributeValue("content", string.Empty));

        var caloriesNode = doc.DocumentNode.SelectSingleNode("//span[@itemprop='calories']");
        if (caloriesNode != null)
        {
            var caloriesText = CleanText(caloriesNode.InnerText);
            if (!string.IsNullOrEmpty(caloriesText))
                result.Tags.Add($"≈ {caloriesText} ккал/100 г");
        }

        result.ImageUrl = ReadImageUrl(doc.DocumentNode.SelectSingleNode("//img[@itemprop='image']"));

        var ingredientNodes = doc.DocumentNode.SelectNodes(
            "//div[@id='ingredients']//div[contains(@class,'ingredient')]");
        if (ingredientNodes != null)
        {
            foreach (var node in ingredientNodes)
            {
                var ingredient = ParseIngredient(node);
                if (ingredient != null) result.Ingredients.Add(ingredient);
            }
        }

        var stepNodes = doc.DocumentNode.SelectNodes("//ol[contains(@class,'instructions')]//li");
        if (stepNodes != null)
        {
            foreach (var node in stepNodes)
            {
                var instruction = node.SelectSingleNode(".//div[contains(@class,'instruction')]");
                var text = CleanText((instruction ?? node).InnerText);
                if (!string.IsNullOrEmpty(text)) result.Steps.Add(text);
            }
        }

        return result;
    }

    private static ExternalIngredient? ParseIngredient(HtmlNode node)
    {
        var nameNode = node.SelectSingleNode(".//a[contains(@class,'name')]");
        var name = CleanText(nameNode?.InnerText ?? string.Empty);
        if (string.IsNullOrEmpty(name)) return null;

        var amountNode = node.SelectSingleNode(".//span[contains(@class,'squant')]");
        var unitNode = node.SelectSingleNode(".//select[contains(@class,'recalc_s_num')]//option[@selected]");

        var amountText = CleanText(amountNode?.InnerText ?? string.Empty);
        var unitText = CleanText(unitNode?.InnerText ?? string.Empty);

        var (amount, unit) = MeasurementUnitParser.Parse($"{amountText} {unitText}".Trim());

        var raw = $"{name} - {amountText} {unitText}".Trim(' ', '-');

        return new ExternalIngredient
        {
            Name = name,
            RawText = raw,
            Amount = amount ?? 0,
            Unit = unit ?? default
        };
    }

    private static int? ParseIsoDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var match = Regex.Match(value, @"^PT(?:(?<h>\d+)H)?(?:(?<m>\d+)M)?$");
        if (!match.Success) return null;

        var hours = match.Groups["h"].Success ? int.Parse(match.Groups["h"].Value) : 0;
        var minutes = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value) : 0;
        var total = hours * 60 + minutes;
        return total > 0 ? total : null;
    }

    private static string ReadCardTitle(HtmlNode card, HtmlNode link)
    {
        var titleNode = card.SelectSingleNode(".//div[contains(@class,'info')]//a")
            ?? card.SelectSingleNode(".//div[contains(@class,'info')]//span")
            ?? card.SelectSingleNode(".//h2")
            ?? card.SelectSingleNode(".//h3");

        var title = CleanText(titleNode?.InnerText ?? string.Empty);
        if (!string.IsNullOrEmpty(title)) return title;

        title = CleanText(link.GetAttributeValue("title", string.Empty));
        if (!string.IsNullOrEmpty(title)) return title;

        return CleanText(link.InnerText);
    }

    private static string? ReadImageUrl(HtmlNode? imageNode)
    {
        if (imageNode == null) return null;

        var src = imageNode.GetAttributeValue("data-original", null)
            ?? imageNode.GetAttributeValue("data-src", null)
            ?? imageNode.GetAttributeValue("src", null);
        if (string.IsNullOrEmpty(src)) return null;

        return src.StartsWith("//", StringComparison.Ordinal) ? "https:" + src : src;
    }

    private static string BuildAbsoluteUrl(string href)
    {
        if (string.IsNullOrEmpty(href)) return href;
        if (href.StartsWith("//", StringComparison.Ordinal)) return "https:" + href;
        if (href.StartsWith("/", StringComparison.Ordinal)) return BaseUrl + href;
        return href;
    }

    private static string CleanText(string? text)
        => Regex.Replace(WebUtility.HtmlDecode(text ?? string.Empty), @"\s+", " ").Trim(' ', '-');
}