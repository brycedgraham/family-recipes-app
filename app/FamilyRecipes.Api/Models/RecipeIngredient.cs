using System.ComponentModel.DataAnnotations;

namespace FamilyRecipes.Api.Models;

public class RecipeIngredient
{
    public int RecipeId { get; set; }
    public int IngredientId { get; set; }
    
    [Required]
    public string Quantity { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;
    
    // Navigation properties
    public Recipe Recipe { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
