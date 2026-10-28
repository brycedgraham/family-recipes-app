using System.Text.Json;
using RecipeApp.Api.Services;
using RecipeApp.Core.Models;

namespace RecipeApp.Api.Dtos;

/// <summary>One photo-import job (Ticket 15), as consumed by the PhotoImportModal.</summary>
public record PhotoImportJobDto(
    Guid Id,
    string Status,
    List<string> FileNames,
    string? Category,
    Guid? RecipeId,
    string? RecipeTitle,
    string? ModelUsed,
    string? Error,
    DateTime CreatedAt,
    DateTime? CompletedAt)
{
    public static PhotoImportJobDto From(RecipeImportJob job) => new(
        job.Id,
        job.Status,
        ImportQueueStore.ReadFileNames(job),
        job.Category,
        job.RecipeId,
        job.RecipeTitle,
        job.ModelUsed,
        job.Error,
        job.CreatedAt,
        job.CompletedAt);
}
