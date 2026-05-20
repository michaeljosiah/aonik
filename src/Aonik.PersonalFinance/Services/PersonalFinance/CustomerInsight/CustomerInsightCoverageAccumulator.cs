using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Tracks which source domains were available, missing or omitted while
/// loading the input data for a snapshot. The accumulator is mutated by
/// loaders and finally projected into <see cref="CustomerInsightCoverage"/>.
/// </summary>
internal sealed class CustomerInsightCoverageAccumulator
{
    private readonly HashSet<string> _availableDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _omittedSections = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Warnings => _warnings;

    public void MarkAvailable(string domainName)
    {
        _availableDomains.Add(domainName);
    }

    public void MarkMissing(string domainName, string omittedSection, string reason)
    {
        _missingDomains.Add(domainName);
        _omittedSections.Add(omittedSection);
        _warnings.Add($"{domainName} domain could not be loaded: {reason}");
    }

    public CustomerInsightCoverage Build()
    {
        return new CustomerInsightCoverage(
            _missingDomains.Count > 0,
            _availableDomains.OrderBy(x => x).ToList(),
            _missingDomains.OrderBy(x => x).ToList(),
            _omittedSections.OrderBy(x => x).ToList(),
            _warnings.Distinct(StringComparer.Ordinal).ToList());
    }
}
