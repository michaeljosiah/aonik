using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services.Seeding.Phases;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.PersonalFinance.Services.Seeding;

/// <summary>
/// PersonalFinance module's demo-seed contributor. Owns the two personal-finance
/// demo phases — households (<see cref="HouseholdsSeedPhase"/>) and the
/// personal-finance persona activity (<see cref="PersonalFinanceActivitySeedPhase"/>) —
/// writing exclusively through <see cref="PersonalFinanceDbContext"/>.
///
/// Spec 027 S5 (#118/#126): split out of <c>FinanceDemoSeedContributor</c> so
/// the PF phases move onto PersonalFinance and Finance no longer references it.
/// <c>DemoSeedService</c> invokes every contributor for every phase, so a
/// per-module contributor that returns empty for phases it doesn't own composes
/// cleanly with the Finance one.
/// </summary>
internal sealed class PersonalFinanceDemoSeedContributor : IDemoSeedContributor
{
    private readonly PersonalFinanceDbContext _personalFinanceDbContext;

    // Accumulated results that the orchestrator can read via GetResults()
    private readonly Dictionary<string, object> _results = new();

    // Phase helpers
    private readonly HouseholdsSeedPhase _households;
    private readonly PersonalFinanceActivitySeedPhase _personalFinanceActivity;

    // Primary constructor — used by DI (all phase helpers injected).
    public PersonalFinanceDemoSeedContributor(
        PersonalFinanceDbContext personalFinanceDbContext,
        HouseholdsSeedPhase households,
        PersonalFinanceActivitySeedPhase personalFinanceActivity)
    {
        _personalFinanceDbContext = personalFinanceDbContext;
        _households = households;
        _personalFinanceActivity = personalFinanceActivity;
    }

    // Legacy constructor — used by tests that construct the contributor directly
    // without DI. Builds phase helpers inline from the provided context.
    //
    // internal (not public) so the DI container sees only the primary ctor
    // above — two resolvable public ctors, neither a superset of the other,
    // throws "ambiguous constructors" at activation. Tests reach this via
    // InternalsVisibleTo.
    internal PersonalFinanceDemoSeedContributor(
        PersonalFinanceDbContext personalFinanceDbContext)
        : this(
            personalFinanceDbContext,
            new HouseholdsSeedPhase(personalFinanceDbContext),
            new PersonalFinanceActivitySeedPhase(personalFinanceDbContext))
    {
    }

    public string ModuleName => "PersonalFinance";

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedPhase phase,
        DemoSeedContext context,
        CancellationToken cancellationToken = default)
    {
        return phase switch
        {
            DemoSeedPhase.Households => await _households.SeedAsync(context, _results, cancellationToken),
            DemoSeedPhase.Activity   => await _personalFinanceActivity.SeedAsync(context, _results, cancellationToken),
            _                        => Array.Empty<string>()
        };
    }

    public void ClearTracking()
    {
        _personalFinanceDbContext.ChangeTracker.Clear();
    }

    public IReadOnlyDictionary<string, object> GetResults() => _results;
}
