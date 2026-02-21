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
        "and the new value as a string. For numeric (Double) parameters with units, provide the value " +
        "in Revit internal units (decimal feet for length, square feet for area, etc.). " +
        "Returns the previous value and the new value for confirmation. " +
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

    /// <summary>
    /// Deletes one or more elements from the Revit model.
    /// </summary>
    [McpServerTool(Name = "delete_elements"), Description(
        "Delete one or more elements from the Revit model. " +
        "IMPORTANT: By default (confirm=false), this runs in preview mode and only shows what " +
        "would be deleted without actually deleting anything. Set confirm=true to perform the deletion. " +
        "Maximum 100 elements per call. Returns deleted element details and any errors.")]
    public static async Task<string> DeleteElements(
        RevitBridgeClient bridgeClient,
        [Description("Array of Revit element IDs to delete.")]
        long[] elementIds,
        [Description("Set to true to actually delete the elements. " +
                     "When false (default), runs in preview mode showing what would be deleted.")]
        bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { elementIds, confirm });

        var request = new BridgeRequest(
            Command: CommandNames.DeleteElements,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets elements visible in a specific view.
    /// </summary>
    [McpServerTool(Name = "get_elements_in_view"), Description(
        "Get elements visible in a specific Revit view. " +
        "Uses a view-scoped collector to return only elements that appear in the given view. " +
        "Optionally filter by category. " +
        "Returns element Id, Name, and Category for each element in the view.")]
    public static async Task<string> GetElementsInView(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID of the view to query.")]
        long viewId,
        [Description("Built-in category name to filter by (e.g. 'Walls', 'Doors'). Omit to return all categories.")]
        string? category = null,
        [Description("Maximum number of elements to return. Defaults to 200.")]
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { viewId, category, limit });
    /// Gets elements visible in a specific Revit view, optionally filtered by category and region.
    /// </summary>
    [McpServerTool(Name = "get_elements_in_view"), Description(
        "Get elements visible in a specific Revit view, optionally filtered by category and a rectangular region. " +
        "Uses a view-scoped FilteredElementCollector so only elements visible in the view are returned. " +
        "Optionally provide min/max coordinates in the view's coordinate system to limit results to a region. " +
        "Returns element ID, name, category, type name, and bounding box center for each element. " +
        "Use this to identify which elements a PDF markup is targeting based on its position in a view.")]
    public static async Task<string> GetElementsInView(
        RevitBridgeClient bridgeClient,
        [Description("Element ID of the view to query elements in.")]
        long viewId,
        [Description("Built-in category name to filter by (e.g. 'Walls', 'Conduits', 'ElectricalEquipment'). Omit to return all categories.")]
        string? category = null,
        [Description("Minimum X coordinate in decimal feet to define a search region. Must be paired with maxX, minY, maxY.")]
        double? minX = null,
        [Description("Minimum Y coordinate in decimal feet for the search region.")]
        double? minY = null,
        [Description("Maximum X coordinate in decimal feet for the search region.")]
        double? maxX = null,
        [Description("Maximum Y coordinate in decimal feet for the search region.")]
        double? maxY = null,
        [Description("Maximum number of elements to return. Defaults to 100.")]
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { viewId, category, minX, minY, maxX, maxY, limit });

        var request = new BridgeRequest(
            Command: CommandNames.GetElementsInView,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Finds elements by matching a parameter value.
    /// </summary>
    [McpServerTool(Name = "find_elements_by_parameter"), Description(
        "Find elements whose parameter value matches a search string. " +
        "Requires a parameter name and search value. " +
        "Supports match types: 'exact' (default), 'contains', 'startsWith', 'endsWith'. " +
        "Optionally filter by category. " +
        "Returns matched elements with their Id, Name, Category, and the matched parameter value.")]
    public static async Task<string> FindElementsByParameter(
        RevitBridgeClient bridgeClient,
        [Description("The exact parameter name to search (e.g. 'Mark', 'Comments', 'Type Name').")]
        string parameterName,
        [Description("The value to match against the parameter value.")]
        string value,
        [Description("Match type: 'exact' (default), 'contains', 'startsWith', or 'endsWith'.")]
        string matchType = "exact",
        [Description("Built-in category name to filter by (e.g. 'Walls', 'Doors'). Omit to search all categories.")]
        string? category = null,
        [Description("Maximum number of results to return. Defaults to 200.")]
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            parameterName, value, matchType, category, limit
        });

        var request = new BridgeRequest(
            Command: CommandNames.FindElementsByParameter,
    /// Moves one or more Revit elements by a translation vector.
    /// </summary>
    [McpServerTool(Name = "move_elements"), Description(
        "Move one or more Revit elements by a translation vector in decimal feet. " +
        "Supports preview mode (confirm=false) that shows the current and proposed positions " +
        "without moving anything. Set confirm=true to execute the move. " +
        "Use this for dimension corrections where an element needs to shift to a new position. " +
        "Returns the element's old and new locations. Maximum 50 elements per call.")]
    public static async Task<string> MoveElements(
        RevitBridgeClient bridgeClient,
        [Description("Array of element IDs to move.")]
        long[] elementIds,
        [Description("Translation distance along X axis in decimal feet.")]
        double deltaX,
        [Description("Translation distance along Y axis in decimal feet.")]
        double deltaY,
        [Description("Translation distance along Z axis in decimal feet. Defaults to 0.")]
        double deltaZ = 0.0,
        [Description("Set to true to execute the move. When false (default), runs in preview mode showing current and proposed positions.")]
        bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { elementIds, deltaX, deltaY, deltaZ, confirm });

        var request = new BridgeRequest(
            Command: CommandNames.MoveElements,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Sets a parameter value on multiple elements in a single transaction.
    /// </summary>
    [McpServerTool(Name = "batch_set_parameters"), Description(
        "Set a parameter value on multiple Revit elements in a single transaction. " +
        "IMPORTANT: By default (confirm=false), this runs in preview mode and shows current values " +
        "without modifying anything. Set confirm=true to apply changes. " +
        "Maximum 500 elements per call. For numeric (Double) parameters with units, " +
        "provide the value in Revit internal units (decimal feet for length, etc.). " +
        "Returns updated elements with old and new values, plus any errors.")]
    public static async Task<string> BatchSetParameters(
        RevitBridgeClient bridgeClient,
        [Description("Array of Revit element IDs to update.")]
        long[] elementIds,
        [Description("The exact parameter name to set (e.g. 'Comments', 'Mark').")]
        string parameterName,
        [Description("The new value as a string. Will be parsed to the correct type automatically.")]
        string value,
        [Description("Set to true to actually apply the changes. " +
                     "When false (default), runs in preview mode showing current values.")]
        bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            elementIds, parameterName, value, confirm
        });

        var request = new BridgeRequest(
            Command: CommandNames.BatchSetParameters,
    /// Searches for Revit elements by name, family name, type name, or mark value.
    /// </summary>
    [McpServerTool(Name = "find_elements_by_name"), Description(
        "Search for Revit elements by name, family name, type name, or mark value. " +
        "Supports partial matching and optional view scoping. " +
        "Returns element ID, name, category, type name, mark, and location for each match. " +
        "Use this when a PDF markup references an element by its designation or label rather than by position.")]
    public static async Task<string> FindElementsByName(
        RevitBridgeClient bridgeClient,
        [Description("Text to search for. Matches against element name, family name, type name, and the 'Mark' parameter. Case insensitive, supports partial matches.")]
        string searchText,
        [Description("Optional view ID to scope the search to elements visible in a specific view.")]
        long? viewId = null,
        [Description("Optional category filter (e.g. 'ElectricalEquipment', 'Walls').")]
        string? category = null,
        [Description("Maximum number of elements to return. Defaults to 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { searchText, viewId, category, limit });

        var request = new BridgeRequest(
            Command: CommandNames.FindElementsByName,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
