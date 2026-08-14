using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Entitlements;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Entitlements;
using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Catalogue;
using Aonik.Subscriptions.Services.Entitlements;
using Aonik.Subscriptions.Services.Subscriptions;
using Aonik.Subscriptions.Services.Usage;
using Aonik.IntegrationTests.Support;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 090 §6.1, §7–§9 — issuance against a real entitlement read, the retirement invariant, and revocation
/// that names nobody.
/// </summary>
public class EntitlementTokenIssuerSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public EntitlementTokenIssuerSqlServerTests(SqlLocalDbFixture db) => _db = db;

    // A tenant per TEST, minted in the harness: this lane shares one physical database.

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class PlaintextProtector : IEntitlementKeyProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }

    private sealed class BouncySigner : IEd25519Signer
    {
        public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair()
        {
            var privateKey = new Ed25519PrivateKeyParameters(new SecureRandom());
            return (privateKey.GeneratePublicKey().GetEncoded(), privateKey.GetEncoded());
        }

        public byte[] Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey)
        {
            var signer = new Ed25519Signer();
            signer.Init(true, new Ed25519PrivateKeyParameters(privateKey.ToArray()));
            signer.BlockUpdate(message.ToArray(), 0, message.Length);
            return signer.GenerateSignature();
        }

        public bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey)
        {
            if (publicKey.Length != 32 || signature.Length != 64)
            {
                return false;
            }

            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(publicKey.ToArray()));
            verifier.BlockUpdate(message.ToArray(), 0, message.Length);
            return verifier.VerifySignature(signature.ToArray());
        }
    }

    private sealed class AllowAll : ISubscriberAuthorizer
    {
        public IReadOnlyCollection<string> SupportedKinds => [SubscriberKinds.Tenant];
        public Task<bool> CanActForAsync(SubscriberRef s, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanManageBillingForAsync(SubscriberRef s, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class Harness
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public SubscriptionsDbContext Db { get; }
        public TestClock Clock { get; } = new();
        public BouncySigner Signer { get; } = new();
        public EntitlementKeyRing KeyRing { get; }
        public EntitlementTokenIssuer Issuer { get; }
        public CatalogueService Catalogue { get; }
        public SubscriptionService Subscriptions { get; }

        public Harness(string connectionString)
        {
            Db = new SubscriptionsDbContext(
                new DbContextOptionsBuilder<SubscriptionsDbContext>()
                    .UseSqlServer(connectionString)
                    .Options,
                new TestTenantProvider(TenantId));

            var tenant = new TestTenantProvider(TenantId);
            var auth = new SubscriberAuthorization([new AllowAll()]);
            var options = Microsoft.Extensions.Options.Options.Create(new EntitlementTokenOptions());

            Catalogue = new CatalogueService(Db, tenant, Clock);
            Subscriptions = new SubscriptionService(
                Db, tenant, auth, new EntitlementMaterialiser(Db, Clock), Clock);

            KeyRing = new EntitlementKeyRing(
                Db, Signer, new PlaintextProtector(), tenant, Clock, options,
                NullLogger<EntitlementKeyRing>.Instance);

            Issuer = new EntitlementTokenIssuer(
                Db, new EntitlementReader(Db, tenant, auth, Clock), KeyRing, Signer,
                new PlaintextProtector(), tenant, Clock, options,
                NullLogger<EntitlementTokenIssuer>.Instance);
        }

        public async Task SeedPlanAndSubscribeAsync()
        {
            await Catalogue.CreateMeterAsync(new CreateMeterRequest(
                "workspaces", "Workspaces", MeterKinds.Ceiling, "workspaces"));
            await Catalogue.CreateMeterAsync(new CreateMeterRequest(
                "cloud-sync", "Cloud sync", MeterKinds.Flag));

            var plan = await Catalogue.CreatePlanAsync(
                new CreatePlanRequest("studio-pro", "Studio Pro", BillingIntervals.None));
            var draft = await Catalogue.CreateDraftVersionAsync(
                plan.Id, new CreatePlanVersionRequest(0m, "GBP"));
            await Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [
                new PlanEntitlementSpec("workspaces", 25, ResetPolicies.Never),
                new PlanEntitlementSpec("cloud-sync", 1, ResetPolicies.Never),
            ]));
            await Catalogue.PublishVersionAsync(draft.Id);

            await Subscriptions.SubscribeAsync(Subscriber(), "studio-pro");
        }

        public SubscriberRef Subscriber() => new(SubscriberKinds.Tenant, TenantId);
    }

    // ── Issue ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AnIssuedToken_Should_VerifyAgainstThePublishedKey_AndStateRealEntitlements()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();
        await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));

        var issued = await h.Issuer.IssueAsync(h.Subscriber());

        var key = await h.KeyRing.GetSigningKeyAsync();
        EntitlementTokenFormat.TryBase64UrlDecode(key!.PublicKey, out var publicKey).Should().BeTrue();

        var result = EntitlementTokenVerifier.Verify(
            issued.Token,
            kid => kid == key.Kid ? publicKey : null,
            (m, s, k) => h.Signer.Verify(m, s, k),
            new DateTimeOffset(h.Clock.UtcNow, TimeSpan.Zero));

        // The P2 ship condition, whole: verifies against the published key and states the subscriber's
        // real flags and ceilings — not a hardcoded sample.
        result.Verdict.Should().Be(EntitlementVerdict.Valid);
        result.Payload.GetProperty("plan").GetString().Should().Be("studio-pro");
        result.Payload.GetProperty("feat")[0].GetString().Should().Be("cloud-sync");
        result.Payload.GetProperty("lim").GetProperty("workspaces").GetInt64().Should().Be(25);
        result.Payload.GetProperty("sub").GetString().Should().Be($"tenant:{h.TenantId:D}");
    }

    [SkippableFact]
    public async Task Issuing_Should_WriteTheAuditRow_WithThePersistedGrace()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();
        await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));

        var issued = await h.Issuer.IssueAsync(h.Subscriber(), "device-abc");

        var audit = await h.Db.EntitlementTokenIssues.AsNoTracking().SingleAsync();

        audit.Jti.Should().Be(issued.Jti);
        audit.DeviceFingerprint.Should().Be("device-abc");

        // Persisted, not derived. exp and gra are distinct claims and the grace window can change
        // after signing — §6.1's invariant needs MAX(gra) per kid over what was ACTUALLY signed.
        audit.GraceUntil.Should().Be(issued.GraceUntil);
    }

    [SkippableFact]
    public async Task Issuing_WithoutAKey_Should_RefuseRatherThanInventOne()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();

        var act = async () => await h.Issuer.IssueAsync(h.Subscriber());

        // Generating a key implicitly would make the first token of a deployment depend on whichever
        // request arrived first.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── §6.1 — the retirement invariant, enforced at issue ───────────────

    [SkippableFact]
    public async Task Issuing_Should_ExtendVerifyNotAfter_WhenGraceOutlivesIt()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();

        // A key whose published life would end BEFORE a new token's grace: the ordinary,
        // well-intentioned configuration change §6.1 warns about, reproduced by a short allowance.
        await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(1));

        var issued = await h.Issuer.IssueAsync(h.Subscriber());

        var key = await h.Db.EntitlementSigningKeys.AsNoTracking().SingleAsync();

        // The same statement that observed the bound extended it. Without this, a retirement computed
        // yesterday silently invalidates a token issued today — during its grace, offline, for a
        // paying customer, looking exactly like a licensing bug.
        key.VerifyNotAfter.Should().BeOnOrAfter(issued.GraceUntil);
    }

    [SkippableFact]
    public async Task AWithdrawnKey_Should_RefuseIssuance()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();
        await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));

        await h.Db.EntitlementSigningKeys
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.Status, EntitlementKeyStatuses.Withdrawn));
        h.Db.ChangeTracker.Clear();

        var act = async () => await h.Issuer.IssueAsync(h.Subscriber());

        // The compromise path: a token signed now would fail at every verifier that has fetched the
        // new set. Refusing is the only outcome that is not a lie to the client.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Keys ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Rotation_Should_LeaveTwoKeysValid_WithOnlyTheNewOneSigning()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        var first = await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));
        var second = await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));

        (await h.KeyRing.GetSigningKeyAsync())!.Kid.Should().Be(second.Kid);

        var published = await h.KeyRing.GetPublishedSetAsync();
        EntitlementTokenFormat.TryBase64UrlDecode(published.SignedBytes, out var bytes).Should().BeTrue();
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        // The P1 ship condition: rotated with two valid at once. The predecessor stops signing but
        // stays published, which is what makes rotation a non-event for tokens in flight.
        json.Should().Contain(first.Kid).And.Contain(second.Kid);
    }

    [SkippableFact]
    public async Task AWithdrawnKey_Should_VanishFromThePublishedSet()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        var key = await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));

        await h.Db.EntitlementSigningKeys
            .Where(k => k.Kid == key.Kid)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.Status, EntitlementKeyStatuses.Withdrawn));
        h.Db.ChangeTracker.Clear();

        var published = await h.KeyRing.GetPublishedSetAsync();
        EntitlementTokenFormat.TryBase64UrlDecode(published.SignedBytes, out var bytes).Should().BeTrue();

        // Absence IS the message. The list is complete, never a delta, so a client comparing sets
        // learns the key is gone — including a client that shipped it in its binary.
        System.Text.Encoding.UTF8.GetString(bytes).Should().NotContain(key.Kid);
    }

    // ── Revocation (§9) ──────────────────────────────────────────────────

    [SkippableFact]
    public async Task RevokingAToken_Should_ListItsJti()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();
        await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));
        var issued = await h.Issuer.IssueAsync(h.Subscriber());

        (await h.Issuer.RevokeAsync(issued.Jti, null, "device stolen")).Should().BeTrue();

        (await h.Issuer.GetRevocationsAsync()).TokenIds.Should().Contain(issued.Jti);
    }

    [SkippableFact]
    public async Task RevokingASubscriber_Should_PublishHandles_NeverTheSubscriber()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();
        await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));
        await h.Issuer.IssueAsync(h.Subscriber());
        await h.Issuer.IssueAsync(h.Subscriber());

        (await h.Issuer.RevokeAsync(null, h.Subscriber(), "chargeback")).Should().BeTrue();

        var list = await h.Issuer.GetRevocationsAsync();

        list.Handles.Should().HaveCount(2);

        // §9.1: the public list must not name subscribers. The handle is random, rotated per refresh,
        // and only a holder of the token can compute it to look up — so publishing the list reveals
        // nothing about who was revoked.
        foreach (var handle in list.Handles)
        {
            handle.Should().NotContain(h.TenantId.ToString("N"));
            handle.Should().NotContain(h.TenantId.ToString("D"));
        }
    }

    [SkippableFact]
    public async Task ARevocationPastItsGrace_Should_LeaveTheList()
    {
        RequireSqlServer();
        var h = new Harness(_db.ConnectionString);
        await h.SeedPlanAndSubscribeAsync();
        await h.KeyRing.RotateAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(37));
        var issued = await h.Issuer.IssueAsync(h.Subscriber());
        await h.Issuer.RevokeAsync(issued.Jti, null, "test");

        // After grace the verifier rejects on time alone; the entry is dead weight in a public list.
        h.Clock.UtcNow = issued.GraceUntil.AddDays(1);

        (await h.Issuer.GetRevocationsAsync()).TokenIds.Should().BeEmpty();
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
