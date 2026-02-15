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
}
