using Microsoft.EntityFrameworkCore;
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
}
