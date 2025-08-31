using System.ComponentModel.DataAnnotations;

namespace FamilyRecipes.Api.Models;

public class Ingredient
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<RecipeIngredient> Recipes { get; set; } = new List<RecipeIngredient>();
}
