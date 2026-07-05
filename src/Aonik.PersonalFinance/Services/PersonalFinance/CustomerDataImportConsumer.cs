using System.Text.Json;

using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Finance-module implementation of <see cref="ICustomerDataImportConsumer"/>.
/// Imports all personal-finance entities from the customer data bundle.
/// Entities are inserted in topological (FK-dependency) order.
/// </summary>
internal class CustomerDataImportConsumer : ICustomerDataImportConsumer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly PersonalFinanceDbContext _db;

    public CustomerDataImportConsumer(PersonalFinanceDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerDataImportModuleResult> ImportAsync(
        Guid tenantId,
        Dictionary<string, List<JsonElement>> data,
        Dictionary<Guid, Guid> idMap,
        CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, int>();
        var warnings = new List<string>();

        // Import in topological order (FK parents before children)

        // Tier 1: No FK deps on other finance entities
        Import<PersonalProfile>("PersonalProfile", data, idMap, tenantId, counts);
        Import<Household>("Household", data, idMap, tenantId, counts);

        // Tier 2: Depend on Tier 1
        Import<HouseholdMember>("HouseholdMember", data, idMap, tenantId, counts);
        Import<FinancialConnection>("FinancialConnection", data, idMap, tenantId, counts);
        Import<PersonalAccount>("PersonalAccount", data, idMap, tenantId, counts);
        Import<FinancialContext>("FinancialContext", data, idMap, tenantId, counts);

        // Tier 3: Depend on Tier 2
        Import<PersonalLinkedAccount>("PersonalLinkedAccount", data, idMap, tenantId, counts);
        Import<FinancialContextFundingSource>("FinancialContextFundingSource", data, idMap, tenantId, counts);

        // Tier 4: Depend on Tier 2/3
        Import<Budget>("Budget", data, idMap, tenantId, counts);
        Import<Goal>("Goal", data, idMap, tenantId, counts);
        Import<Bill>("Bill", data, idMap, tenantId, counts);
        Import<PersonalRecurringBill>("PersonalRecurringBill", data, idMap, tenantId, counts);
        Import<Subscription>("Subscription", data, idMap, tenantId, counts);
        Import<DebtRepayment>("DebtRepayment", data, idMap, tenantId, counts);
        Import<PersonalTransaction>("PersonalTransaction", data, idMap, tenantId, counts);
        Import<StatementImport>("StatementImport", data, idMap, tenantId, counts);

        // Tier 5: Depend on Tier 4
        Import<BudgetLine>("BudgetLine", data, idMap, tenantId, counts);
        Import<TransactionAttachment>("TransactionAttachment", data, idMap, tenantId, counts);
        Import<StatementImportRow>("StatementImportRow", data, idMap, tenantId, counts);
        Import<CategorisationRule>("CategorisationRule", data, idMap, tenantId, counts);

        // Tier 6: Self-referencing / graph
        Import<CustomerInsightSnapshot>("CustomerInsightSnapshot", data, idMap, tenantId, counts);
        Import<FinancialLifeGraphNode>("FinancialLifeGraphNode", data, idMap, tenantId, counts);
        Import<FinancialLifeGraphEdge>("FinancialLifeGraphEdge", data, idMap, tenantId, counts);

        // Save all Finance entities
        await _db.SaveChangesAsync(cancellationToken);

        return new CustomerDataImportModuleResult(counts, warnings);
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private void Import<T>(
        string entityTypeName,
        Dictionary<string, List<JsonElement>> data,
        Dictionary<Guid, Guid> idMap,
        Guid tenantId,
        Dictionary<string, int> counts) where T : class
    {
        if (!data.TryGetValue(entityTypeName, out var entityList) || entityList.Count == 0)
            return;

        var count = 0;
        foreach (var json in entityList)
        {
            var entity = JsonSerializer.Deserialize<T>(json.GetRawText(), JsonOptions);
            if (entity == null) continue;

            RemapIds(entity, idMap);
            SetTenantId(entity, tenantId);
            NullifyAiRunId(entity);
            ResetAuditFields(entity);

            _db.Set<T>().Add(entity);
            count++;
        }

        counts[entityTypeName] = count;
    }

    private static void RemapIds<T>(T entity, Dictionary<Guid, Guid> idMap) where T : class
    {
        var type = entity.GetType();
        foreach (var prop in type.GetProperties())
        {
            if (prop.PropertyType == typeof(Guid) && prop.CanWrite)
            {
                var value = (Guid)prop.GetValue(entity)!;
                if (idMap.TryGetValue(value, out var newValue))
                    prop.SetValue(entity, newValue);
            }
            else if (prop.PropertyType == typeof(Guid?) && prop.CanWrite)
            {
                var value = (Guid?)prop.GetValue(entity);
                if (value.HasValue && idMap.TryGetValue(value.Value, out var newValue))
                    prop.SetValue(entity, newValue);
            }
        }
    }

    private static void SetTenantId<T>(T entity, Guid tenantId) where T : class
    {
        var tenantProp = entity.GetType().GetProperty("TenantId");
        if (tenantProp is { CanWrite: true } && tenantProp.PropertyType == typeof(Guid))
            tenantProp.SetValue(entity, tenantId);
    }

    private static void NullifyAiRunId<T>(T entity) where T : class
    {
        var aiRunProp = entity.GetType().GetProperty("AiRunId");
        if (aiRunProp is { CanWrite: true } && aiRunProp.PropertyType == typeof(Guid?))
            aiRunProp.SetValue(entity, null);
    }

    private static void ResetAuditFields<T>(T entity) where T : class
    {
        var type = entity.GetType();

        var rowVersion = type.GetProperty("RowVersion");
        if (rowVersion is { CanWrite: true } && rowVersion.PropertyType == typeof(byte[]))
            rowVersion.SetValue(entity, Array.Empty<byte>());

        var updatedAt = type.GetProperty("UpdatedAt");
        if (updatedAt is { CanWrite: true } && updatedAt.PropertyType == typeof(DateTime?))
            updatedAt.SetValue(entity, null);
    }
}
