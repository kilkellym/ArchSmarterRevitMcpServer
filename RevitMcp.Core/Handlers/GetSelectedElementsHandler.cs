using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetSelectedElements"/> command.
/// Returns the currently selected elements in the active Revit view with
/// basic element info and optionally all instance parameters.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>includeParameters</c> (bool, optional) – If true, include all instance
///     parameters for each selected element. Defaults to false.</item>
/// </list>
/// </remarks>
public sealed class GetSelectedElementsHandler : ICommandHandler
{
    /// <summary>
    /// Maximum number of elements for which detailed parameter data is returned.
    /// Elements beyond this threshold get basic info only.
    /// </summary>
    private const int MaxDetailedElements = 50;

    /// <inheritdoc />
    public string Command => CommandNames.GetSelectedElements;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            var includeParameters = request.Payload?.TryGetProperty("includeParameters", out var inclProp) == true
                && inclProp.GetBoolean();

            var selectedIds = uiDoc.Selection.GetElementIds();

            if (selectedIds.Count == 0)
            {
                var emptyResult = new
                {
                    Message = "No elements currently selected in Revit.",
                    Elements = Array.Empty<object>()
                };

                var emptyData = JsonSerializer.SerializeToElement(emptyResult);
                return new BridgeResponse(Success: true, Data: emptyData);
            }

            var elements = new List<object>();
            var truncated = false;

            foreach (var id in selectedIds)
            {
                var element = doc.GetElement(id);
                if (element is null)
                    continue;

                var useDetailedParams = includeParameters && elements.Count < MaxDetailedElements;

                var info = new Dictionary<string, object?>
                {
                    ["Id"] = element.Id.Value,
                    ["Name"] = SafeGetName(element),
                    ["Category"] = element.Category?.Name,
                    ["TypeName"] = GetTypeName(doc, element),
                    ["LevelName"] = GetLevelName(doc, element),
                    ["Location"] = GetLocation(element)
                };

                if (useDetailedParams)
                {
                    info["Parameters"] = GetParameters(element);
                }

                elements.Add(info);

                if (includeParameters && elements.Count == MaxDetailedElements + 1)
                    truncated = true;
            }

            var result = new Dictionary<string, object?>
            {
                ["SelectedCount"] = selectedIds.Count,
                ["Elements"] = elements
            };

            if (truncated)
            {
                result["Note"] = $"Parameter details limited to the first {MaxDetailedElements} elements. " +
                                 $"Remaining {selectedIds.Count - MaxDetailedElements} elements have basic info only.";
            }

            var data = JsonSerializer.SerializeToElement(result);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Gets all instance parameters for an element.
    /// </summary>
    private static List<object> GetParameters(Element element)
    {
        var parameters = new List<object>();

        foreach (Parameter param in element.Parameters)
        {
            if (param.Definition is null)
                continue;

            parameters.Add(new
            {
                Name = param.Definition.Name,
                Value = SafeGetParameterValue(param),
                StorageType = param.StorageType.ToString()
            });
        }

        return parameters;
    }

    /// <summary>
    /// Safely reads a parameter value, preferring <see cref="Parameter.AsValueString"/>
    /// and falling back to <see cref="Parameter.AsString"/>.
    /// </summary>
    private static string? SafeGetParameterValue(Parameter param)
    {
        if (!param.HasValue)
            return null;

        try
        {
            return param.AsValueString() ?? param.AsString();
        }
        catch
        {
            try
            {
                return param.AsString();
            }
            catch
            {
                return null;
            }
        }
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
    /// Gets the level name for an element, if it has a level association.
    /// </summary>
    private static string? GetLevelName(Document doc, Element element)
    {
        if (element.LevelId is null || element.LevelId == ElementId.InvalidElementId)
            return null;

        return doc.GetElement(element.LevelId) is Level level ? level.Name : null;
    }

    /// <summary>
    /// Gets the location as a point or curve endpoints, converted to millimeters.
    /// </summary>
    private static object? GetLocation(Element element)
    {
        return element.Location switch
        {
            LocationPoint lp => new
            {
                Type = "Point",
                Position = ConvertToMm(lp.Point)
            },
            LocationCurve lc => new
            {
                Type = "Curve",
                Start = ConvertToMm(lc.Curve.GetEndPoint(0)),
                End = ConvertToMm(lc.Curve.GetEndPoint(1))
            },
            _ => null
        };
    }

    /// <summary>
    /// Converts an XYZ point from Revit internal units (feet) to millimeters.
    /// </summary>
    private static object ConvertToMm(XYZ point)
    {
        return new
        {
            X = UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Millimeters),
            Y = UnitUtils.ConvertFromInternalUnits(point.Y, UnitTypeId.Millimeters),
            Z = UnitUtils.ConvertFromInternalUnits(point.Z, UnitTypeId.Millimeters)
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
