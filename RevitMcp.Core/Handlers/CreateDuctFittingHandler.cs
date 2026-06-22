using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateDuctFitting"/> command.
/// Creates an elbow, tee, or transition fitting connecting existing duct elements.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>fittingType</c> (string, required) – One of "elbow", "tee", or "transition".</item>
///   <item><c>elementId1</c> (long, required) – First duct element ID.</item>
///   <item><c>elementId2</c> (long, required) – Second duct element ID.</item>
///   <item><c>elementId3</c> (long, optional) – Third duct element ID (required for tee fittings).</item>
/// </list>
/// </remarks>
public sealed class CreateDuctFittingHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateDuctFitting;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // --- Extract parameters ---
            if (request.Payload?.TryGetProperty("fittingType", out var ftProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: fittingType");
            if (request.Payload?.TryGetProperty("elementId1", out var id1Prop) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementId1");
            if (request.Payload?.TryGetProperty("elementId2", out var id2Prop) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementId2");

            var fittingType = ftProp.GetString()?.ToLowerInvariant();
            var elementId1 = id1Prop.GetInt64();
            var elementId2 = id2Prop.GetInt64();

            if (fittingType is not ("elbow" or "tee" or "transition"))
                return new BridgeResponse(Success: false,
                    Error: $"Invalid fittingType: '{fittingType}'. Must be 'elbow', 'tee', or 'transition'.");

            long? elementId3 = null;
            if (request.Payload?.TryGetProperty("elementId3", out var id3Prop) == true)
                elementId3 = id3Prop.GetInt64();

            if (fittingType == "tee" && !elementId3.HasValue)
                return new BridgeResponse(Success: false,
                    Error: "elementId3 is required for tee fittings.");

            // --- Look up elements ---
            var elem1 = doc.GetElement(new ElementId(elementId1));
            var elem2 = doc.GetElement(new ElementId(elementId2));

            if (elem1 is null)
                return new BridgeResponse(Success: false, Error: $"Element not found: {elementId1}");
            if (elem2 is null)
                return new BridgeResponse(Success: false, Error: $"Element not found: {elementId2}");

            var connectors1 = GetHvacConnectors(elem1);
            var connectors2 = GetHvacConnectors(elem2);

            if (connectors1.Count == 0)
                return new BridgeResponse(Success: false, Error: $"Element {elementId1} has no HVAC connectors.");
            if (connectors2.Count == 0)
                return new BridgeResponse(Success: false, Error: $"Element {elementId2} has no HVAC connectors.");

            // --- Create fitting ---
            using var transaction = new Transaction(doc, "MCP: Create Duct Fitting");
            transaction.Start();

            try
            {
                FamilyInstance fitting;

                switch (fittingType)
                {
                    case "elbow":
                    {
                        var (c1, c2) = FindNearestUnconnectedPair(connectors1, connectors2);
                        if (c1 is null || c2 is null)
                            return RollBackAndError(transaction, "No unconnected HVAC connector pair found between the two elements.");

                        fitting = doc.Create.NewElbowFitting(c1, c2);
                        break;
                    }
                    case "transition":
                    {
                        var (c1, c2) = FindNearestUnconnectedPair(connectors1, connectors2);
                        if (c1 is null || c2 is null)
                            return RollBackAndError(transaction, "No unconnected HVAC connector pair found between the two elements.");

                        fitting = doc.Create.NewTransitionFitting(c1, c2);
                        break;
                    }
                    case "tee":
                    {
                        var elem3 = doc.GetElement(new ElementId(elementId3!.Value));
                        if (elem3 is null)
                            return RollBackAndError(transaction, $"Element not found: {elementId3}");

                        var connectors3 = GetHvacConnectors(elem3);
                        if (connectors3.Count == 0)
                            return RollBackAndError(transaction, $"Element {elementId3} has no HVAC connectors.");

                        var (c1, c2) = FindNearestUnconnectedPair(connectors1, connectors2);
                        if (c1 is null || c2 is null)
                            return RollBackAndError(transaction, "No unconnected HVAC connector pair found between elements 1 and 2.");

                        var (_, c3) = FindNearestUnconnectedPair(connectors1, connectors3);
                        if (c3 is null)
                            return RollBackAndError(transaction, "No unconnected HVAC connector found on element 3.");

                        fitting = doc.Create.NewTeeFitting(c1, c2, c3);
                        break;
                    }
                    default:
                        return RollBackAndError(transaction, $"Unknown fitting type: {fittingType}");
                }

                transaction.Commit();

                var result = new
                {
                    Id = fitting.Id.Value,
                    FittingType = fittingType,
                    FamilyName = fitting.Symbol.Family.Name,
                    TypeName = fitting.Symbol.Name
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create {fittingType} fitting: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    private static List<Connector> GetHvacConnectors(Element element)
    {
        ConnectorSet? connectorSet = null;

        if (element is MEPCurve mepCurve)
            connectorSet = mepCurve.ConnectorManager?.Connectors;
        else if (element is FamilyInstance fi)
            connectorSet = fi.MEPModel?.ConnectorManager?.Connectors;

        if (connectorSet is null)
            return [];

        var result = new List<Connector>();
        foreach (Connector c in connectorSet)
        {
            if (c.Domain == Domain.DomainHvac)
                result.Add(c);
        }
        return result;
    }

    private static (Connector?, Connector?) FindNearestUnconnectedPair(
        List<Connector> set1, List<Connector> set2)
    {
        Connector? best1 = null;
        Connector? best2 = null;
        var bestDist = double.MaxValue;

        foreach (var c1 in set1)
        {
            if (c1.IsConnected) continue;

            foreach (var c2 in set2)
            {
                if (c2.IsConnected) continue;

                var dist = c1.Origin.DistanceTo(c2.Origin);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best1 = c1;
                    best2 = c2;
                }
            }
        }

        return (best1, best2);
    }

    private static BridgeResponse RollBackAndError(Transaction transaction, string error)
    {
        if (transaction.GetStatus() == TransactionStatus.Started)
            transaction.RollBack();
        return new BridgeResponse(Success: false, Error: error);
    }
}
