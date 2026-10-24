using RecipeApp.Core.Models;

namespace RecipeApp.Dtos;

// Response shapes for the weekly meal planner. Entries carry joined recipe fields so the client
// can render titles and times without a second fetch.

public record MealPlanEntryDto(
    Guid Id,
    int DayOfWeek,
    string MealType,
    Guid? RecipeId,
    string? RecipeTitle,
    int? PrepTimeMinutes,
    int? CookTimeMinutes,
    string? CustomTitle,
    decimal Multiplier,
    string? Notes);

public record MealPlanDto(
    Guid Id,
    string WeekStartDate,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<MealPlanEntryDto> Entries);

// Requests
public record AddMealPlanEntryRequest(int DayOfWeek, string MealType, Guid? RecipeId, string? CustomTitle, decimal? Multiplier, string? Notes);
public record PatchMealPlanEntryRequest(int? DayOfWeek, string? MealType, decimal? Multiplier, string? CustomTitle, string? Notes);
public record BuildWeekShoppingListRequest(Guid? ShoppingListId, string? ListName);

public static class MealPlanMapper
{
    private static readonly string[] MealTypeOrder = { "Breakfast", "Lunch", "Dinner", "Snack" };

    public static MealPlanDto ToDto(MealPlan plan) => new(
        plan.Id,
        plan.WeekStartDate,
        plan.Name,
        plan.CreatedAt,
        plan.UpdatedAt,
        plan.Entries
            .OrderBy(e => MealTypeOrder.IndexOf(e.MealType))
            .ThenBy(e => e.SortOrder)
            .Select(ToEntryDto)
            .ToList());

    private static MealPlanEntryDto ToEntryDto(MealPlanEntry entry) => new(
        entry.Id,
        entry.DayOfWeek,
        entry.MealType,
        entry.RecipeId,
        entry.Recipe?.Title,
        entry.Recipe?.PrepTimeMinutes,
        entry.Recipe?.CookTimeMinutes,
        entry.CustomTitle,
        entry.Multiplier,
        entry.Notes);
}
