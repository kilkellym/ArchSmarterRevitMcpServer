using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateRailing"/> command.
/// Creates a railing along a path of connected straight line segments.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>pointsX</c> (double[], required) – X coordinates in decimal feet (min 2).</item>
///   <item><c>pointsY</c> (double[], required) – Y coordinates in decimal feet (same length as pointsX).</item>
///   <item><c>levelName</c> (string, required) – The level name.</item>
///   <item><c>railingTypeName</c> (string, optional) – Railing type name.</item>
/// </list>
/// </remarks>
public sealed class CreateRailingHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateRailing;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // --- Extract required parameters ---
            if (request.Payload?.TryGetProperty("pointsX", out var pxProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: pointsX");
            if (request.Payload?.TryGetProperty("pointsY", out var pyProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: pointsY");
            if (request.Payload?.TryGetProperty("levelName", out var levelProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: levelName");

            var xCoords = pxProp.EnumerateArray().Select(e => e.GetDouble()).ToList();
            var yCoords = pyProp.EnumerateArray().Select(e => e.GetDouble()).ToList();
            var levelName = levelProp.GetString();

            if (string.IsNullOrEmpty(levelName))
                return new BridgeResponse(Success: false, Error: "levelName cannot be empty.");

            if (xCoords.Count < 2)
                return new BridgeResponse(Success: false, Error: "pointsX must have at least 2 values.");
            if (xCoords.Count != yCoords.Count)
                return new BridgeResponse(Success: false,
                    Error: $"pointsX ({xCoords.Count}) and pointsY ({yCoords.Count}) must have the same length.");

            var railingTypeName = request.Payload?.TryGetProperty("railingTypeName", out var rtProp) == true
                ? rtProp.GetString() : null;

            // --- Build points ---
            var points = new List<XYZ>();
            for (var i = 0; i < xCoords.Count; i++)
            {
                points.Add(new XYZ(xCoords[i], yCoords[i], 0));
            }

            // --- Find level ---
            using var levelCollector = new FilteredElementCollector(doc);
            var levels = levelCollector.OfClass(typeof(Level)).Cast<Level>().ToList();
            var level = levels.FirstOrDefault(l =>
                string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));

            if (level is null)
            {
                var available = string.Join(", ", levels.Select(l => l.Name));
                return new BridgeResponse(Success: false,
                    Error: $"Level not found: '{levelName}'. Available levels: {available}");
            }

            // --- Find railing type ---
            using var rtCollector = new FilteredElementCollector(doc);
            var railingTypes = rtCollector.OfClass(typeof(RailingType)).Cast<RailingType>().ToList();

            if (railingTypes.Count == 0)
                return new BridgeResponse(Success: false, Error: "No railing types found in the project.");

            RailingType railingType;
            if (!string.IsNullOrEmpty(railingTypeName))
            {
                railingType = railingTypes.FirstOrDefault(rt =>
                    string.Equals(rt.Name, railingTypeName, StringComparison.OrdinalIgnoreCase))!;

                if (railingType is null)
                {
                    var available = string.Join(", ", railingTypes.Select(rt => rt.Name).Take(30));
                    return new BridgeResponse(Success: false,
                        Error: $"Railing type not found: '{railingTypeName}'. Available: {available}");
                }
            }
            else
            {
                railingType = railingTypes.First();
            }

            // --- Build curve list from consecutive points ---
            var curves = new List<Curve>();
            for (var i = 0; i < points.Count - 1; i++)
            {
                var start = points[i];
                var end = points[i + 1];

                if (start.DistanceTo(end) < 0.001)
                    continue; // skip degenerate segments

                curves.Add(Line.CreateBound(start, end));
            }

            if (curves.Count == 0)
                return new BridgeResponse(Success: false,
                    Error: "No valid line segments could be created from the provided points.");

            // --- Create the railing ---
            using var transaction = new Transaction(doc, "MCP: Create Railing");
            transaction.Start();

            try
            {
                var railing = Railing.Create(doc, curves, railingType.Id, level.Id);

                transaction.Commit();

                var result = new
                {
                    Id = railing.Id.Value,
                    RailingType = railingType.Name,
                    LevelName = level.Name,
                    SegmentCount = curves.Count
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create railing: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
