#nullable enable
using RevitMcp.Addin.Bridge;
using RevitMcp.Core.Handlers;

namespace RevitMcp.Addin;

/// <summary>
/// Revit external application entry point.
/// Wires up the MCP bridge pipeline on startup and tears it down on shutdown.
/// </summary>
internal class App : IExternalApplication
{
    private PipeServer? _pipeServer;

    public Result OnStartup(UIControlledApplication app)
    {
        // 1. Build the handler registry with all known command handlers.
        var registry = new HandlerRegistry(new ICommandHandler[]
        {
            new GetElementsHandler()
        });

        // 2. Create the channel that bridges the pipe thread → Revit main thread.
        var channel = new RequestChannel();

        // 3. Create the external-event handler that drains the channel on Revit's main thread.
        var executor = new ExternalEventExecutor(channel, registry);
        var externalEvent = ExternalEvent.Create(executor);

        // 4. Start the named-pipe server on a background thread.
        _pipeServer = new PipeServer(channel, externalEvent);
        _pipeServer.Start();

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a)
    {
        _pipeServer?.Dispose();
        _pipeServer = null;

        return Result.Succeeded;
    }
}
