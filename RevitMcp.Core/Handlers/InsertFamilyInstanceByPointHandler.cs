using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.InsertFamilyInstanceByPoint"/> command.
/// Inserts a component family instance at a specified point on a level.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>familyName</c> (string, required) – The family name.</item>
///   <item><c>x</c> (double, required) – X coordinate in millimeters.</item>
///   <item><c>y</c> (double, required) – Y coordinate in millimeters.</item>
///   <item><c>levelName</c> (string, required) – The level name.</item>
///   <item><c>typeName</c> (string, optional) – The type name within the family.</item>
///   <item><c>z</c> (double, optional) – Z coordinate in millimeters. Defaults to 0.</item>
///   <item><c>structural</c> (bool, optional) – Whether to place as structural.</item>
/// </list>
/// </remarks>
public sealed class InsertFamilyInstanceByPointHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.InsertFamilyInstanceByPoint;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // --- Extract required parameters ---
            if (request.Payload?.TryGetProperty("familyName", out var fnProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: familyName");
            if (request.Payload?.TryGetProperty("x", out var xProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: x");
            if (request.Payload?.TryGetProperty("y", out var yProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: y");
            if (request.Payload?.TryGetProperty("levelName", out var levelProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: levelName");

            var familyName = fnProp.GetString();
            if (string.IsNullOrEmpty(familyName))
                return new BridgeResponse(Success: false, Error: "familyName cannot be empty.");

            var xMm = xProp.GetDouble();
            var yMm = yProp.GetDouble();
            var levelName = levelProp.GetString();

            if (string.IsNullOrEmpty(levelName))
                return new BridgeResponse(Success: false, Error: "levelName cannot be empty.");

            var typeName = request.Payload?.TryGetProperty("typeName", out var tnProp) == true
                ? tnProp.GetString() : null;
            var zMm = request.Payload?.TryGetProperty("z", out var zProp) == true
                ? zProp.GetDouble() : 0.0;
            var structural = request.Payload?.TryGetProperty("structural", out var strProp) == true
                && strProp.GetBoolean();

            // --- Convert mm to feet ---
            var xFt = UnitUtils.ConvertToInternalUnits(xMm, UnitTypeId.Millimeters);
            var yFt = UnitUtils.ConvertToInternalUnits(yMm, UnitTypeId.Millimeters);
            var zFt = UnitUtils.ConvertToInternalUnits(zMm, UnitTypeId.Millimeters);

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

            // --- Find FamilySymbol ---
            using var fsCollector = new FilteredElementCollector(doc);
            var allSymbols = fsCollector.OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();

            // Filter by family name
            var matchingSymbols = allSymbols
                .Where(fs => string.Equals(fs.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingSymbols.Count == 0)
            {
                // List distinct family names to help
                var familyNames = allSymbols
                    .Select(fs => fs.FamilyName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .Take(50)
                    .ToList();

                var available = string.Join(", ", familyNames);
                return new BridgeResponse(Success: false,
                    Error: $"Family not found: '{familyName}'. Available families (first 50): {available}");
            }

            FamilySymbol familySymbol;
            if (!string.IsNullOrEmpty(typeName))
            {
                familySymbol = matchingSymbols.FirstOrDefault(fs =>
                    string.Equals(fs.Name, typeName, StringComparison.OrdinalIgnoreCase))!;

                if (familySymbol is null)
                {
                    var availableTypes = string.Join(", ", matchingSymbols.Select(fs => fs.Name));
                    return new BridgeResponse(Success: false,
                        Error: $"Type '{typeName}' not found in family '{familyName}'. " +
                               $"Available types: {availableTypes}");
                }
            }
            else
            {
                familySymbol = matchingSymbols.First();
            }

            // --- Create the family instance ---
            var structuralType = structural ? StructuralType.Column : StructuralType.NonStructural;
            var location = new XYZ(xFt, yFt, zFt);

            using var transaction = new Transaction(doc, "MCP: Insert Family Instance");
            transaction.Start();

            try
            {
                if (!familySymbol.IsActive)
                    familySymbol.Activate();

                var instance = doc.Create.NewFamilyInstance(location, familySymbol, level, structuralType);

                transaction.Commit();

                var result = new
                {
                    Id = instance.Id.Value,
                    FamilyName = familySymbol.FamilyName,
                    TypeName = familySymbol.Name,
                    LevelName = level.Name,
                    LocationMm = new { X = xMm, Y = yMm, Z = zMm }
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false,
                    Error: $"Failed to insert family instance: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
