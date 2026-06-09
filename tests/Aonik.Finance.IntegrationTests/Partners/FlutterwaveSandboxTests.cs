using System.Net;
using System.Text.Json;

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
/// credentials are supplied and otherwise report <em>Skipped</em> (never Failed), so CI and other
/// developers are unaffected. Credentials are resolved from (in priority order):
/// <list type="number">
///   <item>environment variables — <c>FLW_SANDBOX_CLIENT_ID</c>, <c>FLW_SANDBOX_CLIENT_SECRET</c>,
///   optionally <c>FLW_SANDBOX_ENCRYPTION_KEY</c> / <c>FLW_SANDBOX_BASE_URL</c> / <c>FLW_SANDBOX_IDP_URL</c>;</item>
///   <item>a local <c>appsettings.Development.json</c> in this test project (git-ignored — see
///   <c>appsettings.Development.json.example</c> for the shape).</item>
/// </list>
///
/// Secrets are <strong>never committed</strong>: the local <c>appsettings.Development.json</c> is in
/// <c>.gitignore</c>, and nothing here reads the API's appsettings.
///
/// The tests exercise the request-shaping that unit tests can only stub — OAuth token acquisition,
/// <c>X-Trace-Id</c> on the GET status poll, and the <c>rcb_…</c> recipient-id round-trip — i.e. the
/// exact paths where live-only defects hide.
/// </summary>
public class FlutterwaveSandboxTests
{
    private static string? ClientId => Value("FLW_SANDBOX_CLIENT_ID", "ClientId");
    private static string? ClientSecret => Value("FLW_SANDBOX_CLIENT_SECRET", "ClientSecret");

    private static bool Configured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    private const string SkipReason =
        "Flutterwave sandbox credentials not set. Provide ClientId/ClientSecret via "
        + "tests/Aonik.Finance.IntegrationTests/appsettings.Development.json (git-ignored) or the "
        + "FLW_SANDBOX_CLIENT_ID / FLW_SANDBOX_CLIENT_SECRET environment variables.";

    // Flutterwave's documented NGN sandbox test bank account (bank code 044, 10-digit account).
    private const string TestBankCode = "044";
    private const string TestAccountNumber = "0690000040";

    [SkippableFact]
    public async Task TokenProvider_AcquiresAccessToken_FromSandboxIdp()
    {
        Skip.IfNot(Configured, SkipReason);

        var token = await TokenProvider.GetAccessTokenAsync(Options(), CancellationToken.None);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public async Task ResolveAccount_ReturnsAccountName_ForSandboxTestAccount()
    {
        Skip.IfNot(Configured, SkipReason);

        AccountResolutionResult? result = null;
        FlutterwaveException? denied = null;
        try
        {
            result = await CreateConnector().ResolveAccountAsync(
                new AccountResolutionRequest(TestBankCode, TestAccountNumber, "NGN"));
        }
        catch (FlutterwaveException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            // Some sandbox clients are entitled to transfers but not to /banks/account-resolve.
            denied = ex;
        }

        Skip.If(denied is not null,
            "This Flutterwave sandbox client is not entitled to /banks/account-resolve (403). "
            + "Name enquiry is a helper, not on the critical payout path — enable account verification "
            + "on the sandbox account to exercise it.");

        result!.Resolved.Should().BeTrue();
        result.AccountName.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public async Task Payout_RegisterRecipient_Initiate_Status_RoundTrips()
    {
        Skip.IfNot(Configured, SkipReason);

        var connector = CreateConnector();

        // 1) Register a recipient — must return a real Flutterwave recipient id (e.g. rcb_…).
        //    Regression guard for the recipient-id prefix bug.
        RecipientRegistrationResult? registration = null;
        FlutterwaveException? duplicateRecipient = null;
        try
        {
            registration = await connector.RegisterRecipientAsync(new RecipientRegistrationRequest(
                new BankAccountDestination(TestBankCode, TestAccountNumber, null, "Alex James"),
                Currency: "NGN", AccountName: "Alex James", Country: "NG", Metadata: null));
        }
        catch (FlutterwaveException ex) when (ex.Message.Contains("Recipient already exists", StringComparison.OrdinalIgnoreCase))
        {
            duplicateRecipient = ex;
        }

        Skip.If(duplicateRecipient is not null,
            "This Flutterwave sandbox already has the static test recipient registered. "
            + "Use a fresh sandbox account to exercise the register->initiate round-trip.");

        registration.Should().NotBeNull();
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
        BaseUrl = Value("FLW_SANDBOX_BASE_URL", "BaseUrl") ?? "https://developersandbox-api.flutterwave.com",
        IdpTokenUrl = Value("FLW_SANDBOX_IDP_URL", "IdpTokenUrl")
            ?? "https://idp.flutterwave.com/realms/flutterwave/protocol/openid-connect/token",
        ClientId = ClientId ?? string.Empty,
        ClientSecret = ClientSecret ?? string.Empty,
        EncryptionKey = Value("FLW_SANDBOX_ENCRYPTION_KEY", "EncryptionKey") ?? string.Empty,
    };

    // Shared across the whole test class so only ONE token is fetched. The sandbox IdP rate-limits
    // rapid client-credentials requests (returns 403), and one fetch mirrors production — where the
    // 10-minute token is cached. Lazy so it is created after the static config fields are initialized.
    private static readonly Lazy<FlutterwaveTokenProvider> LazyTokenProvider = new(() =>
    {
        var options = Options();
        return new FlutterwaveTokenProvider(
            new StubHttpClientFactory(new Uri(options.IdpTokenUrl)),
            new StaticFlutterwaveConfigProvider(options));
    });

    private static FlutterwaveTokenProvider TokenProvider => LazyTokenProvider.Value;

    private static FlutterwavePayoutConnector CreateConnector()
    {
        var options = Options();
        var authHandler = new FlutterwaveAuthHandler(TokenProvider)
        {
            InnerHandler = new HttpClientHandler()
        };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri(options.BaseUrl) };
        var configProvider = new StaticFlutterwaveConfigProvider(options);
        return new FlutterwavePayoutConnector(new FlutterwaveClient(httpClient), configProvider);
    }

    private sealed class StaticFlutterwaveConfigProvider(FlutterwaveOptions options) : IFlutterwaveConfigProvider
    {
        public Task<FlutterwaveOptions> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(options);

        public Task<FlutterwaveOptions> GetAsync(
            Aonik.Finance.Services.Partners.Connectors.ConnectorBinding binding,
            CancellationToken cancellationToken = default)
            => Task.FromResult(options);
    }

    // The token provider resolves its IdP client through IHttpClientFactory; the real DI uses a named
    // client. Here a tiny factory returns a client pointed at the IdP token endpoint.
    private sealed class StubHttpClientFactory(Uri idpBaseAddress) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new() { BaseAddress = idpBaseAddress };
    }

    // ── Credential resolution (env var first, then the git-ignored local json) ─
    private static string? Value(string envName, string fileKey)
    {
        var env = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        return FileValues.TryGetValue(fileKey, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static readonly IReadOnlyDictionary<string, string?> FileValues = LoadLocalFile();

    private static IReadOnlyDictionary<string, string?> LoadLocalFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");
        var empty = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("Flutterwave", out var flutterwave)
                && flutterwave.TryGetProperty("Sandbox", out var sandbox)
                && sandbox.ValueKind == JsonValueKind.Object)
            {
                var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in sandbox.EnumerateObject())
                {
                    map[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : null;
                }

                return map;
            }
        }
        catch (JsonException)
        {
            // Malformed local file — treat as unconfigured (tests skip).
        }

        return empty;
    }
}
