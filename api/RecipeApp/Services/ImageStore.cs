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

    /// <summary>Detects an image format from its magic bytes; null when the content is not a known image.</summary>
    public static string? DetectImageExtension(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ".jpg";
        if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
        if (data.Length >= 6 && StartsWith(data, GifMagic)) return ".gif";
        if (data.Length >= 12 && StartsWith(data, RiffMagic) && StartsWith(data.AsSpan(8), WebpMagic)) return ".webp";
        return null;
    }

    private static readonly byte[] GifMagic = { 0x47, 0x49, 0x46, 0x38 }; // "GIF8"
    private static readonly byte[] RiffMagic = { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
    private static readonly byte[] WebpMagic = { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

    private static bool StartsWith(ReadOnlySpan<byte> data, byte[] magic) =>
        data.Length >= magic.Length && data.Slice(0, magic.Length).SequenceEqual(magic);

    /// <summary>Persists downloaded image bytes (magic-byte validated, ≤ 10 MB) and returns the stored file name.</summary>
    public string SaveBytes(Guid recipeId, byte[] data)
    {
        if (data.Length == 0 || data.Length > MaxFileSizeBytes)
            throw new InvalidDataException("Downloaded image is empty or larger than 10 MB.");
        var extension = DetectImageExtension(data);
        if (extension is null)
            throw new InvalidDataException("Downloaded content is not a recognized image (jpg, png, webp or gif).");

        var dir = RecipeDirectory(recipeId);
        Directory.CreateDirectory(dir);
        var fileName = Guid.NewGuid().ToString("N") + extension;
        File.WriteAllBytes(Path.Combine(dir, fileName), data);
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
