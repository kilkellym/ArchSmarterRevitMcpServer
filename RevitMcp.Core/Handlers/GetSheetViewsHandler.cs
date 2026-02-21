using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetSheetViews"/> command.
/// Returns all views (viewports) placed on a sheet, identified by sheet number or ID.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>sheetId</c> (long, optional) – The sheet element ID. Either sheetId or sheetNumber is required.</item>
///   <item><c>sheetNumber</c> (string, optional) – The sheet number. Either sheetId or sheetNumber is required.</item>
/// </list>
/// </remarks>
public sealed class GetSheetViewsHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetSheetViews;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // Resolve the sheet - either by ID or by sheet number
            ViewSheet? sheet = null;

            if (request.Payload?.TryGetProperty("sheetId", out var sheetIdProp) == true)
            {
                var sheetId = new ElementId(sheetIdProp.GetInt64());
                sheet = doc.GetElement(sheetId) as ViewSheet;
                if (sheet is null)
                    return new BridgeResponse(Success: false, Error: $"Sheet not found with ID: {sheetIdProp.GetInt64()}");
            }
            else if (request.Payload?.TryGetProperty("sheetNumber", out var sheetNumProp) == true)
            {
                var sheetNumber = sheetNumProp.GetString();
                if (string.IsNullOrEmpty(sheetNumber))
                    return new BridgeResponse(Success: false, Error: "sheetNumber cannot be empty.");

                using var collector = new FilteredElementCollector(doc);
                sheet = collector.OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .FirstOrDefault(s => string.Equals(s.SheetNumber, sheetNumber, StringComparison.OrdinalIgnoreCase));

                if (sheet is null)
                    return new BridgeResponse(Success: false, Error: $"Sheet not found with number: '{sheetNumber}'");
            }
            else
            {
                return new BridgeResponse(Success: false, Error: "Either sheetId or sheetNumber is required.");
            }

            // Get all viewports on the sheet
            var viewportIds = sheet.GetAllViewports();
            var views = new List<object>();

            foreach (var vpId in viewportIds)
            {
                var viewport = doc.GetElement(vpId) as Viewport;
                if (viewport is null)
                    continue;

                var viewId = viewport.ViewId;
                var view = doc.GetElement(viewId) as View;
                if (view is null)
                    continue;

                var center = viewport.GetBoxCenter();

                views.Add(new
                {
                    ViewportId = viewport.Id.Value,
                    ViewId = view.Id.Value,
                    ViewName = view.Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    Center = new { X = Math.Round(center.X, 4), Y = Math.Round(center.Y, 4) }
                });
            }

            var result = new
            {
                SheetId = sheet.Id.Value,
                SheetNumber = sheet.SheetNumber,
                SheetName = sheet.Name,
                ViewCount = views.Count,
                Views = views
            };

            var data = JsonSerializer.SerializeToElement(result);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
