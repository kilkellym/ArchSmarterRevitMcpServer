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
    /// Gets all views placed on a Revit sheet with viewport bounds.
    /// </summary>
    [McpServerTool(Name = "get_views_on_sheet"), Description(
        "Get all views placed on a Revit sheet. Provide either a sheet number (e.g. 'E-101') " +
        "or a sheet element ID. Returns each viewport's view ID, view name, view type, view scale, " +
        "and the viewport's bounding box on the sheet in decimal feet. Use this to determine which " +
        "view a PDF markup targets based on its position on the sheet.")]
    public static async Task<string> GetViewsOnSheet(
        RevitBridgeClient bridgeClient,
        [Description("Sheet number (e.g. 'E-101', 'A-201'). Use this or sheetId.")]
        string? sheetNumber = null,
        [Description("Element ID of the sheet. Use this or sheetNumber.")]
        long? sheetId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { sheetNumber, sheetId });

        var request = new BridgeRequest(
            Command: CommandNames.GetViewsOnSheet,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets a complete mapping of all sheets to their views.
    /// </summary>
    [McpServerTool(Name = "get_sheet_view_mapping"), Description(
        "Get a complete mapping of all sheets in the project to the views placed on them. " +
        "For each sheet, returns the sheet number, sheet name, and all viewports with their view names, " +
        "view types, and bounding box positions on the sheet. Use this to build a lookup table that maps " +
        "PDF pages (by sheet number) to their Revit views. This is designed for batch export alongside " +
        "PDF generation to create the metadata sidecar file.")]
    public static async Task<string> GetSheetViewMapping(
        RevitBridgeClient bridgeClient,
        [Description("Optional array of sheet numbers to include. If omitted, returns all sheets.")]
        string[]? sheetsFilter = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { sheetsFilter });

        var request = new BridgeRequest(
            Command: CommandNames.GetSheetViewMapping,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
