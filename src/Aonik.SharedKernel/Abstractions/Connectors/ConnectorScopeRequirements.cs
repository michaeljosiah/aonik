namespace Aonik.SharedKernel.Abstractions.Connectors;

public static class ConnectorScopeRequirements
{
    public static bool Satisfied(ConnectorActionDescriptor action, IReadOnlyCollection<string> granted)
        => (action.ScopeAlternatives ?? [action.RequiredScopes]).Any(set => set.All(scope => granted.Contains(scope, StringComparer.Ordinal)));

    public static IReadOnlyList<string> Minimal(IReadOnlyList<ConnectorActionDescriptor> actions)
    {
        var scopes = actions.SelectMany(action => action.RequiredScopes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        foreach (string scope in scopes.ToArray())
        {
            List<string> reduced = scopes.Where(candidate => candidate != scope).ToList();
            if (actions.All(action => Satisfied(action, reduced))) scopes = reduced;
        }
        return scopes;
    }
}
