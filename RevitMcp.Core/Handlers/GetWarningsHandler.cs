using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetWarnings"/> command.
/// Returns all warnings (failure messages) in the active Revit document
/// including severity, description, and the element IDs involved.
/// </summary>
public sealed class GetWarningsHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetWarnings;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var warnings = doc.GetWarnings();

            var warningList = new List<Dictionary<string, object?>>();

            foreach (var warning in warnings)
            {
                var failingIds = warning.GetFailingElements()
                    .Select(id => id.Value)
                    .ToList();

                var additionalIds = warning.GetAdditionalElements()
                    .Select(id => id.Value)
                    .ToList();

                warningList.Add(new Dictionary<string, object?>
                {
                    ["Severity"] = warning.GetSeverity().ToString(),
                    ["Description"] = warning.GetDescriptionText(),
                    ["FailingElementIds"] = failingIds,
                    ["AdditionalElementIds"] = additionalIds
                });
            }

            var result = new Dictionary<string, object?>
            {
                ["TotalWarnings"] = warningList.Count,
                ["Warnings"] = warningList
            };

            var data = JsonSerializer.SerializeToElement(result);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
