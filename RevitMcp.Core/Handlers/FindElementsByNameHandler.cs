using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.FindElementsByName"/> command.
/// Searches for elements by name, family name, type name, or mark value.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>searchText</c> (string, required) – Text to search for. Case insensitive, partial match.</item>
///   <item><c>viewId</c> (long, optional) – Optional view ID to scope the search.</item>
///   <item><c>category</c> (string, optional) – Optional category filter.</item>
///   <item><c>limit</c> (int, optional) – Maximum number of elements to return. Defaults to 50.</item>
/// </list>
/// </remarks>
public sealed class FindElementsByNameHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.FindElementsByName;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("searchText", out var searchProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: searchText");

            var searchText = searchProp.GetString();
            if (string.IsNullOrWhiteSpace(searchText))
                return new BridgeResponse(Success: false, Error: "searchText cannot be empty.");

            var category = request.Payload?.TryGetProperty("category", out var catProp) == true
                ? catProp.GetString()
                : null;

            var limit = request.Payload?.TryGetProperty("limit", out var limitProp) == true
                ? limitProp.GetInt32()
                : 50;

            // Create collector scoped to view if viewId is provided
            JsonElement viewIdProp = default;
            var hasViewId = request.Payload?.TryGetProperty("viewId", out viewIdProp) == true
                && viewIdProp.ValueKind == JsonValueKind.Number;

            FilteredElementCollector collector;
            if (hasViewId)
            {
                var viewId = new ElementId(viewIdProp.GetInt64());
                var view = doc.GetElement(viewId) as View;
                if (view is null)
                    return new BridgeResponse(Success: false,
                        Error: $"View not found for ID: {viewIdProp.GetInt64()}");
                if (view.IsTemplate)
                    return new BridgeResponse(Success: false,
                        Error: $"Cannot search in a view template: '{view.Name}'");

                collector = new FilteredElementCollector(doc, viewId);
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }

            using (collector)
            {
                collector.WhereElementIsNotElementType();

                if (category is not null)
                {
                    if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", out var bic))
                        return new BridgeResponse(Success: false, Error: $"Unknown category: {category}");

                    collector.OfCategory(bic);
                }

                var matches = new List<object>();

                foreach (var elem in collector)
                {
                    if (matches.Count >= limit) break;

                    if (!IsMatch(doc, elem, searchText))
                        continue;

                    string? typeName = null;
                    string? familyName = null;
                    var typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        var elemType = doc.GetElement(typeId) as ElementType;
                        typeName = elemType?.Name;
                        familyName = elemType?.FamilyName;
                    }

                    var mark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();

                    matches.Add(new
                    {
                        Id = elem.Id.Value,
                        Name = SafeGetName(elem),
                        Category = elem.Category?.Name,
                        FamilyName = familyName,
                        TypeName = typeName,
                        Mark = mark,
                        Location = GetLocation(elem)
                    });
                }

                var result = new
                {
                    SearchText = searchText,
                    MatchCount = matches.Count,
                    Elements = matches
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Checks if the element matches the search text in any of its identifying fields.
    /// </summary>
    private static bool IsMatch(Document doc, Element elem, string searchText)
    {
        // Check element name
        var name = SafeGetName(elem);
        if (name is not null && name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check type name and family name
        var typeId = elem.GetTypeId();
        if (typeId != ElementId.InvalidElementId)
        {
            var elemType = doc.GetElement(typeId) as ElementType;
            if (elemType is not null)
            {
                if (elemType.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
                if (elemType.FamilyName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
        }

        // Check Mark parameter
        var mark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
        if (mark is not null && mark.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Gets the location as a point or curve endpoints in decimal feet.
    /// </summary>
    private static object? GetLocation(Element element)
    {
        return element.Location switch
        {
            LocationPoint lp => new
            {
                Type = "Point",
                Position = new { lp.Point.X, lp.Point.Y, lp.Point.Z }
            },
            LocationCurve lc => new
            {
                Type = "Curve",
                Start = new { X = lc.Curve.GetEndPoint(0).X, Y = lc.Curve.GetEndPoint(0).Y, Z = lc.Curve.GetEndPoint(0).Z },
                End = new { X = lc.Curve.GetEndPoint(1).X, Y = lc.Curve.GetEndPoint(1).Y, Z = lc.Curve.GetEndPoint(1).Z }
            },
            _ => null
        };
    }

    /// <summary>
    /// Safely reads <see cref="Element.Name"/>. Some element types throw when
    /// the Name property is accessed.
    /// </summary>
    private static string? SafeGetName(Element element)
    {
        try { return element.Name; }
        catch { return null; }
    }
}
