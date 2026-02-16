using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for querying Revit elements.
/// Each method sends a <see cref="BridgeRequest"/> to the Revit add-in and returns the result.
/// </summary>
[McpServerToolType]
public sealed class ElementTools
{
    /// <summary>
    /// Retrieves elements from the active Revit document.
    /// Returns element Id, Name, and Category for each matched element.
    /// </summary>
    /// <param name="bridgeClient">The bridge client injected by the DI container.</param>
    /// <param name="category">
    /// Optional built-in category name to filter by (e.g. "Walls", "Doors", "Windows").
    /// When omitted, returns elements across all categories.
    /// </param>
    /// <param name="limit">
    /// Maximum number of elements to return. Defaults to 100.
    /// Use a smaller value for faster responses on large models.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON array of elements with Id, Name, and Category properties.</returns>
    [McpServerTool(Name = "get_elements"), Description(
        "Retrieves elements from the active Revit document. " +
        "Optionally filter by category (e.g. 'Walls', 'Doors'). " +
        "Returns element Id, Name, and Category for each match.")]
    public static async Task<string> GetElements(
        RevitBridgeClient bridgeClient,
        [Description("Built-in category name to filter by (e.g. 'Walls', 'Doors'). Omit to return all categories.")]
        string? category = null,
        [Description("Maximum number of elements to return. Defaults to 100.")]
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { category, limit });

        var request = new BridgeRequest(
            Command: CommandNames.GetElements,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets all parameters and their values for a specific Revit element.
    /// </summary>
    [McpServerTool(Name = "get_element_parameters"), Description(
        "Get all parameters and their values for a specific Revit element. " +
        "Returns parameter name, value, storage type (String, Integer, Double, ElementId), " +
        "and whether it is a type or instance parameter. " +
        "Use this after get_elements or get_element_by_id to inspect an element's properties.")]
    public static async Task<string> GetElementParameters(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID obtained from get_elements or get_element_by_id.")]
        int elementId,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { elementId });

        var request = new BridgeRequest(
            Command: CommandNames.GetElementParameters,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets detailed information about a single Revit element by its ID.
    /// </summary>
    [McpServerTool(Name = "get_element_by_id"), Description(
        "Get detailed information about a single Revit element by its ID. " +
        "Returns the element's name, category, level, type name, bounding box coordinates, " +
        "and location. Use this when you need full details about a specific element " +
        "after discovering it through get_elements or count_elements.")]
    public static async Task<string> GetElementById(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID.")]
        int elementId,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { elementId });

        var request = new BridgeRequest(
            Command: CommandNames.GetElementById,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Sets a parameter value on a Revit element.
    /// </summary>
    [McpServerTool(Name = "set_parameter"), Description(
        "Set a parameter value on a Revit element. Requires the element ID, the parameter name, " +
        "and the new value. The value will be converted to the appropriate type based on the " +
        "parameter's StorageType. Returns the previous value and the new value for confirmation. " +
        "This modifies the model and requires a Transaction.")]
    public static async Task<string> SetParameter(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID.")]
        int elementId,
        [Description("The exact parameter name as shown in Revit properties.")]
        string parameterName,
        [Description("The new value as a string. Will be parsed to the correct type automatically.")]
        string value,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { elementId, parameterName, value });

        var request = new BridgeRequest(
            Command: CommandNames.SetParameter,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets the currently selected elements in the active Revit view.
    /// </summary>
    [McpServerTool(Name = "get_selected_elements"), Description(
        "Get the currently selected elements in the active Revit view. " +
        "Returns the element ID, name, category, type name, level, and location for each " +
        "selected element. If nothing is selected, returns an empty list with a message. " +
        "Use this when the user says 'selected elements', 'current selection', or " +
        "'what I have selected' to understand what they are working with.")]
    public static async Task<string> GetSelectedElements(
        RevitBridgeClient bridgeClient,
        [Description("If true, include all instance parameters for each selected element. " +
                     "If false, return only basic element info. Set to false for large selections " +
                     "to keep the response concise.")]
        bool includeParameters = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { includeParameters });

        var request = new BridgeRequest(
            Command: CommandNames.GetSelectedElements,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
