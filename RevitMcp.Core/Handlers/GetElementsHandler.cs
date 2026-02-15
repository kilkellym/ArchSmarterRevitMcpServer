using System.Text.Json;
using Autodesk.Revit.DB;
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
    public BridgeResponse Handle(BridgeRequest request, Document doc)
    {
        try
        {
            var category = request.Payload?.TryGetProperty("category", out var catProp) == true
                ? catProp.GetString()
                : null;

            var limit = request.Payload?.TryGetProperty("limit", out var limitProp) == true
                ? limitProp.GetInt32()
                : 100;

            using var collector = new FilteredElementCollector(doc);
            collector.WhereElementIsNotElementType();

            if (category is not null)
            {
                if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", out var bic))
                    return new BridgeResponse(Success: false, Error: $"Unknown category: {category}");

                collector.OfCategory(bic);
            }

            var elements = collector
                .Take(limit)
                .Select(e => new
                {
                    Id = e.Id.Value,
                    Name = SafeGetName(e),
                    Category = e.Category?.Name
                })
                .ToList();

            var data = JsonSerializer.SerializeToElement(elements);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Safely reads <see cref="Element.Name"/>. Some element types throw when
    /// the Name property is accessed.
    /// </summary>
    private static string? SafeGetName(Element element)
    {
        try
        {
            return element.Name;
        }
        catch
        {
            return null;
        }
    }
}
