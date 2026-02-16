#nullable enable
using RevitMcp.Addin.Status;

namespace RevitMcp.Addin.UI;

/// <summary>
/// External command invoked by the MCP Status ribbon button.
/// Reads from <see cref="McpStatusTracker"/> and displays a TaskDialog
/// with connection status, uptime, session statistics, and last error.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class McpStatusCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        var tracker = McpStatusTracker.Instance;

        var status = tracker.OverallStatus switch
        {
            ConnectionStatus.Connected => "MCP Server Running",
            ConnectionStatus.Waiting => "MCP Server Waiting for Client",
            ConnectionStatus.Error => "MCP Server Error",
            _ => "Unknown"
        };

        var uptime = tracker.ServerStartTime.HasValue
            ? FormatUptime(DateTime.Now - tracker.ServerStartTime.Value)
            : "Not started";

        var lastCall = tracker.LastToolCallTime.HasValue
            ? $"{tracker.LastToolCallName} ({FormatTimeAgo(tracker.LastToolCallTime.Value)})"
            : "None";

        var lastError = tracker.LastError is not null
            ? $"{tracker.LastError} ({FormatTimeAgo(tracker.LastErrorTime!.Value)})"
            : "None";

        var body = $"Pipe Server: {tracker.PipeState}\n"
                 + $"Client Connected: {(tracker.IsClientConnected ? "Yes" : "No")}\n"
                 + $"Server Uptime: {uptime}\n"
                 + $"\n"
                 + $"Session Statistics:\n"
                 + $"  Tool calls processed: {tracker.ToolCallsThisSession}\n"
                 + $"  Total tool calls (all sessions): {tracker.TotalToolCallsProcessed}\n"
                 + $"  Last tool call: {lastCall}\n"
                 + $"\n"
                 + $"Last Error: {lastError}";

        var dialog = new TaskDialog("Revit MCP Status")
        {
            MainInstruction = status,
            MainContent = body,
            CommonButtons = TaskDialogCommonButtons.Ok
        };

        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Reset Counters",
            "Clear session statistics and last error.");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Copy to Clipboard",
            "Copy the full status text for troubleshooting.");

        var result = dialog.Show();

        if (result == TaskDialogResult.CommandLink1)
        {
            tracker.Reset();
            TaskDialog.Show("Revit MCP Status", "Session counters have been reset.");
        }
        else if (result == TaskDialogResult.CommandLink2)
        {
            var clipboardText = $"{status}\n\n{body}";
            System.Windows.Clipboard.SetText(clipboardText);
            TaskDialog.Show("Revit MCP Status", "Status copied to clipboard.");
        }

        return Result.Succeeded;
    }

    private static string FormatUptime(TimeSpan span)
    {
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m (since {(DateTime.Now - span):h:mm tt})";

        return $"{(int)span.TotalMinutes}m (since {(DateTime.Now - span):h:mm tt})";
    }

    private static string FormatTimeAgo(DateTime time)
    {
        var ago = DateTime.Now - time;
        if (ago.TotalSeconds < 60) return "just now";
        if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes} minutes ago";
        return $"{(int)ago.TotalHours}h {ago.Minutes}m ago";
    }
}
