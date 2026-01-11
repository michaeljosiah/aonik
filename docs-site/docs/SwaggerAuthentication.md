# Swagger OAuth2 Authentication Configuration Guide

This guide explains how to configure Swagger UI to authenticate with your OAuth2 provider (Azure AD or Auth0) in the AONIK API.

## Overview

The AONIK API now supports OAuth2 authentication in Swagger UI, allowing you to test authenticated endpoints directly from the Swagger interface. The configuration automatically adapts based on your chosen authentication provider (Azure AD or Auth0).

**See also:**
- [Local API Usage](features/authentication-authorization.md#local-api-usage)
- [Securing an Endpoint](features/authentication-authorization.md#securing-an-endpoint)

## Configuration Steps

### 1. Register Swagger as an OAuth2 Client

#### For Azure AD (Entra ID)

1. Go to Azure Portal → Azure Active Directory → App Registrations
2. Select your API application registration (or create a new one)
3. Navigate to **Authentication** → **Add a platform** → **Single-page application**
4. Add redirect URI: `https://localhost:5001/swagger/oauth2-redirect.html`
5. Enable **Access tokens** and **ID tokens** under Implicit grant
6. Go to **Expose an API** and note your scope (e.g., `api://{client-id}/access_as_user`)
7. Create a separate app registration for Swagger client (recommended) or use the same client ID
8. Copy the **Application (client) ID** for Swagger configuration

**Security Note:** For production, create a separate app registration for Swagger UI with restricted permissions.

#### For Auth0

1. Go to Auth0 Dashboard → Applications → Create Application
2. Select **Single Page Application** type
3. Configure the following settings:
   - **Allowed Callback URLs**: `https://localhost:5001/swagger/oauth2-redirect.html`
   - **Allowed Web Origins**: `https://localhost:5001`
   - **Application Type**: Single Page Application
4. Go to **APIs** and ensure your API is configured with required scopes
5. Copy the **Client ID** for Swagger configuration

### 2. Update appsettings.json

Update your `appsettings.json` with the Swagger OAuth2 configuration:

```json
{
  "Auth": {
    "Provider": "AzureAd",
    "TenantRouting": "Claim",
    "AzureAd": {
      "Authority": "https://login.microsoftonline.com/{your-tenant-id}/v2.0",
      "Audience": "api://{your-api-client-id}",
      "ValidateIssuer": true,
      "ClockSkewSeconds": 300
    }
  },
  "Swagger": {
    "ClientId": "{swagger-client-id}",
    "Scopes": [
      "api://{your-api-client-id}/access_as_user"
    ],
    "RedirectUri": "/swagger/oauth2-redirect.html"
  }
}
```

**For Auth0**, configure like this:

```json
{
  "Auth": {
    "Provider": "Auth0",
    "TenantRouting": "Claim",
    "Auth0": {
      "Authority": "https://{your-domain}.auth0.com/",
      "Audience": "{your-api-identifier}",
      "ValidateIssuer": true,
      "ClockSkewSeconds": 300
    }
  },
  "Swagger": {
    "ClientId": "{swagger-client-id}",
    "Scopes": [
      "openid",
      "profile",
      "email",
      "{your-api-identifier}"
    ],
    "RedirectUri": "/swagger/oauth2-redirect.html"
  }
}
```

### 3. Development Environment Configuration

For local development, update `appsettings.Development.json`:

```json
{
  "Auth": {
    "TenantRouting": "Header"
  },
  "Swagger": {
    "ClientId": "swagger-dev-client",
    "Scopes": [
      "openid",
      "profile",
      "email",
      "api://{your-api-client-id}/access_as_user"
    ],
    "RedirectUri": "https://localhost:5001/swagger/oauth2-redirect.html"
  }
}
```

## Using Swagger with Authentication

### Step 1: Start the API

```bash
dotnet run --project src/Aonik.Api
```

Navigate to: `https://localhost:5001/swagger`

### Step 2: Authenticate

1. Click the **Authorize** button (lock icon) at the top of the Swagger UI
2. Check the scopes you want to request
3. Click **Authorize**
4. You'll be redirected to your OAuth2 provider's login page
5. Sign in with your credentials
6. After successful authentication, you'll be redirected back to Swagger UI
7. The UI will now include your access token in all API requests

### Step 3: Test Endpoints

- All endpoints marked with the lock icon require authentication
- Your JWT token is automatically included in the `Authorization: Bearer {token}` header
- You can view the token by opening browser developer tools

### Step 4: Handle Multi-Tenancy

Since AONIK is multi-tenant, you need to provide tenant context:

**Development (Header mode):**
```bash
# Add X-Tenant-Id header in Swagger UI
X-Tenant-Id: {your-tenant-guid}
```

**Production (Claim mode):**
- Ensure your JWT token includes the `aonik_tenant_id` claim
- The tenant is automatically resolved from the token

## Architecture

### Components

1. **SwaggerConfiguration.cs** (`src/Aonik.Api/Configuration/SwaggerConfiguration.cs`)
   - Configures OAuth2 security scheme
   - Sets up authorization endpoints based on provider
   - Injects OAuth2 initialization into Swagger UI

2. **Program.cs** updates:
   - Replaced `SwaggerDocument()` with `AddAonikSwagger()`
   - Replaced `UseSwaggerGen()` with `UseAonikSwagger()`

3. **Configuration classes:**
   - `SwaggerOptions`: Client ID, scopes, redirect URI
   - `AuthOptions`: Provider-specific OAuth2 settings

### OAuth2 Flow

```
User → Swagger UI → Authorize Button
  ↓
OAuth2 Provider (Azure AD/Auth0) ← Authorization Request
  ↓
User Login → Token Issued
  ↓
Swagger UI ← Access Token (JWT)
  ↓
API Requests with Authorization: Bearer {token}
  ↓
AONIK API → Validates Token → Resolves Tenant → Returns Response
```

## Troubleshooting

### Error: "CORS error during OAuth2 callback"

**Solution:** Ensure the redirect URI is registered in your OAuth2 provider:
- Azure AD: Add to **Authentication** → **Redirect URIs**
- Auth0: Add to **Allowed Callback URLs**

### Error: "Invalid client_id"

**Solution:** Verify the `ClientId` in `appsettings.json` matches your OAuth2 application registration.

### Error: "Invalid scope"

**Solution:** 
- For Azure AD: Ensure the scope format is `api://{client-id}/{scope-name}`
- For Auth0: Use your API identifier (audience) as the scope

### Error: "Tenant could not be resolved"

**Solution:**
- **Development:** Add `X-Tenant-Id` header with a valid tenant GUID
- **Production:** Ensure your JWT token includes the `aonik_tenant_id` claim

### Token doesn't include required claims

**Solution:**
- Azure AD: Configure custom claims in **Token Configuration**
- Auth0: Use Actions/Rules to add custom claims to tokens

## Security Best Practices

1. **Use separate client IDs** for Swagger UI (dev) and production clients
2. **Restrict redirect URIs** to known development/testing URLs
3. **Disable Swagger in production** or restrict access with IP allowlisting
4. **Use PKCE (Proof Key for Code Exchange)** for enhanced security (enabled by default)
5. **Rotate client secrets** regularly if using client credentials
6. **Monitor authentication logs** for suspicious activity

## Additional Resources

- [Azure AD OAuth2 Documentation](https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-implicit-grant-flow)
- [Auth0 SPA Quickstart](https://auth0.com/docs/quickstart/spa)
- [OpenAPI Security Scheme](https://swagger.io/docs/specification/authentication/oauth2/)
- [FastEndpoints Swagger Documentation](https://fast-endpoints.com/docs/swagger-support)

## Configuration Reference

### SwaggerOptions Properties

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| `ClientId` | string | OAuth2 client ID for Swagger UI | `"swagger-dev-client"` |
| `Scopes` | List<string> | OAuth2 scopes to request | `["openid", "profile", "api://xxx/access"]` |
| `RedirectUri` | string | OAuth2 callback URL | `"/swagger/oauth2-redirect.html"` |

### AuthOptions Properties (Relevant for Swagger)

| Property | Type | Description |
|----------|------|-------------|
| `Provider` | string | `"AzureAd"` or `"Auth0"` |
| `AzureAd.Authority` | string | Azure AD token endpoint |
| `AzureAd.Audience` | string | API audience/client ID |
| `Auth0.Authority` | string | Auth0 domain URL |
| `Auth0.Audience` | string | Auth0 API identifier |

## Next Steps

After completing this configuration:

1. Test the authentication flow in Swagger UI
2. Verify that protected endpoints return 401 without authentication
3. Confirm that authenticated requests succeed with valid tokens
4. Test multi-tenant scenarios with different tenant IDs
5. Monitor logs for authentication events

For production deployment, review the **Security Best Practices** section and adjust configuration accordingly.
