using RecipeApp.Core.Models;

namespace RecipeApp.Core.Interfaces;

public interface IMealPlanRepository
{
    /// <summary>Returns the plan for the given ISO week, or null. <paramref name="anyDate"/> may be any day of the week.</summary>
    Task<MealPlan?> GetByWeekAsync(string anyDate, CancellationToken cancellationToken = default);

    Task<MealPlan> AddAsync(MealPlan plan);

    /// <summary>Saves changes to the plan and its entries; bumps UpdatedAt.</summary>
    Task<MealPlan> UpdateAsync(MealPlan plan);

    Task<MealPlanEntry?> GetEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task RemoveEntryAsync(MealPlanEntry entry);
}
