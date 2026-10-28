using System.Globalization;
using System.Text.RegularExpressions;

namespace RecipeApp.Core;

/// <summary>
/// Splits raw ingredient text into structured parts and normalizes names and units.
/// "1/2 pkg (8 ounces) bow tie pasta" -> (0.5, "pkg", "bow tie pasta", "(8 ounces)").
/// "2 pounds boneless chicken breasts; cut into bite-size pieces" -> (2, "lb", "boneless chicken breasts", "cut into bite-size pieces").
/// Only words in the known unit whitelist become units; anything else stays in the name ("3 eggs" -> (3, null, "eggs", null)).
/// Lines without a leading number have no quantity or unit.
/// </summary>
public static partial class IngredientText
{
    // Leading number: mixed fraction ("1 1/2"), simple fraction ("3/4"), decimal ("0.5"), or integer ("2").
    [GeneratedRegex(@"^(?:(\d+)\s+)?(\d+)\s*/\s*(\d+)|^(\d+(?:\.\d+)?)")]
    private static partial Regex QuantityRegex();

    // First alphabetic token after the number: "cup", "tbsp.", "package".
    [GeneratedRegex(@"^[A-Za-z][A-Za-z/-]*")]
    private static partial Regex UnitWordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    // Unicode fraction characters and the fraction slash ("½", "1⁄2").
    [GeneratedRegex(@"½|¼|¾|⅓|⅔|⅛|⅜|⅕|⅖|⅗|⅘|⅙|⅚|⁄")]
    private static partial Regex UnicodeFractionRegex();

    // Quantity range tails after a leading number: "-6", "- 1 1/2", "-2/3", "to 8".
    [GeneratedRegex(@"^-\s*\d+(?:\.\d+)?(?:\s+\d+/\d+)?(?:/\d+)?|^\s*to\s+\d+(?:\.\d+)?(?:\s+\d+/\d+)?|^\s*or\s+\d+(?:\.\d+)?(?:\s+\d+/\d+)?")]
    private static partial Regex RangeTailRegex();

    // Leading list bullets from markdown source ("- 1 cup water", "+ 2 tbsp ...").
    [GeneratedRegex(@"^[+\-•]+")]
    private static partial Regex BulletRegex();

    // Whitespace before a comma or period ("potatoes , peeled").
    [GeneratedRegex(@"\s+([,.])")]
    private static partial Regex SpaceBeforePunctRegex();

    // Compound quantity after a unit: "1 cup + 2 tbsp X" / "1 lb - 2 oz X".
    [GeneratedRegex(@"^[+-]\s*(\d+(?:\.\d+)?(?:\s*/\s*\d+)?)\s*([A-Za-z][A-Za-z/-]*)")]
    private static partial Regex ExtraQtyRegex();

    // Leading label prefix on a line without a quantity: "optional: fresh lemon zest ...".
    [GeneratedRegex(@"^(?:optional|note|notes|tip|tips)\s*:\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LabelPrefixRegex();

    // Package size qualifier inside the name: "1/2 9 oz. bag fresh spinach" -> note "(9 oz.)".
    [GeneratedRegex(@"^\d+(?:\.\d+)?\s*(?:oz|ounce|lb|pound|g|gram)s?\.?\s+(?:bag|box|can|cans|pkg|package)\b")]
    private static partial Regex SizeQualifierRegex();

    // Canonical culinary units. Keys are case-insensitive aliases; values are the canonical form stored in the DB.
    private static readonly Dictionary<string, string> UnitCanonicalization = new(StringComparer.OrdinalIgnoreCase)
    {
        // Volume
        ["t"] = "tsp", ["tsp"] = "tsp", ["teaspoon"] = "tsp", ["teaspoons"] = "tsp",
        ["tbsp"] = "tbsp", ["tb"] = "tbsp", ["tbs"] = "tbsp", ["tablespoon"] = "tbsp", ["tablespoons"] = "tbsp",
        ["c"] = "cup", ["cup"] = "cup", ["cups"] = "cup",
        ["pt"] = "pt", ["pint"] = "pt", ["pints"] = "pt",
        ["qt"] = "qt", ["quart"] = "qt", ["quarts"] = "qt",
        ["gal"] = "gal", ["gallon"] = "gal", ["gallons"] = "gal",
        ["ml"] = "ml", ["milliliter"] = "ml", ["milliliters"] = "ml",
        ["l"] = "l", ["liter"] = "l", ["liters"] = "l",
        // Weight
        ["oz"] = "oz", ["ounce"] = "oz", ["ounces"] = "oz",
        ["lb"] = "lb", ["lbs"] = "lb", ["pound"] = "lb", ["pounds"] = "lb",
        ["g"] = "g", ["gram"] = "g", ["grams"] = "g",
        ["kg"] = "kg", ["kilogram"] = "kg", ["kilograms"] = "kg",
        // Count / container
        ["clove"] = "clove", ["cloves"] = "clove",
        ["can"] = "can", ["cans"] = "can",
        ["pkg"] = "pkg", ["package"] = "pkg", ["packages"] = "pkg", ["packet"] = "pkg", ["packets"] = "pkg",
        ["stick"] = "stick", ["sticks"] = "stick",
        ["slice"] = "slice", ["slices"] = "slice",
        ["pinch"] = "pinch", ["pinches"] = "pinch",
        ["dash"] = "dash", ["dashes"] = "dash",
        ["bunch"] = "bunch", ["bunches"] = "bunch",
        ["head"] = "head", ["heads"] = "head",
        ["stalk"] = "stalk", ["stalks"] = "stalk",
        ["sprig"] = "sprig", ["sprigs"] = "sprig",
        ["jar"] = "jar", ["jars"] = "jar",
        ["bottle"] = "bottle", ["bottles"] = "bottle",
        ["box"] = "box", ["boxes"] = "box",
        ["bag"] = "bag", ["bags"] = "bag",
    };

    // Container words dropped from the name when they lead it ("15 oz can diced tomatoes" -> "diced tomatoes").
    private static readonly HashSet<string> ContainerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "can", "cans", "jar", "jars", "bottle", "bottles", "box", "boxes", "bag", "bags", "pkg", "package", "packages",
    };

    // Leading prep participles moved from the name into notes ("melted butter" -> "butter", note "melted").
    private static readonly string[] LeadingPrepWords =
    {
        "melted", "softened", "chopped", "diced", "minced", "sliced", "grated", "shredded",
    };

    // Culinary terms of art where the prep word is part of the product name; never split.
    private static readonly HashSet<string> LeadingPrepExceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "diced tomatoes", "crushed tomatoes", "stewed tomatoes", "crushed red pepper", "shredded wheat",
        "powdered sugar", "evaporated milk", "condensed milk", "sliced almonds",
    };

    // Multi-word units, checked before the single-word match so "fl oz" is not rejected on "fl".
    private static readonly (string Alias, string Canonical)[] MultiWordUnits =
    {
        ("fl oz", "fl oz"),
    };

    // Trailing prep phrases after a comma that move into Notes.
    private static readonly HashSet<string> PrepPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "diced", "chopped", "minced", "softened", "melted", "divided", "to taste", "optional",
        "grated", "shredded", "sliced", "crushed", "drained",
        "roughly chopped", "bite size pieces", "cut to bite size pieces", "casings removed", "grated and squeezed dry",
    };

    // Unicode fraction characters found in the seed data ("½ cup Butter").
    private static readonly Dictionary<char, decimal> UnicodeFractions = new()
    {
        ['½'] = 0.5m, ['¼'] = 0.25m, ['¾'] = 0.75m,
        ['⅓'] = 1m / 3m, ['⅔'] = 2m / 3m,
        ['⅛'] = 0.125m, ['⅜'] = 0.375m,
        ['⅕'] = 0.2m, ['⅖'] = 0.4m, ['⅗'] = 0.6m, ['⅘'] = 0.8m,
        ['⅙'] = 1m / 6m, ['⅚'] = 5m / 6m,
    };

    // Standard culinary fractions for display rounding; a decimal within 0.01 of one formats as that fraction.
    private static readonly (decimal Value, string Display)[] CulinaryFractions =
    {
        (1m / 8m, "1/8"), (1m / 4m, "1/4"), (1m / 3m, "1/3"), (3m / 8m, "3/8"),
        (1m / 2m, "1/2"), (5m / 8m, "5/8"), (2m / 3m, "2/3"), (3m / 4m, "3/4"), (7m / 8m, "7/8"),
    };

    // ASCII equivalents for CleanLine ("½" -> "1/2", "⁄" -> "/").
    private static readonly Dictionary<string, string> FractionAscii = new()
    {
        ["½"] = "1/2", ["¼"] = "1/4", ["¾"] = "3/4",
        ["⅓"] = "1/3", ["⅔"] = "2/3", ["⅛"] = "1/8", ["⅜"] = "3/8",
        ["⅕"] = "1/5", ["⅖"] = "2/5", ["⅗"] = "3/5", ["⅘"] = "4/5",
        ["⅙"] = "1/6", ["⅚"] = "5/6", ["⁄"] = "/",
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
    /// Cleans a raw ingredient line before parsing: converts unicode fractions to ASCII,
    /// strips markdown emphasis and leading list bullets. Returns null for blank lines and
    /// section headings ("Glaze Topping:"), which are not ingredients.
    /// </summary>
    public static string? CleanLine(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0) return null;

        // "1½" -> "1 1/2", "3 1⁄2" -> "3 1/2".
        text = UnicodeFractionRegex().Replace(text, m =>
            m.Index > 0 && char.IsDigit(text[m.Index - 1]) && FractionAscii[m.Value] != "/"
                ? " " + FractionAscii[m.Value]
                : FractionAscii[m.Value]);

        text = BulletRegex().Replace(text.Replace("*", string.Empty), string.Empty).TrimStart();
        if (text.Length > 0 && !char.IsDigit(text[0]))
        {
            var label = LabelPrefixRegex().Match(text);
            if (label.Success) text = text[label.Length..].TrimStart();
        }
        return text.Length == 0 || text.EndsWith(':') ? null : text;
    }

    /// <summary>
    /// Splits a raw ingredient line into quantity, canonical unit, clean name, and notes.
    /// </summary>
    public static (decimal? Quantity, string? Unit, string Name, string? Notes) SplitLine(string? raw)
    {
        var text = NormalizeLeadingFraction((raw ?? string.Empty).Trim());
        if (text.Length == 0) return (null, null, string.Empty, null);

        var match = QuantityRegex().Match(text);
        decimal? quantity;
        if (match.Groups[2].Success) // fraction form
        {
            var denominator = int.Parse(match.Groups[3].Value);
            if (denominator == 0) return (null, null, text, null);
            var whole = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            quantity = whole + (decimal)int.Parse(match.Groups[2].Value) / denominator;
        }
        else if (match.Groups[4].Success) // plain number
        {
            quantity = decimal.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            var (noQtyName, noQtyNotes) = SplitNotes(text);
            noQtyName = StripLeadingPrep(noQtyName, ref noQtyNotes);
            return (null, null, noQtyName, noQtyNotes);
        }

        var rest = text[match.Length..].TrimStart();
        // Consume quantity ranges ("5-6 eggs", "1/2-1 tsp", "1 1/2 - 2 cups", "6 to 8 ounces"): keep the first value.
        rest = RangeTailRegex().Replace(rest, string.Empty).TrimStart();

        // Package size qualifier inside the name: "1/2 9 oz. bag fresh spinach" -> note "(9 oz.)".
        var sizeNote = string.Empty;
        var size = SizeQualifierRegex().Match(rest);
        if (size.Success)
        {
            sizeNote = $"({size.Groups[0].Value})";
            rest = rest[size.Length..].TrimStart();
        }

        var unit = MatchUnit(rest, out rest);

        // Compound quantity: "1 cup + 2 tbsp X" -> keep the first quantity, note the addition.
        var extraNote = string.Empty;
        var extra = ExtraQtyRegex().Match(rest);
        if (extra.Success)
        {
            extraNote = (extra.Groups[0].Value[0] == '+' ? "plus " : "minus ") + extra.Groups[1].Value;
            if (extra.Groups[2].Success && UnitCanonicalization.TryGetValue(extra.Groups[2].Value, out var canonical))
                extraNote += " " + canonical;
            rest = rest[extra.Length..];
        }

        var (name, notes) = SplitNotes(rest.TrimStart());

        // Drop a leading container word from the name ("15 oz can diced tomatoes" -> "diced tomatoes").
        // When no other unit was parsed, promote the container to its canonical unit instead of losing it.
        var word = UnitWordRegex().Match(name);
        if (word.Success && ContainerWords.Contains(word.Value))
        {
            name = name[word.Length..].TrimStart();
            if (unit is null && UnitCanonicalization.TryGetValue(word.Value, out var containerUnit))
                unit = containerUnit;
        }

        name = StripLeadingPrep(name, ref notes);

        if (extraNote.Length > 0 || sizeNote.Length > 0)
        {
            var prefix = string.IsNullOrEmpty(extraNote) ? sizeNote : string.IsNullOrEmpty(sizeNote) ? extraNote : extraNote + "; " + sizeNote;
            notes = string.IsNullOrEmpty(notes) ? prefix : prefix + "; " + notes;
        }
        return (Math.Round(quantity.Value, 4), unit, name, notes);
    }

    /// <summary>
    /// Moves a leading prep participle into notes ("melted butter" -> "butter", note "melted"),
    /// unless the full phrase is a culinary term of art ("diced tomatoes").
    /// </summary>
    private static string StripLeadingPrep(string name, ref string? notes)
    {
        foreach (var prep in LeadingPrepWords)
        {
            if (!name.StartsWith(prep + ' ', StringComparison.OrdinalIgnoreCase)) continue;
            if (LeadingPrepExceptions.Contains(name, StringComparer.OrdinalIgnoreCase)) break;
            var note = name[..prep.Length];
            name = name[(prep.Length + 1)..].TrimStart();
            notes = string.IsNullOrEmpty(notes) ? note : notes + "; " + note;
            break;
        }
        return name;
    }

    /// <summary>
    /// Matches a known culinary unit at the start of <paramref name="rest"/> and canonicalizes it.
    /// Unknown words (e.g. "eggs") are NOT units: Unit stays null and the word remains in the remainder.
    /// </summary>
    private static string? MatchUnit(string rest, out string remainder)
    {
        foreach (var (alias, canonical) in MultiWordUnits)
        {
            if (rest.StartsWith(alias, StringComparison.OrdinalIgnoreCase) && IsTokenBoundary(rest, alias.Length))
            {
                remainder = SkipTrailingDot(rest[alias.Length..]);
                return canonical;
            }
        }

        var match = UnitWordRegex().Match(rest);
        if (match.Success && UnitCanonicalization.TryGetValue(match.Value, out var single))
        {
            remainder = SkipTrailingDot(rest[match.Length..]);
            return single;
        }

        remainder = rest;
        return null;
    }

    private static bool IsTokenBoundary(string text, int index) =>
        index >= text.Length || !char.IsLetterOrDigit(text[index]);

    private static string SkipTrailingDot(string rest)
    {
        rest = rest.TrimStart();
        return rest.StartsWith('.') ? rest[1..].TrimStart() : rest;
    }

    /// <summary>
    /// Splits remainder text into a clean name and optional notes: semicolon clauses, a leading
    /// parenthetical size qualifier right after the unit, trailing parentheticals, and trailing
    /// comma prep phrases all move into Notes.
    /// </summary>
    private static (string Name, string? Notes) SplitNotes(string rest)
    {
        var notes = new List<string>();
        var name = rest.Trim();

        // Leading "of" left behind when the container word was consumed as a unit ("1 packet of biscuits").
        if (name.StartsWith("of ", StringComparison.OrdinalIgnoreCase))
            name = name[3..].TrimStart();

        // 1) Semicolon: everything after the first ';' is a note.
        var semi = name.IndexOf(';');
        if (semi >= 0)
        {
            AddNote(notes, name[(semi + 1)..]);
            name = name[..semi].Trim();
        }

        // 2) Leading parenthetical right after the unit: "(8 ounces) bow tie pasta".
        if (name.StartsWith('('))
        {
            var close = FindClosingParen(name, 0);
            if (close > 0 && close + 1 < name.Length)
            {
                var noteText = name[..(close + 1)];
                var after = name[(close + 1)..];
                // "(70)g" — gram marker attached to the parenthetical: fold into the note.
                if (after.StartsWith("g ", StringComparison.Ordinal) || after == "g")
                {
                    noteText += " g";
                    after = after[1..];
                }
                AddNote(notes, noteText);
                name = after.Trim();
            }
        }

        // 3) Trailing parentheticals (possibly stacked): "diced tomatoes (undrained) (Blender these)".
        while (name.EndsWith(')'))
        {
            var open = FindOpeningParen(name);
            if (open <= 0 || !IsBalanced(name[open..])) break;
            AddNote(notes, name[open..]);
            name = name[..open].Trim();
        }

        // 4) Trailing prep phrases after a comma: ", diced", ", to taste".
        while (true)
        {
            var comma = name.LastIndexOf(',');
            if (comma < 0) break;
            var tail = name[(comma + 1)..].Trim();
            if (!PrepPhrases.Contains(tail)) break;
            AddNote(notes, tail);
            name = name[..comma].Trim();
        }

        return (name, notes.Count > 0 ? string.Join("; ", notes) : null);
    }

    private static void AddNote(List<string> notes, string note)
    {
        var trimmed = note.Trim().Trim('.').Trim();
        if (trimmed.Length > 0) notes.Add(trimmed);
    }

    /// <summary>Index of the ')' matching the '(' at <paramref name="openIndex"/>, or -1.</summary>
    private static int FindClosingParen(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    /// <summary>Rightmost '(' whose parenthesized span is balanced and ends at the final character.</summary>
    private static int FindOpeningParen(string text)
    {
        for (var i = text.Length - 2; i >= 0; i--)
        {
            if (text[i] == '(' && IsBalanced(text[i..])) return i;
        }
        return -1;
    }

    private static bool IsBalanced(string parenText)
    {
        if (parenText.Length < 2 || parenText[0] != '(' || parenText[^1] != ')') return false;
        var depth = 0;
        foreach (var ch in parenText)
        {
            if (ch == '(') depth++;
            else if (ch == ')')
            {
                depth--;
                if (depth < 0) return false;
            }
        }
        return depth == 0;
    }

    /// <summary>
    /// Normalizes an ingredient name for dedup: lowercase, trimmed punctuation, collapsed whitespace.
    /// </summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var collapsed = SpaceBeforePunctRegex().Replace(WhitespaceRegex().Replace(name.Trim(), " ").ToLowerInvariant(), "$1");
        var start = 0;
        while (start < collapsed.Length && char.IsPunctuation(collapsed[start])) start++;
        var end = collapsed.Length;
        while (end > start && char.IsPunctuation(collapsed[end - 1])) end--;
        return collapsed[start..end];
    }

    /// <summary>
    /// Parses a quantity string ("1/2 cup") into a number and canonical unit word.
    /// </summary>
    public static (decimal? Quantity, string? Unit) ParseQuantity(string? text)
    {
        var (quantity, unit, _, _) = SplitLine(text);
        return (quantity, unit);
    }

    /// <summary>
    /// Formats a decimal for display. Values within 0.01 of a standard culinary fraction render as an
    /// ASCII fraction ("0.6666" -> "2/3", "1.3333" -> "1 1/3", "0.5" -> "1/2"); everything else keeps up
    /// to four decimals with trailing zeros trimmed ("2.5", "4"). Near-integers snap to the integer.
    /// </summary>
    public static string FormatNumber(decimal value)
    {
        var sign = value < 0 ? "-" : string.Empty;
        var abs = Math.Abs(value);
        var whole = Math.Floor(abs);
        var fraction = abs - whole;

        // Near-integer values snap to the integer ("0.99" -> "1", "2.004" -> "2").
        if (fraction >= 0.99m) return sign + (whole + 1).ToString("0", CultureInfo.InvariantCulture);
        if (fraction <= 0.01m) return sign + whole.ToString("0", CultureInfo.InvariantCulture);

        foreach (var (target, display) in CulinaryFractions)
        {
            if (Math.Abs(fraction - target) <= 0.01m)
                return sign + (whole > 0 ? $"{whole} {display}" : display);
        }

        return sign + abs.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats quantity + unit back into a display string ("0.6667 cup" -> "2/3 cup"); null when neither is present.
    /// </summary>
    public static string? FormatQuantity(decimal? quantity, string? unit)
    {
        if (quantity == null && string.IsNullOrEmpty(unit)) return null;
        var number = quantity.HasValue ? FormatNumber(quantity.Value) : string.Empty;
        var suffix = unit is null ? string.Empty : " " + unit;
        return (number + suffix).Trim();
    }
}
