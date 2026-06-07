using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave;
using Aonik.SharedKernel.Primitives;
using FluentAssertions;

namespace Aonik.Finance.IntegrationTests.Partners;

/// <summary>
/// Live Flutterwave v4 <strong>sandbox</strong> smoke tests for the real connector
/// (<see cref="FlutterwavePayoutConnector"/>).
///
/// These hit the real sandbox API, so they are <strong>opt-in</strong>: they run only when sandbox
/// credentials are present in the environment and otherwise report <em>Skipped</em> (never Failed),
/// so CI and other developers are unaffected. Secrets are <strong>never</strong> committed — they are
/// NOT read from appsettings; supply them as environment variables before running:
///
/// <code>
///   # PowerShell
///   $env:FLW_SANDBOX_CLIENT_ID     = "&lt;client id&gt;"
///   $env:FLW_SANDBOX_CLIENT_SECRET = "&lt;client secret&gt;"
///   # optional: $env:FLW_SANDBOX_ENCRYPTION_KEY, FLW_SANDBOX_BASE_URL, FLW_SANDBOX_IDP_URL
///   dotnet test tests/Aonik.Finance.IntegrationTests --filter FullyQualifiedName~FlutterwaveSandboxTests
/// </code>
///
/// They exercise the request-shaping that unit tests can only stub — OAuth token acquisition,
/// <c>X-Trace-Id</c> on the GET status poll, and the <c>rcb_…</c> recipient-id round-trip — i.e. the
/// exact paths where live-only defects hide.
/// </summary>
public class FlutterwaveSandboxTests
{
    private static string? ClientId => Env("FLW_SANDBOX_CLIENT_ID");
    private static string? ClientSecret => Env("FLW_SANDBOX_CLIENT_SECRET");

    private static bool Configured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    private const string SkipReason =
        "Flutterwave sandbox credentials not set. Provide FLW_SANDBOX_CLIENT_ID and "
        + "FLW_SANDBOX_CLIENT_SECRET environment variables to run these live sandbox tests.";

    // Flutterwave's documented NGN sandbox test bank account (bank code 044, 10-digit account).
    private const string TestBankCode = "044";
    private const string TestAccountNumber = "0690000040";

    [SkippableFact]
    public async Task TokenProvider_AcquiresAccessToken_FromSandboxIdp()
    {
        Skip.IfNot(Configured, SkipReason);

        var token = await CreateTokenProvider().GetAccessTokenAsync(CancellationToken.None);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public async Task ResolveAccount_ReturnsAccountName_ForSandboxTestAccount()
    {
        Skip.IfNot(Configured, SkipReason);

        var result = await CreateConnector().ResolveAccountAsync(
            new AccountResolutionRequest(TestBankCode, TestAccountNumber, "NGN"));

        result.Resolved.Should().BeTrue();
        result.AccountName.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public async Task Payout_RegisterRecipient_Initiate_Status_RoundTrips()
    {
        Skip.IfNot(Configured, SkipReason);

        var connector = CreateConnector();

        // 1) Register a recipient — must return a real Flutterwave recipient id (e.g. rcb_…).
        //    Regression guard for the recipient-id prefix bug.
        var registration = await connector.RegisterRecipientAsync(new RecipientRegistrationRequest(
            new BankAccountDestination(TestBankCode, TestAccountNumber, null, "Alex James"),
            Currency: "NGN", AccountName: "Alex James", Country: "NG", Metadata: null));

        registration.Registered.Should().BeTrue();
        registration.ProviderBeneficiaryId.Should().NotBeNullOrWhiteSpace();

        // 2) Initiate a transfer via that recipient_id — exercises the recipient-shape guard end to end.
        var clientReference = $"AONIKIT{Guid.NewGuid():N}"[..20];
        var initiation = await connector.InitiatePayoutAsync(new PayoutInstruction(
            ClientReference: clientReference,
            Amount: new Money(100m, "NGN"),
            DebitCurrency: "NGN",
            Destination: new BankAccountDestination(
                TestBankCode, registration.ProviderBeneficiaryId!, null, "Alex James"),
            Narration: "Aonik sandbox integration test",
            CallbackUrl: null,
            Metadata: new Dictionary<string, string> { ["transfer_purpose"] = "personal" }));

        initiation.Status.Should().NotBe(PartnerTransactionStatus.Failed);
        initiation.Reference.ProviderReference.Should().NotBeNullOrWhiteSpace();

        // 3) Poll status — exercises GET /transfers/{id}, which must carry X-Trace-Id.
        //    Regression guard for the missing-trace-id-on-GET bug.
        var status = await connector.GetPayoutStatusAsync(initiation.Reference);

        status.Status.Should().NotBe(PartnerTransactionStatus.Unknown);
        status.Reference.ProviderReference.Should().Be(initiation.Reference.ProviderReference);
    }

    // ── Wiring ────────────────────────────────────────────────────────────────
    private static FlutterwaveOptions Options() => new()
    {
        UseRealFlutterwaveApi = true,
        BaseUrl = Env("FLW_SANDBOX_BASE_URL") ?? "https://developersandbox-api.flutterwave.com",
        IdpTokenUrl = Env("FLW_SANDBOX_IDP_URL")
            ?? "https://idp.flutterwave.com/realms/flutterwave/protocol/openid-connect/token",
        ClientId = ClientId ?? string.Empty,
        ClientSecret = ClientSecret ?? string.Empty,
        EncryptionKey = Env("FLW_SANDBOX_ENCRYPTION_KEY") ?? string.Empty,
    };

    private static FlutterwaveTokenProvider CreateTokenProvider()
    {
        var options = Options();
        return new FlutterwaveTokenProvider(
            new StubHttpClientFactory(new Uri(options.IdpTokenUrl)),
            Microsoft.Extensions.Options.Options.Create(options));
    }

    private static FlutterwavePayoutConnector CreateConnector()
    {
        var options = Options();
        var authHandler = new FlutterwaveAuthHandler(CreateTokenProvider())
        {
            InnerHandler = new HttpClientHandler()
        };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri(options.BaseUrl) };
        return new FlutterwavePayoutConnector(
            new FlutterwaveClient(httpClient), Microsoft.Extensions.Options.Options.Create(options));
    }

    // The token provider resolves its IdP client through IHttpClientFactory; the real DI uses a named
    // client. Here a tiny factory returns a client pointed at the IdP token endpoint.
    private sealed class StubHttpClientFactory(Uri idpBaseAddress) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new() { BaseAddress = idpBaseAddress };
    }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
