namespace RecipeApp.Core.Models;

public class RecipeStep
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipeId { get; set; }
    public int StepNumber { get; set; }
    public string Text { get; set; } = string.Empty;

    // Navigation
    public Recipe Recipe { get; set; } = null!;
}
