namespace RecipeApp.Core.Models;

/// <summary>One line on a shopping list. May link to a normalized Ingredient or be a standalone manual item.</summary>
public class ShoppingListItem
{
    public Guid Id { get; set; }

    public Guid ShoppingListId { get; set; }

    /// <summary>Link to the shared ingredient catalog, when this line came from (or matches) a recipe ingredient.</summary>
    public Guid? IngredientId { get; set; }

    /// <summary>Display name, e.g. "diced tomatoes".</summary>
    public string Name { get; set; } = "";

    /// <summary>Grocery aisle (Ingredient.Category value), used for grouping.</summary>
    public string Category { get; set; } = "";

    /// <summary>Total quantity after merging all contributing recipes, in Unit.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Canonical unit the quantity is expressed in (tsp/tbsp/cup/oz/lb/can/...).</summary>
    public string? Unit { get; set; }

    /// <summary>Merged notes: sizes, prep words, parentheticals.</summary>
    public string? Notes { get; set; }

    /// <summary>Comma-separated recipe titles contributing to this line, e.g. "Bourbon Chicken, Teriyaki Salmon".</summary>
    public string? RecipeSources { get; set; }

    public bool IsChecked { get; set; }

    /// <summary>Store to buy this line at ("WinCo", "Costco"); null when unassigned.</summary>
    public string? Store { get; set; }

    /// <summary>Inherited from the ingredient (or toggled per item): show in the "Pantry Check" section.</summary>
    public bool IsPantryStaple { get; set; }
    /// <summary>Display order within a category (0 = first).</summary>
    public int SortOrder { get; set; }

    public ShoppingList? ShoppingList { get; set; }

    /// <summary>Case-insensitive key used to merge lines across recipes.</summary>
    public string NormalizedName => Name.ToLowerInvariant();
}
