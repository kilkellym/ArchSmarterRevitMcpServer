using System.Text.Json;

namespace RevitMcp.Core.Messages;

/// <summary>
/// Request sent from the MCP server to the Revit add-in over the named pipe bridge.
/// </summary>
/// <param name="Command">The command name that maps to an <see cref="Handlers.ICommandHandler"/>.</param>
/// <param name="Payload">Optional JSON payload containing command-specific parameters.</param>
public sealed record BridgeRequest(
    string Command,
    JsonElement? Payload = null);
