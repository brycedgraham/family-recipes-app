namespace RecipeApp.Core.Models;

/// <summary>A weekly meal plan. Keyed by the Monday of its ISO week (one plan per week).</summary>
public class MealPlan
{
    public Guid Id { get; set; }

    /// <summary>Monday of the ISO week this plan covers, "YYYY-MM-DD". Always normalized to Monday.</summary>
    public string WeekStartDate { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<MealPlanEntry> Entries { get; set; } = new();
}

/// <summary>One scheduled meal in a weekly plan: a recipe (scaled) or a free-text custom entry.</summary>
public class MealPlanEntry
{
    public Guid Id { get; set; }

    public Guid MealPlanId { get; set; }

    /// <summary>0 = Monday through 6 = Sunday.</summary>
    public int DayOfWeek { get; set; }

    /// <summary>"Breakfast", "Lunch", "Dinner", or "Snack".</summary>
    public string MealType { get; set; } = "";

    /// <summary>Null for free-text custom entries ("Leftovers", "Taco Night").</summary>
    public Guid? RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public string? CustomTitle { get; set; }

    /// <summary>Scale applied to the recipe's quantities (1.0 = as written).</summary>
    public decimal Multiplier { get; set; } = 1.0m;

    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    public MealPlan? MealPlan { get; set; }
}
