namespace Aonik.Application.Abstractions.Settings;

public interface ISettingValueProtector
{
    string Protect(string value);
    string Unprotect(string value);
}
