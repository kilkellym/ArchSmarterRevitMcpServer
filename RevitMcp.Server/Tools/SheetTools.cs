using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for creating and managing Revit sheets.
/// </summary>
[McpServerToolType]
public sealed class SheetTools
{
    /// <summary>
    /// Creates a new sheet in the project with a title block.
    /// </summary>
    [McpServerTool(Name = "create_sheet"), Description(
        "Create a new sheet in the Revit project. " +
        "Optionally specify a title block name, sheet number, and sheet name. " +
        "If no title block is specified, uses the first available one. " +
        "Returns the new sheet's Id, SheetNumber, Name, TitleBlock, and ViewType.")]
    public static async Task<string> CreateSheet(
        RevitBridgeClient bridgeClient,
        [Description("Optional sheet number (e.g. 'A101'). If omitted, Revit assigns a default number.")]
        string? sheetNumber = null,
        [Description("Optional sheet name/title (e.g. 'First Floor Plan').")]
        string? sheetName = null,
        [Description("Optional title block family name. If omitted, uses the first available title block.")]
        string? titleBlockName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { sheetNumber, sheetName, titleBlockName });

        var request = new BridgeRequest(
            Command: CommandNames.CreateSheet,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Places a view on a sheet by creating a viewport.
    /// </summary>
    [McpServerTool(Name = "add_view_to_sheet"), Description(
        "Place a view on a sheet by creating a viewport. " +
        "Requires the sheet ID and view ID. The view must not already be placed on another sheet. " +
        "Optional center coordinates control viewport placement on the sheet (in feet). " +
        "Returns the ViewportId, SheetId, SheetNumber, ViewId, and ViewName.")]
    public static async Task<string> AddViewToSheet(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID of the sheet to place the view on.")]
        long sheetId,
        [Description("The Revit element ID of the view to place on the sheet.")]
        long viewId,
        [Description("X coordinate for the viewport center on the sheet in feet. Defaults to 1.0 (roughly center).")]
        double centerX = 1.0,
        [Description("Y coordinate for the viewport center on the sheet in feet. Defaults to 0.75 (roughly center).")]
        double centerY = 0.75,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { sheetId, viewId, centerX, centerY });

        var request = new BridgeRequest(
            Command: CommandNames.AddViewToSheet,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets all views placed on a sheet.
    /// </summary>
    [McpServerTool(Name = "get_sheet_views"), Description(
        "Get all views (viewports) placed on a sheet. " +
        "Identify the sheet by either its element ID or sheet number. " +
        "Returns the sheet info and a list of views with their viewport IDs, view IDs, " +
        "names, types, scales, and center coordinates in decimal feet.")]
    public static async Task<string> GetSheetViews(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID of the sheet. Provide either sheetId or sheetNumber.")]
        long? sheetId = null,
        [Description("The sheet number (e.g. 'A101'). Provide either sheetId or sheetNumber.")]
        string? sheetNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { sheetId, sheetNumber });

        var request = new BridgeRequest(
            Command: CommandNames.GetSheetViews,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
