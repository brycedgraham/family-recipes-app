using Microsoft.EntityFrameworkCore;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Infrastructure.Data;

namespace RecipeApp.Infrastructure.Repositories;

public class RecipeRepository : IRecipeRepository
{
    private readonly RecipeDbContext _context;

    public RecipeRepository(RecipeDbContext context)
    {
        _context = context;
    }

    public async Task<Recipe?> GetByIdAsync(Guid id)
    {
        return await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Steps)
            .Include(r => r.Categories)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IReadOnlyList<Recipe>> GetAllAsync(string? search = null, string? categoryName = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Recipes
            .Include(r => r.Categories)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Steps)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => EF.Functions.ILike(r.Title, $"%{term}%")
                || (r.Description != null && EF.Functions.ILike(r.Description, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var category = categoryName.Trim();
            query = query.Where(r => r.Categories.Any(c => c.Name == category));
        }

        return await query.OrderBy(r => r.Title).ToListAsync(cancellationToken);
    }

    public async Task<Recipe> AddAsync(Recipe recipe)
    {
        recipe.CreatedAt = DateTime.UtcNow;
        recipe.UpdatedAt = DateTime.UtcNow;
        await _context.Recipes.AddAsync(recipe);
        await _context.SaveChangesAsync();
        return recipe;
    }

    public async Task<Recipe> UpdateAsync(Recipe recipe)
    {
        recipe.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return recipe;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null) return false;

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .Include(c => c.Recipes)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return categories
            .Select(c => new CategorySummary(c.Id, c.Name, c.Description, c.Recipes.Count))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Category> FindOrCreateCategoryAsync(string name)
    {
        var trimmed = name.Trim();
        var existing = await _context.Categories.FirstOrDefaultAsync(c => EF.Functions.ILike(c.Name, trimmed));
        if (existing != null) return existing;

        var category = new Category { Name = trimmed };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<Ingredient> FindOrCreateIngredientAsync(string name)
    {
        var trimmed = name.Trim();
        var normalized = trimmed.ToLowerInvariant();
        var existing = await _context.Ingredients.FirstOrDefaultAsync(i => i.NormalizedName == normalized);
        if (existing != null) return existing;

        var ingredient = new Ingredient { Name = trimmed, NormalizedName = normalized };
        _context.Ingredients.Add(ingredient);
        await _context.SaveChangesAsync();
        return ingredient;
    }
}
