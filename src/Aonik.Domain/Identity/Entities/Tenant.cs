using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class Tenant : AuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public string DefaultCurrency { get; private set; } = string.Empty;
    public string SupportedCountriesJson { get; private set; } = string.Empty;

    private Tenant() { }

    public Tenant(string name, string environment, string defaultCurrency)
    {
        TenantId = Id;
        Name = name;
        Environment = environment;
        DefaultCurrency = defaultCurrency;
        SupportedCountriesJson = "[]";
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateDefaultCurrency(string currency)
    {
        DefaultCurrency = currency;
    }

    public void UpdateSupportedCountries(string supportedCountriesJson)
    {
        SupportedCountriesJson = supportedCountriesJson;
    }
}
