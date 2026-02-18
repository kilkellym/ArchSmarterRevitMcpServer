using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for reading Revit element parameters.
/// </summary>
[McpServerToolType]
public sealed class ParameterTools
{
    /// <summary>
    /// Gets a single parameter value from a Revit element by parameter name.
    /// </summary>
    [McpServerTool(Name = "get_parameter_value"), Description(
        "Get a single parameter value from a Revit element by parameter name. " +
        "More efficient than get_element_parameters when you only need one specific value. " +
        "Checks both instance and type parameters. " +
        "Returns the parameter value, storage type, and whether it is read-only. " +
        "If the parameter is not found, lists available parameter names.")]
    public static async Task<string> GetParameterValue(
        RevitBridgeClient bridgeClient,
        [Description("The Revit element ID.")]
        long elementId,
        [Description("The exact parameter name as shown in Revit properties (e.g. 'Area', 'Volume', 'Mark').")]
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { elementId, parameterName });

        var request = new BridgeRequest(
            Command: CommandNames.GetParameterValue,
            Payload: payload);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
