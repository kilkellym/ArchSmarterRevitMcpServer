#nullable enable
using RevitMcp.Addin.Status;

namespace RevitMcp.Addin.UI;

/// <summary>
/// External command that restarts the MCP named-pipe server.
/// Stops the current pipe listener, clears pending requests, resets status tracking,
/// and starts a fresh pipe server.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class RestartConnectionCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        try
        {
            var pipeServer = App.PipeServer;
            var channel = App.Channel;

            if (pipeServer is null || channel is null)
            {
                TaskDialog.Show("Restart Connection",
                    "The MCP pipe server has not been initialized.");
                return Result.Failed;
            }

            // 1. Stop the current pipe server
            pipeServer.Stop();

            // 2. Clear any pending requests from the channel
            channel.Clear();

            // 3. Reset status tracker counters and error state
            McpStatusTracker.Instance.Reset();

            // 4. Start a fresh pipe server
            pipeServer.Start();

            TaskDialog.Show("Restart Connection",
                "Connection restarted. The pipe server is listening for new connections.");

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Restart Connection",
                $"Failed to restart connection: {ex.Message}");
            return Result.Failed;
        }
    }
}
