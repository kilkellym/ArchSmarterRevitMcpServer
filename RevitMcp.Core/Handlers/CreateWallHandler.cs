using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateWall"/> command.
/// Creates a straight wall between two points on a specified level.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>startX</c> (double, required) – X coordinate of wall start in decimal feet.</item>
///   <item><c>startY</c> (double, required) – Y coordinate of wall start in decimal feet.</item>
///   <item><c>endX</c> (double, required) – X coordinate of wall end in decimal feet.</item>
///   <item><c>endY</c> (double, required) – Y coordinate of wall end in decimal feet.</item>
///   <item><c>levelName</c> (string, required) – The level name.</item>
///   <item><c>height</c> (double, optional) – Wall height in decimal feet. Defaults to 10.</item>
///   <item><c>wallTypeName</c> (string, optional) – Wall type name.</item>
///   <item><c>isStructural</c> (bool, optional) – Whether the wall is structural.</item>
/// </list>
/// </remarks>
public sealed class CreateWallHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateWall;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // --- Extract required parameters ---
            if (request.Payload?.TryGetProperty("startX", out var sxProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: startX");
            if (request.Payload?.TryGetProperty("startY", out var syProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: startY");
            if (request.Payload?.TryGetProperty("endX", out var exProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: endX");
            if (request.Payload?.TryGetProperty("endY", out var eyProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: endY");
            if (request.Payload?.TryGetProperty("levelName", out var levelProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: levelName");

            var startX = sxProp.GetDouble();
            var startY = syProp.GetDouble();
            var endX = exProp.GetDouble();
            var endY = eyProp.GetDouble();
            var levelName = levelProp.GetString();

            if (string.IsNullOrEmpty(levelName))
                return new BridgeResponse(Success: false, Error: "levelName cannot be empty.");

            var height = request.Payload?.TryGetProperty("height", out var htProp) == true
                ? htProp.GetDouble() : 10.0;
            var wallTypeName = request.Payload?.TryGetProperty("wallTypeName", out var wtProp) == true
                ? wtProp.GetString() : null;
            var isStructural = request.Payload?.TryGetProperty("isStructural", out var strProp) == true
                && strProp.GetBoolean();

            // --- Validate wall has non-zero length ---
            var startPt = new XYZ(startX, startY, 0);
            var endPt = new XYZ(endX, endY, 0);

            if (startPt.DistanceTo(endPt) < 0.001)
                return new BridgeResponse(Success: false,
                    Error: "Start and end points are too close together. Wall must have non-zero length.");

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

            // --- Find wall type ---
            using var wtCollector = new FilteredElementCollector(doc);
            var wallTypes = wtCollector.OfClass(typeof(WallType)).Cast<WallType>().ToList();

            if (wallTypes.Count == 0)
                return new BridgeResponse(Success: false, Error: "No wall types found in the project.");

            WallType wallType;
            if (!string.IsNullOrEmpty(wallTypeName))
            {
                wallType = wallTypes.FirstOrDefault(wt =>
                    string.Equals(wt.Name, wallTypeName, StringComparison.OrdinalIgnoreCase))!;

                if (wallType is null)
                {
                    var available = string.Join(", ", wallTypes.Select(wt => wt.Name).Take(30));
                    return new BridgeResponse(Success: false,
                        Error: $"Wall type not found: '{wallTypeName}'. Available: {available}");
                }
            }
            else
            {
                wallType = wallTypes.First();
            }

            // --- Create the wall ---
            var line = Line.CreateBound(startPt, endPt);

            using var transaction = new Transaction(doc, "MCP: Create Wall");
            transaction.Start();

            try
            {
                var wall = Wall.Create(doc, line, wallType.Id, level.Id, height, 0.0, false, isStructural);

                transaction.Commit();

                var result = new
                {
                    Id = wall.Id.Value,
                    WallType = wallType.Name,
                    LevelName = level.Name,
                    LengthFt = Math.Round(line.Length, 4),
                    HeightFt = height
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create wall: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
