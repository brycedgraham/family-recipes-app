using System.ComponentModel.DataAnnotations;

namespace FamilyRecipes.Api.Models;

public class Tag
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
