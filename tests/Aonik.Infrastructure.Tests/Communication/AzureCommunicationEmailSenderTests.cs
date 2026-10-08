using Azure;
using Azure.Communication.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

using Aonik.Infrastructure.Communication;
using Aonik.Infrastructure.Communication.Configuration;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using EmailMessage = Aonik.Platform.Contracts.Services.Messaging.EmailMessage;
using SdkEmailMessage = Azure.Communication.Email.EmailMessage;

namespace Aonik.Infrastructure.Tests.Communication;

public class AzureCommunicationEmailSenderTests
{
    private const string LegacyConnection = "Communication.Azure.ConnectionString";
    private const string LegacyFrom = "Communication.Azure.Email.FromAddress";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_Should_UseTenantSettingsAndPreserveTheMessageAndCancellation(bool html)
    {
        var h = new Harness();
        h.ConfigureTenant("tenant-connection", "tenant@example.test");
        h.TenantValues[(h.Tenants.TenantId!.Value, CommunicationSettingNames.EmailProvider)] = "AzureCommunicationServices";
        h.GlobalValues[CommunicationSettingNames.EmailProvider] = "OtherProvider";
        h.GlobalValues[CommunicationSettingNames.EmailAzureConnectionString] = "global-connection";
        h.GlobalValues[CommunicationSettingNames.EmailAzureFromAddress] = "global@example.test";
        using var cancellation = new CancellationTokenSource();
        var message = new EmailMessage("buyer@example.test", "Order confirmed", "Customer content", html);

        await h.Sender.SendAsync(message, cancellation.Token);

        h.Connections.Should().Equal("tenant-connection");
        var sent = h.Sent.Should().ContainSingle().Which;
        sent.Message.SenderAddress.Should().Be("tenant@example.test");
        sent.Message.Recipients.To.Should().ContainSingle().Which.Address.Should().Be(message.To);
        sent.Message.Content.Subject.Should().Be(message.Subject);
        sent.Message.Content.Html.Should().Be(html ? message.Body : null);
        sent.Message.Content.PlainText.Should().Be(html ? null : message.Body);
        sent.Cancellation.Should().Be(cancellation.Token);
        h.Global.Verify(settings => settings.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.TenantStore.Verify(settings => settings.GetTenantValueAsync(It.IsAny<string>(), h.Tenants.TenantId.Value,
            cancellation.Token), Times.Exactly(3));
    }

    [Fact]
    public async Task Send_Should_ReadTheCurrentTenantAndRotatedValues_OnEveryCall()
    {
        var h = new Harness();
        var tenantA = h.Tenants.TenantId;
        h.ConfigureTenant("connection-a", "a@example.test");
        await h.Sender.SendAsync(Message());
        h.Tenants.TenantId = Guid.NewGuid();
        h.ConfigureTenant("connection-b", "b@example.test");
        await h.Sender.SendAsync(Message());
        h.Tenants.TenantId = tenantA;
        h.ConfigureTenant("rotated-a", "new-a@example.test");

        await h.Sender.SendAsync(Message());

        h.Connections.Should().Equal("connection-a", "connection-b", "rotated-a");
        h.Sent.Select(sent => sent.Message.SenderAddress).Should()
            .Equal("a@example.test", "b@example.test", "new-a@example.test");
    }

    [Fact]
    public async Task Send_Should_AllowSharedGlobalCredentials_WithATenantOwnedFromAddress()
    {
        var h = new Harness();
        h.GlobalValues[CommunicationSettingNames.EmailAzureConnectionString] = "shared-connection";
        h.GlobalValues[CommunicationSettingNames.EmailAzureFromAddress] = "platform@example.test";
        h.TenantValues[(h.Tenants.TenantId!.Value, CommunicationSettingNames.EmailAzureFromAddress)] = "business@example.test";

        await h.Sender.SendAsync(Message());

        h.Connections.Should().Equal("shared-connection");
        h.Sent.Should().ContainSingle().Which.Message.SenderAddress.Should().Be("business@example.test");
        h.Sender.IsConfigured.Should().BeFalse("the legacy probe reports options, not stored runtime readiness");
    }

    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    [InlineData("options")]
    public async Task Send_Should_PreserveGlobalThenLegacyThenOptionsFallback(string source)
    {
        var h = new Harness();
        h.Options.Azure.ConnectionString = "options-connection";
        h.Options.Azure.Email.FromAddress = "options@example.test";
        h.TenantValues[(h.Tenants.TenantId!.Value, CommunicationSettingNames.EmailAzureConnectionString)] = " ";
        h.TenantValues[(h.Tenants.TenantId.Value, CommunicationSettingNames.EmailAzureFromAddress)] = " ";
        if (source != "options")
        {
            h.GlobalValues[LegacyConnection] = "legacy-connection";
            h.GlobalValues[LegacyFrom] = "legacy@example.test";
        }
        if (source == "canonical")
        {
            h.GlobalValues[CommunicationSettingNames.EmailAzureConnectionString] = "canonical-connection";
            h.GlobalValues[CommunicationSettingNames.EmailAzureFromAddress] = "canonical@example.test";
        }

        await h.Sender.SendAsync(Message());

        h.Connections.Should().Equal(source + "-connection");
        h.Sent.Should().ContainSingle().Which.Message.SenderAddress.Should().Be(source + "@example.test");
    }

    [Fact]
    public async Task Send_WithoutTenant_Should_OnlyReadGlobalConfiguration()
    {
        var h = new Harness();
        h.ConfigureTenant("other-tenant-connection", "other@example.test");
        h.Tenants.TenantId = null;
        h.GlobalValues[LegacyConnection] = "platform-connection";
        h.GlobalValues[LegacyFrom] = "platform@example.test";

        await h.Sender.SendAsync(Message());

        h.TenantStore.VerifyNoOtherCalls();
        h.Connections.Should().Equal("platform-connection");
        h.Sent.Should().ContainSingle().Which.Message.SenderAddress.Should().Be("platform@example.test");
    }

    [Fact]
    public async Task Send_Should_PreserveExplicitServerSuppliedFromOverrides()
    {
        var h = new Harness();
        h.ConfigureTenant("tenant-connection", "tenant@example.test");

        await h.Sender.SendAsync(Message() with { From = "approved@example.test" });

        h.Sent.Should().ContainSingle().Which.Message.SenderAddress.Should().Be("approved@example.test");
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("connection")]
    [InlineData("sender")]
    public async Task Send_Should_FailWithoutCallingTheProvider_WhenConfigurationIsMissing(string missing)
    {
        var h = new Harness();
        h.ConfigureTenant(missing == "connection" ? "" : "tenant-connection",
            missing == "sender" ? "" : "tenant@example.test");
        if (missing == "provider")
            h.TenantValues[(h.Tenants.TenantId!.Value, CommunicationSettingNames.EmailProvider)] = "UnavailableProvider";

        var act = () => h.Sender.SendAsync(Message());

        await act.Should().ThrowAsync<InvalidOperationException>();
        h.Client.VerifyNoOtherCalls();
        h.Log.Entries.Should().NotContain(entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public async Task Send_Should_SanitizeClientInitializationErrorsAndLogs()
    {
        var h = new Harness();
        h.ConfigureTenant("sensitive-connection-value", "tenant@example.test");
        h.InitializationFailure = new FormatException("Credential sensitive-connection-value is invalid.");

        var act = () => h.Sender.SendAsync(Message());

        var failure = (await act.Should().ThrowAsync<MessagingNotConfiguredException>()).Which;
        failure.Channel.Should().Be("Email");
        failure.Message.Should().NotContain("sensitive-connection-value");
        failure.InnerException.Should().BeNull();
        var warning = h.Log.Entries.Should().ContainSingle().Which;
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Message.Should().NotContain("sensitive-connection-value");
        warning.Exception.Should().BeNull();
        h.Client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_Should_PropagateProviderFailureOrCancellation_WithoutLoggingSuccess(bool cancelled)
    {
        var h = new Harness();
        h.ConfigureTenant("tenant-connection", "tenant@example.test");
        Exception expected = cancelled ? new OperationCanceledException() : new RequestFailedException(503, "Provider unavailable");
        h.Client.Setup(client => client.SendAsync(WaitUntil.Completed, It.IsAny<SdkEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var act = () => h.Sender.SendAsync(Message());

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeSameAs(expected);
        h.Log.Entries.Should().NotContain(entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public async Task Send_Should_LogTheProviderOperationWithoutRecipientOrContent()
    {
        var h = new Harness();
        h.ConfigureTenant("tenant-connection", "tenant@example.test");
        var message = Message();

        await h.Sender.SendAsync(message);

        var log = h.Log.Entries.Should().ContainSingle().Which;
        log.Level.Should().Be(LogLevel.Information);
        log.Message.Should().Contain("operation-349").And.Contain("Succeeded")
            .And.NotContain(message.To).And.NotContain(message.Subject).And.NotContain(message.Body)
            .And.NotContain("delivered").And.NotContain("tenant-connection");
    }

    private static EmailMessage Message() => new("recipient@example.test", "Private subject", "Private body");

    private sealed class Harness
    {
        public Dictionary<(Guid TenantId, string Key), string?> TenantValues { get; } = new();
        public Dictionary<string, string?> GlobalValues { get; } = new();
        public MutableTenantProvider Tenants { get; } = new();
        public Mock<ITenantSettingStore> TenantStore { get; } = new();
        public Mock<ISettingProvider> Global { get; } = new();
        public CommunicationOptions Options { get; } = new();
        public Mock<EmailClient> Client { get; } = new(MockBehavior.Strict);
        public RecordingLogger Log { get; } = new();
        public List<string> Connections { get; } = [];
        public List<(SdkEmailMessage Message, CancellationToken Cancellation)> Sent { get; } = [];
        public Exception? InitializationFailure { get; set; }
        public AzureCommunicationEmailSender Sender { get; }

        public Harness()
        {
            TenantStore.Setup(settings => settings.GetTenantValueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, Guid tenant, CancellationToken _) => TenantValues.GetValueOrDefault((tenant, key)));
            Global.Setup(settings => settings.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) => GlobalValues.GetValueOrDefault(key));
            var operation = new Mock<EmailSendOperation>();
            operation.SetupGet(value => value.Id).Returns("operation-349");
            operation.SetupGet(value => value.Value).Returns(EmailModelFactory.EmailSendResult("operation-349", EmailSendStatus.Succeeded));
            Client.Setup(client => client.SendAsync(WaitUntil.Completed, It.IsAny<SdkEmailMessage>(), It.IsAny<CancellationToken>()))
                .Callback<WaitUntil, SdkEmailMessage, CancellationToken>((_, message, cancellation) => Sent.Add((message, cancellation)))
                .ReturnsAsync(operation.Object);
            Sender = new RecordingSender(Global.Object, Options, Log, TenantStore.Object, Tenants, connection =>
            {
                Connections.Add(connection);
                if (InitializationFailure is not null) throw InitializationFailure;
                return Client.Object;
            });
        }

        public void ConfigureTenant(string connection, string from)
        {
            TenantValues[(Tenants.TenantId!.Value, CommunicationSettingNames.EmailAzureConnectionString)] = connection;
            TenantValues[(Tenants.TenantId.Value, CommunicationSettingNames.EmailAzureFromAddress)] = from;
        }
    }

    private sealed class RecordingSender(ISettingProvider settings, CommunicationOptions options,
        ILogger<AzureCommunicationEmailSender> logger, ITenantSettingStore tenantSettings,
        ITenantProvider tenants, Func<string, EmailClient> createClient)
        : AzureCommunicationEmailSender(settings, Microsoft.Extensions.Options.Options.Create(options), logger, tenantSettings, tenants)
    {
        protected override EmailClient CreateClient(string connectionString) => createClient(connectionString);
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; set; } = Guid.NewGuid();
        public Guid GetCurrentTenantId() => TenantId ?? throw new InvalidOperationException("Tenant is absent.");
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = TenantId ?? Guid.Empty;
            return TenantId.HasValue;
        }
    }

    private sealed class RecordingLogger : ILogger<AzureCommunicationEmailSender>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
