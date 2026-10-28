namespace RecipeApp.Core;

/// <summary>
/// Default detection for new and backfilled ingredients: which names are pantry staples that are
/// usually already on hand, and which name/notes carry a store hint ("(winco)" → WinCo,
/// "kirkland" → Costco). Pure domain logic — no EF Core or ASP.NET dependencies.
/// </summary>
public static class IngredientDefaults
{
    /// <summary>Basic household staples matched against <see cref="Models.Ingredient.NormalizedName"/>.</summary>
    private static readonly HashSet<string> PantryStaples = new(StringComparer.OrdinalIgnoreCase)
    {
        "water", "warm water", "cold water", "boiling water", "ice water",
        "salt", "kosher salt", "sea salt",
        "black pepper", "pepper",
        "cooking spray", "vegetable oil", "canola oil",
    };

    /// <summary>The two stores the app routes to by default, in display/cycle order.</summary>
    public static readonly string[] DefaultStores = { "WinCo", "Costco" };

    public static bool IsPantryStaple(string? normalizedName) =>
        !string.IsNullOrWhiteSpace(normalizedName) && PantryStaples.Contains(normalizedName.Trim());

    /// <summary>
    /// Store hint from the ingredient name or notes: "winco" (case-insensitive, e.g.
    /// "At least 4 small (winco) Potatoes") → "WinCo"; "costco" or "kirkland" → "Costco"; otherwise null.
    /// </summary>
    public static string? DetectPreferredStore(string? name, string? notes = null)
    {
        var text = $"{name} {notes}";
        if (text.Contains("winco", StringComparison.OrdinalIgnoreCase)) return "WinCo";
        if (text.Contains("costco", StringComparison.OrdinalIgnoreCase) || text.Contains("kirkland", StringComparison.OrdinalIgnoreCase)) return "Costco";
        return null;
    }
}
