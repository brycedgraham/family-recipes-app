using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Core;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Dtos;
using RecipeApp.Infrastructure.Repositories;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Route("api/meal-plans")]
public class MealPlansController : ControllerBase
{
    private static readonly string[] MealTypes = { "Breakfast", "Lunch", "Dinner", "Snack" };

    private readonly IMealPlanRepository _plans;
    private readonly IRecipeRepository _recipes;
    private readonly IShoppingListRepository _lists;

    public MealPlansController(IMealPlanRepository plans, IRecipeRepository recipes, IShoppingListRepository lists)
    {
        _plans = plans;
        _recipes = recipes;
        _lists = lists;
    }

    /// <summary>Returns the plan for the ISO week containing <paramref name="date"/> (any day of that week).
    /// A week with no saved plan returns an empty virtual plan carrying the normalized Monday.</summary>
    [HttpGet("week/{date}")]
    public async Task<ActionResult<MealPlanDto>> GetWeek(string date, CancellationToken cancellationToken)
    {
        var monday = TryNormalize(date, out var error);
        if (monday is null) return BadRequest(error);

        var plan = await _plans.GetByWeekAsync(monday, cancellationToken);
        if (plan is null)
        {
            var now = DateTime.UtcNow;
            return Ok(new MealPlanDto(Guid.Empty, monday, "", now, now, []));
        }
        return Ok(MealPlanMapper.ToDto(plan));
    }

    /// <summary>Adds an entry to the week's plan, auto-creating the plan row if needed.</summary>
    [HttpPost("week/{date}/entries")]
    public async Task<ActionResult<MealPlanDto>> AddEntry(string date, [FromBody] AddMealPlanEntryRequest request, CancellationToken cancellationToken)
    {
        var monday = TryNormalize(date, out var error);
        if (monday is null) return BadRequest(error);

        if (request.DayOfWeek is < 0 or > 6)
            return BadRequest("dayOfWeek must be 0 (Monday) through 6 (Sunday).");
        var mealType = CanonicalMealType(request.MealType);
        if (mealType is null)
            return BadRequest($"mealType must be one of: {string.Join(", ", MealTypes)}.");
        if (request.RecipeId is null && string.IsNullOrWhiteSpace(request.CustomTitle))
            return BadRequest("Provide either recipeId or customTitle.");
        var multiplier = request.Multiplier ?? 1.0m;
        if (multiplier <= 0)
            return BadRequest("multiplier must be positive.");
        // Resolve the referenced recipe once: validates existence and provides a title snapshot,
        // so the entry keeps its name if the recipe is deleted later (ON DELETE SET NULL).
        Recipe? recipe = null;
        if (request.RecipeId is { } recipeId)
        {
            recipe = await _recipes.GetByIdAsync(recipeId);
            if (recipe is null) return BadRequest("recipeId does not exist.");
        }

        var entry = new MealPlanEntry
        {
            DayOfWeek = request.DayOfWeek,
            MealType = mealType,
            RecipeId = request.RecipeId,
            CustomTitle = !string.IsNullOrWhiteSpace(request.CustomTitle) ? request.CustomTitle.Trim() : recipe?.Title,
            Multiplier = multiplier,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        var plan = await _plans.GetByWeekAsync(monday, cancellationToken);
        if (plan is not null)
        {
            entry.SortOrder = plan.Entries.Count == 0 ? 0 : plan.Entries.Max(e => e.SortOrder) + 1;
            plan.Entries.Add(entry);
            plan = await _plans.UpdateAsync(plan);
        }
        else
        {
            entry.SortOrder = 0;
            plan = new MealPlan
            {
                WeekStartDate = monday,
                Name = DefaultName(monday),
                Entries = { entry },
            };
            plan = await _plans.AddAsync(plan);
        }

        return Ok(MealPlanMapper.ToDto(plan));
    }

    /// <summary>Updates an entry's day, slot, scale, custom title, or notes (enables moving a meal).</summary>
    [HttpPatch("entries/{entryId:guid}")]
    public async Task<ActionResult<MealPlanDto>> PatchEntry(Guid entryId, [FromBody] PatchMealPlanEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await _plans.GetEntryByIdAsync(entryId, cancellationToken);
        if (entry is null) return NotFound();

        if (request.DayOfWeek is not null && request.DayOfWeek.Value is < 0 or > 6)
            return BadRequest("dayOfWeek must be 0 (Monday) through 6 (Sunday).");
        string? mealType = null;
        if (request.MealType is not null)
        {
            mealType = CanonicalMealType(request.MealType);
            if (mealType is null)
                return BadRequest($"mealType must be one of: {string.Join(", ", MealTypes)}.");
        }
        if (request.Multiplier is not null && request.Multiplier.Value <= 0)
            return BadRequest("multiplier must be positive.");

        if (request.DayOfWeek is not null) entry.DayOfWeek = request.DayOfWeek.Value;
        if (mealType is not null) entry.MealType = mealType;
        if (request.Multiplier is not null) entry.Multiplier = request.Multiplier.Value;
        if (request.CustomTitle is not null)
            entry.CustomTitle = string.IsNullOrWhiteSpace(request.CustomTitle) ? null : request.CustomTitle.Trim();
        if (request.Notes is not null)
            entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // Keep a title snapshot on recipe-backed entries so the meal name survives recipe deletion.
        if (entry.RecipeId is not null && string.IsNullOrWhiteSpace(entry.CustomTitle))
            entry.CustomTitle = entry.Recipe?.Title;

        var plan = await _plans.UpdateAsync(entry.MealPlan!);
        return Ok(MealPlanMapper.ToDto(plan));
    }

    /// <summary>Removes an entry from its week's plan.</summary>
    [HttpDelete("entries/{entryId:guid}")]
    public async Task<ActionResult<MealPlanDto>> DeleteEntry(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await _plans.GetEntryByIdAsync(entryId, cancellationToken);
        if (entry is null) return NotFound();

        var plan = entry.MealPlan!;
        plan.Entries.Remove(entry);
        await _plans.RemoveEntryAsync(entry);
        return Ok(MealPlanMapper.ToDto(plan));
    }

    /// <summary>Collects every recipe entry in the week (summing multipliers when a recipe repeats)
    /// and merges them into an existing or newly created shopping list.</summary>
    [HttpPost("week/{date}/shopping-list")]
    public async Task<ActionResult<ShoppingListDto>> BuildShoppingList(string date, [FromBody] BuildWeekShoppingListRequest? request, CancellationToken cancellationToken)
    {
        var monday = TryNormalize(date, out var error);
        if (monday is null) return BadRequest(error);

        var plan = await _plans.GetByWeekAsync(monday, cancellationToken);
        // Sum multipliers per recipe so a meal scheduled twice in the week scales correctly.
        var totals = (plan?.Entries ?? [])
            .Where(e => e.RecipeId is not null)
            .GroupBy(e => e.RecipeId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Multiplier));

        var list = request?.ShoppingListId is { } targetId
            ? await _lists.GetByIdAsync(targetId, cancellationToken)
            : new ShoppingList
            {
                Name = string.IsNullOrWhiteSpace(request?.ListName) ? DefaultName(monday) : request!.ListName.Trim(),
                ShareToken = ShoppingListsController.NewShareToken(),
            };
        if (list is null) return NotFound("Shopping list not found.");

        foreach (var (recipeId, multiplier) in totals)
        {
            var recipe = await _recipes.GetByIdAsync(recipeId);
            if (recipe is null) continue;
            ShoppingListAggregator.AddRecipe(list, recipe.Title, recipe.RecipeIngredients, multiplier);
        }

        ShoppingListAggregator.Resort(list);
        list = request?.ShoppingListId is null ? await _lists.AddAsync(list) : await _lists.UpdateAsync(list);
        return Ok(ShoppingListMapper.ToDto(list));
    }

    private static string? CanonicalMealType(string? value) =>
        MealTypes.FirstOrDefault(t => t.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static string DefaultName(string monday) =>
        $"Week of {MealPlanRepository.ParseDate(monday):MMM d, yyyy}";

    /// <summary>Normalizes any YYYY-MM-DD date to its ISO-week Monday; returns null + error message on bad input.</summary>
    private static string? TryNormalize(string date, out string? error)
    {
        try
        {
            error = null;
            return MealPlanRepository.NormalizeToMonday(date);
        }
        catch (FormatException)
        {
            error = "Expected a YYYY-MM-DD date.";
            return null;
        }
    }
}
