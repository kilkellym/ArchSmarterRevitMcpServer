using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateElevationView"/> command.
/// Creates a new elevation view at a specified location and direction.
/// </summary>
public sealed class CreateElevationViewHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateElevationView;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("x", out var xProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: x");
            if (request.Payload?.TryGetProperty("y", out var yProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: y");
            if (request.Payload?.TryGetProperty("levelName", out var levelProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: levelName");

            var xMm = xProp.GetDouble();
            var yMm = yProp.GetDouble();
            var levelName = levelProp.GetString();

            var direction = request.Payload?.TryGetProperty("direction", out var dirProp) == true
                ? dirProp.GetString() ?? "north" : "north";
            var viewName = request.Payload?.TryGetProperty("viewName", out var nameProp) == true
                ? nameProp.GetString() : null;

            // Convert mm to feet
            var xFt = UnitUtils.ConvertToInternalUnits(xMm, UnitTypeId.Millimeters);
            var yFt = UnitUtils.ConvertToInternalUnits(yMm, UnitTypeId.Millimeters);

            // Find the level
            using var levelCollector = new FilteredElementCollector(doc);
            var level = levelCollector.OfClass(typeof(Level)).Cast<Level>()
                .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));

            if (level is null)
                return new BridgeResponse(Success: false, Error: $"Level not found: '{levelName}'");

            // Find elevation ViewFamilyType
            using var vftCollector = new FilteredElementCollector(doc);
            var viewFamilyType = vftCollector.OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Elevation);

            if (viewFamilyType is null)
                return new BridgeResponse(Success: false, Error: "No elevation ViewFamilyType found.");

            // Find a plan view at this level for the marker
            using var planCollector = new FilteredElementCollector(doc);
            var planView = planCollector.OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                .FirstOrDefault(v => !v.IsTemplate && v.GenLevel?.Id == level.Id);

            if (planView is null)
                return new BridgeResponse(Success: false,
                    Error: $"No plan view found at level '{levelName}'. Create a plan view first.");

            // Map direction to side index: 0=south-facing(looks north), 1=west-facing(looks east),
            // 2=north-facing(looks south), 3=east-facing(looks west)
            // The user specifies which direction the elevation FACES (looks toward)
            var sideIndex = direction.ToLowerInvariant() switch
            {
                "north" => 0,  // index 0 faces the marker's forward direction
                "east" => 1,
                "south" => 2,
                "west" => 3,
                _ => 0
            };

            using var transaction = new Transaction(doc, "MCP: Create Elevation View");
            transaction.Start();

            try
            {
                var markerLocation = new XYZ(xFt, yFt, level.Elevation);
                var marker = ElevationMarker.CreateElevationMarker(doc, viewFamilyType.Id, markerLocation, 100);
                var elevView = marker.CreateElevation(doc, planView.Id, sideIndex);

                if (!string.IsNullOrEmpty(viewName))
                    elevView.Name = viewName;

                transaction.Commit();

                var result = new
                {
                    Id = elevView.Id.Value,
                    Name = elevView.Name,
                    MarkerId = marker.Id.Value,
                    ViewType = elevView.ViewType.ToString()
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create elevation view: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
