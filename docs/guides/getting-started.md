# Getting Started

This guide walks through a fast local setup with clear steps for Auth0 or Azure AD, including how the initial admin user is created.

## What You Need

- .NET 10 SDK
- Git
- Database option:
  - Docker Desktop (recommended with Aspire), or
  - SQL Server LocalDB (no Docker)
- An identity provider account (Auth0 or Azure AD) that can issue JWTs for the API

## Quick Start Flow

1. Choose Auth0 or Azure AD configuration.
2. Configure `PlatformAdmin` and `Bootstrap`.
3. Run the API (or AppHost).
4. Call the bootstrap endpoint to create the initial tenant/admin.

## Required Configuration

AONIK relies on external authentication. Configure the identity provider and specify who can perform the initial bootstrap.

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
    "TenantName": "Aonik Dev Tenant",
    "Environment": "Development",
    "DefaultCurrency": "USD",
    "SupportedCountries": ["US"]
  }
}
```

Notes:
- `PlatformAdmin.AdminEmails` is the simplest way to declare the initial admin user in local development.
- The platform allows bootstrap when no tenants exist.
- In production, platform admin is typically granted via claims rather than email.

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

## Create the Initial Tenant and Admin User

The first authenticated user to call the bootstrap endpoint becomes the initial tenant admin.

Requirements:
- No tenants exist yet
- There are no tenants yet in the database
- The user is authenticated
- In non-Development environments, the user must be a PlatformAdmin

Steps:
1. Sign in via your identity provider and obtain a JWT.
2. Call the bootstrap endpoint:

```bash
curl -X POST "https://localhost:5001/bootstrap" \
  -H "Authorization: Bearer <jwt>"
```

The response includes the new `tenantId`. Use it for API calls with header-based routing:

```bash
curl -X GET "https://localhost:5001/billing/invoices" \
  -H "Authorization: Bearer <jwt>" \
  -H "X-Tenant-Id: <tenant-guid>"
```

What happens during bootstrap:
- A tenant is created (or reused if one already exists)
- A user record is created from the external identity
- The user is assigned the `TenantAdmin` role

## Adding More Users

Additional users are created automatically on first login but receive no roles by default. A tenant admin must assign roles before they can access protected endpoints.

## Next Steps

- Auth system and claims: `docs/features/authentication-authorization.md`
- Auth0 setup guide: `docs/guides/authentication-auth0.md`
- Azure AD setup guide: `docs/guides/authentication-azure-ad.md`
- Local development notes: `docs/deployment/local-development.md`
- Run tests: `dotnet test Aonik.sln`
