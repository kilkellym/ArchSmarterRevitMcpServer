using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetElementsInView"/> command.
/// Returns elements visible in a specific view using a view-scoped FilteredElementCollector.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>viewId</c> (long, required) – The view to query elements from.</item>
///   <item><c>category</c> (string, optional) – Built-in category name to filter by.</item>
///   <item><c>limit</c> (int, optional) – Maximum results. Defaults to 200.</item>
/// </list>
/// </remarks>
public sealed class GetElementsInViewHandler : ICommandHandler
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 2000;

    /// <inheritdoc />
    public string Command => CommandNames.GetElementsInView;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("viewId", out var viewIdProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: viewId");

            var viewId = new ElementId(viewIdProp.GetInt64());
            var view = doc.GetElement(viewId) as View;
            if (view is null)
                return new BridgeResponse(Success: false, Error: $"View not found: {viewIdProp.GetInt64()}");

            if (view.IsTemplate)
                return new BridgeResponse(Success: false, Error: "Cannot query elements from a view template.");

            var category = request.Payload?.TryGetProperty("category", out var catProp) == true
                ? catProp.GetString() : null;

            var limit = request.Payload?.TryGetProperty("limit", out var limitProp) == true
                ? Math.Min(limitProp.GetInt32(), MaxLimit) : DefaultLimit;

            // Use the view-scoped FilteredElementCollector
            using var collector = new FilteredElementCollector(doc, viewId);
            collector.WhereElementIsNotElementType();

            if (category is not null)
            {
                if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", out var bic))
                    return new BridgeResponse(Success: false, Error: $"Unknown category: {category}");
                collector.OfCategory(bic);
            }

            var elements = new List<object>();
            foreach (var element in collector)
            {
                if (elements.Count >= limit)
                    break;

                string? name = null;
                try { name = element.Name; } catch { /* some elements throw on Name */ }

                elements.Add(new
                {
                    Id = element.Id.Value,
                    Name = name,
                    Category = element.Category?.Name
                });
            }

            var result = new
            {
                ViewId = view.Id.Value,
                ViewName = view.Name,
                ViewType = view.ViewType.ToString(),
                ElementCount = elements.Count,
                Truncated = elements.Count >= limit,
                Elements = elements
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
