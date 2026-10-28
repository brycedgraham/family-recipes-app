using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Infrastructure.Data;

namespace RecipeApp.Infrastructure.Repositories;

public class MealPlanRepository : IMealPlanRepository
{
    private readonly RecipeDbContext _context;

    public MealPlanRepository(RecipeDbContext context)
    {
        _context = context;
    }

    /// <summary>Normalizes any "YYYY-MM-DD" date to the Monday of its ISO week ("YYYY-MM-DD").</summary>
    /// <exception cref="FormatException">The input is not a valid YYYY-MM-DD date.</exception>
    public static string NormalizeToMonday(string anyDate) =>
        MondayOf(ParseDate(anyDate)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The Monday of the ISO week containing <paramref name="date"/>.</summary>
    public static DateOnly MondayOf(DateOnly date)
    {
        // DayOfWeek: Sunday=0 ... Saturday=6; ISO weeks start on Monday.
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        return date.AddDays(-offset);
    }

    public static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task<MealPlan?> GetByWeekAsync(string anyDate, CancellationToken cancellationToken = default)
    {
        var monday = NormalizeToMonday(anyDate);
        return await _context.MealPlans
            .Include(p => p.Entries).ThenInclude(e => e.Recipe)
            .FirstOrDefaultAsync(p => p.WeekStartDate == monday, cancellationToken);
    }

    public async Task<MealPlan> AddAsync(MealPlan plan)
    {
        var now = DateTime.UtcNow;
        plan.CreatedAt = now;
        plan.UpdatedAt = now;
        await _context.MealPlans.AddAsync(plan);
        await _context.SaveChangesAsync();
        return plan;
    }

    public async Task<MealPlan> UpdateAsync(MealPlan plan)
    {
        plan.UpdatedAt = DateTime.UtcNow;
        _context.MealPlans.Update(plan);
        await _context.SaveChangesAsync();
        return plan;
    }

    public async Task<MealPlanEntry?> GetEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MealPlanEntries
            .Include(e => e.MealPlan!).ThenInclude(p => p.Entries)
            .Include(e => e.Recipe)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task RemoveEntryAsync(MealPlanEntry entry)
    {
        if (entry.MealPlan is not null)
        {
            entry.MealPlan.UpdatedAt = DateTime.UtcNow;
        }
        _context.MealPlanEntries.Remove(entry);
        await _context.SaveChangesAsync();
    }
}
