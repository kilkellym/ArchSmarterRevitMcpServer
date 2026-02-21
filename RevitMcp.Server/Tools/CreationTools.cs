using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for creating model elements and annotations in Revit.
/// </summary>
[McpServerToolType]
public sealed class CreationTools
{
    /// <summary>
    /// Creates a straight wall between two points on a level.
    /// </summary>
    [McpServerTool(Name = "create_wall"), Description(
        "Create a straight wall between two points. " +
        "Requires start and end coordinates in decimal feet, a level name, " +
        "and optionally a wall type name and height. " +
        "Returns the new wall's Id, type name, level, length, and height.")]
    public static async Task<string> CreateWall(
        RevitBridgeClient bridgeClient,
        [Description("X coordinate of wall start point in decimal feet.")]
        double startX,
        [Description("Y coordinate of wall start point in decimal feet.")]
        double startY,
        [Description("X coordinate of wall end point in decimal feet.")]
        double endX,
        [Description("Y coordinate of wall end point in decimal feet.")]
        double endY,
        [Description("Name of the level the wall is placed on (e.g. 'Level 1').")]
        string levelName,
        [Description("Wall height in decimal feet. Defaults to 10 ft.")]
        double height = 10.0,
        [Description("Wall type name (e.g. 'Generic - 200mm'). If omitted, uses the first available wall type.")]
        string? wallTypeName = null,
        [Description("Whether this is a structural wall.")]
        bool isStructural = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            startX, startY, endX, endY, levelName, height, wallTypeName, isStructural
        });

        var request = new BridgeRequest(
            Command: CommandNames.CreateWall,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a text note annotation in a view.
    /// </summary>
    [McpServerTool(Name = "create_text_note"), Description(
        "Create a text note annotation in a Revit view. " +
        "Requires a view ID, position coordinates in decimal feet, and the text content. " +
        "Optionally specify a text note type. " +
        "Returns the new text note's Id, view name, text content, and type name.")]
    public static async Task<string> CreateTextNote(
        RevitBridgeClient bridgeClient,
        [Description("Element ID of the view to place the text note in.")]
        long viewId,
        [Description("X coordinate in decimal feet for the text note position.")]
        double x,
        [Description("Y coordinate in decimal feet for the text note position.")]
        double y,
        [Description("The text content for the note.")]
        string text,
        [Description("Name of the TextNoteType. If omitted, uses the first available.")]
        string? textNoteTypeName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { viewId, x, y, text, textNoteTypeName });

        var request = new BridgeRequest(
            Command: CommandNames.CreateTextNote,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a railing along a path of connected straight line segments.
    /// </summary>
    [McpServerTool(Name = "create_railing"), Description(
        "Create a railing along a path of connected straight line segments. " +
        "Provide arrays of X and Y coordinates in decimal feet for the path vertices (minimum 2 points). " +
        "Requires a level name. Optionally specify a railing type name. " +
        "Returns the new railing's Id, type name, level, and segment count.")]
    public static async Task<string> CreateRailing(
        RevitBridgeClient bridgeClient,
        [Description("Array of X coordinates in decimal feet defining railing path vertices. Must have at least 2 values. Paired with pointsY.")]
        double[] pointsX,
        [Description("Array of Y coordinates in decimal feet defining railing path vertices. Must have same length as pointsX.")]
        double[] pointsY,
        [Description("Name of the level for the railing (e.g. 'Level 1').")]
        string levelName,
        [Description("Railing type name. If omitted, uses the first available railing type.")]
        string? railingTypeName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { pointsX, pointsY, levelName, railingTypeName });

        var request = new BridgeRequest(
            Command: CommandNames.CreateRailing,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a dimension between two or more elements in a view.
    /// </summary>
    [McpServerTool(Name = "create_dimension"), Description(
        "Create a dimension between two or more elements in a view. " +
        "Requires a view ID and an array of at least 2 element IDs. " +
        "The elements must be visible in the view. References are automatically extracted from the elements' geometry. " +
        "Optionally specify a dimension type name. " +
        "Returns the dimension's Id, view name, measured value, and element count. " +
        "Works best with walls, columns, and other elements that have clear geometric faces.")]
    public static async Task<string> CreateDimension(
        RevitBridgeClient bridgeClient,
        [Description("Element ID of the view to place the dimension in.")]
        long viewId,
        [Description("Array of at least 2 element IDs to dimension between. Elements must be visible in the specified view.")]
        long[] elementIds,
        [Description("Dimension type name. If omitted, uses the default dimension type.")]
        string? dimensionTypeName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { viewId, elementIds, dimensionTypeName });

        var request = new BridgeRequest(
            Command: CommandNames.CreateDimension,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a floor from a closed boundary polygon.
    /// </summary>
    [McpServerTool(Name = "create_floor"), Description(
        "Create a floor from a closed boundary polygon. " +
        "Provide arrays of X and Y coordinates in decimal feet for the boundary vertices (minimum 3 points). " +
        "The boundary is automatically closed (last point connects to first). " +
        "Requires a level name. Optionally specify a floor type name. " +
        "Returns the new floor's Id, type name, level, and area.")]
    public static async Task<string> CreateFloor(
        RevitBridgeClient bridgeClient,
        [Description("Array of X coordinates in decimal feet defining floor boundary vertices. Must have at least 3 values. The boundary is automatically closed. Paired with pointsY.")]
        double[] pointsX,
        [Description("Array of Y coordinates in decimal feet defining floor boundary vertices. Must have same length as pointsX.")]
        double[] pointsY,
        [Description("Name of the level for the floor (e.g. 'Level 1').")]
        string levelName,
        [Description("Floor type name. If omitted, uses the first available floor type.")]
        string? floorTypeName = null,
        [Description("Whether this is a structural floor.")]
        bool isStructural = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            pointsX, pointsY, levelName, floorTypeName, isStructural
        });

        var request = new BridgeRequest(
            Command: CommandNames.CreateFloor,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a detail line annotation in a view.
    /// </summary>
    [McpServerTool(Name = "create_detail_line"), Description(
        "Create a detail line (annotation) in a Revit view between two points. " +
        "Requires a view ID and start/end coordinates in decimal feet. " +
        "Optionally specify a line style name. " +
        "Returns the new detail line's Id, view name, start/end coordinates, length, and line style.")]
    public static async Task<string> CreateDetailLine(
        RevitBridgeClient bridgeClient,
        [Description("Element ID of the view to draw the detail line in.")]
        long viewId,
        [Description("X coordinate of the start point in decimal feet.")]
        double startX,
        [Description("Y coordinate of the start point in decimal feet.")]
        double startY,
        [Description("X coordinate of the end point in decimal feet.")]
        double endX,
        [Description("Y coordinate of the end point in decimal feet.")]
        double endY,
        [Description("Optional line style name (e.g. 'Thin Lines', 'Medium Lines'). If omitted, uses the default style.")]
        string? lineStyleName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            viewId, startX, startY, endX, endY, lineStyleName
        });

        var request = new BridgeRequest(
            Command: CommandNames.CreateDetailLine,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
