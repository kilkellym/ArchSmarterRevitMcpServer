using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;
using RevitMcp.Server.Bridge;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tools for diagnosing the MCP server &lt;-&gt; Revit add-in connection.
/// </summary>
[McpServerToolType]
public sealed class DiagnosticTools
{
    /// <summary>
    /// How long to wait for the Revit add-in to respond to a ping before giving up.
    /// Short enough that the tool returns quickly when Revit is not running.
    /// </summary>
    private static readonly TimeSpan PingTimeout = TimeSpan.FromMilliseconds(2500);

    /// <summary>
    /// Reports whether the MCP server, Revit add-in, and Revit session are reachable.
    /// </summary>
    [McpServerTool(Name = "ping_revit"), Description(
        "Check the connection between the MCP server, the Revit add-in, and the current Revit session. " +
        "Returns whether the MCP server is reachable, whether the Revit add-in responded, the Revit version, " +
        "the active document name and type (Project, Family, or None), and the round trip latency in milliseconds. " +
        "Use this as the first call when debugging any connection issue, or to confirm the environment is ready " +
        "before running other Revit tools. This tool never throws: if Revit is not running, it returns " +
        "revitConnected=false with other fields null.")]
    public static async Task<string> PingRevit(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(PingTimeout);

        var stopwatch = Stopwatch.StartNew();
        var request = new BridgeRequest(Command: CommandNames.PingRevit);

        try
        {
            var response = await bridgeClient.SendAsync(request, timeoutCts.Token);
            stopwatch.Stop();

            if (!response.Success || response.Data is null)
            {
                return BuildResult(
                    revitConnected: false,
                    revitVersion: null,
                    activeDocument: null,
                    documentType: null,
                    pipeLatencyMs: (int)stopwatch.ElapsedMilliseconds,
                    error: response.Error);
            }

            var data = response.Data.Value;
            var revitVersion = TryGetString(data, "revitVersion");
            var activeDocument = TryGetString(data, "activeDocument");
            var documentType = TryGetString(data, "documentType");

            return BuildResult(
                revitConnected: true,
                revitVersion: revitVersion,
                activeDocument: activeDocument,
                documentType: documentType,
                pipeLatencyMs: (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Any failure below this layer (pipe not found, timeout, cancellation,
            // deserialization) is reported as revitConnected=false rather than thrown.
            stopwatch.Stop();
            return BuildResult(
                revitConnected: false,
                revitVersion: null,
                activeDocument: null,
                documentType: null,
                pipeLatencyMs: (int)stopwatch.ElapsedMilliseconds,
                error: ex.Message);
        }
    }

    private static string BuildResult(
        bool revitConnected,
        string? revitVersion,
        string? activeDocument,
        string? documentType,
        int pipeLatencyMs,
        string? error = null)
    {
        var payload = new
        {
            serverReachable = true,
            revitConnected,
            revitVersion,
            activeDocument,
            documentType,
            pipeLatencyMs,
            error
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string? TryGetString(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var prop))
            return null;
        if (prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
            return null;
        return prop.GetString();
    }
}
