using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

public sealed class PackageVerifierRouter(DockerPackageVerifier docker, SessionPackageVerifier sessions) : IPackageVerifier
{
    public Task<PackageVerificationResult> VerifyAsync(VerifiedInput input, PackageVerifierOptions policy, CancellationToken cancellationToken = default)
        => policy.Provider switch
        {
            "Docker" => docker.VerifyAsync(input, policy, cancellationToken),
            "AzureDynamicSessions" => sessions.VerifyAsync(input, policy, cancellationToken),
            _ => Task.FromResult(VerifierProtocol.Refused("verifier-unavailable", policy))
        };
}
