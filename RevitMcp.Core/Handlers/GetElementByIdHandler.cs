using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetElementById"/> command.
/// Returns detailed information about a single Revit element including name,
/// category, level, type name, bounding box, and location.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>elementId</c> (int, required) – The Revit element ID.</item>
/// </list>
/// </remarks>
public sealed class GetElementByIdHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetElementById;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            if (request.Payload?.TryGetProperty("elementId", out var idProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementId");

            var elementId = new ElementId((long)idProp.GetInt64());
            var element = doc.GetElement(elementId);

            if (element is null)
                return new BridgeResponse(Success: false, Error: $"Element not found: {idProp.GetInt64()}");

            var result = new Dictionary<string, object?>
            {
                ["Id"] = element.Id.Value,
                ["Name"] = SafeGetName(element),
                ["Category"] = element.Category?.Name,
                ["LevelName"] = GetLevelName(doc, element),
                ["TypeName"] = GetTypeName(doc, element),
                ["BoundingBox"] = GetBoundingBox(element),
                ["Location"] = GetLocation(element)
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
    /// Gets the level name for an element, if it has a level association.
    /// </summary>
    private static string? GetLevelName(Document doc, Element element)
    {
        if (element.LevelId is null || element.LevelId == ElementId.InvalidElementId)
            return null;

        return doc.GetElement(element.LevelId) is Level level ? level.Name : null;
    }

    /// <summary>
    /// Gets the type name from the element's type ID.
    /// </summary>
    private static string? GetTypeName(Document doc, Element element)
    {
        var typeId = element.GetTypeId();
        if (typeId == ElementId.InvalidElementId)
            return null;

        return doc.GetElement(typeId) is ElementType elementType ? elementType.Name : null;
    }

    /// <summary>
    /// Gets the bounding box min/max coordinates in decimal feet.
    /// </summary>
    private static object? GetBoundingBox(Element element)
    {
        var bb = element.get_BoundingBox(null);
        if (bb is null)
            return null;

        return new
        {
            Min = ToPoint(bb.Min),
            Max = ToPoint(bb.Max)
        };
    }

    /// <summary>
    /// Extracts XYZ coordinates as decimal feet.
    /// </summary>
    private static object ToPoint(XYZ point)
    {
        return new
        {
            point.X,
            point.Y,
            point.Z
        };
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
                Position = ToPoint(lp.Point)
            },
            LocationCurve lc => new
            {
                Type = "Curve",
                Start = ToPoint(lc.Curve.GetEndPoint(0)),
                End = ToPoint(lc.Curve.GetEndPoint(1))
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
