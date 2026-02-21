using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.MoveElements"/> command.
/// Moves one or more elements by a translation vector, with a preview mode.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>elementIds</c> (long[], required) – Array of element IDs to move.</item>
///   <item><c>deltaX</c> (double, required) – Translation distance along X axis in decimal feet.</item>
///   <item><c>deltaY</c> (double, required) – Translation distance along Y axis in decimal feet.</item>
///   <item><c>deltaZ</c> (double, optional) – Translation distance along Z axis in decimal feet. Defaults to 0.</item>
///   <item><c>confirm</c> (bool, optional) – Set to true to execute. Defaults to false (preview).</item>
/// </list>
/// </remarks>
public sealed class MoveElementsHandler : ICommandHandler
{
    private const int MaxElements = 50;

    /// <inheritdoc />
    public string Command => CommandNames.MoveElements;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("elementIds", out var idsProp) != true
                || idsProp.ValueKind != JsonValueKind.Array)
                return new BridgeResponse(Success: false,
                    Error: "Missing required parameter: elementIds (array of integers)");

            var elementIds = new List<long>();
            foreach (var idVal in idsProp.EnumerateArray())
                elementIds.Add(idVal.GetInt64());

            if (elementIds.Count == 0)
                return new BridgeResponse(Success: false, Error: "elementIds array cannot be empty.");

            if (elementIds.Count > MaxElements)
                return new BridgeResponse(Success: false,
                    Error: $"Too many elements ({elementIds.Count}). Maximum is {MaxElements} per call.");

            if (request.Payload?.TryGetProperty("deltaX", out var dxProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: deltaX");
            if (request.Payload?.TryGetProperty("deltaY", out var dyProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: deltaY");

            var deltaX = dxProp.GetDouble();
            var deltaY = dyProp.GetDouble();
            var deltaZ = request.Payload?.TryGetProperty("deltaZ", out var dzProp) == true
                ? dzProp.GetDouble() : 0.0;

            var translation = new XYZ(deltaX, deltaY, deltaZ);

            var confirm = request.Payload?.TryGetProperty("confirm", out var confirmProp) == true
                && confirmProp.GetBoolean();

            if (!confirm)
            {
                // Preview mode: show current and proposed positions
                var previews = new List<object>();
                foreach (var id in elementIds)
                {
                    var elemId = new ElementId(id);
                    var elem = doc.GetElement(elemId);

                    if (elem is null)
                    {
                        previews.Add(new { Id = id, Status = "NotFound" });
                        continue;
                    }

                    string? name = SafeGetName(elem);
                    var category = elem.Category?.Name ?? "(none)";
                    var currentPos = GetPosition(elem);

                    if (currentPos is not null)
                    {
                        var proposed = new XYZ(currentPos.X + deltaX, currentPos.Y + deltaY, currentPos.Z + deltaZ);
                        previews.Add(new
                        {
                            Id = id,
                            Name = name ?? "(unnamed)",
                            Category = category,
                            CurrentPosition = new { currentPos.X, currentPos.Y, currentPos.Z },
                            ProposedPosition = new { proposed.X, proposed.Y, proposed.Z }
                        });
                    }
                    else
                    {
                        previews.Add(new
                        {
                            Id = id,
                            Name = name ?? "(unnamed)",
                            Category = category,
                            CurrentPosition = (object?)null,
                            ProposedPosition = (object?)null
                        });
                    }
                }

                var previewResult = new
                {
                    Mode = "Preview",
                    Message = "Set confirm=true to execute the move.",
                    Translation = new { X = deltaX, Y = deltaY, Z = deltaZ },
                    Elements = previews
                };

                var previewData = JsonSerializer.SerializeToElement(previewResult);
                return new BridgeResponse(Success: true, Data: previewData);
            }

            // Execute mode
            using var transaction = new Transaction(doc, "MCP: Move Elements");
            transaction.Start();

            try
            {
                var moved = new List<object>();
                var errors = new List<object>();

                foreach (var id in elementIds)
                {
                    var elemId = new ElementId(id);
                    var elem = doc.GetElement(elemId);

                    if (elem is null)
                    {
                        errors.Add(new { Id = id, Error = "Element not found" });
                        continue;
                    }

                    string? name = SafeGetName(elem);
                    var category = elem.Category?.Name ?? "(none)";
                    var oldPos = GetPosition(elem);

                    try
                    {
                        ElementTransformUtils.MoveElement(doc, elemId, translation);
                        var newPos = GetPosition(elem);

                        moved.Add(new
                        {
                            Id = id,
                            Name = name ?? "(unnamed)",
                            Category = category,
                            OldPosition = oldPos is not null
                                ? new { oldPos.X, oldPos.Y, oldPos.Z }
                                : (object?)null,
                            NewPosition = newPos is not null
                                ? new { newPos.X, newPos.Y, newPos.Z }
                                : (object?)null
                        });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new { Id = id, Name = name ?? "(unnamed)", Error = ex.Message });
                    }
                }

                transaction.Commit();

                var result = new
                {
                    Mode = "Move",
                    Translation = new { X = deltaX, Y = deltaY, Z = deltaZ },
                    MovedCount = moved.Count,
                    ErrorCount = errors.Count,
                    Elements = moved,
                    Errors = errors
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to move elements: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Gets the position of an element from its location.
    /// Returns the point for LocationPoint, or the midpoint for LocationCurve.
    /// </summary>
    private static XYZ? GetPosition(Element element)
    {
        return element.Location switch
        {
            LocationPoint lp => lp.Point,
            LocationCurve lc => lc.Curve.Evaluate(0.5, true),
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
