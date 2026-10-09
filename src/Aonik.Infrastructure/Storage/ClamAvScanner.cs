using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Options;

using Aonik.Platform.Contracts.Models.ContactEnquiries;

namespace Aonik.Infrastructure.Storage;

/// <summary>Streams bounded uploads to an explicitly configured private clamd endpoint.</summary>
internal sealed class ClamAvScanner(IOptions<ClamAvOptions> options)
{
    public async Task ScanAsync(byte[] content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Host) || settings.Host.Length > 253 ||
            settings.Port is < 1 or > 65535 || settings.TimeoutSeconds is < 1 or > 30)
            throw Unavailable();
        if (content.Length is 0 or > ContactImageProcessor.MaximumInputBytes)
            throw new ContactImageValidationException("image_size", "Choose an image no larger than 10 MB.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(settings.Host, timeout.Token);
            // clamd TCP has no authentication or TLS. Resolve once, validate, then connect to that IP.
            if (addresses.Length == 0 || addresses.Any(address => !IsPrivate(address)))
                throw Unavailable();
            using var client = new TcpClient(addresses[0].AddressFamily);
            await client.ConnectAsync(addresses[0], settings.Port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);

            var header = new byte[4];
            for (var offset = 0; offset < content.Length; offset += 64 * 1024)
            {
                var length = Math.Min(64 * 1024, content.Length - offset);
                BinaryPrimitives.WriteInt32BigEndian(header, length);
                await stream.WriteAsync(header, timeout.Token);
                await stream.WriteAsync(content.AsMemory(offset, length), timeout.Token);
            }
            Array.Clear(header);
            await stream.WriteAsync(header, timeout.Token);

            var response = new byte[1024];
            var lengthRead = 0;
            while (lengthRead < response.Length)
            {
                var read = await stream.ReadAsync(response.AsMemory(lengthRead, 1), timeout.Token);
                if (read == 0)
                    throw Unavailable();
                if (response[lengthRead] == 0)
                {
                    var verdict = Encoding.ASCII.GetString(response, 0, lengthRead);
                    if (verdict == "stream: OK")
                        return;
                    if (verdict.StartsWith("stream: ", StringComparison.Ordinal) &&
                        verdict.EndsWith(" FOUND", StringComparison.Ordinal))
                        throw new ContactImageValidationException("image_unsafe", "This image did not pass the security check. Choose another image.");
                    throw Unavailable();
                }
                lengthRead++;
            }
            throw Unavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable();
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            // Provider responses and socket diagnostics must not appear in public errors or logs.
            throw Unavailable();
        }
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address))
            return true;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork
            ? bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168
            : (bytes[0] & 0xfe) == 0xfc;
    }

    private static ContactEnquiryUnavailableException Unavailable() =>
        new("Image security checks are temporarily unavailable. Please try again later.");
}
