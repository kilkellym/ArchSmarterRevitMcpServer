using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetSheetViewMapping"/> command.
/// Returns a complete mapping of all sheets to their views with viewport bounds.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>sheetsFilter</c> (string[], optional) – Array of sheet numbers to include. If omitted, returns all sheets.</item>
/// </list>
/// </remarks>
public sealed class GetSheetViewMappingHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetSheetViewMapping;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // Parse optional sheets filter
            HashSet<string>? sheetsFilter = null;
            if (request.Payload?.TryGetProperty("sheetsFilter", out var filterProp) == true
                && filterProp.ValueKind == JsonValueKind.Array)
            {
                sheetsFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in filterProp.EnumerateArray())
                {
                    var val = item.GetString();
                    if (!string.IsNullOrEmpty(val))
                        sheetsFilter.Add(val);
                }
            }

            using var collector = new FilteredElementCollector(doc);
            var allSheets = collector
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            if (sheetsFilter is not null)
            {
                allSheets = allSheets
                    .Where(s => sheetsFilter.Contains(s.SheetNumber))
                    .ToList();
            }

            var sheets = new List<object>();

            foreach (var sheet in allSheets)
            {
                var viewportIds = sheet.GetAllViewports();
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

                sheets.Add(new
                {
                    SheetId = sheet.Id.Value,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    Viewports = viewports
                });
            }

            var result = new
            {
                SheetCount = sheets.Count,
                Sheets = sheets
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
