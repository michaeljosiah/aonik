using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Options;

using Aonik.Infrastructure.Storage;

namespace Aonik.Infrastructure.Tests.Storage;

/// <summary>A local protocol peer; no real malware scanner or customer upload is contacted.</summary>
internal sealed class ScriptedClamAvServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping = new(TimeSpan.FromSeconds(10));
    private readonly Task _connection;
    public TaskCompletionSource<byte[]> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ClamAvScanner Scanner { get; }

    public ScriptedClamAvServer(string response = "stream: OK\0", bool holdResponse = false)
    {
        _listener.Start();
        Scanner = new ClamAvScanner(Options.Create(new ClamAvOptions
        {
            Enabled = true, Host = "127.0.0.1", Port = ((IPEndPoint)_listener.LocalEndpoint).Port, TimeoutSeconds = 1
        }));
        _connection = ServeAsync(response, holdResponse);
    }

    private async Task ServeAsync(string response, bool holdResponse)
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            await using var stream = client.GetStream();
            var command = new byte[10];
            await stream.ReadExactlyAsync(command, _stopping.Token);
            if (!command.AsSpan().SequenceEqual("zINSTREAM\0"u8))
                throw new InvalidOperationException("Incorrect INSTREAM command.");
            var header = new byte[4];
            using var content = new MemoryStream();
            while (true)
            {
                await stream.ReadExactlyAsync(header, _stopping.Token);
                var length = BinaryPrimitives.ReadInt32BigEndian(header);
                if (length == 0)
                    break;
                if (length is < 0 or > 64 * 1024)
                    throw new InvalidOperationException("Incorrect INSTREAM chunk length.");
                var chunk = new byte[length];
                await stream.ReadExactlyAsync(chunk, _stopping.Token);
                await content.WriteAsync(chunk, _stopping.Token);
            }
            Received.TrySetResult(content.ToArray());
            if (holdResponse)
                await Task.Delay(Timeout.InfiniteTimeSpan, _stopping.Token);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stopping.Token);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Received.TrySetException(exception);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        await _connection;
        _stopping.Dispose();
    }
}
