using System.ComponentModel;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools that operate on the Revit Family Editor. Every tool except
/// <c>is_family_document</c> requires a family (.rfa) to be the active document;
/// the add-in returns a structured error if a project document is active.
/// </summary>
[McpServerToolType]
public sealed class FamilyEditorTools
{
    /// <summary>
    /// Reports whether the active document is a family document.
    /// </summary>
    [McpServerTool(Name = "is_family_document"), Description(
        "Check whether the active Revit document is a family document (.rfa). " +
        "Returns true or false, and when true also returns the family category name and family name. " +
        "Call this first before attempting any other family editor tools, since those tools require " +
        "a family document to be open.")]
    public static async Task<string> IsFamilyDocument(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.IsFamilyDocument);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Returns high-level metadata about the active family document.
    /// </summary>
    [McpServerTool(Name = "get_family_info"), Description(
        "Get top level information about the active family document, including the family category, " +
        "placement type, parameter count, and type count. " +
        "Use this after is_family_document returns true to understand what kind of family you are " +
        "working with before reading elements or parameters in detail.")]
    public static async Task<string> GetFamilyInfo(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.GetFamilyInfo);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Lists geometric and reference elements in the active family document grouped by type.
    /// </summary>
    [McpServerTool(Name = "list_family_elements"), Description(
        "List all geometric and reference elements in the active family document, grouped by element type. " +
        "Returns element IDs, names, and element type (Extrusion, Sweep, Blend, Revolution, ReferencePlane, Dimension). " +
        "Use this to understand the current geometry of a family before adding, modifying, or flexing anything.")]
    public static async Task<string> ListFamilyElements(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.ListFamilyElements);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Returns all reference planes in the active family document with geometry.
    /// </summary>
    [McpServerTool(Name = "get_reference_planes"), Description(
        "Get all reference planes in the active family document with their geometry and reference settings. " +
        "Returns the plane ID, name, bubble end and free end coordinates, normal vector, and whether the plane " +
        "is named. All coordinates in decimal feet. " +
        "Use this when you need to dimension to or from reference planes, or before creating new geometry that " +
        "must align with existing references.")]
    public static async Task<string> GetReferencePlanes(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.GetReferencePlanes);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }

    /// <summary>
    /// Returns all family parameters in the active family document with metadata and current values.
    /// </summary>
    [McpServerTool(Name = "get_parameters"), Description(
        "Get all family parameters in the active family document with their metadata and current values. " +
        "Returns parameter name, storage type (String, Integer, Double, ElementId), whether the parameter " +
        "is an instance or type parameter, whether it is shared, its formula if present, and its current " +
        "value for the active type. Double values are in Revit internal units (decimal feet for lengths, " +
        "radians for angles). " +
        "Use this before creating new parameters to avoid duplicates, or before associating dimensions to parameters.")]
    public static async Task<string> GetParameters(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.GetParameters);

        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
