using System.Threading.Channels;

namespace RecipeApp.Api.Services;

/// <summary>
/// One-slot wake channel so a fresh photo upload starts the <see cref="VisionImportWorker"/> immediately
/// instead of waiting for the next poll tick. Bounded at 1 with DropWrite: repeated wakes while the worker
/// is busy coalesce into one extra pass, which is exactly what we want (no unbounded buffering).
/// </summary>
public sealed class VisionImportWakeSignal
{
    private readonly System.Threading.Channels.Channel<byte> _channel = System.Threading.Channels.Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <summary>Non-blocking: signals the worker that new pending work may exist.</summary>
    public void Wake() => _channel.Writer.TryWrite(0);

    internal Channel<byte> Channel => _channel;
}
