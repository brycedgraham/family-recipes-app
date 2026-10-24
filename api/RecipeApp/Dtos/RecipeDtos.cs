using RecipeApp.Core;
using RecipeApp.Core.Models;

namespace RecipeApp.Dtos;

// Response shapes mirror the client's existing data model (client/src/dataModels.js):
// recipe "name", category names as strings, ingredient quantity as a display string.

public record IngredientDto(string Name, string? Quantity, decimal? Amount, string? Unit);

public record RecipeDto(
    Guid Id,
    string Name,
    string? Description,
    List<string> Categories,
    List<IngredientDto> Ingredients,
    List<string> Instructions,
    List<string> ImageUrls,
    int? PrepTime,
    int? CookTime,
    int? Servings);

public record CategoryDto(Guid Id, string Name, string? Description, int RecipeCount);

public record IngredientInput(string Name, string? Quantity);

public record RecipeRequest(
    string Name,
    string? Description,
    List<string> Categories,
    List<IngredientInput> Ingredients,
    List<string> Instructions,
    int? PrepTime,
    int? CookTime,
    int? Servings);

public static class RecipeMapper
{
    public static RecipeDto ToDto(Recipe recipe) => new(
        recipe.Id,
        recipe.Title,
        recipe.Description,
        recipe.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToList(),
        recipe.RecipeIngredients
            .Select(ri => new IngredientDto(ri.Ingredient.Name, IngredientText.FormatQuantity(ri.Quantity, ri.Unit), ri.Quantity, ri.Unit))
            .ToList(),
        recipe.Steps.OrderBy(s => s.StepNumber).Select(s => s.Text).ToList(),
        recipe.Images
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .Select(i => $"/api/images/{recipe.Id}/{i.FileName}")
            .ToList(),
        recipe.PrepTimeMinutes,
        recipe.CookTimeMinutes,
        recipe.Servings);
}
