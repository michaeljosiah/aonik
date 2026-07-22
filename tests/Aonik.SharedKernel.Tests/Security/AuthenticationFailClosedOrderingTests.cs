using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Security;

/// <summary>
/// Spec 072 review finding (P1) — pins that <c>AonikAuthenticationSetup</c> validates the resolved
/// user (Active status, Spec 026 session revocation) BEFORE populating <c>ICurrentUserContext</c>.
/// <c>context.Fail()</c> does not stop an AllowAnonymous endpoint from executing, so a user id
/// stamped before those checks would let a deactivated user or revoked session keep acting as an
/// authenticated principal wherever anonymous access is allowed — e.g. the Spec 072 current-party
/// resolution that authorizes party-bound carts. Same wiring-pin style as
/// <see cref="McpHostGuardWiringTests"/>: the behavior lives in a private JWT-events handler, so
/// the ORDER of its source is the enforceable contract.
/// </summary>
public class AuthenticationFailClosedOrderingTests
{
    [Fact]
    public void TokenValidation_Should_RunStatusAndRevocationChecks_BeforePopulatingCurrentUser()
    {
        var setupPath = Path.Combine(
            RepositoryRoot(),
            "src", "Aonik.Infrastructure", "Authentication", "AonikAuthenticationSetup.cs");
        File.Exists(setupPath).Should().BeTrue("the authentication setup should exist");

        var source = File.ReadAllText(setupPath);

        var statusCheckIndex = source.IndexOf("context.Fail($\"User account is", StringComparison.Ordinal);
        var revocationIndex = source.IndexOf("IsRevokedAsync(", StringComparison.Ordinal);
        var populateIndex = source.IndexOf("currentUserContext.UserId = user.Id", StringComparison.Ordinal);

        statusCheckIndex.Should().BeGreaterThanOrEqualTo(0, "inactive accounts must fail token validation");
        revocationIndex.Should().BeGreaterThanOrEqualTo(0, "revoked sessions must fail token validation (Spec 026)");
        populateIndex.Should().BeGreaterThanOrEqualTo(0, "successful validation populates the current-user context");

        statusCheckIndex.Should().BeLessThan(populateIndex,
            "a non-Active user must be rejected before any identity state is stamped — " +
            "AllowAnonymous endpoints still run after context.Fail()");
        revocationIndex.Should().BeLessThan(populateIndex,
            "a revoked session must be rejected before any identity state is stamped — " +
            "AllowAnonymous endpoints still run after context.Fail()");
    }

    /// <summary>Walks up from the test assembly location to the directory containing Aonik.sln.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aonik.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull(
            "the test must run from within the repository so it can read the authentication setup source");
        return dir!.FullName;
    }
}
