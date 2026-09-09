using System.Globalization;
using System.Text.RegularExpressions;
using FoodPlanner.Core.Enums;

namespace FoodPlanner.Services.ExternalRecipe;

public static class MeasurementUnitParser
{
    public static MeasurementUnit? FromRussian(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var value = text.Trim().TrimEnd('.').ToLowerInvariant();
        switch (value)
        {
            case "г":
            case "гр":
            case "грамм":
            case "грамма":
            case "граммы":
            case "граммов":
                return MeasurementUnit.Grams;
            case "мл":
            case "миллилитр":
            case "миллилитра":
            case "миллилитров":
                return MeasurementUnit.Milliliters;
            case "шт":
            case "шт.":
            case "штука":
            case "штуки":
            case "штук":
            case "штучку":
                return MeasurementUnit.Pieces;
            case "ст":
            case "ст.":
            case "ст.л":
            case "ст.л.":
            case "столов":
            case "ст.ложка":
            case "ст.ложки":
            case "столовая":
            case "столовая ложка":
            case "столовые ложки":
            case "ложка":
            case "ложки":
                return MeasurementUnit.Tablespoons;
            case "ч":
            case "ч.":
            case "ч.л":
            case "ч.л.":
            case "чайная":
            case "чайная ложка":
            case "чайные ложки":
            case "чайных":
                return MeasurementUnit.Teaspoons;
            case "стакан":
            case "стакана":
            case "стаканов":
                return MeasurementUnit.Cups;
            case "кг":
            case "килограмм":
            case "килограмма":
            case "килограммы":
            case "килограммов":
            case "л":
            case "литр":
            case "литра":
            case "литры":
            case "литров":
                return null;
            default:
                return null;
        }
    }

    public static (double? Amount, MeasurementUnit? Unit) Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null);

        var text = raw.Trim()
            .Replace("½", "0.5")
            .Replace("⅓", "0.33")
            .Replace("⅔", "0.66")
            .Replace("¼", "0.25")
            .Replace("¾", "0.75")
            .Replace("⅛", "0.125");

        var match = Regex.Match(text, @"^\s*(\d+\s*/\s*\d+|\d+(?:[.,]\d+)?)");
        double? amount = null;

        if (match.Success)
        {
            var numberText = match.Value.Trim();
            if (numberText.Contains('/'))
            {
                var parts = numberText.Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
                    double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var den) &&
                    den != 0)
                    amount = num / den;
            }
            else if (double.TryParse(numberText.Replace(',', '.'), NumberStyles.Any,
                         CultureInfo.InvariantCulture, out var parsed))
            {
                amount = parsed;
            }

            text = text[match.Length..].Trim();
        }
        else if (IsWithoutMeasure(text))
        {
            return (null, null);
        }

        var unit = FromRussian(text);
        if (amount != null)
        {
            var low = text.ToLowerInvariant().TrimEnd('.');
            if (unit == null)
            {
                if (low is "кг" or "килограмм" or "килограмма" or "килограммы" or "килограммов")
                {
                    amount *= 1000;
                    unit = MeasurementUnit.Grams;
                }
                else if (low is "л" or "литр" or "литра" or "литры" or "литров")
                {
                    amount *= 1000;
                    unit = MeasurementUnit.Milliliters;
                }
            }
        }

        return (amount, unit);
    }

    private static bool IsWithoutMeasure(string text)
    {
        var low = text.ToLowerInvariant();
        return low is "по вкусу" or "для украшения" or "по желанию" or "щепотка" or "щепотки" or "щепотку";
    }
}