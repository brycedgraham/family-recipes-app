using System.Globalization;
using System.Text.RegularExpressions;

namespace RecipeApp.Core;

/// <summary>
/// Splits raw ingredient text into structured parts.
/// "1/2 package (8 ounces) bow tie pasta" -> (0.5, "package", "(8 ounces) bow tie pasta").
/// Lines without a leading number have no quantity or unit.
/// </summary>
public static partial class IngredientText
{
    // Leading number: mixed fraction ("1 1/2"), simple fraction ("3/4"), decimal ("0.5"), or integer ("2").
    [GeneratedRegex(@"^(?:(\d+)\s+)?(\d+)/(\d+)|^(\d+(?:\.\d+)?)")]
    private static partial Regex QuantityRegex();

    // First alphabetic word after the number: "cup", "tbsp", "package".
    [GeneratedRegex(@"^[A-Za-z][A-Za-z/-]*")]
    private static partial Regex UnitRegex();

    // Unicode fraction characters found in the seed data ("½ cup Butter").
    private static readonly Dictionary<char, decimal> UnicodeFractions = new()
    {
        ['½'] = 0.5m, ['¼'] = 0.25m, ['¾'] = 0.75m,
        ['⅓'] = 1m / 3m, ['⅔'] = 2m / 3m,
        ['⅛'] = 0.125m, ['⅜'] = 0.375m,
        ['⅕'] = 0.2m, ['⅖'] = 0.4m, ['⅗'] = 0.6m, ['⅘'] = 0.8m,
        ['⅙'] = 1m / 6m, ['⅚'] = 5m / 6m,
    };

    /// <summary>
    /// Rewrites a leading unicode fraction (optionally after a whole number, "1½") as a decimal ("1.5").
    /// </summary>
    private static string NormalizeLeadingFraction(string text)
    {
        var i = 0;
        while (i < text.Length && char.IsDigit(text[i])) i++;
        if (i < text.Length && UnicodeFractions.TryGetValue(text[i], out var fraction))
        {
            var whole = i > 0 ? text[..i] : string.Empty;
            return whole + fraction.ToString("0.####", CultureInfo.InvariantCulture) + text[(i + 1)..];
        }
        return text;
    }

    /// <summary>
    /// Splits a raw ingredient line into quantity, unit, and remaining name text.
    /// </summary>
    public static (decimal? Quantity, string? Unit, string Name) SplitLine(string? raw)
    {
        var text = NormalizeLeadingFraction((raw ?? string.Empty).Trim());
        if (text.Length == 0) return (null, null, string.Empty);

        var match = QuantityRegex().Match(text);
        decimal? quantity;
        if (match.Groups[2].Success) // fraction form
        {
            var denominator = int.Parse(match.Groups[3].Value);
            if (denominator == 0) return (null, null, text);
            var whole = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            quantity = whole + (decimal)int.Parse(match.Groups[2].Value) / denominator;
        }
        else if (match.Groups[4].Success) // plain number
        {
            quantity = decimal.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            return (null, null, text);
        }

        var rest = text[match.Length..].TrimStart();
        string? unit = null;
        var unitMatch = UnitRegex().Match(rest);
        if (unitMatch.Success)
        {
            unit = unitMatch.Value;
            rest = rest[unitMatch.Length..].TrimStart();
        }

        return (Math.Round(quantity.Value, 4), unit, rest);
    }

    /// <summary>
    /// Parses a quantity string ("1/2 cup") into a number and unit word.
    /// </summary>
    public static (decimal? Quantity, string? Unit) ParseQuantity(string? text)
    {
        var (quantity, unit, _) = SplitLine(text);
        return (quantity, unit);
    }

    /// <summary>
    /// Formats quantity + unit back into a display string ("0.5 cup"); null when neither is present.
    /// </summary>
    public static string? FormatQuantity(decimal? quantity, string? unit)
    {
        if (quantity == null && string.IsNullOrEmpty(unit)) return null;
        var number = quantity?.ToString("0.####", CultureInfo.InvariantCulture);
        var suffix = unit is null ? string.Empty : " " + unit;
        return (number + suffix).Trim();
    }
}
