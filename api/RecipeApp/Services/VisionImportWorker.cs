using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Core;
using RecipeApp.Infrastructure.Data;

namespace RecipeApp.Api.Services;

/// <summary>
/// Opportunistic photo-import worker (Ticket 15). Every <c>VisionImport:PollIntervalSeconds</c> — or
/// immediately when a fresh upload wakes it — it looks for "Pending" <see cref="RecipeImportJob"/> rows,
/// probes the local OpenAI-compatible vision endpoint (<c>GET /v1/models</c>, 3s), and if online runs each
/// job's photos through <c>POST /v1/chat/completions</c> (180s) to extract a recipe. Offline is normal:
/// jobs stay "Pending" and the pass logs at Debug. Ingested recipes reuse the exact URL-import ingredient
/// pipeline (<see cref="IngredientText.SplitLine"/> + <see cref="IRecipeRepository.FindOrCreateIngredientAsync"/>)
/// so normalization, pantry dedupe, and WinCo/Costco routing apply; staged photos are promoted into the
/// recipe's gallery. A failed job keeps its staged files so Retry can re-run it.
/// </summary>
public sealed class VisionImportWorker : BackgroundService
{
    public const string ProbeClientName = "vision-probe";
    public const string InferenceClientName = "vision-inference";

    private const string SystemPrompt = """
        You are a recipe transcription assistant. You will be given one or more photos of cookbook pages,
        recipe cards, or screenshots showing a single recipe. Transcribe it and respond with ONLY a JSON
        object — no prose, no code fences — with exactly these fields:
          "title": string (recipe name)
          "description": string or null (short blurb; null if the page has none)
          "category": string or null (meal type such as Dessert, Dinner, Breakfast, Side Dish; null if unclear)
          "servings": integer or null
          "prepTimeMinutes": integer or null
          "cookTimeMinutes": integer or null
          "ingredients": array of strings — one entry per ingredient line exactly as printed, e.g. "1 1/2 cups flour", "2 tbsp olive oil"
          "steps": array of strings — one entry per instruction step (drop the leading numbers)
        Keep fractions as written ("1 1/2"). Omit amounts the page leaves out. If a field is not visible in
        the photos use null (or [] for arrays). The JSON object is the entire response.
        """;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClients;
    private readonly VisionEndpointStatus _status;
    private readonly VisionImportWakeSignal _wake;
    private readonly ImportQueueStore _queue;
    private readonly ImageStore _images;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger<VisionImportWorker> _logger;

    /// <summary>Null = never probed; true/false = last probe outcome (used to log state changes once).</summary>
    private bool? _lastProbeSucceeded;

    public VisionImportWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClients,
        VisionEndpointStatus status,
        VisionImportWakeSignal wake,
        ImportQueueStore queue,
        ImageStore images,
        IConfiguration configuration,
        ILogger<VisionImportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClients = httpClients;
        _status = status;
        _wake = wake;
        _queue = queue;
        _images = images;
        _logger = logger;
        var seconds = configuration.GetValue<int?>("VisionImport:PollIntervalSeconds");
        _pollInterval = TimeSpan.FromSeconds(seconds is > 0 ? seconds.Value : 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Probe once at startup so the UI's status pill reflects reality before the first upload.
        await ProbeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await WaitForWakeOrTimeoutAsync(stoppingToken)) break;
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutdown — any job left "Processing" is requeued by the next startup
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo import pass failed; jobs remain queued");
            }
        }
    }

    /// <summary>Waits for a wake signal or the poll timeout; returns false only when shutting down.</summary>
    private async Task<bool> WaitForWakeOrTimeoutAsync(CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_pollInterval);
        try
        {
            await _wake.Channel.Reader.WaitToReadAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // poll tick elapsed; fall through
        }
        while (_wake.Channel.Reader.TryRead(out _)) { } // drain coalesced wakes so one pass covers them all
        return !ct.IsCancellationRequested;
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();

        var pendingIds = await db.RecipeImportJobs
            .Where(j => j.Status == "Pending")
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.Id)
            .ToListAsync(ct);
        if (pendingIds.Count == 0) return;

        // Opportunistic: only bother the endpoint when there is actually work queued.
        var modelId = await ProbeAsync(ct);
        if (modelId is null) return; // already logged; jobs stay Pending until the endpoint comes back

        foreach (var jobId in pendingIds)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessJobAsync(scope.ServiceProvider, db, jobId, modelId, ct);
        }
    }

    /// <summary>GET /v1/models (3s timeout). Returns the loaded model id (<c>data[0].id</c>) or null when unreachable.</summary>
    private async Task<string?> ProbeAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClients.CreateClient(ProbeClientName);
            using var response = await client.GetAsync($"{_status.EndpointUrl}/v1/models", ct);
            if (!response.IsSuccessStatusCode)
            {
                return ProbeFailed($"HTTP {(int)response.StatusCode}");
            }
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            string? modelId = null;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
                && data[0].TryGetProperty("id", out var idElement))
            {
                modelId = idElement.GetString();
            }
            _status.RecordOnline(modelId);
            if (_lastProbeSucceeded != true)
            {
                _logger.LogInformation("Vision endpoint online at {Url} (model: {Model})", _status.EndpointUrl, modelId ?? "unknown");
            }
            _lastProbeSucceeded = true;
            return modelId;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ProbeFailed("probe timed out after 3s");
        }
        catch (Exception ex)
        {
            return ProbeFailed(ex.Message);
        }
    }

    private string? ProbeFailed(string reason)
    {
        _status.RecordOffline();
        // Log once when the endpoint goes away, not every idle poll while it is down.
        if (_lastProbeSucceeded != false)
        {
            _logger.LogDebug("Vision endpoint at {Url} unreachable ({Reason}); queued photo imports stay Pending", _status.EndpointUrl, reason);
        }
        _lastProbeSucceeded = false;
        return null;
    }

    private async Task ProcessJobAsync(
        IServiceProvider scopedProvider, RecipeDbContext db, Guid jobId, string modelId, CancellationToken ct)
    {
        var job = await db.RecipeImportJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status != "Pending") return; // deleted or claimed meanwhile

        job.Status = "Processing";
        await db.SaveChangesAsync(ct);

        try
        {
            var fileNames = ImportQueueStore.ReadFileNames(job)
                .Select(Path.GetFileName) // never trust stored paths to escape the staging dir
                .Where(name => name is not null && File.Exists(Path.Combine(_queue.JobDirectory(job.Id), name!)))
                .Select(name => name!)
                .ToList();
            if (fileNames.Count == 0)
            {
                throw new InvalidOperationException("staged photos are missing from import_queue; delete and re-upload");
            }

            var extracted = await ExtractRecipeAsync(job.Id, fileNames, modelId, ct);
            var recipes = scopedProvider.GetRequiredService<IRecipeRepository>();
            var recipe = await IngestRecipeAsync(recipes, job, extracted, ct);
            PromotePhotos(recipe, job, fileNames);
            await recipes.UpdateAsync(recipe); // image rows added above

            job.Status = "Completed";
            job.RecipeId = recipe.Id;
            job.RecipeTitle = recipe.Title;
            job.ModelUsed = modelId;
            job.Error = null;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Photo import {JobId} created recipe {RecipeId} ({Title})", job.Id, recipe.Id, recipe.Title);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-inference: leave the job "Processing" — startup requeues it next boot.
            return;
        }
        catch (Exception ex)
        {
            // Staged files are deliberately kept so Retry can re-run the same photos.
            job.Status = "Failed";
            job.Error = Truncate(ex.Message, 1000);
            try
            {
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Could not persist failure state for photo import {JobId}", jobId);
            }
            _logger.LogWarning(ex, "Photo import {JobId} failed", jobId);
        }
    }

    /// <summary>POSTs the staged photos as base64 image parts and parses the model's JSON reply.</summary>
    private async Task<ExtractedRecipe> ExtractRecipeAsync(Guid jobId, List<string> fileNames, string modelId, CancellationToken ct)
    {
        var directory = _queue.JobDirectory(jobId);
        var contentParts = new List<object>
        {
            new
            {
                type = "text",
                text = $"Extract the single recipe shown in these {fileNames.Count} photo(s). Respond with ONLY the JSON object.",
            },
        };
        foreach (var fileName in fileNames)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, fileName), ct);
            if (bytes.Length is 0 || bytes.Length > ImageStore.MaxFileSizeBytes
                || ImageStore.DetectImageExtension(bytes) is not string mimeExt)
            {
                continue; // stale/corrupt staged file: skip it rather than poison the whole request
            }
            contentParts.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:{MimeFor(mimeExt)};base64,{Convert.ToBase64String(bytes)}" },
            });
        }
        if (contentParts.Count == 1)
        {
            throw new InvalidOperationException("none of the staged photos are valid image files");
        }

        var payload = JsonSerializer.Serialize(new
        {
            model = modelId,
            temperature = 0.1,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = contentParts },
            },
        });

        var client = _httpClients.CreateClient(InferenceClientName);
        using var response = await client.PostAsync(
            $"{_status.EndpointUrl}/v1/chat/completions",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"vision endpoint HTTP {(int)response.StatusCode}: {Truncate(body, 300)}");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content))
        {
            throw new InvalidOperationException("vision endpoint returned no completion choices");
        }
        return ParseExtraction(ContentText(content));
    }

    /// <summary>Content is normally a string; some OpenAI-compatible servers use an array of text parts.</summary>
    private static string ContentText(JsonElement content) => content.ValueKind switch
    {
        JsonValueKind.String => content.GetString() ?? "",
        JsonValueKind.Array => string.Concat(
            content.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String).Select(p => p.GetProperty("text").GetString())),
        _ => throw new InvalidOperationException($"unexpected completion content type ({content.ValueKind})"),
    };

    /// <summary>Parses the model's reply into an <see cref="ExtractedRecipe"/>, stripping markdown code fences.</summary>
    internal static ExtractedRecipe ParseExtraction(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..]; // drop the ```/```json opener line
            if (text.EndsWith("```", StringComparison.Ordinal)) text = text[..^3];
            text = text.Trim();
        }

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(text).RootElement;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"vision model reply was not valid JSON: {Truncate(ex.Message, 200)}");
        }

        var title = GetString(root, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("vision model did not extract a recipe title");
        }
        var ingredients = GetStringList(root, "ingredients", maxItems: 60);
        var steps = GetStringList(root, "steps", maxItems: 40);
        if (ingredients.Count == 0 && steps.Count == 0)
        {
            throw new InvalidOperationException("vision model extracted neither ingredients nor steps");
        }

        return new ExtractedRecipe(
            Title: title.Trim(),
            Description: Truncate(GetString(root, "description")?.Trim(), 2000),
            Category: Truncate(GetString(root, "category")?.Trim(), 100),
            Servings: GetInt(root, "servings"),
            PrepTimeMinutes: GetInt(root, "prepTimeMinutes"),
            CookTimeMinutes: GetInt(root, "cookTimeMinutes"),
            Ingredients: ingredients,
            Steps: steps);
    }

    /// <summary>Mirrors the URL-import ingest: SplitLine + FindOrCreateIngredientAsync dedupe/normalize/route.</summary>
    private static async Task<Recipe> IngestRecipeAsync(IRecipeRepository recipes, RecipeImportJob job, ExtractedRecipe extracted, CancellationToken ct)
    {
        var recipe = new Recipe
        {
            Title = Truncate(extracted.Title, 200)!,
            Description = extracted.Description,
            Servings = extracted.Servings,
            PrepTimeMinutes = extracted.PrepTimeMinutes,
            CookTimeMinutes = extracted.CookTimeMinutes,
        };

        // The category picked at upload time wins; the model's guess is added alongside when present.
        var categoryNames = new List<string?> { job.Category, extracted.Category };
        foreach (var categoryName in categoryNames)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) continue;
            var category = await recipes.FindOrCreateCategoryAsync(categoryName.Trim());
            if (!recipe.Categories.Any(c => string.Equals(c.Name, category.Name, StringComparison.OrdinalIgnoreCase)))
            {
                recipe.Categories.Add(category);
            }
        }

        foreach (var line in extracted.Ingredients)
        {
            var (quantity, unit, name, notes) = IngredientText.SplitLine(Truncate(line, 300)!);
            if (string.IsNullOrWhiteSpace(name)) continue;
            var ingredient = await recipes.FindOrCreateIngredientAsync(name, notes);
            if (recipe.RecipeIngredients.Any(ri => ri.IngredientId == ingredient.Id)) continue;
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Quantity = quantity,
                Unit = unit,
                IngredientId = ingredient.Id,
                Notes = notes,
            });
        }

        var stepNumber = 1;
        foreach (var stepText in extracted.Steps)
        {
            if (string.IsNullOrWhiteSpace(stepText)) continue;
            recipe.Steps.Add(new RecipeStep { StepNumber = stepNumber++, Text = Truncate(stepText.Trim(), 2000)! });
        }

        return await recipes.AddAsync(recipe);
    }

    /// <summary>Moves staged photos into data/images/{recipeId}/ as gallery images, then removes the staging dir.</summary>
    private void PromotePhotos(Recipe recipe, RecipeImportJob job, List<string> fileNames)
    {
        var sortOrder = 0;
        foreach (var fileName in fileNames)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(Path.Combine(_queue.JobDirectory(job.Id), fileName));
            }
            catch (IOException)
            {
                continue;
            }
            if (bytes.Length is 0 || bytes.Length > ImageStore.MaxFileSizeBytes || ImageStore.DetectImageExtension(bytes) is null)
            {
                _logger.LogWarning("Skipping staged photo {FileName} for job {JobId}: not a valid image", fileName, job.Id);
                continue;
            }
            recipe.Images.Add(new RecipeImage
            {
                RecipeId = recipe.Id,
                FileName = _images.SaveBytes(recipe.Id, bytes),
                SortOrder = ++sortOrder,
            });
        }
        // Photos are safely copied into the gallery (or skipped as corrupt) — staging is done.
        _queue.DeleteJobDirectory(job.Id);
    }

    private static string MimeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    private static string? GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : null
            : null;

    private static int? GetInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n)) return n;
        // Models sometimes stringify numbers ("servings": "4"); be forgiving.
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static List<string> GetStringList(JsonElement root, string property, int maxItems)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
            .Select(e => e.GetString()!.Trim())
            .Take(maxItems)
            .ToList();
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    internal sealed record ExtractedRecipe(
        string Title,
        string? Description,
        string? Category,
        int? Servings,
        int? PrepTimeMinutes,
        int? CookTimeMinutes,
        List<string> Ingredients,
        List<string> Steps);
}
