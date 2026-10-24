using RecipeApp.Core.Models;

namespace RecipeApp.Core;

/// <summary>
/// Pure domain logic that merges recipe ingredients and manual lines into a <see cref="ShoppingList"/>.
/// Lines merge by (normalized name, unit group): US volume units (tsp/tbsp/cup) unify to the largest
/// sensible container (cups once the total reaches 0.25 cup), weight units (oz/lb) unify to lb at 16 oz,
/// and all other units stay distinct per exact unit. No EF Core or ASP.NET dependencies.
/// </summary>
public static class ShoppingListAggregator
{
    private const decimal TspPerTbsp = 3m;
    private const decimal TspPerCup = 48m;
    private const decimal OzPerLb = 16m;
    private const decimal MinCupTotalTsp = 12m; // 0.25 cup

    /// <summary>
    /// Merges a recipe's ingredients into the list, scaling each quantity by <paramref name="multiplier"/>.
    /// </summary>
    public static void AddRecipe(ShoppingList list, string recipeTitle, IEnumerable<RecipeIngredient> ingredients, decimal multiplier = 1m)
    {
        foreach (var ri in ingredients)
        {
            decimal? qty = ri.Quantity.HasValue ? ri.Quantity.Value * multiplier : null;
            MergeLine(list, ri.Ingredient.Name, ri.Ingredient.Category, qty, ri.Unit, ri.Notes, recipeTitle);
        }
    }

    /// <summary>Merges a single manual line (already parsed via IngredientText + classifier) into the list.</summary>
    public static void AddManualItem(ShoppingList list, string name, string category, decimal? quantity, string? unit, string? notes)
    {
        MergeLine(list, name, category, quantity, unit, notes, recipeTitle: null);
    }

    /// <summary>
    /// Renumbers SortOrder so items display grouped by category (uncategorized last), unchecked before checked, then by name.
    /// </summary>
    public static void Resort(ShoppingList list)
    {
        var ordered = list.Items
            .OrderBy(i => string.IsNullOrEmpty(i.Category))
            .ThenBy(i => i.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.IsChecked)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var idx = 0; idx < ordered.Count; idx++)
            ordered[idx].SortOrder = idx;
    }

    private static void MergeLine(ShoppingList list, string name, string category, decimal? quantity, string? unit, string? notes, string? recipeTitle)
    {
        var key = (name.ToLowerInvariant(), UnitGroup(unit));
        var existing = list.Items.FirstOrDefault(i => i.NormalizedName == key.Item1 && UnitGroup(i.Unit) == key.Item2);

        if (existing is null)
        {
            list.Items.Add(new ShoppingListItem
            {
                Name = name,
                Category = category ?? "",
                Quantity = quantity,
                Unit = unit,
                Notes = notes,
                RecipeSources = recipeTitle,
            });
            return;
        }

        var (q, u) = CombineQuantities(existing, quantity, unit);
        existing.Quantity = q;
        existing.Unit = u;
        existing.Notes = MergeNotes(existing.Notes, notes);
        if (recipeTitle is not null) existing.RecipeSources = AddSource(existing.RecipeSources, recipeTitle);
    }

    /// <summary>Unit group for merging: volume and weight each form one group; everything else groups by exact unit.</summary>
    private static string? UnitGroup(string? unit)
    {
        var u = unit?.Trim().ToLowerInvariant();
        if (u is "tsp" or "tbsp" or "cup") return "volume";
        if (u is "oz" or "lb") return "weight";
        return u; // null stays null: unquantified lines merge together, each other unit stands alone
    }

    private static (decimal? Quantity, string? Unit) CombineQuantities(ShoppingListItem existing, decimal? quantity, string? incomingUnit)
    {
        var group = UnitGroup(existing.Unit);
        if (group is not ("volume" or "weight"))
            return ((existing.Quantity.HasValue || quantity.HasValue ? (existing.Quantity ?? 0m) + (quantity ?? 0m) : null), existing.Unit);

        if (!existing.Quantity.HasValue && !quantity.HasValue) return (null, existing.Unit);

        // Volume: accumulate in tsp.
        if (group == "volume")
        {
            var totalTsp = ToTsp(existing.Quantity, existing.Unit) + ToTsp(quantity, incomingUnit);
            if (totalTsp >= MinCupTotalTsp) return (Round(totalTsp / TspPerCup), "cup"); // express in cups
            if (totalTsp >= TspPerTbsp) return (Round(totalTsp / TspPerTbsp), "tbsp");   // express in tbsp
            return (Round(totalTsp), "tsp");                                             // stay in tsp
        }

        // Weight: accumulate in oz.
        var totalOz = ToOz(existing.Quantity, existing.Unit) + ToOz(quantity, incomingUnit);
        if (totalOz >= OzPerLb) return (Round(totalOz / OzPerLb), "lb"); // express in lb
        return (Round(totalOz), "oz");                                   // stay in oz
    }

    private static decimal ToTsp(decimal? qty, string? unit) => (qty ?? 0m) * unit?.Trim().ToLowerInvariant() switch
    {
        "cup" => TspPerCup,
        "tbsp" => TspPerTbsp,
        _ => 1m, // tsp or null
    };
    private static decimal ToOz(decimal? qty, string? unit) => (qty ?? 0m) * (unit?.Trim().ToLowerInvariant() == "lb" ? OzPerLb : 1m);

    private static decimal Round(decimal v) => Math.Round(v, 4);

    /// <summary>Joins notes with "; ", ignoring duplicates (case-insensitive).</summary>
    internal static string? MergeNotes(string? existing, string? incoming)
    {
        var parts = new List<string>();
        foreach (var chunk in new[] { existing, incoming })
        {
            if (string.IsNullOrWhiteSpace(chunk)) continue;
            foreach (var piece in chunk.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                if (!parts.Contains(piece, StringComparer.OrdinalIgnoreCase)) parts.Add(piece);
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>Appends a recipe title to the comma-separated source list (case-insensitive dedup).</summary>
    internal static string? AddSource(string? sources, string title)
    {
        var list = sources is null
            ? new List<string>()
            : sources.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

        if (!list.Contains(title, StringComparer.OrdinalIgnoreCase)) list.Add(title);
        return string.Join(", ", list);
    }
}
