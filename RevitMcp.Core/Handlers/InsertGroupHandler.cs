using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.InsertGroup"/> command.
/// Inserts a group instance at a specified point.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>groupName</c> (string, required) – The group type name.</item>
///   <item><c>x</c> (double, required) – X coordinate in decimal feet.</item>
///   <item><c>y</c> (double, required) – Y coordinate in decimal feet.</item>
///   <item><c>z</c> (double, optional) – Z coordinate in decimal feet. Defaults to 0.</item>
/// </list>
/// </remarks>
public sealed class InsertGroupHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.InsertGroup;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("groupName", out var nameProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: groupName");
            if (request.Payload?.TryGetProperty("x", out var xProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: x");
            if (request.Payload?.TryGetProperty("y", out var yProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: y");

            var groupName = nameProp.GetString();
            if (string.IsNullOrEmpty(groupName))
                return new BridgeResponse(Success: false, Error: "groupName cannot be empty.");

            var x = xProp.GetDouble();
            var y = yProp.GetDouble();
            var z = request.Payload?.TryGetProperty("z", out var zProp) == true
                ? zProp.GetDouble() : 0.0;

            // Find GroupType by name
            using var collector = new FilteredElementCollector(doc);
            var groupTypes = collector.OfClass(typeof(GroupType)).Cast<GroupType>().ToList();

            if (groupTypes.Count == 0)
                return new BridgeResponse(Success: false, Error: "No group types found in the project.");

            var groupType = groupTypes.FirstOrDefault(g =>
                string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));

            if (groupType is null)
            {
                var available = string.Join(", ", groupTypes.Select(g => g.Name).Take(30));
                return new BridgeResponse(Success: false,
                    Error: $"Group type not found: '{groupName}'. Available: {available}");
            }

            using var transaction = new Transaction(doc, "MCP: Insert Group");
            transaction.Start();

            try
            {
                var location = new XYZ(x, y, z);
                var group = doc.Create.PlaceGroup(location, groupType);

                transaction.Commit();

                var result = new
                {
                    Id = group.Id.Value,
                    GroupTypeName = groupType.Name,
                    LocationFt = new { X = x, Y = y, Z = z }
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to insert group: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
