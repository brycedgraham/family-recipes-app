namespace RecipeApp.Core.Models;

/// <summary>A named, shareable grocery list aggregated from one or more recipes plus manual items.</summary>
public class ShoppingList
{
    public Guid Id { get; set; }

    /// <summary>Display name, e.g. "Saturday dinner".</summary>
    public string Name { get; set; } = "";

    /// <summary>URL-safe token for the share link (/shopping-lists/shared/{token}). Unique.</summary>
    public string ShareToken { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<ShoppingListItem> Items { get; set; } = new();
}
