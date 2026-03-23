# Getting Started

This guide walks through a fast local setup with clear steps for Auth0 or Azure AD, including how the first tenant and owner are created.

## What You Need

- .NET 10 SDK
- Git
- Database option:
  - Docker Desktop (recommended with Aspire), or
  - SQL Server LocalDB (no Docker)
- An identity provider account (Auth0 or Azure AD) that can issue JWTs for the API

## Quick Start Flow

1. Choose Auth0 or Azure AD configuration.
2. Configure `PlatformAdmin` and `Bootstrap` defaults.
3. Initialize the database (migrations + base seed data).
4. Configure a one-time bootstrap install code.
5. Run the API (or AppHost).
6. Call the bootstrap endpoint once to create the initial tenant/owner.

## Required Configuration

AONIK relies on external authentication for normal runtime access. First-run bootstrap now uses a one-time install code so the platform can be initialized before the first external identity is linked.

Update `src/Aonik.Api/appsettings.Development.json` (or user secrets) with one of the configurations below.

### Option A: Auth0

```json
{
  "Auth": {
    "Provider": "Auth0",
    "TenantRouting": "Header",
    "Auth0": {
      "Authority": "https://{your-domain}.auth0.com/",
      "Audience": "{your-api-identifier}",
      "ValidateIssuer": true,
      "ClockSkewSeconds": 300
    }
  },
  "PlatformAdmin": {
    "RoleClaimType": "roles",
    "RoleValue": "Aonik.PlatformAdmin",
    "ScopeClaimType": "aonik_platform_admin",
    "AdminEmails": ["you@example.com"]
  },
  "Bootstrap": {
    "Enabled": true,
    "SetupSecret": "set-a-strong-install-code",
    "TenantName": "Aonik Dev Tenant",
    "Environment": "Development",
    "DefaultCurrency": "USD",
    "SupportedCountries": ["US"]
  }
}
```

### Option B: Azure AD (Microsoft Entra ID)

```json
{
  "Auth": {
    "Provider": "AzureAd",
    "TenantRouting": "Header",
    "AzureAd": {
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "Audience": "api://{client-id}",
      "ValidateIssuer": true,
      "ClockSkewSeconds": 300
    }
  },
  "PlatformAdmin": {
    "RoleClaimType": "roles",
    "RoleValue": "Aonik.PlatformAdmin",
    "ScopeClaimType": "aonik_platform_admin",
    "AdminEmails": ["you@example.com"]
  },
  "Bootstrap": {
    "Enabled": true,
    "SetupSecret": "set-a-strong-install-code",
    "TenantName": "Aonik Dev Tenant",
    "Environment": "Development",
    "DefaultCurrency": "USD",
    "SupportedCountries": ["US"]
  }
}
```

Notes:
- `Bootstrap.SetupSecret` should come from user secrets, environment variables, or deployment configuration rather than committed JSON.
- The platform allows bootstrap only when no tenants exist.
- After bootstrap, the first owner signs in normally and AONIK links that identity to the pending owner profile using the configured email.

## Initialize Database (Fresh Install)

Run the migrator before first API start:

```bash
dotnet run --project src/Aonik.Migrator
```

This applies migrations and seeds global base data used by all modules.

## Run With Aspire (Recommended)

Runs API + Worker + SQL Server via the AppHost:

```bash
dotnet run --project src/Aonik.AppHost
```

Aspire provisions SQL Server based on `src/Aonik.AppHost/AppHost.cs`.

## Run API Directly (Development)

```bash
dotnet run --project src/Aonik.Api
```

Development uses `src/Aonik.Api/appsettings.Development.json` for the connection string and will fall back to LocalDB if not set.

## Create the Initial Tenant and Owner

First-run bootstrap no longer requires an external login. Instead, the system owner uses the one-time install code and the email that should own the deployment.

Requirements:
- No tenants exist yet
- `Bootstrap.Enabled=true`
- `Bootstrap.SetupSecret` is configured

Steps:
1. Call the bootstrap endpoint with the install code and owner email:

```bash
curl -X POST "https://localhost:5001/bootstrap" \
  -H "Content-Type: application/json" \
  -d '{
    "setupSecret": "<install-code>",
    "ownerEmail": "owner@example.com",
    "ownerDisplayName": "System Owner"
  }'
```

What happens during bootstrap:
- A tenant is created
- The tenant is provisioned and activated
- A pending owner user/profile is created
- The owner receives `PlatformAdmin` and `TenantAdmin`

After bootstrap, sign in normally through your identity provider using the same email address. AONIK links that external identity to the pending owner profile and then you can continue tenant setup.

Bootstrap is one-time for fresh install. If a tenant already exists, `/bootstrap` returns `409 Conflict`.

## Adding More Users

Additional users are created automatically on first login but receive no roles by default. A tenant admin must assign roles before they can access protected endpoints.

To add additional tenants after bootstrap, use the tenant admin endpoints (`/admin/tenants`) instead of `/bootstrap`.

## Next Steps

- Auth system and claims: `docs/features/authentication-authorization.md`
- Auth0 setup guide: `docs/guides/authentication-auth0.md`
- Azure AD setup guide: `docs/guides/authentication-azure-ad.md`
- Local development notes: `docs/deployment/local-development.md`
- Run tests: `dotnet test Aonik.sln`
