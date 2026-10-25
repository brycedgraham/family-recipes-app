using Microsoft.EntityFrameworkCore;
using RecipeApp.Core;
using RecipeApp.Core.Models;
namespace RecipeApp.Infrastructure.Data;

/// <summary>
/// Idempotent backfill of store / pantry-staple defaults for ingredient rows created before that
/// support existed (the live data/recipes.db). Runs at startup and after seeding — it only writes
/// when a row is actually missing a default, so steady-state boots are a no-op save.
/// Store hints come from the ingredient name (e.g. "At least 4 small (winco) Potatoes"); note-based
/// hints are applied on the seed / quick-add paths where the line's notes are available.
/// </summary>
public static class IngredientBackfill
{
    public static async Task<int> ApplyDefaultsAsync(RecipeDbContext db, CancellationToken cancellationToken = default)
    {
        var rows = await db.Ingredients.ToListAsync(cancellationToken);
        var changed = 0;

        foreach (var row in rows)
        {
            if (row.PreferredStore is null)
            {
                var hint = IngredientDefaults.DetectPreferredStore(row.Name);
                if (hint is not null)
                {
                    row.PreferredStore = hint;
                    changed++;
                }
            }

            if (!row.IsPantryStaple && IngredientDefaults.IsPantryStaple(row.NormalizedName))
            {
                row.IsPantryStaple = true;
                changed++;
            }
        }

        if (changed > 0) await db.SaveChangesAsync(cancellationToken);
        return changed;
    }

    /// <summary>
    /// Ensures the canonical default stores ("WinCo", "Costco") plus every store name already assigned to an
    /// ingredient (PreferredStore) or shopping-list item (Store) exists in the Stores table. Idempotent and
    /// case-insensitive; defaults keep their canonical order, ad-hoc names are appended in creation order.
    /// </summary>
    public static async Task<int> EnsureStoresAsync(RecipeDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Stores.ToListAsync(cancellationToken);
        var known = new HashSet<string>(existing.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

        var candidates = IngredientDefaults.DefaultStores
            .Concat(await db.Ingredients.Where(i => i.PreferredStore != null)
                .Select(i => i.PreferredStore!).Distinct().ToListAsync(cancellationToken))
            .Concat(await db.ShoppingListItems.Where(l => l.Store != null)
                .Select(l => l.Store!).Distinct().ToListAsync(cancellationToken));

        var nextOrder = existing.Count > 0 ? existing.Max(s => s.SortOrder) + 1 : 0;
        var added = 0;
        foreach (var raw in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var name = raw.Trim();
            if (known.Contains(name)) continue;
            db.Stores.Add(new Store { Name = name, SortOrder = nextOrder++, CreatedAt = DateTime.UtcNow });
            known.Add(name);
            added++;
        }

        if (added > 0) await db.SaveChangesAsync(cancellationToken);
        return added;
    }
}
