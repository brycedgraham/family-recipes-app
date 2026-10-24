using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Core;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Dtos;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Route("api/shopping-lists")]
public class ShoppingListsController : ControllerBase
{
    private readonly IShoppingListRepository _lists;
    private readonly IRecipeRepository _recipes;

    public ShoppingListsController(IShoppingListRepository lists, IRecipeRepository recipes)
    {
        _lists = lists;
        _recipes = recipes;
    }

    [HttpGet]
    public async Task<List<ShoppingListSummaryDto>> GetAll(CancellationToken cancellationToken)
    {
        var lists = await _lists.GetAllAsync(cancellationToken);
        return lists.Select(ShoppingListMapper.ToSummaryDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShoppingListDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var list = await _lists.GetByIdAsync(id, cancellationToken);
        return list is null ? NotFound() : Ok(ShoppingListMapper.ToDto(list));
    }

    [HttpGet("shared/{shareToken}")]
    public async Task<ActionResult<ShoppingListDto>> GetShared(string shareToken, CancellationToken cancellationToken)
    {
        var list = await _lists.GetByShareTokenAsync(shareToken, cancellationToken);
        return list is null ? NotFound() : Ok(ShoppingListMapper.ToDto(list));
    }

    [HttpPost]
    public async Task<ActionResult<ShoppingListDto>> Create([FromBody] CreateShoppingListRequest? request, CancellationToken cancellationToken)
    {
        var list = new ShoppingList
        {
            Name = string.IsNullOrWhiteSpace(request?.Name) ? "My List" : request.Name.Trim(),
            ShareToken = NewShareToken(),
        };

        foreach (var item in request?.Items ?? [])
        {
            AddParsedLine(list, item.RawText);
        }

        ShoppingListAggregator.Resort(list);
        await _lists.AddAsync(list);
        return CreatedAtAction(nameof(GetById), new { id = list.Id }, ShoppingListMapper.ToDto(list));
    }

    [HttpPost("{id:guid}/add-recipes")]
    public async Task<ActionResult<ShoppingListDto>> AddRecipes(Guid id, [FromBody] AddRecipesRequest request, CancellationToken cancellationToken)
    {
        var list = await _lists.GetByIdAsync(id, cancellationToken);
        if (list is null) return NotFound();

        foreach (var entry in request.Items ?? [])
        {
            var recipe = await _recipes.GetByIdAsync(entry.RecipeId);
            if (recipe is null) continue;
            // Re-adding a recipe that already contributed is a no-op so the button can be tapped safely.
            if (list.Items.Any(i => SourceListContains(i.RecipeSources, recipe.Title))) continue;
            ShoppingListAggregator.AddRecipe(list, recipe.Title, recipe.RecipeIngredients, entry.Multiplier);
        }

        ShoppingListAggregator.Resort(list);
        await _lists.UpdateAsync(list);
        return Ok(ShoppingListMapper.ToDto(list));
    }

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<ShoppingListDto>> AddItem(Guid id, [FromBody] RawTextItem request, CancellationToken cancellationToken)
    {
        var list = await _lists.GetByIdAsync(id, cancellationToken);
        if (list is null) return NotFound();

        AddParsedLine(list, request.RawText);
        ShoppingListAggregator.Resort(list);
        await _lists.UpdateAsync(list);
        return Ok(ShoppingListMapper.ToDto(list));
    }

    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ShoppingListDto>> PatchItem(Guid id, Guid itemId, [FromBody] PatchItemRequest request, CancellationToken cancellationToken)
    {
        var list = await _lists.GetByIdAsync(id, cancellationToken);
        if (list is null) return NotFound();

        var item = list.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return NotFound();

        if (request.IsChecked.HasValue) item.IsChecked = request.IsChecked.Value;
        if (!string.IsNullOrWhiteSpace(request.Name)) item.Name = request.Name.Trim();
        if (request.Category is not null) item.Category = request.Category.Trim();
        if (request.Quantity.HasValue) item.Quantity = request.Quantity;
        if (request.Unit is not null) item.Unit = request.Unit.Trim();

        ShoppingListAggregator.Resort(list);
        await _lists.UpdateAsync(list);
        return Ok(ShoppingListMapper.ToDto(list));
    }

    [HttpDelete("{id:guid}/items/checked")]
    public async Task<ActionResult<ShoppingListDto>> ClearChecked(Guid id, CancellationToken cancellationToken)
    {
        var list = await _lists.GetByIdAsync(id, cancellationToken);
        if (list is null) return NotFound();

        foreach (var item in list.Items.Where(i => i.IsChecked).ToList())
            list.Items.Remove(item);

        ShoppingListAggregator.Resort(list);
        await _lists.UpdateAsync(list);
        return Ok(ShoppingListMapper.ToDto(list));
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ShoppingListDto>> DeleteItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var list = await _lists.GetByIdAsync(id, cancellationToken);
        if (list is null) return NotFound();

        var item = list.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return NotFound();

        list.Items.Remove(item);
        ShoppingListAggregator.Resort(list);
        await _lists.UpdateAsync(list);
        return Ok(ShoppingListMapper.ToDto(list));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return (await _lists.DeleteAsync(id)) ? NoContent() : NotFound();
    }

    /// <summary>Parses a raw line and merges it into the list with a guessed aisle category.</summary>
    private static void AddParsedLine(ShoppingList list, string? rawText)
    {
        var (quantity, unit, name, notes) = IngredientText.SplitLine(rawText);
        if (string.IsNullOrWhiteSpace(name)) return;
        ShoppingListAggregator.AddManualItem(list, name, IngredientCategoryClassifier.GuessCategory(name.ToLowerInvariant()), quantity, unit, notes);
    }

    private static bool SourceListContains(string? sources, string title) =>
        sources is not null && sources.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(s => s.Equals(title, StringComparison.OrdinalIgnoreCase));

    internal static string NewShareToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
