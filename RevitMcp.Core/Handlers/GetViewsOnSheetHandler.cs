using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetViewsOnSheet"/> command.
/// Returns all views placed on a sheet along with their viewport bounds.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>sheetNumber</c> (string, optional) – Sheet number (e.g. "E-101"). Use this or sheetId.</item>
///   <item><c>sheetId</c> (long, optional) – Element ID of the sheet. Use this or sheetNumber.</item>
/// </list>
/// </remarks>
public sealed class GetViewsOnSheetHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetViewsOnSheet;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            JsonElement sheetNumberProp = default;
            JsonElement sheetIdProp = default;
            var hasSheetNumber = request.Payload?.TryGetProperty("sheetNumber", out sheetNumberProp) == true
                && sheetNumberProp.ValueKind == JsonValueKind.String;
            var hasSheetId = request.Payload?.TryGetProperty("sheetId", out sheetIdProp) == true
                && sheetIdProp.ValueKind == JsonValueKind.Number;

            if (!hasSheetNumber && !hasSheetId)
                return new BridgeResponse(Success: false, Error: "Provide either sheetNumber or sheetId.");

            ViewSheet? sheet = null;

            if (hasSheetId)
            {
                var elementId = new ElementId(sheetIdProp.GetInt64());
                sheet = doc.GetElement(elementId) as ViewSheet;

                if (sheet is null)
                    return new BridgeResponse(Success: false,
                        Error: $"Sheet not found for ID: {sheetIdProp.GetInt64()}");
            }
            else if (hasSheetNumber)
            {
                var number = sheetNumberProp.GetString();
                if (string.IsNullOrEmpty(number))
                    return new BridgeResponse(Success: false, Error: "sheetNumber cannot be empty.");

                using var collector = new FilteredElementCollector(doc);
                sheet = collector
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .FirstOrDefault(s => string.Equals(s.SheetNumber, number, StringComparison.OrdinalIgnoreCase));

                if (sheet is null)
                    return new BridgeResponse(Success: false, Error: $"Sheet not found: '{number}'");
            }

            var viewportIds = sheet!.GetAllViewports();
            var viewports = new List<object>();

            foreach (var vpId in viewportIds)
            {
                var viewport = doc.GetElement(vpId) as Viewport;
                if (viewport is null) continue;

                var view = doc.GetElement(viewport.ViewId) as View;
                if (view is null) continue;

                var center = viewport.GetBoxCenter();
                var outline = viewport.GetBoxOutline();

                viewports.Add(new
                {
                    ViewportId = viewport.Id.Value,
                    ViewId = view.Id.Value,
                    ViewName = view.Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    Center = new { center.X, center.Y },
                    BoundsMin = new { X = outline.MinimumPoint.X, Y = outline.MinimumPoint.Y },
                    BoundsMax = new { X = outline.MaximumPoint.X, Y = outline.MaximumPoint.Y }
                });
            }

            var result = new
            {
                SheetId = sheet.Id.Value,
                SheetNumber = sheet.SheetNumber,
                SheetName = sheet.Name,
                Viewports = viewports
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
