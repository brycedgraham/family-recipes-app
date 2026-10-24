using RecipeApp.Core.Models;

namespace RecipeApp.Core.Interfaces;

public interface IShoppingListRepository
{
    Task<IReadOnlyList<ShoppingList>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ShoppingList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShoppingList?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default);
    Task<ShoppingList> AddAsync(ShoppingList list);
    Task<ShoppingList> UpdateAsync(ShoppingList list);
    Task<bool> DeleteAsync(Guid id);
}
