using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Dtos;
using RecipeApp.Api.Services;
using RecipeApp.Core.Models;
using RecipeApp.Infrastructure.Data;

namespace RecipeApp.Api.Controllers;

/// <summary>
/// Photo recipe import endpoints (Ticket 15). Uploads stage photos under data/import_queue/{jobId}/ and
/// create a "Pending" <see cref="RecipeImportJob"/>; the <see cref="VisionImportWorker"/> picks them up
/// opportunistically whenever the LAN vision endpoint is online. Routes are siblings of import-url under
/// api/recipes — the literal "import-photo" never collides with the {id:guid} constraints in RecipesController.
/// </summary>
[ApiController]
[Route("api/recipes/import-photo")]
public class PhotoImportsController : ControllerBase
{
    private readonly RecipeDbContext _db;
    private readonly ImportQueueStore _queue;
    private readonly ImageStore _images;
    private readonly VisionEndpointStatus _status;
    private readonly VisionImportWakeSignal _wake;

    public PhotoImportsController(
        RecipeDbContext db,
        ImportQueueStore queue,
        ImageStore images,
        VisionEndpointStatus status,
        VisionImportWakeSignal wake)
    {
        _db = db;
        _queue = queue;
        _images = images;
        _status = status;
        _wake = wake;
    }

    /// <summary>Stage 1–4 photos and enqueue a vision extraction job; returns 202 (processing is async).</summary>
    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFileCollection? files, [FromForm] string? category)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest("Attach 1-4 photos (.jpg, .jpeg, .png, .webp).");
        }
        if (files.Count > ImportQueueStore.MaxPhotosPerJob)
        {
            return BadRequest($"At most {ImportQueueStore.MaxPhotosPerJob} photos per import.");
        }

        // Validate everything before touching disk so a bad file can't leave a half-staged job.
        var staged = new List<(byte[] Bytes, string Extension)>(files.Count);
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!ImportQueueStore.AllowedExtensions.Contains(extension))
            {
                return BadRequest($"'{file.FileName}' is not a supported photo (.jpg, .jpeg, .png, .webp).");
            }
            if (file.Length is 0 or > 10 * 1024 * 1024)
            {
                return BadRequest($"'{file.FileName}' must be between 1 byte and 10 MB.");
            }
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (ImageStore.DetectImageExtension(bytes) is null)
            {
                return BadRequest($"'{file.FileName}' is not a valid image (magic bytes don't match its extension).");
            }
            staged.Add((bytes, extension));
        }

        var jobId = Guid.NewGuid(); // root entity with an explicit key; no children, so EF tracking stays clean
        try
        {
            Directory.CreateDirectory(_queue.JobDirectory(jobId));
            var fileNames = new List<string>(staged.Count);
            for (var i = 0; i < staged.Count; i++)
            {
                fileNames.Add(_queue.StageFile(jobId, i + 1, Guid.NewGuid(), staged[i].Extension, staged[i].Bytes));
            }

            var job = new RecipeImportJob
            {
                Id = jobId,
                Status = "Pending",
                FileNamesJson = System.Text.Json.JsonSerializer.Serialize(fileNames),
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
            _db.RecipeImportJobs.Add(job);
            await _db.SaveChangesAsync();

            _wake.Wake(); // don't wait for the next poll tick while the GPU is likely warm
            return StatusCode(StatusCodes.Status202Accepted, PhotoImportJobDto.From(job));
        }
        catch
        {
            _queue.DeleteJobDirectory(jobId); // never strand staging on a failed enqueue
            throw;
        }
    }

    /// <summary>Recent jobs (newest 25) plus the worker's cached vision-endpoint status for the UI pill.</summary>
    [HttpGet("jobs")]
    public async Task<IActionResult> Jobs()
    {
        var jobs = await _db.RecipeImportJobs
            .OrderByDescending(j => j.CreatedAt)
            .Take(25)
            .ToListAsync();
        return Ok(new
        {
            endpointOnline = _status.Online,
            activeModel = _status.ActiveModel,
            endpointUrl = _status.EndpointUrl,
            jobs = jobs.Select(PhotoImportJobDto.From),
        });
    }

    /// <summary>Requeue a failed job (its staged photos were kept on purpose).</summary>
    [HttpPost("jobs/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        var job = await _db.RecipeImportJobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job is null) return NotFound();
        if (job.Status != "Failed")
        {
            return BadRequest("Only failed jobs can be retried.");
        }
        job.Status = "Pending";
        job.Error = null;
        await _db.SaveChangesAsync();
        _wake.Wake();
        return Ok(PhotoImportJobDto.From(job));
    }

    /// <summary>Drop the job row and any staged files still on disk.</summary>
    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var job = await _db.RecipeImportJobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job is null) return NotFound();
        _db.RecipeImportJobs.Remove(job);
        await _db.SaveChangesAsync();
        _queue.DeleteJobDirectory(id);
        return NoContent();
    }
}
