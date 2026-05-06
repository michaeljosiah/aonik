using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Persistence;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Shared upsert helpers for catalog entities (categories, billers, services).
/// Used by both <see cref="CatalogSeedPhase"/> and
/// <see cref="CrossBorderCatalogSeedPhase"/>.
/// </summary>
internal sealed class CatalogUpsertHelper
{
    private readonly FinanceDbContext _db;

    public CatalogUpsertHelper(FinanceDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> UpsertCategoryAsync(
        Guid tenantId,
        Guid categoryId,
        string countryCode,
        string name,
        string description,
        int sortOrder,
        DateTime now,
        Guid? userId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var category = await _db.CatalogBillerCategories
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == countryCode
                                         && item.Name == name,
                cancellationToken);

        if (category == null)
        {
            category = new CatalogBillerCategory
            {
                Id = categoryId,
                TenantId = tenantId,
                CountryCode = countryCode,
                Name = name,
                Description = description,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _db.CatalogBillerCategories.Add(category);
            operations.Add($"Catalog category seeded: {countryCode} {name}");
        }
        else
        {
            category.Name = name;
            category.Description = description;
            category.SortOrder = sortOrder;
            category.IsActive = true;
            category.UpdatedAt = now;
            category.UpdatedBy = userId;
        }

        return category.Id;
    }

    public async Task<Guid> UpsertBillerAsync(
        Guid tenantId,
        Guid categoryId,
        Guid billerId,
        string name,
        string description,
        DateTime now,
        Guid? userId,
        List<string> operations,
        CancellationToken cancellationToken,
        Guid correspondentPartnerId,
        string countryCode = "GH")
    {
        var biller = await _db.CatalogBillers
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == countryCode
                                         && item.Name == name,
                cancellationToken);

        if (biller == null)
        {
            biller = new CatalogBiller
            {
                Id = billerId,
                TenantId = tenantId,
                CategoryId = categoryId,
                CountryCode = countryCode,
                Name = name,
                Description = description,
                CorrespondentPartnerId = correspondentPartnerId,
                SupportEmail = "support@aonik.demo",
                SupportPhone = "+233-000-0000",
                IsActive = true,
                IsFeatured = true,
                SortOrder = 1,
                CreatedAt = now,
                CreatedBy = userId
            };
            _db.CatalogBillers.Add(biller);
            operations.Add($"Catalog biller seeded: {name}");
        }
        else
        {
            biller.CategoryId = categoryId;
            biller.Name = name;
            biller.Description = description;
            biller.CountryCode = countryCode;
            biller.CorrespondentPartnerId = correspondentPartnerId;
            biller.IsActive = true;
            biller.UpdatedAt = now;
            biller.UpdatedBy = userId;
        }

        return biller.Id;
    }

    public async Task<Guid> UpsertServiceAsync(
        Guid tenantId,
        Guid billerId,
        Guid serviceId,
        string serviceCode,
        string name,
        string type,
        string currency,
        decimal minAmount,
        decimal maxAmount,
        bool supportsPartial,
        bool requiresValidation,
        string fieldsJson,
        string? validationJson,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var service = await _db.CatalogBillerServices
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.ServiceCode == serviceCode,
                cancellationToken);

        var now = DateTime.UtcNow;

        if (service == null)
        {
            service = new CatalogBillerService
            {
                Id = serviceId,
                TenantId = tenantId,
                BillerId = billerId,
                ServiceCode = serviceCode,
                Name = name,
                Type = type,
                Currency = currency,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                SupportsPartialPayment = supportsPartial,
                RequiresValidation = requiresValidation,
                IsActive = true,
                FieldsJson = fieldsJson,
                ValidationJson = validationJson,
                SortOrder = 1,
                CreatedAt = now
            };
            _db.CatalogBillerServices.Add(service);
            operations.Add($"Catalog service seeded: {name}");
        }
        else
        {
            service.BillerId = billerId;
            service.ServiceCode = serviceCode;
            service.Name = name;
            service.Type = type;
            service.Currency = currency;
            service.MinAmount = minAmount;
            service.MaxAmount = maxAmount;
            service.SupportsPartialPayment = supportsPartial;
            service.RequiresValidation = requiresValidation;
            service.IsActive = true;
            service.FieldsJson = fieldsJson;
            service.ValidationJson = validationJson;
            service.UpdatedAt = now;
        }

        return service.Id;
    }

    public static string BuildServiceFieldsJson(IEnumerable<CatalogServiceField> fields)
    {
        return JsonSerializer.Serialize(fields);
    }
}
