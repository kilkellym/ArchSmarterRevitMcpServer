using System.Text.Json;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetElements"/> command.
/// Retrieves elements from the active Revit document, optionally filtered by category.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>category</c> (string, optional) – Built-in category name (e.g. "Walls", "Doors").</item>
///   <item><c>limit</c> (int, optional) – Maximum number of elements to return. Defaults to 100.</item>
/// </list>
/// </remarks>
public sealed class GetElementsHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetElements;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request)
    {
        try
        {
            var category = request.Payload?.TryGetProperty("category", out var catProp) == true
                ? catProp.GetString()
                : null;

            var limit = request.Payload?.TryGetProperty("limit", out var limitProp) == true
                ? limitProp.GetInt32()
                : 100;

            // TODO: Replace with actual Revit API calls once the Revit API package is referenced.
            // Implementation will use:
            //   var doc = commandData.Application.ActiveUIDocument.Document;
            //   using var collector = new FilteredElementCollector(doc);
            //   if (category != null)
            //       collector.OfCategory(ParseCategory(category));
            //   var elements = collector
            //       .WhereElementIsNotElementType()
            //       .Take(limit)
            //       .Select(e => new { Id = e.Id.Value, Name = SafeGetName(e), Category = e.Category?.Name })
            //       .ToList();

            var placeholder = JsonSerializer.SerializeToElement(new
            {
                message = "Handler registered. Revit API integration pending.",
                requestedCategory = category,
                limit
            });

            return new BridgeResponse(Success: true, Data: placeholder);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
