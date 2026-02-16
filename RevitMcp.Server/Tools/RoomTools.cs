using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for querying Revit room and spatial data.
/// </summary>
[McpServerToolType]
public sealed class RoomTools
{
    /// <summary>
    /// Extracts all rooms from the active Revit model with detailed spatial and property data.
    /// </summary>
    [McpServerTool(Name = "export_room_data"), Description(
        "Extract all rooms from the active Revit model with detailed spatial and property data. " +
        "Returns room name, number, level, area (in square feet and square meters), " +
        "volume (in cubic feet and cubic meters), perimeter (in feet and meters), department, " +
        "upper and lower offsets, bounding elements, and any custom parameters. " +
        "Use this for programming verification, space analysis, or code compliance checks.")]
    public static async Task<string> ExportRoomData(
        RevitBridgeClient bridgeClient,
        [Description("Filter rooms to a specific level name. Omit to return rooms on all levels.")]
        string? level = null,
        [Description("Filter rooms by department. Omit to return all departments.")]
        string? department = null,
        [Description("Maximum number of rooms to return. Defaults to 500.")]
        int maxResults = 500,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { level, department, maxResults });

        var request = new BridgeRequest(
            Command: CommandNames.ExportRoomData,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
