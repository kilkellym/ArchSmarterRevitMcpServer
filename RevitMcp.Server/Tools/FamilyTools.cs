using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for placing family instances and groups in the Revit model.
/// </summary>
[McpServerToolType]
public sealed class FamilyTools
{
    /// <summary>
    /// Inserts a component family instance at a point on a level.
    /// </summary>
    [McpServerTool(Name = "insert_family_instance_by_point"), Description(
        "Insert a component family instance (e.g. furniture, fixtures, doors, windows) at a point. " +
        "Requires the family name, X/Y coordinates in millimeters, and a level name. " +
        "Optionally specify a type name within the family. " +
        "Use get_elements with a category filter to discover available family names first. " +
        "Returns the new instance's Id, family name, type name, and level.")]
    public static async Task<string> InsertFamilyInstanceByPoint(
        RevitBridgeClient bridgeClient,
        [Description("The family name (e.g. 'Single-Flush', 'M_Single-Flush').")]
        string familyName,
        [Description("X coordinate in millimeters for the placement point.")]
        double x,
        [Description("Y coordinate in millimeters for the placement point.")]
        double y,
        [Description("Name of the level to place the instance on (e.g. 'Level 1').")]
        string levelName,
        [Description("The specific type/size within the family. If omitted, uses the first available type.")]
        string? typeName = null,
        [Description("Z coordinate in millimeters (elevation above level). Defaults to 0.")]
        double z = 0,
        [Description("Whether to place as structural (e.g. structural column vs architectural column).")]
        bool structural = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { familyName, x, y, levelName, typeName, z, structural });

        var request = new BridgeRequest(
            Command: CommandNames.InsertFamilyInstanceByPoint,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Inserts a group instance at a point.
    /// </summary>
    [McpServerTool(Name = "insert_group"), Description(
        "Insert a group instance at a point. " +
        "Requires the group type name and X/Y coordinates in millimeters. " +
        "Returns the new group's Id, group type name, and location.")]
    public static async Task<string> InsertGroup(
        RevitBridgeClient bridgeClient,
        [Description("Name of the group type to insert.")]
        string groupName,
        [Description("X coordinate in millimeters for the group placement point.")]
        double x,
        [Description("Y coordinate in millimeters for the group placement point.")]
        double y,
        [Description("Z coordinate in millimeters. Defaults to 0.")]
        double z = 0,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { groupName, x, y, z });

        var request = new BridgeRequest(
            Command: CommandNames.InsertGroup,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
