#nullable enable
using RevitMcp.Core.Handlers;
using RevitMcp.Core.Messages;

namespace RevitMcp.Addin.Bridge;

/// <summary>
/// <see cref="IExternalEventHandler"/> that runs on Revit's main thread.
/// When raised, it drains all pending requests from the <see cref="RequestChannel"/>,
/// dispatches each one to the matching <see cref="ICommandHandler"/> via the
/// <see cref="HandlerRegistry"/>, and completes the awaiting
/// <see cref="TaskCompletionSource{BridgeResponse}"/> so the pipe server can
/// write the response back to the MCP server process.
/// </summary>
internal sealed class ExternalEventExecutor : IExternalEventHandler
{
    private readonly RequestChannel _channel;
    private readonly HandlerRegistry _registry;

    /// <summary>
    /// Creates a new executor.
    /// </summary>
    /// <param name="channel">The channel to read pending requests from.</param>
    /// <param name="registry">The handler registry for command dispatch.</param>
    public ExternalEventExecutor(RequestChannel channel, HandlerRegistry registry)
    {
        _channel = channel;
        _registry = registry;
    }

    /// <summary>
    /// Called by Revit on the main thread when the associated <see cref="ExternalEvent"/>
    /// is raised. Drains every queued request synchronously.
    /// </summary>
    public void Execute(UIApplication app)
    {
        while (_channel.Reader.TryRead(out var pending))
        {
            try
            {
                if (_registry.TryGetHandler(pending.Request.Command, out var handler) && handler is not null)
                {
                    var response = handler.Handle(pending.Request);
                    pending.Completion.TrySetResult(response);
                }
                else
                {
                    pending.Completion.TrySetResult(
                        new BridgeResponse(
                            Success: false,
                            Error: $"Unknown command: {pending.Request.Command}"));
                }
            }
            catch (Exception ex)
            {
                pending.Completion.TrySetResult(
                    new BridgeResponse(Success: false, Error: ex.Message));
            }
        }
    }

    /// <inheritdoc />
    public string GetName() => "RevitMcpExecutor";
}
