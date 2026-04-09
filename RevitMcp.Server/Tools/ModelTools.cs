using System.ComponentModel;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for model-level analysis and view queries.
/// </summary>
[McpServerToolType]
public sealed class ModelTools
{
    /// <summary>
    /// Analyzes the overall complexity and composition of the active Revit model.
    /// </summary>
    [McpServerTool(Name = "analyze_model_statistics"), Description(
        "Analyze the overall complexity and composition of the active Revit model. " +
        "Returns element counts broken down by category, by family, by type, and by level. " +
        "Use this as a first step to understand what a model contains before drilling into specific elements.")]
    public static async Task<string> AnalyzeModelStatistics(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.AnalyzeModelStatistics);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets information about the currently active view in Revit.
    /// </summary>
    [McpServerTool(Name = "get_current_view_info"), Description(
        "Get information about the currently active view in Revit. " +
        "Returns the view name, view type (FloorPlan, Section, ThreeD, Elevation, Schedule, Sheet, etc.), " +
        "scale, detail level, associated level name and elevation if applicable, and whether it is a template. " +
        "Use this to understand what the user is currently looking at before making recommendations or queries " +
        "scoped to their active context.")]
    public static async Task<string> GetCurrentViewInfo(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.GetCurrentViewInfo);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Gets all warnings in the active Revit document.
    /// </summary>
    [McpServerTool(Name = "get_warnings"), Description(
        "Get all warnings (failure messages) in the active Revit document. " +
        "Returns each warning's severity, description text, failing element IDs, and additional element IDs. " +
        "Also returns the total warning count. Use this to audit model health, identify issues, " +
        "or find elements that need attention.")]
    public static async Task<string> GetWarnings(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.GetWarnings);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
