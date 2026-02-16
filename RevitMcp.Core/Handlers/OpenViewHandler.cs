using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.OpenView"/> command.
/// Opens a view and makes it the active view using <see cref="UIDocument.RequestViewChange"/>.
/// </summary>
public sealed class OpenViewHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.OpenView;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            JsonElement viewIdProp = default;
            JsonElement viewNameProp = default;
            var hasViewId = request.Payload?.TryGetProperty("viewId", out viewIdProp) == true;
            var hasViewName = request.Payload?.TryGetProperty("viewName", out viewNameProp) == true;

            if (!hasViewId && !hasViewName)
                return new BridgeResponse(Success: false, Error: "Provide either viewId or viewName.");

            View? view = null;

            if (hasViewId)
            {
                var elementId = new ElementId((long)viewIdProp.GetInt64());
                view = doc.GetElement(elementId) as View;

                if (view is null)
                    return new BridgeResponse(Success: false, Error: $"View not found for ID: {viewIdProp.GetInt64()}");
            }
            else if (hasViewName)
            {
                var name = viewNameProp.GetString();
                if (string.IsNullOrEmpty(name))
                    return new BridgeResponse(Success: false, Error: "viewName cannot be empty.");

                using var collector = new FilteredElementCollector(doc);
                view = collector
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

                if (view is null)
                    return new BridgeResponse(Success: false, Error: $"View not found: '{name}'");
            }

            if (view!.IsTemplate)
                return new BridgeResponse(Success: false, Error: $"Cannot open a view template: '{view.Name}'");

            uiDoc.RequestViewChange(view);

            var result = new
            {
                Id = view.Id.Value,
                Name = view.Name,
                ViewType = view.ViewType.ToString()
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
