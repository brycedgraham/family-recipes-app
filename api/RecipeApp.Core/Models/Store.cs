namespace RecipeApp.Core.Models;

/// <summary>A store shopping lines can be routed to ("WinCo", "Costco", …). Managed through
/// /api/stores; the default stores are ensured in this table at startup.</summary>
public class Store
{
    public Guid Id { get; set; }

    /// <summary>Display name. Unique, case-insensitive (NOCASE unique index — see AddStoresTable migration).</summary>
    public string Name { get; set; } = null!;

    /// <summary>Order in the store bar: defaults first in canonical order, then creation order.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}
