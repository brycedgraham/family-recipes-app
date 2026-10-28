namespace RecipeApp.Core.Models;

public class Ingredient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Category { get; set; } // "produce", "dairy", "pantry", etc.

    /// <summary>Store this ingredient is usually bought at ("WinCo", "Costco"); null when unassigned.</summary>
    public string? PreferredStore { get; set; }

    /// <summary>Basic household staple that's usually already on hand (water, salt, pepper, oils…).</summary>
    public bool IsPantryStaple { get; set; }
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
