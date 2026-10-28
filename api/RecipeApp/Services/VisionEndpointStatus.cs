namespace RecipeApp.Api.Services;

/// <summary>
/// Singleton cache of the last known OpenAI-compatible vision endpoint status (Ticket 15).
/// Written by <see cref="VisionImportWorker"/> on every probe, read by the photo-import API so the
/// UI can render a live "GPU Online/Offline" pill without probing itself. Assignments are single-field
/// references/values; readers tolerate a torn snapshot (status is advisory).
/// </summary>
public sealed class VisionEndpointStatus(string endpointUrl)
{
    /// <summary>Base URL of the vision endpoint, without trailing slash.</summary>
    public string EndpointUrl { get; } = endpointUrl.TrimEnd('/');

    public bool Online { get; private set; }

    /// <summary>Model id reported by the endpoint (<c>data[0].id</c> of GET /v1/models), if known.</summary>
    public string? ActiveModel { get; private set; }

    public DateTime? LastCheckedAt { get; private set; }

    public void RecordOnline(string? modelId)
    {
        Online = true;
        ActiveModel = modelId;
        LastCheckedAt = DateTime.UtcNow;
    }

    public void RecordOffline()
    {
        Online = false;
        ActiveModel = null;
        LastCheckedAt = DateTime.UtcNow;
    }
}
