namespace RecipeApp.Core.Models;

/// <summary>A photo attached to a recipe. The file lives on disk under data/images/{RecipeId}/{FileName};
/// only the file name is stored in the database (no binary blobs).</summary>
public class RecipeImage
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Recipe? Recipe { get; set; }
}
