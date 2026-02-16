using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateSectionView"/> command.
/// Creates a new section view defined by an origin, direction, and dimensions.
/// </summary>
public sealed class CreateSectionViewHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateSectionView;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("originX", out var oxProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: originX");
            if (request.Payload?.TryGetProperty("originY", out var oyProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: originY");
            if (request.Payload?.TryGetProperty("originZ", out var ozProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: originZ");
            if (request.Payload?.TryGetProperty("directionX", out var dxProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: directionX");
            if (request.Payload?.TryGetProperty("directionY", out var dyProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: directionY");

            var widthMm = request.Payload?.TryGetProperty("width", out var wProp) == true ? wProp.GetDouble() : 10000.0;
            var heightMm = request.Payload?.TryGetProperty("height", out var hProp) == true ? hProp.GetDouble() : 10000.0;
            var depthMm = request.Payload?.TryGetProperty("depth", out var dpProp) == true ? dpProp.GetDouble() : 10000.0;
            var viewName = request.Payload?.TryGetProperty("viewName", out var nameProp) == true ? nameProp.GetString() : null;

            // Convert mm to feet
            var origin = new XYZ(
                UnitUtils.ConvertToInternalUnits(oxProp.GetDouble(), UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(oyProp.GetDouble(), UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(ozProp.GetDouble(), UnitTypeId.Millimeters));

            var viewDir = new XYZ(dxProp.GetDouble(), dyProp.GetDouble(), 0).Normalize();
            if (viewDir.IsZeroLength())
                return new BridgeResponse(Success: false, Error: "View direction cannot be zero.");

            var halfW = UnitUtils.ConvertToInternalUnits(widthMm / 2, UnitTypeId.Millimeters);
            var halfH = UnitUtils.ConvertToInternalUnits(heightMm / 2, UnitTypeId.Millimeters);
            var depth = UnitUtils.ConvertToInternalUnits(depthMm, UnitTypeId.Millimeters);

            // Find section ViewFamilyType
            using var vftCollector = new FilteredElementCollector(doc);
            var viewFamilyType = vftCollector.OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Section);

            if (viewFamilyType is null)
                return new BridgeResponse(Success: false, Error: "No section ViewFamilyType found.");

            // Build the bounding box transform
            var up = XYZ.BasisZ;
            var right = viewDir.CrossProduct(up);

            var transform = Transform.Identity;
            transform.Origin = origin;
            transform.BasisX = right;
            transform.BasisY = up;
            transform.BasisZ = viewDir;

            var sectionBox = new BoundingBoxXYZ
            {
                Transform = transform,
                Min = new XYZ(-halfW, -halfH, 0),
                Max = new XYZ(halfW, halfH, depth)
            };

            using var transaction = new Transaction(doc, "MCP: Create Section View");
            transaction.Start();

            try
            {
                var sectionView = ViewSection.CreateSection(doc, viewFamilyType.Id, sectionBox);

                if (!string.IsNullOrEmpty(viewName))
                    sectionView.Name = viewName;

                transaction.Commit();

                var result = new
                {
                    Id = sectionView.Id.Value,
                    Name = sectionView.Name,
                    ViewType = sectionView.ViewType.ToString()
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create section view: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
