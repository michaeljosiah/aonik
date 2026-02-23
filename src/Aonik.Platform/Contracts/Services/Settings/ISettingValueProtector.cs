namespace Aonik.Platform.Contracts.Services.Settings;

public interface ISettingValueProtector
{
    string Protect(string value);
    string Unprotect(string value);
}
