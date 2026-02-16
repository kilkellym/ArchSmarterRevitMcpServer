#nullable enable
using System.Diagnostics;
using RevitMcp.Addin.Status;

namespace RevitMcp.Addin.UI;

/// <summary>
/// External command that terminates the external MCP server process.
/// Claude Desktop will respawn a new instance when it next needs to call a tool.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class KillMcpServerCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        try
        {
            var processes = Process.GetProcessesByName("RevitMcp.Server");

            if (processes.Length == 0)
            {
                TaskDialog.Show("Kill MCP Server",
                    "No MCP server process is currently running.");
                return Result.Succeeded;
            }

            var killed = 0;
            var errors = new List<string>();

            foreach (var process in processes)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                    killed++;
                }
                catch (Exception ex)
                {
                    errors.Add($"PID {process.Id}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            McpStatusTracker.Instance.IsClientConnected = false;

            if (errors.Count > 0)
            {
                TaskDialog.Show("Kill MCP Server",
                    $"Terminated {killed} process(es), but {errors.Count} failed:\n" +
                    string.Join("\n", errors));
            }
            else
            {
                TaskDialog.Show("Kill MCP Server",
                    "MCP server process terminated. Claude Desktop will start a new instance automatically when needed.");
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Kill MCP Server",
                $"Could not terminate the MCP server: {ex.Message}");
            return Result.Failed;
        }
    }
}
