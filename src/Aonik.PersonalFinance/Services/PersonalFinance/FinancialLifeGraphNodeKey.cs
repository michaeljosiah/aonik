namespace Aonik.PersonalFinance.Services;

internal readonly record struct FinancialLifeGraphNodeKey(string Prefix, Guid Id)
{
    public override string ToString() => FinancialLifeGraphNodeKeys.Build(Prefix, Id);

    public static FinancialLifeGraphNodeKey Create(string prefix, Guid id)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Node key prefix is required.", nameof(prefix));
        }

        return new FinancialLifeGraphNodeKey(prefix.Trim().ToLowerInvariant(), id);
    }

    public static bool TryParse(string nodeKey, out FinancialLifeGraphNodeKey parsed)
    {
        parsed = default;
        if (!FinancialLifeGraphNodeKeys.TryParse(nodeKey, out var prefix, out var nodeId))
        {
            return false;
        }

        parsed = new FinancialLifeGraphNodeKey(prefix, nodeId);
        return true;
    }
}
