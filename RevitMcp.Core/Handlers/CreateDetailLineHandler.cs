using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateDetailLine"/> command.
/// Creates a detail line (annotation) in a specified view between two points.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>viewId</c> (long, required) – The view to draw the detail line in.</item>
///   <item><c>startX</c> (double, required) – Start X coordinate in decimal feet.</item>
///   <item><c>startY</c> (double, required) – Start Y coordinate in decimal feet.</item>
///   <item><c>endX</c> (double, required) – End X coordinate in decimal feet.</item>
///   <item><c>endY</c> (double, required) – End Y coordinate in decimal feet.</item>
///   <item><c>lineStyleName</c> (string, optional) – Name of the line style to use.</item>
/// </list>
/// </remarks>
public sealed class CreateDetailLineHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateDetailLine;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("viewId", out var viewIdProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: viewId");
            if (request.Payload?.TryGetProperty("startX", out var sxProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: startX");
            if (request.Payload?.TryGetProperty("startY", out var syProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: startY");
            if (request.Payload?.TryGetProperty("endX", out var exProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: endX");
            if (request.Payload?.TryGetProperty("endY", out var eyProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: endY");

            var lineStyleName = request.Payload?.TryGetProperty("lineStyleName", out var lsProp) == true
                ? lsProp.GetString() : null;

            var viewId = new ElementId(viewIdProp.GetInt64());
            var view = doc.GetElement(viewId) as View;
            if (view is null)
                return new BridgeResponse(Success: false, Error: $"View not found: {viewIdProp.GetInt64()}");

            if (!SupportsDetailLines(view))
                return new BridgeResponse(Success: false,
                    Error: $"Detail lines are not allowed in view '{view.Name}' (ViewType: {view.ViewType}).");

            var startPt = new XYZ(sxProp.GetDouble(), syProp.GetDouble(), 0);
            var endPt = new XYZ(exProp.GetDouble(), eyProp.GetDouble(), 0);

            if (startPt.DistanceTo(endPt) < 0.001)
                return new BridgeResponse(Success: false, Error: "Start and end points are too close together (< 0.001 ft).");

            var line = Line.CreateBound(startPt, endPt);

            using var transaction = new Transaction(doc, "MCP: Create Detail Line");
            transaction.Start();

            try
            {
                var detailCurve = doc.Create.NewDetailCurve(view, line);

                // Apply line style if requested
                if (!string.IsNullOrEmpty(lineStyleName))
                {
                    var lineStyle = FindLineStyle(doc, lineStyleName);
                    if (lineStyle is not null)
                        detailCurve.LineStyle = lineStyle;
                }

                transaction.Commit();

                var result = new
                {
                    Id = detailCurve.Id.Value,
                    ViewId = view.Id.Value,
                    ViewName = view.Name,
                    Start = new { X = startPt.X, Y = startPt.Y, Z = startPt.Z },
                    End = new { X = endPt.X, Y = endPt.Y, Z = endPt.Z },
                    LengthFt = Math.Round(line.Length, 4),
                    LineStyle = detailCurve.LineStyle?.Name
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create detail line: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the given view supports detail lines.
    /// </summary>
    private static bool SupportsDetailLines(View view) => view.ViewType switch
    {
        ViewType.FloorPlan      => true,
        ViewType.CeilingPlan    => true,
        ViewType.EngineeringPlan => true,
        ViewType.AreaPlan       => true,
        ViewType.Section        => true,
        ViewType.Elevation      => true,
        ViewType.Detail         => true,
        ViewType.DraftingView   => true,
        _                       => false
    };

    /// <summary>
    /// Searches the document's line style categories for a match by name.
    /// </summary>
    private static GraphicsStyle? FindLineStyle(Document doc, string styleName)
    {
        var lineCategories = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        if (lineCategories?.SubCategories is null)
            return null;

        foreach (Category subCat in lineCategories.SubCategories)
        {
            if (string.Equals(subCat.Name, styleName, StringComparison.OrdinalIgnoreCase))
                return subCat.GetGraphicsStyle(GraphicsStyleType.Projection);
        }

        return null;
    }
}
