#nullable enable

using RevitMcp.Addin;

namespace RevitMcp.Addin.Status;

/// <summary>
/// Singleton that tracks the current state of the MCP pipe server and client connection.
/// Updated by <see cref="Bridge.PipeServer"/> and read by the ribbon button's Idling handler
/// and the <see cref="Addin.McpStatusCommand"/> dialog.
/// </summary>
internal sealed class McpStatusTracker
{
    private static McpStatusTracker? _instance;

    /// <summary>
    /// Gets the shared singleton instance.
    /// </summary>
    public static McpStatusTracker Instance => _instance ??= new();

    /// <summary>Current state of the named-pipe server.</summary>
    public PipeServerState PipeState { get; set; } = PipeServerState.Stopped;

    /// <summary>Whether an MCP client is currently connected to the pipe.</summary>
    public bool IsClientConnected { get; set; }

    /// <summary>Total tool calls processed since the add-in loaded.</summary>
    public int TotalToolCallsProcessed { get; set; }

    /// <summary>Tool calls processed in the current session (resettable).</summary>
    public int ToolCallsThisSession { get; set; }

    /// <summary>Timestamp of the most recent tool call.</summary>
    public DateTime? LastToolCallTime { get; set; }

    /// <summary>Command name of the most recent tool call.</summary>
    public string? LastToolCallName { get; set; }

    /// <summary>Most recent error message, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Timestamp of the most recent error.</summary>
    public DateTime? LastErrorTime { get; set; }

    /// <summary>Timestamp when the pipe server was started.</summary>
    public DateTime? ServerStartTime { get; set; }

    /// <summary>
    /// Derives the overall connection status from the current pipe and client state.
    /// </summary>
    public ConnectionStatus OverallStatus
    {
        get
        {
            if (PipeState == PipeServerState.Error)
                return ConnectionStatus.Error;
            if (PipeState == PipeServerState.Stopped)
                return ConnectionStatus.Error;
            if (IsClientConnected)
                return ConnectionStatus.Connected;
            return ConnectionStatus.Waiting;
        }
    }

    /// <summary>
    /// Records a successful tool call.
    /// </summary>
    public void RecordToolCall(string toolName)
    {
        TotalToolCallsProcessed++;
        ToolCallsThisSession++;
        LastToolCallTime = DateTime.Now;
        LastToolCallName = toolName;
    }

    /// <summary>
    /// Records an error that occurred in the pipe server or handler dispatch.
    /// </summary>
    public void RecordError(string error)
    {
        LastError = error;
        LastErrorTime = DateTime.Now;
    }

    /// <summary>
    /// Resets the per-session counters and last-error state.
    /// </summary>
    public void Reset()
    {
        ToolCallsThisSession = 0;
        LastError = null;
        LastErrorTime = null;
    }
}

/// <summary>
/// Named-pipe server lifecycle states.
/// </summary>
internal enum PipeServerState
{
    /// <summary>Pipe server is not running.</summary>
    Stopped,

    /// <summary>Pipe server is running and listening for connections.</summary>
    Listening,

    /// <summary>Pipe server encountered an error and is not operational.</summary>
    Error
}

/// <summary>
/// Overall MCP connection status used to drive the ribbon icon color.
/// </summary>
internal enum ConnectionStatus
{
    /// <summary>Green: pipe running and client connected.</summary>
    Connected,

    /// <summary>Yellow: pipe running but no client connected.</summary>
    Waiting,

    /// <summary>Red: pipe failed or stopped.</summary>
    Error
}
