using Aonik.Infrastructure.Storage;
using Aonik.Platform.Contracts.Models.ContactEnquiries;

using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Tests.Storage;

public class ClamAvScannerTests
{
    [Fact]
    public async Task Scan_Should_StreamOriginalBytesInBoundedChunks_AndRequireCleanVerdict()
    {
        await using var server = new ScriptedClamAvServer();
        var content = Enumerable.Range(0, 150_000).Select(index => (byte)index).ToArray();

        await server.Scanner.ScanAsync(content);

        (await server.Received.Task).Should().Equal(content);
    }

    [Fact]
    public async Task Scan_Should_RejectInfectedContent_WithoutDisclosingScannerDetails()
    {
        await using var server = new ScriptedClamAvServer("stream: Private.Virus.Signature FOUND\0");

        var action = () => server.Scanner.ScanAsync(new byte[] { 1 });

        var error = (await action.Should().ThrowAsync<ContactImageValidationException>()).Which;
        error.Code.Should().Be("image_unsafe");
        error.ToString().Should().NotContain("Private.Virus");
    }

    [Theory]
    [InlineData("stream: size limit exceeded ERROR\0")]
    [InlineData("stream: NOT OK\0")]
    [InlineData("other: OK\0")]
    [InlineData("stream: OK")]
    [InlineData("")]
    public async Task Scan_Should_FailClosedOnAnyIncompleteOrUnknownVerdict(string response)
    {
        await using var server = new ScriptedClamAvServer(response);

        var action = () => server.Scanner.ScanAsync(new byte[] { 1 });

        await action.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }

    [Fact]
    public async Task Scan_Should_BoundProviderResponse()
    {
        await using var server = new ScriptedClamAvServer(new string('x', 1024));

        var action = () => server.Scanner.ScanAsync(new byte[] { 1 });

        await action.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }

    [Fact]
    public async Task Scan_Should_TimeOut_UnresponsiveScanner()
    {
        await using var server = new ScriptedClamAvServer(holdResponse: true);

        var action = () => server.Scanner.ScanAsync(new byte[] { 1 });

        await action.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }

    [Fact]
    public async Task Scan_Should_PreserveCallerCancellation_DuringProviderRead()
    {
        await using var server = new ScriptedClamAvServer(holdResponse: true);
        using var cancellation = new CancellationTokenSource();
        var pending = server.Scanner.ScanAsync(new byte[] { 1 }, cancellation.Token);
        await server.Received.Task;

        await cancellation.CancelAsync();

        await ((Func<Task>)(() => pending)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(false, "127.0.0.1", 3310, 10)]
    [InlineData(true, "", 3310, 10)]
    [InlineData(true, "8.8.8.8", 3310, 10)]
    [InlineData(true, "127.0.0.1", 0, 10)]
    [InlineData(true, "127.0.0.1", 3310, 31)]
    public async Task Scan_Should_RejectUnconfiguredOrNonPrivateEndpoint(bool enabled, string host, int port, int timeout)
    {
        var scanner = new ClamAvScanner(Options.Create(new ClamAvOptions
        {
            Enabled = enabled, Host = host, Port = port, TimeoutSeconds = timeout
        }));

        var action = () => scanner.ScanAsync(new byte[] { 1 });

        await action.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }
}
