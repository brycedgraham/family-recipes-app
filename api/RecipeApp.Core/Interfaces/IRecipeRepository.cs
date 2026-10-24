using RecipeApp.Core.Models;

namespace RecipeApp.Core.Interfaces;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Recipe>> GetAllAsync(string? search = null, string? categoryName = null, CancellationToken cancellationToken = default);
    Task<Recipe> AddAsync(Recipe recipe);
    Task<Recipe> UpdateAsync(Recipe recipe);
    Task<bool> DeleteAsync(Guid id);
    Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Category> FindOrCreateCategoryAsync(string name);
    /// <param name="notes">Optional prep notes from the same line; used for store-hint detection ("(winco)").</param>
    Task<Ingredient> FindOrCreateIngredientAsync(string name, string? notes = null);
    /// <summary>
    /// Persists a remembered store / pantry-staple choice back to the shared ingredient catalog row, so future
    /// lists inherit it. Matches by <paramref name="ingredientId"/> or, when that is null, by normalized name.
    /// When <paramref name="storeTouched"/> is true the item's new store value is mirrored verbatim — null/empty clears
    /// the remembered preference; when false the stored preference is left untouched. A null <paramref name="isPantryStaple"/>
    /// leaves that flag untouched. No-op when no catalog row matches (standalone manual items).
    /// </summary>
    Task UpdateIngredientPreferencesAsync(Guid? ingredientId, string normalizedName, string? store, bool storeTouched, bool? isPantryStaple);
}

public sealed record CategorySummary(Guid Id, string Name, string? Description, int RecipeCount);
