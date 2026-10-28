namespace RecipeApp.Core.Models;

/// <summary>
/// One opportunistic "import recipe from photos" job (Ticket 15). Uploaded photos are staged on disk under
/// {dataDir}/import_queue/{Id}/ (file names recorded in <see cref="FileNamesJson"/>); the VisionImportWorker
/// extracts a recipe from them whenever the local vision endpoint is online. Staged files survive failures
/// so a "Failed" job can be retried.
/// </summary>
public class RecipeImportJob
{
    // Id has NO constructor initializer — EF must generate keys at save time (see AGENTS.md).
    public Guid Id { get; set; }

    /// <summary>"Pending" | "Processing" | "Completed" | "Failed".</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>JSON array of staged file names under import_queue/{Id}/, in upload order.</summary>
    public string FileNamesJson { get; set; } = "[]";

    /// <summary>Optional category chosen at upload time.</summary>
    public string? Category { get; set; }

    /// <summary>Populated when Status is "Completed".</summary>
    public Guid? RecipeId { get; set; }

    /// <summary>Populated when Status is "Completed".</summary>
    public string? RecipeTitle { get; set; }

    /// <summary>Model id the endpoint reported using for the extraction.</summary>
    public string? ModelUsed { get; set; }

    /// <summary>Concise failure reason; kept (with staged files) so the job can be retried.</summary>
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
