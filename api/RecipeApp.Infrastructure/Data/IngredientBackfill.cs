using Microsoft.EntityFrameworkCore;
using RecipeApp.Core;
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
}
