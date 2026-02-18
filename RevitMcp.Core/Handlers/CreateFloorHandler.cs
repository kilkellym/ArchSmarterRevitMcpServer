using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateFloor"/> command.
/// Creates a floor from a closed boundary polygon on a specified level.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>pointsX</c> (double[], required) – X coordinates in millimeters (min 3).</item>
///   <item><c>pointsY</c> (double[], required) – Y coordinates in millimeters (same length as pointsX).</item>
///   <item><c>levelName</c> (string, required) – The level name.</item>
///   <item><c>floorTypeName</c> (string, optional) – Floor type name.</item>
///   <item><c>isStructural</c> (bool, optional) – Whether the floor is structural.</item>
/// </list>
/// </remarks>
public sealed class CreateFloorHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateFloor;

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

            if (xCoords.Count < 3)
                return new BridgeResponse(Success: false, Error: "pointsX must have at least 3 values.");
            if (xCoords.Count != yCoords.Count)
                return new BridgeResponse(Success: false,
                    Error: $"pointsX ({xCoords.Count}) and pointsY ({yCoords.Count}) must have the same length.");

            var floorTypeName = request.Payload?.TryGetProperty("floorTypeName", out var ftProp) == true
                ? ftProp.GetString() : null;
            var isStructural = request.Payload?.TryGetProperty("isStructural", out var strProp) == true
                && strProp.GetBoolean();

            // --- Convert mm to feet ---
            var pointsFt = new List<XYZ>();
            for (var i = 0; i < xCoords.Count; i++)
            {
                var xFt = UnitUtils.ConvertToInternalUnits(xCoords[i], UnitTypeId.Millimeters);
                var yFt = UnitUtils.ConvertToInternalUnits(yCoords[i], UnitTypeId.Millimeters);
                pointsFt.Add(new XYZ(xFt, yFt, 0));
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

            // --- Find floor type ---
            using var ftCollector = new FilteredElementCollector(doc);
            var floorTypes = ftCollector.OfClass(typeof(FloorType)).Cast<FloorType>().ToList();

            if (floorTypes.Count == 0)
                return new BridgeResponse(Success: false, Error: "No floor types found in the project.");

            FloorType floorType;
            if (!string.IsNullOrEmpty(floorTypeName))
            {
                floorType = floorTypes.FirstOrDefault(ft =>
                    string.Equals(ft.Name, floorTypeName, StringComparison.OrdinalIgnoreCase))!;

                if (floorType is null)
                {
                    var available = string.Join(", ", floorTypes.Select(ft => ft.Name).Take(30));
                    return new BridgeResponse(Success: false,
                        Error: $"Floor type not found: '{floorTypeName}'. Available: {available}");
                }
            }
            else
            {
                floorType = floorTypes.First();
            }

            // --- Build the CurveLoop ---
            var curveLoop = new CurveLoop();
            for (var i = 0; i < pointsFt.Count; i++)
            {
                var start = pointsFt[i];
                var end = pointsFt[(i + 1) % pointsFt.Count];

                if (start.DistanceTo(end) < 0.001)
                    continue; // skip degenerate segments

                curveLoop.Append(Line.CreateBound(start, end));
            }

            // --- Create the floor ---
            using var transaction = new Transaction(doc, "MCP: Create Floor");
            transaction.Start();

            try
            {
                var floor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, level.Id);

                if (isStructural)
                {
                    var structParam = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    if (structParam is not null && !structParam.IsReadOnly)
                        structParam.Set(1);
                }

                transaction.Commit();

                // Read area after commit
                var areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                var areaSqFt = areaParam?.AsDouble() ?? 0;
                var areaSqM = UnitUtils.ConvertFromInternalUnits(areaSqFt, UnitTypeId.SquareMeters);

                var result = new
                {
                    Id = floor.Id.Value,
                    FloorType = floorType.Name,
                    LevelName = level.Name,
                    AreaSqM = Math.Round(areaSqM, 2),
                    PointCount = xCoords.Count
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create floor: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
