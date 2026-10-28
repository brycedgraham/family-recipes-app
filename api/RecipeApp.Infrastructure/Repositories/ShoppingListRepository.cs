using Microsoft.EntityFrameworkCore;
using RecipeApp.Core;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Infrastructure.Data;

namespace RecipeApp.Infrastructure.Repositories;

public class ShoppingListRepository : IShoppingListRepository
{
    private readonly RecipeDbContext _context;

    public ShoppingListRepository(RecipeDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ShoppingList>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ShoppingLists
            .Include(l => l.Items)
            .AsNoTracking()
            .OrderByDescending(l => l.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ShoppingList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<ShoppingList?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default)
    {
        return await _context.ShoppingLists
            .Include(l => l.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ShareToken == shareToken, cancellationToken);
    }

    public async Task<ShoppingList> AddAsync(ShoppingList list)
    {
        var now = DateTime.UtcNow;
        list.CreatedAt = now;
        list.UpdatedAt = now;
        await _context.ShoppingLists.AddAsync(list);
        await _context.SaveChangesAsync();
        return list;
    }

    public async Task<ShoppingList> UpdateAsync(ShoppingList list)
    {
        list.UpdatedAt = DateTime.UtcNow;
        _context.ShoppingLists.Update(list);
        await _context.SaveChangesAsync();
        return list;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var list = await _context.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (list is null) return false;

        _context.ShoppingLists.Remove(list);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<string>> GetKnownStoresAsync(CancellationToken cancellationToken = default)
    {
        // Registered stores first (canonical order), then any ad-hoc names still on items or ingredients that are not registered.
        var result = await _context.Stores
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

        var adHoc = await _context.ShoppingListItems.Where(i => i.Store != null).Select(i => i.Store!)
            .Concat(_context.Ingredients.Where(i => i.PreferredStore != null).Select(i => i.PreferredStore!))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var name in adHoc.Where(s => !result.Contains(s, StringComparer.OrdinalIgnoreCase))
                     .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(name);
        }
        return result;
    }

    public async Task<IReadOnlyList<string>> CreateStoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        var existing = await _context.Stores.ToListAsync(cancellationToken);
        // Case-insensitive uniqueness; the NOCASE unique index is the backstop.
        if (!existing.Any(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            var nextOrder = existing.Count > 0 ? existing.Max(s => s.SortOrder) + 1 : 0;
            _context.Stores.Add(new Store { Name = trimmed, SortOrder = nextOrder, CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync(cancellationToken);
        }
        return await GetKnownStoresAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> DeleteStoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();

        var store = (await _context.Stores.ToListAsync(cancellationToken))
            .FirstOrDefault(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (store is not null) _context.Stores.Remove(store);

        // Unassign anything pointing at this store (case-insensitive) so no row references a deleted store.
        // Matched in memory: the data set is small and EF 10 has no translatable case-insensitive equality.
        foreach (var item in await _context.ShoppingListItems.Where(i => i.Store != null).ToListAsync(cancellationToken))
        {
            if (string.Equals(item.Store, trimmed, StringComparison.OrdinalIgnoreCase)) item.Store = null;
        }

        foreach (var ingredient in await _context.Ingredients.Where(i => i.PreferredStore != null).ToListAsync(cancellationToken))
        {
            if (string.Equals(ingredient.PreferredStore, trimmed, StringComparison.OrdinalIgnoreCase)) ingredient.PreferredStore = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetKnownStoresAsync(cancellationToken);
    }
}
