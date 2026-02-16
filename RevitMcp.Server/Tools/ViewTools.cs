using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for creating and managing Revit views.
/// </summary>
[McpServerToolType]
public sealed class ViewTools
{
    /// <summary>
    /// Opens a view and makes it the active view in Revit.
    /// </summary>
    [McpServerTool(Name = "open_view"), Description(
        "Open a view in Revit and make it the active view. " +
        "Specify either viewId (integer) or viewName (string). " +
        "Returns the opened view's Id, Name, and ViewType.")]
    public static async Task<string> OpenView(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID of the view to open. Use this or viewName.")]
        long? viewId = null,
        [Description("The exact name of the view to open. Use this or viewId.")]
        string? viewName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { viewId, viewName });

        var request = new BridgeRequest(
            Command: CommandNames.OpenView,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a new floor plan or ceiling plan view for a specified level.
    /// </summary>
    [McpServerTool(Name = "create_plan_view"), Description(
        "Create a new floor plan or ceiling plan view for a specified level. " +
        "The level must already exist in the model. Returns the new view's Id, Name, LevelName, and ViewType.")]
    public static async Task<string> CreatePlanView(
        RevitBridgeClient bridgeClient,
        [Description("The name of the level to create the plan view for (e.g. 'Level 1').")]
        string levelName,
        [Description("Optional custom name for the new view. If omitted, Revit assigns a default name.")]
        string? viewName = null,
        [Description("Set to true to create a reflected ceiling plan instead of a floor plan.")]
        bool isCeilingPlan = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { levelName, viewName, isCeilingPlan });

        var request = new BridgeRequest(
            Command: CommandNames.CreatePlanView,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a new elevation view at a specified location and direction.
    /// </summary>
    [McpServerTool(Name = "create_elevation_view"), Description(
        "Create a new elevation view at a specified location. " +
        "Requires X/Y coordinates (in millimeters) and a level name. The level must have an existing plan view. " +
        "Direction can be 'north', 'south', 'east', or 'west'. " +
        "Returns the new view's Id, Name, MarkerId, and ViewType.")]
    public static async Task<string> CreateElevationView(
        RevitBridgeClient bridgeClient,
        [Description("X coordinate in millimeters for the elevation marker location.")]
        double x,
        [Description("Y coordinate in millimeters for the elevation marker location.")]
        double y,
        [Description("The name of the level where the elevation marker is placed (e.g. 'Level 1').")]
        string levelName,
        [Description("Direction the elevation faces: 'north', 'south', 'east', or 'west'. Defaults to 'north'.")]
        string direction = "north",
        [Description("Optional custom name for the new view.")]
        string? viewName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { x, y, levelName, direction, viewName });

        var request = new BridgeRequest(
            Command: CommandNames.CreateElevationView,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a new section view defined by an origin, direction, and dimensions.
    /// </summary>
    [McpServerTool(Name = "create_section_view"), Description(
        "Create a new section view defined by an origin point, view direction, and bounding box dimensions. " +
        "All coordinates and dimensions are in millimeters. " +
        "Returns the new view's Id, Name, and ViewType.")]
    public static async Task<string> CreateSectionView(
        RevitBridgeClient bridgeClient,
        [Description("X coordinate of the section origin in millimeters.")]
        double originX,
        [Description("Y coordinate of the section origin in millimeters.")]
        double originY,
        [Description("Z coordinate of the section origin in millimeters.")]
        double originZ,
        [Description("X component of the view direction vector (horizontal plane).")]
        double directionX,
        [Description("Y component of the view direction vector (horizontal plane).")]
        double directionY,
        [Description("Width of the section view in millimeters. Defaults to 10000.")]
        double width = 10000,
        [Description("Height of the section view in millimeters. Defaults to 10000.")]
        double height = 10000,
        [Description("Depth (far clip) of the section view in millimeters. Defaults to 10000.")]
        double depth = 10000,
        [Description("Optional custom name for the new view.")]
        string? viewName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            originX, originY, originZ, directionX, directionY, width, height, depth, viewName
        });

        var request = new BridgeRequest(
            Command: CommandNames.CreateSectionView,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Creates a new schedule view for a specified category.
    /// </summary>
    [McpServerTool(Name = "create_schedule_view"), Description(
        "Create a new schedule view for a specified category. " +
        "Optionally add specific fields (columns) to the schedule by name. " +
        "Category names should match Revit built-in categories (e.g. 'Walls', 'Doors', 'Rooms'). " +
        "Returns the new schedule's Id, Name, Category, AddedFields, and SkippedFields.")]
    public static async Task<string> CreateScheduleView(
        RevitBridgeClient bridgeClient,
        [Description("The category name for the schedule (e.g. 'Walls', 'Doors', 'Rooms', 'Windows').")]
        string category,
        [Description("Optional custom name for the new schedule.")]
        string? viewName = null,
        [Description("Optional list of field/column names to add to the schedule (e.g. ['Name', 'Area', 'Level']).")]
        string[]? fields = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { category, viewName, fields });

        var request = new BridgeRequest(
            Command: CommandNames.CreateScheduleView,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
