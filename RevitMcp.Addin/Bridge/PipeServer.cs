#nullable enable
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RevitMcp.Core.Messages;

namespace RevitMcp.Addin.Bridge;

/// <summary>
/// Named-pipe server that listens on the "revit-mcp-bridge" pipe for
/// <see cref="BridgeRequest"/> messages from the MCP server process.
/// Each request is queued into the <see cref="RequestChannel"/> and an
/// <see cref="ExternalEvent"/> is raised so Revit dispatches the handler
/// on its main thread. The pipe thread awaits the result via a
/// <see cref="TaskCompletionSource{BridgeResponse}"/> and writes the
/// response back using length-prefixed JSON framing.
/// </summary>
internal sealed class PipeServer : IDisposable
{
    private const string PipeName = "revit-mcp-bridge";

    private readonly RequestChannel _channel;
    private readonly ExternalEvent _externalEvent;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// Creates a new pipe server.
    /// </summary>
    /// <param name="channel">The channel to queue incoming requests into.</param>
    /// <param name="externalEvent">The Revit <see cref="ExternalEvent"/> to raise after enqueuing.</param>
    public PipeServer(RequestChannel channel, ExternalEvent externalEvent)
    {
        _channel = channel;
        _externalEvent = externalEvent;
    }

    /// <summary>
    /// Starts listening for pipe connections on a background thread.
    /// </summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the pipe server and waits for the listen loop to exit.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // Expected on cancellation.
        }
    }

    /// <summary>
    /// Main listen loop. Accepts one client at a time, handles messages until
    /// the client disconnects, then waits for the next connection.
    /// </summary>
    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(ct);
                await HandleConnectionAsync(pipe, ct);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PipeServer] Connection error: {ex.Message}");
            }
            finally
            {
                pipe.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads requests from a connected pipe, queues each one through the channel,
    /// raises the external event, awaits the response, and writes it back.
    /// </summary>
    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            BridgeRequest request;
            try
            {
                request = await ReadMessageAsync<BridgeRequest>(pipe, ct);
            }
            catch (EndOfStreamException)
            {
                // Client disconnected cleanly.
                break;
            }

            var tcs = new TaskCompletionSource<BridgeResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            await _channel.Writer.WriteAsync(new PendingRequest(request, tcs), ct);
            _externalEvent.Raise();

            var response = await tcs.Task;
            await WriteMessageAsync(pipe, response, ct);
        }
    }

    // ── Length-prefixed JSON framing (mirrors RevitBridgeClient) ──────────

    /// <summary>
    /// Writes a 4-byte little-endian length prefix followed by the UTF-8 JSON payload.
    /// </summary>
    private static async Task WriteMessageAsync<T>(NamedPipeServerStream pipe, T message, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        var lengthPrefix = BitConverter.GetBytes(json.Length);

        await pipe.WriteAsync(lengthPrefix, 0, lengthPrefix.Length, ct);
        await pipe.WriteAsync(json, 0, json.Length, ct);
        await pipe.FlushAsync(ct);
    }

    /// <summary>
    /// Reads a 4-byte little-endian length prefix, then reads exactly that many
    /// bytes and deserializes the JSON payload.
    /// </summary>
    private static async Task<T> ReadMessageAsync<T>(NamedPipeServerStream pipe, CancellationToken ct)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(pipe, lengthBuffer, ct);
        var length = BitConverter.ToInt32(lengthBuffer, 0);

        var jsonBuffer = new byte[length];
        await ReadExactAsync(pipe, jsonBuffer, ct);

        return JsonSerializer.Deserialize<T>(jsonBuffer)
            ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from pipe.");
    }

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length bytes from the pipe.
    /// </summary>
    private static async Task ReadExactAsync(NamedPipeServerStream pipe, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await pipe.ReadAsync(buffer, offset, buffer.Length - offset, ct);
            if (read == 0)
                throw new EndOfStreamException("Pipe closed before all bytes were read.");
            offset += read;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
