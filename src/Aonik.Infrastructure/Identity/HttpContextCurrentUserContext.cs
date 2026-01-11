using Microsoft.AspNetCore.Http;

using Aonik.SharedKernel.Abstractions;

namespace Aonik.Infrastructure.Identity;

public class HttpContextCurrentUserContext : ICurrentUserContext
{
    private const string UserIdKey = "AonikUserId";
    private const string TenantIdKey = "AonikTenantId";
    private const string ExternalIssuerKey = "AonikExternalIssuer";
    private const string ExternalSubjectKey = "AonikExternalSubject";
    private const string RolesKey = "AonikRoles";
    private const string IsAuthenticatedKey = "AonikIsAuthenticated";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _userId;
    private Guid? _tenantId;
    private string? _externalIssuer;
    private string? _externalSubject;
    private IReadOnlyCollection<string>? _roles;
    private bool? _isAuthenticated;

    public HttpContextCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get => _userId ?? GetItem<Guid?>(UserIdKey);
        set
        {
            _userId = value;
            SetItem(UserIdKey, value);
        }
    }

    public Guid? TenantId
    {
        get => _tenantId ?? GetItem<Guid?>(TenantIdKey);
        set
        {
            _tenantId = value;
            SetItem(TenantIdKey, value);
        }
    }

    public string? ExternalIssuer
    {
        get => _externalIssuer ?? GetItem<string?>(ExternalIssuerKey);
        set
        {
            _externalIssuer = value;
            SetItem(ExternalIssuerKey, value);
        }
    }

    public string? ExternalSubject
    {
        get => _externalSubject ?? GetItem<string?>(ExternalSubjectKey);
        set
        {
            _externalSubject = value;
            SetItem(ExternalSubjectKey, value);
        }
    }

    public IReadOnlyCollection<string> Roles
    {
        get => _roles ?? GetItem<IReadOnlyCollection<string>>(RolesKey) ?? Array.Empty<string>();
        set
        {
            _roles = value ?? Array.Empty<string>();
            SetItem(RolesKey, _roles);
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated ?? GetItem<bool?>(IsAuthenticatedKey)
            ?? _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
        set
        {
            _isAuthenticated = value;
            SetItem(IsAuthenticatedKey, value);
        }
    }

    private T? GetItem<T>(string key)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue(key, out var value) != true)
        {
            return default;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    private void SetItem(string key, object? value)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return;
        }

        if (value == null)
        {
            context.Items.Remove(key);
            return;
        }

        context.Items[key] = value;
    }
}
