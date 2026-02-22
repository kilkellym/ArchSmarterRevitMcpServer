using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetElementsInView"/> command.
/// Returns elements visible in a specific view using a view-scoped FilteredElementCollector.
/// Retrieves elements visible in a specific view, optionally filtered by category
/// and a rectangular bounding region.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>viewId</c> (long, required) – The view to query elements from.</item>
///   <item><c>category</c> (string, optional) – Built-in category name to filter by.</item>
///   <item><c>limit</c> (int, optional) – Maximum results. Defaults to 200.</item>
///   <item><c>viewId</c> (long, required) – Element ID of the view to query.</item>
///   <item><c>category</c> (string, optional) – Built-in category name to filter by.</item>
///   <item><c>minX</c> (double, optional) – Minimum X coordinate for the search region.</item>
///   <item><c>minY</c> (double, optional) – Minimum Y coordinate for the search region.</item>
///   <item><c>maxX</c> (double, optional) – Maximum X coordinate for the search region.</item>
///   <item><c>maxY</c> (double, optional) – Maximum Y coordinate for the search region.</item>
///   <item><c>limit</c> (int, optional) – Maximum number of elements to return. Defaults to 100.</item>
/// </list>
/// </remarks>
public sealed class GetElementsInViewHandler : ICommandHandler
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 2000;

    /// <inheritdoc />
    public string Command => CommandNames.GetElementsInView;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("viewId", out var viewIdProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: viewId");

            var viewId = new ElementId(viewIdProp.GetInt64());
            var view = doc.GetElement(viewId) as View;

            if (view is null)
                return new BridgeResponse(Success: false,
                    Error: $"View not found for ID: {viewIdProp.GetInt64()}");

            if (view.IsTemplate)
                return new BridgeResponse(Success: false,
                    Error: $"Cannot query elements in a view template: '{view.Name}'");

            var category = request.Payload?.TryGetProperty("category", out var catProp) == true
                ? catProp.GetString()
                : null;

            var limit = request.Payload?.TryGetProperty("limit", out var limitProp) == true
                ? limitProp.GetInt32()
                : 100;

            // Parse optional bounding region
            var hasMinX = request.Payload?.TryGetProperty("minX", out var minXProp) == true;
            var hasMinY = request.Payload?.TryGetProperty("minY", out var minYProp) == true;
            var hasMaxX = request.Payload?.TryGetProperty("maxX", out var maxXProp) == true;
            var hasMaxY = request.Payload?.TryGetProperty("maxY", out var maxYProp) == true;
            var hasBounds = hasMinX && hasMinY && hasMaxX && hasMaxY;

            double minX = 0, minY = 0, maxX = 0, maxY = 0;
            if (hasBounds)
            {
                minX = minXProp.GetDouble();
                minY = minYProp.GetDouble();
                maxX = maxXProp.GetDouble();
                maxY = maxYProp.GetDouble();
            }

            using var collector = new FilteredElementCollector(doc, viewId);
            collector.WhereElementIsNotElementType();

            if (category is not null)
            {
                if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", out var bic))
                    return new BridgeResponse(Success: false, Error: $"Unknown category: {category}");

                collector.OfCategory(bic);
            }

            var elements = new List<object>();

            foreach (var elem in collector)
            {
                if (elements.Count >= limit) break;

                var bb = elem.get_BoundingBox(view);
                object? bbCenter = null;

                if (bb is not null)
                {
                    var cx = (bb.Min.X + bb.Max.X) / 2.0;
                    var cy = (bb.Min.Y + bb.Max.Y) / 2.0;
                    var cz = (bb.Min.Z + bb.Max.Z) / 2.0;

                    // If bounds are specified, filter by center point within region
                    if (hasBounds)
                    {
                        if (cx < minX || cx > maxX || cy < minY || cy > maxY)
                            continue;
                    }

                    bbCenter = new { X = cx, Y = cy, Z = cz };
                }
                else if (hasBounds)
                {
                    // If bounds are specified and element has no bounding box, skip it
                    continue;
                }

                string? typeName = null;
                var typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                    typeName = (doc.GetElement(typeId) as ElementType)?.Name;

                elements.Add(new
                {
                    Id = elem.Id.Value,
                    Name = SafeGetName(elem),
                    Category = elem.Category?.Name,
                    TypeName = typeName,
                    BoundingBoxCenter = bbCenter
                });
            }

            var result = new
            {
                ViewId = view.Id.Value,
                ViewName = view.Name,
                ViewType = view.ViewType.ToString(),
                ElementCount = elements.Count,
                Truncated = elements.Count >= limit,
                Elements = elements
            };

            var data = JsonSerializer.SerializeToElement(result);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Safely reads <see cref="Element.Name"/>. Some element types throw when
    /// the Name property is accessed.
    /// </summary>
    private static string? SafeGetName(Element element)
    {
        try
        {
            return element.Name;
        }
        catch
        {
            return null;
        }
    }
}
