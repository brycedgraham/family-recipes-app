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
    /// <summary>Store names in use: registered stores (by SortOrder, then Name) merged with any ad-hoc names still on list items or ingredients.</summary>
    Task<IReadOnlyList<string>> GetKnownStoresAsync(CancellationToken cancellationToken = default);
    /// <summary>Registers a new store (case-insensitive uniqueness; no-op if already registered). Returns the updated known-store list.</summary>
    Task<IReadOnlyList<string>> CreateStoreAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Removes a registered store and unassigns it from ingredients (PreferredStore) and shopping-list items (Store). Returns the updated known-store list.</summary>
    Task<IReadOnlyList<string>> DeleteStoreAsync(string name, CancellationToken cancellationToken = default);
}
