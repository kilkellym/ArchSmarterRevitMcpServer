using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Handlers;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for interacting with the Launchpad scripting environment in Revit.
/// </summary>
[McpServerToolType]
public sealed class LaunchpadTools
{
    /// <summary>
    /// Sends a C# script to the Launchpad scripting environment inside Revit.
    /// </summary>
    [McpServerTool(Name = "push_script"), Description(
        "Send a C# script to the Launchpad scripting environment inside Revit. " +
        "The script is written to a watched folder and Launchpad opens it automatically " +
        "in its editor for the user to review and run. " +
        "Use this when no existing MCP tool can accomplish the user's request and custom " +
        "Revit API code is needed. Prefer built-in MCP tools when they can handle the task. " +
        "The script should use the Revit API and assume access to 'doc' (Document) and " +
        "'uiDoc' (UIDocument) variables. Include all necessary using statements. " +
        "Use Console.WriteLine() to output results.")]
    public static string PushScript(
        [Description("The C# script code to send to Launchpad.")]
        string code,
        [Description("Brief description of what the script does. Written as a comment at the top of the file.")]
        string description,
        [Description("Name for the script file without extension. If not provided, a timestamp-based name is generated (e.g. mcp-script-20260221-143052).")]
        string? fileName = null)
    {
        try
        {
            var result = ScriptPushHandler.Execute(code, description, fileName);

            var response = new
            {
                success = true,
                filePath = result.FilePath,
                fileName = result.FileName
            };

            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
