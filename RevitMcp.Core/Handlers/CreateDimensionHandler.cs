using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateDimension"/> command.
/// Creates a dimension between two or more elements in a view.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>viewId</c> (long, required) – Element ID of the view.</item>
///   <item><c>elementIds</c> (long[], required) – Array of at least 2 element IDs to dimension between.</item>
///   <item><c>dimensionTypeName</c> (string, optional) – Dimension type name.</item>
/// </list>
/// </remarks>
public sealed class CreateDimensionHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateDimension;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // --- Extract required parameters ---
            if (request.Payload?.TryGetProperty("viewId", out var viewIdProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: viewId");
            if (request.Payload?.TryGetProperty("elementIds", out var eidsProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementIds");

            var viewId = new ElementId(viewIdProp.GetInt64());
            var elementIdValues = eidsProp.EnumerateArray().Select(e => e.GetInt64()).ToList();

            if (elementIdValues.Count < 2)
                return new BridgeResponse(Success: false,
                    Error: "elementIds must contain at least 2 element IDs.");

            var dimensionTypeName = request.Payload?.TryGetProperty("dimensionTypeName", out var dtProp) == true
                ? dtProp.GetString() : null;

            // --- Verify the view ---
            var viewElement = doc.GetElement(viewId);
            if (viewElement is not View view)
                return new BridgeResponse(Success: false,
                    Error: $"View not found or element {viewIdProp.GetInt64()} is not a view.");

            if (view.IsTemplate)
                return new BridgeResponse(Success: false, Error: "Cannot place dimensions in a view template.");

            // --- Look up elements and extract references ---
            var references = new ReferenceArray();
            var elements = new List<Element>();
            var failedIds = new List<long>();

            foreach (var eid in elementIdValues)
            {
                var element = doc.GetElement(new ElementId(eid));
                if (element is null)
                {
                    failedIds.Add(eid);
                    continue;
                }

                var reference = GetElementReference(element, view);
                if (reference is null)
                {
                    failedIds.Add(eid);
                    continue;
                }

                references.Append(reference);
                elements.Add(element);
            }

            if (failedIds.Count > 0 && references.Size < 2)
                return new BridgeResponse(Success: false,
                    Error: $"Could not extract geometric references from elements: [{string.Join(", ", failedIds)}]. " +
                           "Elements must have visible geometry in the view. Works best with walls, columns, and structural elements.");

            if (references.Size < 2)
                return new BridgeResponse(Success: false,
                    Error: "Need at least 2 valid references to create a dimension.");

            // --- Compute the dimension line ---
            var dimLine = ComputeDimensionLine(elements);
            if (dimLine is null)
                return new BridgeResponse(Success: false,
                    Error: "Could not compute a valid dimension line from the element locations.");

            // --- Find dimension type (optional) ---
            DimensionType? dimensionType = null;
            if (!string.IsNullOrEmpty(dimensionTypeName))
            {
                using var dtCollector = new FilteredElementCollector(doc);
                var dimTypes = dtCollector.OfClass(typeof(DimensionType)).Cast<DimensionType>().ToList();

                dimensionType = dimTypes.FirstOrDefault(dt =>
                    string.Equals(dt.Name, dimensionTypeName, StringComparison.OrdinalIgnoreCase));

                if (dimensionType is null)
                {
                    var available = string.Join(", ", dimTypes.Select(dt => dt.Name).Take(30));
                    return new BridgeResponse(Success: false,
                        Error: $"Dimension type not found: '{dimensionTypeName}'. Available: {available}");
                }
            }

            // --- Create the dimension ---
            using var transaction = new Transaction(doc, "MCP: Create Dimension");
            transaction.Start();

            try
            {
                Dimension dimension;
                if (dimensionType is not null)
                    dimension = doc.Create.NewDimension(view, dimLine, references, dimensionType);
                else
                    dimension = doc.Create.NewDimension(view, dimLine, references);

                transaction.Commit();

                var result = new
                {
                    Id = dimension.Id.Value,
                    ViewId = view.Id.Value,
                    ViewName = view.Name,
                    Value = dimension.ValueString,
                    ElementCount = references.Size,
                    FailedElementIds = failedIds
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create dimension: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Extracts a geometric <see cref="Reference"/> from an element by walking its geometry.
    /// Uses <see cref="Options.ComputeReferences"/> to ensure references are available.
    /// </summary>
    private static Reference? GetElementReference(Element element, View view)
    {
        var options = new Options
        {
            ComputeReferences = true,
            View = view
        };

        var geom = element.get_Geometry(options);
        if (geom is null)
            return null;

        return FindReferenceInGeometry(geom);
    }

    /// <summary>
    /// Recursively searches geometry objects for a usable <see cref="Reference"/>.
    /// Prefers planar face references from solids.
    /// </summary>
    private static Reference? FindReferenceInGeometry(GeometryElement geom)
    {
        foreach (var obj in geom)
        {
            switch (obj)
            {
                case Solid solid when solid.Faces.Size > 0:
                    foreach (Face face in solid.Faces)
                    {
                        if (face.Reference is not null)
                            return face.Reference;
                    }
                    break;

                case GeometryInstance instance:
                    var instanceGeom = instance.GetInstanceGeometry();
                    if (instanceGeom is not null)
                    {
                        var reference = FindReferenceInGeometry(instanceGeom);
                        if (reference is not null)
                            return reference;
                    }
                    break;
            }
        }

        return null;
    }

    /// <summary>
    /// Computes a dimension line from the bounding boxes of the first and last elements.
    /// The line is offset above the elements for readability.
    /// </summary>
    private static Line? ComputeDimensionLine(List<Element> elements)
    {
        if (elements.Count < 2)
            return null;

        var bb1 = elements.First().get_BoundingBox(null);
        var bb2 = elements.Last().get_BoundingBox(null);

        if (bb1 is null || bb2 is null)
            return null;

        var center1 = (bb1.Min + bb1.Max) / 2.0;
        var center2 = (bb2.Min + bb2.Max) / 2.0;

        // Use the same Z for a horizontal dimension line, offset above elements
        var maxZ = Math.Max(bb1.Max.Z, bb2.Max.Z);
        var offsetZ = maxZ + 1.5;

        var p1 = new XYZ(center1.X, center1.Y, offsetZ);
        var p2 = new XYZ(center2.X, center2.Y, offsetZ);

        if (p1.DistanceTo(p2) < 0.001)
            return null;

        return Line.CreateBound(p1, p2);
    }
}
