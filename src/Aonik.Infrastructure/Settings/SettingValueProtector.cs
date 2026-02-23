using Microsoft.AspNetCore.DataProtection;

using Aonik.Platform.Contracts.Services.Settings;

namespace Aonik.Infrastructure.Settings;

public class SettingValueProtector : ISettingValueProtector
{
    private readonly IDataProtector _protector;

    public SettingValueProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Aonik.Settings");
    }

    public string Protect(string value)
    {
        return _protector.Protect(value);
    }

    public string Unprotect(string value)
    {
        return _protector.Unprotect(value);
    }
}
