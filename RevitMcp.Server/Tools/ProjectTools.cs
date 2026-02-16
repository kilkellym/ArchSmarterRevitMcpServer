using System.ComponentModel;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for querying Revit project-level information.
/// </summary>
[McpServerToolType]
public sealed class ProjectTools
{
    /// <summary>
    /// Gets metadata about the active Revit project.
    /// </summary>
    [McpServerTool(Name = "get_project_info"), Description(
        "Get metadata about the active Revit project including project name, number, " +
        "client, address, building name, author, organization, and status. " +
        "Use this to orient yourself about the current model.")]
    public static async Task<string> GetProjectInfo(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.GetProjectInfo);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
