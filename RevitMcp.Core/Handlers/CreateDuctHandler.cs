using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateDuct"/> command.
/// Creates a straight duct segment between two 3D points on a specified level.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>startX</c> (double, required) – X coordinate of duct start in decimal feet.</item>
///   <item><c>startY</c> (double, required) – Y coordinate of duct start in decimal feet.</item>
///   <item><c>startZ</c> (double, required) – Z coordinate of duct start in decimal feet.</item>
///   <item><c>endX</c> (double, required) – X coordinate of duct end in decimal feet.</item>
///   <item><c>endY</c> (double, required) – Y coordinate of duct end in decimal feet.</item>
///   <item><c>endZ</c> (double, required) – Z coordinate of duct end in decimal feet.</item>
///   <item><c>levelName</c> (string, required) – The level name.</item>
///   <item><c>width</c> (double, optional) – Duct width in inches for rectangular ducts.</item>
///   <item><c>height</c> (double, optional) – Duct height in inches for rectangular ducts.</item>
///   <item><c>diameter</c> (double, optional) – Duct diameter in inches for round ducts.</item>
///   <item><c>ductTypeName</c> (string, optional) – Duct type name.</item>
/// </list>
/// </remarks>
public sealed class CreateDuctHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateDuct;

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
            if (request.Payload?.TryGetProperty("startZ", out var szProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: startZ");
            if (request.Payload?.TryGetProperty("endX", out var exProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: endX");
            if (request.Payload?.TryGetProperty("endY", out var eyProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: endY");
            if (request.Payload?.TryGetProperty("endZ", out var ezProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: endZ");
            if (request.Payload?.TryGetProperty("levelName", out var levelProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: levelName");

            var startX = sxProp.GetDouble();
            var startY = syProp.GetDouble();
            var startZ = szProp.GetDouble();
            var endX = exProp.GetDouble();
            var endY = eyProp.GetDouble();
            var endZ = ezProp.GetDouble();
            var levelName = levelProp.GetString();

            if (string.IsNullOrEmpty(levelName))
                return new BridgeResponse(Success: false, Error: "levelName cannot be empty.");

            // --- Extract optional parameters ---
            var width = request.Payload?.TryGetProperty("width", out var wProp) == true
                ? wProp.GetDouble() : (double?)null;
            var height = request.Payload?.TryGetProperty("height", out var hProp) == true
                ? hProp.GetDouble() : (double?)null;
            var diameter = request.Payload?.TryGetProperty("diameter", out var dProp) == true
                ? dProp.GetDouble() : (double?)null;
            var ductTypeName = request.Payload?.TryGetProperty("ductTypeName", out var dtProp) == true
                ? dtProp.GetString() : null;

            // --- Validate sizing parameters ---
            if (diameter.HasValue && (width.HasValue || height.HasValue))
                return new BridgeResponse(Success: false,
                    Error: "Specify either diameter (for round ducts) or width and height (for rectangular ducts), not both.");

            if ((width.HasValue && !height.HasValue) || (!width.HasValue && height.HasValue))
                return new BridgeResponse(Success: false,
                    Error: "Both width and height are required for rectangular ducts.");

            // --- Validate duct has non-zero length ---
            var startPt = new XYZ(startX, startY, startZ);
            var endPt = new XYZ(endX, endY, endZ);

            if (startPt.DistanceTo(endPt) < 0.001)
                return new BridgeResponse(Success: false,
                    Error: "Start and end points are too close together. Duct must have non-zero length.");

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

            // --- Find duct type ---
            using var dtCollector = new FilteredElementCollector(doc);
            var ductTypes = dtCollector.OfClass(typeof(DuctType)).Cast<DuctType>().ToList();

            if (ductTypes.Count == 0)
                return new BridgeResponse(Success: false, Error: "No duct types found in the project. Ensure an MEP template is loaded.");

            DuctType ductType;
            if (!string.IsNullOrEmpty(ductTypeName))
            {
                ductType = ductTypes.FirstOrDefault(dt =>
                    string.Equals(dt.Name, ductTypeName, StringComparison.OrdinalIgnoreCase))!;

                if (ductType is null)
                {
                    var available = string.Join(", ", ductTypes.Select(dt => dt.Name).Take(30));
                    return new BridgeResponse(Success: false,
                        Error: $"Duct type not found: '{ductTypeName}'. Available: {available}");
                }
            }
            else
            {
                ductType = ductTypes.First();
            }

            // --- Find system type (use first available) ---
            using var stCollector = new FilteredElementCollector(doc);
            var systemTypes = stCollector.OfClass(typeof(MechanicalSystemType))
                .Cast<MechanicalSystemType>().ToList();

            if (systemTypes.Count == 0)
                return new BridgeResponse(Success: false,
                    Error: "No mechanical system types found in the project. Ensure an MEP template is loaded.");

            var systemType = systemTypes.First();

            // --- Create the duct ---
            using var transaction = new Transaction(doc, "MCP: Create Duct");
            transaction.Start();

            try
            {
                var duct = Duct.Create(doc, systemType.Id, ductType.Id, level.Id, startPt, endPt);

                // Set sizing parameters (convert inches to feet)
                if (diameter.HasValue)
                {
                    var diamParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                    diamParam?.Set(diameter.Value / 12.0);
                }
                else if (width.HasValue && height.HasValue)
                {
                    var widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                    var heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                    widthParam?.Set(width.Value / 12.0);
                    heightParam?.Set(height.Value / 12.0);
                }

                transaction.Commit();

                var lengthParam = duct.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                var lengthFt = lengthParam != null ? Math.Round(lengthParam.AsDouble(), 4) : 0.0;

                var result = new
                {
                    Id = duct.Id.Value,
                    DuctType = ductType.Name,
                    SystemType = systemType.Name,
                    LevelName = level.Name,
                    LengthFt = lengthFt,
                    StartPoint = new { X = startX, Y = startY, Z = startZ },
                    EndPoint = new { X = endX, Y = endY, Z = endZ },
                    SizeInches = diameter.HasValue
                        ? $"{diameter.Value}\" round"
                        : width.HasValue
                            ? $"{width.Value}\"W x {height!.Value}\"H"
                            : "default"
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create duct: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
