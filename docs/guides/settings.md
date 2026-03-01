# Settings

AONIK provides a lightweight settings system inspired by ABP, with global, tenant, and user scopes. Settings are defined in code and can be overridden dynamically in the database.

## Scopes and Resolution

Settings resolve in this order:

1. User scope
2. Tenant scope
3. Global scope
4. App configuration (`appsettings.json` or environment)
5. Default value in code

## Defining Settings

Settings are defined in `SettingDefinitions` and described by `SettingDefinition`.

- `Key`: Unique name for the setting
- `DefaultValue`: Optional fallback value
- `IsEncrypted`: Stored encrypted at rest
- `IsVisibleToClients`: Available via the public settings endpoint

### IDP Settings

The registration flow uses these keys:

- `Auth.Provider` (`AzureAd` or `Auth0`)
- `Auth.Auth0.Domain`, `Auth.Auth0.Audience`, `Auth.Auth0.ClientId`, `Auth.Auth0.ManagementClientId`, `Auth.Auth0.ManagementClientSecret`, `Auth.Auth0.Connection`, `Auth.Auth0.ManagementAudience`
- `Auth.AzureAd.Authority`, `Auth.AzureAd.Audience`, `Auth.AzureAd.ClientId`, `Auth.AzureAd.ClientSecret`, `Auth.AzureAd.TenantId`, `Auth.AzureAd.UserPrincipalNameDomain`

Auth keys are **configuration-managed**. They are read from application configuration (`appsettings*.json`, environment variables, secret stores) and are not writable via settings APIs.

Public clients can read `Auth.Provider` and the non‑secret Auth0/Azure AD values via `GET /v1/settings/public`.

## Endpoints

### Public (client-facing)

- `GET /v1/settings/public`
  - Returns settings marked `IsVisibleToClients`
  - Optional query: `?tenantId=<guid>`

### Resolved values

- `GET /v1/settings/resolved/{key}`
  - Returns the resolved value and source
  - Requires `Settings.Read`

### Tenant scope

- `GET /tenant/settings/values/{key}`
- `PUT /tenant/settings/values/{key}`
  - Requires `Settings.Read` / `Settings.Write`

### User scope

- `GET /v1/settings/user/{key}`
- `PUT /v1/settings/user/{key}`
  - Requires `Settings.Read` / `Settings.Write`

### Global scope (admin)

- `GET /admin/settings/values/{key}`
- `PUT /admin/settings/values/{key}`
  - Requires `PlatformAdmin`

## Examples

### Update a tenant setting

```
PUT /tenant/settings/values/Auth.Provider
{
  "key": "Auth.Provider",
  "value": "Auth0"
}
```

### Read a resolved setting

```
GET /v1/settings/resolved/Auth.Provider
```

## Notes

- Settings marked `IsEncrypted` are encrypted at rest.
- Public settings never include secrets.
- The `Settings.Read` / `Settings.Write` permissions control access for tenant and user scopes.
