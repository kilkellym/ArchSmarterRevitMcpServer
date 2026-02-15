#nullable enable
using System.Threading.Channels;
using RevitMcp.Core.Messages;

namespace RevitMcp.Addin.Bridge;

/// <summary>
/// Pairs a <see cref="BridgeRequest"/> with a <see cref="TaskCompletionSource{BridgeResponse}"/>
/// so the pipe-server thread can await the result after the handler runs on Revit's main thread.
/// </summary>
internal sealed record PendingRequest(
    BridgeRequest Request,
    TaskCompletionSource<BridgeResponse> Completion);

/// <summary>
/// Unbounded channel that transfers <see cref="PendingRequest"/> items from the
/// pipe-server background thread to the <see cref="ExternalEventExecutor"/> running
/// on Revit's main thread.
/// </summary>
internal sealed class RequestChannel
{
    private readonly Channel<PendingRequest> _channel =
        Channel.CreateUnbounded<PendingRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>Writer used by <see cref="PipeServer"/> to enqueue requests.</summary>
    public ChannelWriter<PendingRequest> Writer => _channel.Writer;

    /// <summary>Reader used by <see cref="ExternalEventExecutor"/> to drain requests.</summary>
    public ChannelReader<PendingRequest> Reader => _channel.Reader;
}
