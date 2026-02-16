using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.AddViewToSheet"/> command.
/// Places a view on a sheet by creating a viewport.
/// </summary>
public sealed class AddViewToSheetHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.AddViewToSheet;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("sheetId", out var sheetIdProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: sheetId");
            if (request.Payload?.TryGetProperty("viewId", out var viewIdProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: viewId");

            var sheetId = new ElementId((long)sheetIdProp.GetInt64());
            var viewId = new ElementId((long)viewIdProp.GetInt64());

            // Parse optional center point (in feet on the sheet)
            var centerX = request.Payload?.TryGetProperty("centerX", out var cxProp) == true
                ? cxProp.GetDouble() : 1.0;
            var centerY = request.Payload?.TryGetProperty("centerY", out var cyProp) == true
                ? cyProp.GetDouble() : 0.75;
            var centerPoint = new XYZ(centerX, centerY, 0);

            // Validate the sheet
            var sheet = doc.GetElement(sheetId) as ViewSheet;
            if (sheet is null)
                return new BridgeResponse(Success: false, Error: $"Sheet not found for ID: {sheetIdProp.GetInt64()}");

            // Validate the view
            var view = doc.GetElement(viewId) as View;
            if (view is null)
                return new BridgeResponse(Success: false, Error: $"View not found for ID: {viewIdProp.GetInt64()}");

            // Check if the view can be added to the sheet
            if (!Viewport.CanAddViewToSheet(doc, sheetId, viewId))
                return new BridgeResponse(Success: false,
                    Error: $"View '{view.Name}' cannot be placed on sheet '{sheet.SheetNumber}'. " +
                           "It may already be on another sheet or be a non-placeable view type.");

            using var transaction = new Transaction(doc, "MCP: Add View to Sheet");
            transaction.Start();

            try
            {
                var viewport = Viewport.Create(doc, sheetId, viewId, centerPoint);

                transaction.Commit();

                var result = new
                {
                    ViewportId = viewport.Id.Value,
                    SheetId = sheet.Id.Value,
                    SheetNumber = sheet.SheetNumber,
                    ViewId = view.Id.Value,
                    ViewName = view.Name
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to add view to sheet: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
