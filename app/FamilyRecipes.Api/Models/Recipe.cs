using System.ComponentModel.DataAnnotations;

namespace FamilyRecipes.Api.Models;

public class Recipe
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Instructions { get; set; } = string.Empty;
    
    public int PrepTime { get; set; }
    
    public int CookTime { get; set; }
    
    public int ServingSize { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Foreign keys
    public int CreatedById { get; set; }
    public int CategoryId { get; set; }
    
    // Navigation properties
    public User CreatedBy { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}
