using System.Text.Json;
using RecipeApp.Core.Models;

namespace RecipeApp.Api.Services;

/// <summary>
/// On-disk staging area for photo imports (Ticket 15): <c>{dataDir}/import_queue/{jobId}/</c>.
/// Files live here from upload until a job completes (then they are promoted to the recipe images
/// directory and removed) — failures deliberately keep them so Retry works.
/// </summary>
public sealed class ImportQueueStore(string rootDirectory)
{
    public const int MaxPhotosPerJob = 4;

    /// <summary>Photo extensions accepted for vision import (subset of ImageStore's set; no .gif).</summary>
    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public string RootDirectory { get; } = rootDirectory;

    public string JobDirectory(Guid jobId) => Path.Combine(RootDirectory, jobId.ToString());

    /// <summary>Writes one staged photo under a generated ordered name (<c>{index}-{guid}{ext}</c>) and returns the file name.</summary>
    public string StageFile(Guid jobId, int index, Guid fileId, string extension, byte[] bytes)
    {
        var directory = JobDirectory(jobId);
        Directory.CreateDirectory(directory);
        var fileName = $"{index}-{fileId:N}{extension}";
        File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
        return fileName;
    }

    /// <summary>Parses <see cref="RecipeImportJob.FileNamesJson"/> back to the ordered staged file names.</summary>
    public static List<string> ReadFileNames(RecipeImportJob job)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(job.FileNamesJson) ?? [];
        }
        catch (JsonException)
        {
            return []; // corrupt metadata shouldn't nuke the row; retry UI surfaces the problem instead
        }
    }

    /// <summary>Best-effort removal of a job's staging directory (already promoted, deleted, or superseded).</summary>
    public void DeleteJobDirectory(Guid jobId)
    {
        try
        {
            var directory = JobDirectory(jobId);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Staging leftovers are harmless; never fail the request over them.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
