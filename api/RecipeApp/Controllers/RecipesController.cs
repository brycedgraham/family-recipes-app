using Microsoft.AspNetCore.Mvc;
using RecipeApp.Core;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Dtos;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeRepository _recipes;

    public RecipesController(IRecipeRepository recipes)
    {
        _recipes = recipes;
    }

    [HttpGet]
    public async Task<List<RecipeDto>> GetAll([FromQuery] string? q, [FromQuery] string? category, CancellationToken cancellationToken)
    {
        var recipes = await _recipes.GetAllAsync(q, category, cancellationToken);
        return recipes.Select(RecipeMapper.ToDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var recipe = await _recipes.GetByIdAsync(id);
        return recipe is null ? NotFound() : Ok(RecipeMapper.ToDto(recipe));
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] RecipeRequest request)
    {
        var recipe = new Recipe();
        await ApplyAsync(recipe, request);
        var created = await _recipes.AddAsync(recipe);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, RecipeMapper.ToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(Guid id, [FromBody] RecipeRequest request)
    {
        var recipe = await _recipes.GetByIdAsync(id);
        if (recipe is null) return NotFound();

        recipe.RecipeIngredients.Clear();
        recipe.Steps.Clear();
        recipe.Categories.Clear();
        await ApplyAsync(recipe, request);
        var updated = await _recipes.UpdateAsync(recipe);
        return Ok(RecipeMapper.ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _recipes.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    private async Task ApplyAsync(Recipe recipe, RecipeRequest request)
    {
        recipe.Title = request.Name.Trim();
        recipe.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        recipe.Servings = request.Servings;
        recipe.PrepTimeMinutes = request.PrepTime;
        recipe.CookTimeMinutes = request.CookTime;

        foreach (var categoryName in request.Categories ?? [])
        {
            if (string.IsNullOrWhiteSpace(categoryName)) continue;
            var category = await _recipes.FindOrCreateCategoryAsync(categoryName);
            if (!recipe.Categories.Any(c => c.Id == category.Id))
            {
                recipe.Categories.Add(category);
            }
        }

        foreach (var input in request.Ingredients ?? [])
        {
            if (string.IsNullOrWhiteSpace(input.Name)) continue;
            var (quantity, unit) = IngredientText.ParseQuantity(input.Quantity);
            var ingredient = await _recipes.FindOrCreateIngredientAsync(input.Name);
            if (recipe.RecipeIngredients.Any(ri => ri.IngredientId == ingredient.Id)) continue; // duplicate line
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Quantity = quantity,
                Unit = unit,
                IngredientId = ingredient.Id,
            });
        }

        var stepNumber = 1;
        foreach (var step in request.Instructions ?? [])
        {
            if (string.IsNullOrWhiteSpace(step)) continue;
            recipe.Steps.Add(new RecipeStep { StepNumber = stepNumber++, Text = step.Trim() });
        }
    }
}
