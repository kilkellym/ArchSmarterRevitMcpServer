using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreatePlanView"/> command.
/// Creates a new floor plan or ceiling plan view for a specified level.
/// </summary>
public sealed class CreatePlanViewHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreatePlanView;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("levelName", out var levelProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: levelName");

            var levelName = levelProp.GetString();
            if (string.IsNullOrEmpty(levelName))
                return new BridgeResponse(Success: false, Error: "levelName cannot be empty.");

            var viewName = request.Payload?.TryGetProperty("viewName", out var nameProp) == true
                ? nameProp.GetString() : null;

            var isCeilingPlan = request.Payload?.TryGetProperty("isCeilingPlan", out var ceilProp) == true
                && ceilProp.GetBoolean();

            // Find the level
            using var levelCollector = new FilteredElementCollector(doc);
            var level = levelCollector
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));

            if (level is null)
                return new BridgeResponse(Success: false, Error: $"Level not found: '{levelName}'");

            // Find the appropriate ViewFamilyType
            var targetFamily = isCeilingPlan ? ViewFamily.CeilingPlan : ViewFamily.FloorPlan;

            using var vftCollector = new FilteredElementCollector(doc);
            var viewFamilyType = vftCollector
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == targetFamily);

            if (viewFamilyType is null)
                return new BridgeResponse(Success: false,
                    Error: $"No ViewFamilyType found for {targetFamily}.");

            using var transaction = new Transaction(doc, "MCP: Create Plan View");
            transaction.Start();

            try
            {
                var newView = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);

                if (!string.IsNullOrEmpty(viewName))
                    newView.Name = viewName;

                transaction.Commit();

                var result = new
                {
                    Id = newView.Id.Value,
                    Name = newView.Name,
                    LevelName = level.Name,
                    ViewType = newView.ViewType.ToString()
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create plan view: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
