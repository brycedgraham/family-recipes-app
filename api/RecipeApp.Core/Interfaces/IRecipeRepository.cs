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
    Task<Ingredient> FindOrCreateIngredientAsync(string name);
}

public sealed record CategorySummary(Guid Id, string Name, string? Description, int RecipeCount);
