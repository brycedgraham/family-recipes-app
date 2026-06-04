namespace RecipeApp.Core.Models;

public class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; } // "cups", "tbsp", "g", "pieces", etc.
    public string? Notes { get; set; } // "finely chopped", "room temperature"

    // Navigation
    public Recipe Recipe { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
