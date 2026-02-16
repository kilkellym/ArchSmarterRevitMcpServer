using System.Text.Json;

namespace RevitMcp.Core.Messages;

/// <summary>
/// Response sent from the Revit add-in back to the MCP server over the named pipe bridge.
/// </summary>
/// <param name="Success">Whether the command executed successfully.</param>
/// <param name="Data">Optional JSON payload containing the command result.</param>
/// <param name="Error">Error message if <paramref name="Success"/> is false.</param>
public sealed record BridgeResponse(
    bool Success,
    JsonElement? Data = null,
    string? Error = null);
