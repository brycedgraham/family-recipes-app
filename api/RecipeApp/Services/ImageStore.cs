using Microsoft.AspNetCore.Http;

namespace RecipeApp.Api.Services;

/// <summary>
/// Owns recipe photo files on disk under {dataDir}/images/{recipeId}/{fileName}.
/// The database stores only the file name (see <c>RecipeImage</c>); all filesystem access goes through here.
/// </summary>
public sealed class ImageStore
{
    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    public const long MaxFileSizeBytes = 10L * 1024 * 1024; // 10 MB per file

    private readonly string _rootDirectory;

    public ImageStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);
    }

    public static bool IsAllowedFile(IFormFile file) =>
        AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())
        && file.Length <= MaxFileSizeBytes;

    /// <summary>Persists the uploaded file under the recipe's directory and returns its stored file name.</summary>
    public string Save(Guid recipeId, IFormFile file)
    {
        if (!IsAllowedFile(file))
            throw new InvalidDataException($"Only {string.Join(", ", AllowedExtensions)} files up to 10 MB are allowed.");

        var dir = RecipeDirectory(recipeId);
        Directory.CreateDirectory(dir);
        var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        using (var stream = File.Create(Path.Combine(dir, fileName)))
        {
            file.CopyTo(stream);
        }
        return fileName;
    }

    /// <summary>Removes a single photo file (best effort — a missing file is not an error).</summary>
    public void DeleteFile(Guid recipeId, string fileName)
    {
        var dir = RecipeDirectory(recipeId);
        var path = Path.Combine(dir, fileName);
        if (File.Exists(path)) File.Delete(path);
        // Drop the now-empty per-recipe directory so data/images does not accumulate empty dirs.
        try { Directory.Delete(dir, recursive: false); } catch (IOException) { /* other photos remain */ }
    }

    /// <summary>Removes the recipe's whole photo directory when the recipe itself is deleted.</summary>
    public void DeleteRecipeDirectory(Guid recipeId)
    {
        var dir = RecipeDirectory(recipeId);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private string RecipeDirectory(Guid recipeId) => Path.Combine(_rootDirectory, recipeId.ToString());
}
