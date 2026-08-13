using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Moves a blocked <em>input</em> into protected storage so §12 preservation has something to
/// preserve, without the prompt itself ever becoming a "reference".
///
/// <para>
/// This seam exists because of a genuine tension. An input block's only artefact is the child's own
/// words: writing them into <c>SafetyArtefact.Reference</c> breaks that entity's contract and §11's
/// position that a child's input is not material we keep — but at a
/// <see cref="Aonik.SharedKernel.Abstractions.Safety.SafetyCategories.Reportable"/> category the
/// obligation runs the other way and preservation is not discretionary. The resolution is not to keep
/// the text inline but to put it somewhere access-controlled and keep only the key.
/// </para>
///
/// <para>
/// <strong>Not registered by default</strong>, because no protected store is configured in this
/// solution. That absence is not silently tolerated: the gate logs at critical and the escalation
/// records <c>MaterialPreserved = false</c>, so the responsible person is told they are acting on a
/// record with nothing behind it rather than being left to assume otherwise.
/// </para>
/// </summary>
public interface IPreservedInputStore
{
    /// <summary>
    /// Store the input under access control and return the key. Throwing is a legitimate outcome and
    /// is recorded as a preservation failure — never swallowed into a success.
    /// </summary>
    Task<string> PreserveAsync(
        Guid subjectPartyId,
        string input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Composition rules the safety subsystem refuses to start without (Spec 096 §12).
/// </summary>
public static class SafetyComposition
{
    /// <summary>
    /// A deployment that can classify must also be able to preserve.
    ///
    /// <para>
    /// The pairing is not obvious, which is exactly why it is enforced rather than documented:
    /// classification is the exciting half and preservation is the half nobody remembers until an
    /// incident. Wiring a vendor without a protected store produces a system that can <em>detect</em>
    /// the reporting category and cannot keep what it detected — the worst available combination,
    /// because it looks fully operational.
    /// </para>
    /// </summary>
    public static void RequirePreservationWhenClassifying(
        IEnumerable<ISafetyClassificationProvider> classificationProviders,
        IPreservedInputStore? preservedInputStore)
    {
        if (preservedInputStore is not null || !classificationProviders.Any())
        {
            return;
        }

        throw new InvalidOperationException(
            "A safety classification provider is registered but no IPreservedInputStore is. "
            + "Spec 096 §12 requires reportable material to be preserved, and a system that can "
            + "detect that category without keeping what it detected is worse than one that cannot "
            + "detect it — it looks operational. Register a protected store.");
    }
}

/// <summary>
/// Fails the host at startup when the safety composition is unsafe (Spec 096 §12).
///
/// <para>
/// The same check runs inside the gate's factory, but a scoped factory is evaluated lazily: the host
/// would start clean and the exception would surface on the first child-facing request instead. A
/// composition error that is detectable before serving traffic should be detected before serving
/// traffic — that is the difference between a deployment that will not start and an outage.
/// </para>
/// </summary>
internal sealed class SafetyCompositionValidator : IHostedService
{
    private readonly IServiceProvider _services;

    public SafetyCompositionValidator(IServiceProvider services) => _services = services;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();

        SafetyComposition.RequirePreservationWhenClassifying(
            scope.ServiceProvider.GetServices<ISafetyClassificationProvider>(),
            scope.ServiceProvider.GetService<IPreservedInputStore>());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
