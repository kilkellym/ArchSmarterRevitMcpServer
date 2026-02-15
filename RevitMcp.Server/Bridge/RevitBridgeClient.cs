using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using RevitMcp.Core.Messages;

namespace RevitMcp.Server.Bridge;

/// <summary>
/// Named pipe client that sends <see cref="BridgeRequest"/> messages to the
/// Revit add-in and receives <see cref="BridgeResponse"/> messages back.
/// Uses length-prefixed JSON framing (4-byte little-endian prefix + UTF-8 JSON payload).
/// </summary>
public sealed class RevitBridgeClient : IAsyncDisposable
{
    private const string PipeName = "revit-mcp-bridge";
    private readonly NamedPipeClientStream _pipe;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>
    /// Creates a new bridge client. Call <see cref="ConnectAsync"/> before sending requests.
    /// </summary>
    public RevitBridgeClient()
    {
        _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    }

    /// <summary>
    /// Connects to the Revit add-in's named pipe server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _pipe.ConnectAsync(cancellationToken);
    }

    /// <summary>
    /// Sends a request to the Revit add-in and waits for the response.
    /// Thread-safe: concurrent calls are serialized via an internal lock.
    /// </summary>
    /// <param name="request">The bridge request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bridge response from the Revit add-in.</returns>
    public async Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken = default)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await WriteMessageAsync(request, cancellationToken);
            return await ReadMessageAsync<BridgeResponse>(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Writes a length-prefixed JSON message to the pipe.
    /// </summary>
    private async Task WriteMessageAsync<T>(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        var lengthPrefix = BitConverter.GetBytes(json.Length);

        await _pipe.WriteAsync(lengthPrefix, cancellationToken);
        await _pipe.WriteAsync(json, cancellationToken);
        await _pipe.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Reads a length-prefixed JSON message from the pipe.
    /// </summary>
    private async Task<T> ReadMessageAsync<T>(CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(lengthBuffer, cancellationToken);
        var length = BitConverter.ToInt32(lengthBuffer, 0);

        var jsonBuffer = new byte[length];
        await ReadExactAsync(jsonBuffer, cancellationToken);

        return JsonSerializer.Deserialize<T>(jsonBuffer)
            ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from pipe.");
    }

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length bytes from the pipe.
    /// </summary>
    private async Task ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _pipe.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("Pipe closed before all bytes were read.");
            offset += read;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _sendLock.Dispose();
        await _pipe.DisposeAsync();
    }
}
