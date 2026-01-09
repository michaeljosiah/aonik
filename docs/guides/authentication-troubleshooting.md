# Authentication Troubleshooting Guide

This guide helps you diagnose and fix common authentication and authorization issues in AONIK.

---

## 🔍 Quick Diagnosis Steps

When you encounter authentication/authorization issues:

1. **Check API Response Status Code:**
   - `401 Unauthorized` → Authentication problem (who are you?)
   - `403 Forbidden` → Authorization problem (you can't do this)
   - `500 Internal Server Error` → Configuration or server issue

2. **Inspect JWT Token:**
   - Go to [jwt.ms](https://jwt.ms) and paste your token
   - Verify claims: `iss`, `aud`, `sub`, tenant claim
   - Check expiration: `exp` (Unix timestamp)

3. **Check Logs:**
   - AONIK API logs: Look for authentication errors
   - Browser console: Check for CORS or network errors
   - IdP logs (Azure AD/Auth0): Check token issuance

4. **Verify Configuration:**
   - Authority URL matches IdP
   - Audience matches expected value
   - Required claims are configured

---

## ❌ Common Error Messages

### Error: "IDX10205: Issuer validation failed"

**Full Error:**
```
IDX10205: Issuer validation failed. Issuer: 'https://login.microsoftonline.com/{wrong-tenant-id}/v2.0'. 
Did not match: validationParameters.ValidIssuer: 'https://login.microsoftonline.com/{expected-tenant-id}/v2.0'
```

**Cause:**
- The `iss` claim in your JWT token doesn't match the configured Authority
- Usually happens with multi-tenant Azure AD apps or wrong tenant ID

**Solutions:**

1. **Verify Authority in appsettings.json:**
```json
"Authentication": {
  "Authority": "https://login.microsoftonline.com/{correct-tenant-id}/v2.0",
  "Audience": "api://your-api-client-id"
}
```

2. **For Azure AD: Get Correct Tenant ID:**
   - Go to Azure Portal → Entra ID → Overview
   - Copy "Tenant ID"
   - Update Authority URL

3. **Multi-Tenant Apps:**
   - If using multi-tenant registration, use `common` or `organizations`:
   ```json
   "Authority": "https://login.microsoftonline.com/organizations/v2.0"
   ```
   - Update `AonikAuthenticationSetup.cs` to remove issuer validation:
   ```csharp
   options.TokenValidationParameters.ValidateIssuer = false; // Only for multi-tenant!
   ```

---

### Error: "IDX10214: Audience validation failed"

**Full Error:**
```
IDX10214: Audience validation failed. Audiences: 'api://wrong-audience'. 
Did not match: validationParameters.ValidAudience: 'api://expected-audience'
```

**Cause:**
- The `aud` claim in your JWT token doesn't match the configured Audience
- Client app is requesting token for wrong API

**Solutions:**

1. **Verify Audience in appsettings.json:**
```json
"Authentication": {
  "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
  "Audience": "api://your-actual-api-client-id"
}
```

2. **For Azure AD: Check App Registration:**
   - Go to Azure Portal → App Registrations → Your API
   - Copy "Application (client) ID"
   - Audience should be `api://{this-client-id}`

3. **For Auth0: Check API Identifier:**
   - Go to Auth0 Dashboard → Applications → APIs → Your API
   - Copy "Identifier"
   - Audience should match exactly (e.g., `https://api.aonik.com`)

4. **Update Client Application:**
   - Ensure client requests token with correct scope/audience
   - Azure AD: `scope: "api://{api-client-id}/.default"`
   - Auth0: `audience: "https://api.aonik.com"`

---

### Error: "The signature is invalid"

**Full Error:**
```
IDX10511: Signature validation failed. Unable to match keys
```

**Cause:**
- JWT token signature can't be verified using IdP's public keys
- Wrong Authority configured or keys rotated

**Solutions:**

1. **Verify Authority URL:**
   - Must match token issuer exactly
   - Check `iss` claim in token at [jwt.ms](https://jwt.ms)
   - Update `appsettings.json` Authority to match

2. **Check JWKS Endpoint Accessibility:**
   - Azure AD: `https://login.microsoftonline.com/{tenant-id}/v2.0/.well-known/openid-configuration`
   - Auth0: `https://{your-domain}.auth0.com/.well-known/openid-configuration`
   - Verify AONIK server can reach this URL (firewall/proxy)

3. **Clear Token Cache:**
   - Get fresh token from IdP
   - Old cached tokens may have wrong signature

4. **Key Rotation (Rare):**
   - Restart AONIK API to fetch updated keys
   - IdPs rotate keys periodically

---

### Error: "401 Unauthorized" (Generic)

**Possible Causes & Solutions:**

#### 1. **Missing Authorization Header**

**Check:**
- Browser Dev Tools → Network → Request Headers
- Should see: `Authorization: Bearer eyJhbGc...`

**Fix:**
- Client app must send token in Authorization header
- Example (JavaScript):
```javascript
fetch('https://api.aonik.com/billing/invoices', {
  headers: {
    'Authorization': `Bearer ${accessToken}`
  }
})
```

#### 2. **Token Expired**

**Check:**
- Decode token at [jwt.ms](https://jwt.ms)
- Look at `exp` claim (Unix timestamp)
- Compare with current time: `date +%s` (Linux) or online converter

**Fix:**
- Refresh token or request new one from IdP
- Azure AD tokens expire in 1 hour by default
- Auth0 tokens expire based on API settings

#### 3. **Wrong Environment**

**Check:**
- Are you using development token against production API?
- Are you using production token against localhost?

**Fix:**
- Ensure Authority/Audience match environment
- Development usually uses different IdP tenant/API

#### 4. **CORS Issues (Browser Only)**

**Check:**
- Browser console shows CORS error
- Preflight OPTIONS request fails

**Fix:**
- Update CORS policy in `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://your-frontend-domain.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

---

### Error: "403 Forbidden" (Authorization Failed)

**Cause:**
- User authenticated successfully but lacks permission
- Authorization policy requires permission user doesn't have

**Solutions:**

#### 1. **Check User Has Role**

```sql
-- Run in SQL Server Management Studio
SELECT u.Email, u.ExternalSubject, r.Name as RoleName
FROM Users u
LEFT JOIN UserRoles ur ON u.Id = ur.UserId
LEFT JOIN Roles r ON ur.RoleId = r.Id
WHERE u.TenantId = 'your-tenant-id'
  AND u.Email = 'user@example.com';
```

**If no roles returned:**
- User exists but has no roles assigned
- See [Roles and Permissions Guide](roles-and-permissions.md) to assign role

#### 2. **Check Role Has Permission**

```sql
-- Find required permission for endpoint (check API code or logs)
-- Example: invoices.create

SELECT r.Name as RoleName, p.Name as PermissionName
FROM Roles r
INNER JOIN RolePermissions rp ON r.Id = rp.RoleId
INNER JOIN Permissions p ON rp.PermissionId = p.Id
WHERE r.TenantId = 'your-tenant-id'
  AND p.Name = 'invoices.create';
```

**If no results:**
- Role exists but lacks required permission
- Add permission to role (see guide above)

#### 3. **Check Policy Requirements**

**Find endpoint requiring permission:**
- Check endpoint code or API logs
- Example from `CreateInvoiceEndpoint.cs`:
```csharp
Policies.RequirePermission("invoices.create")
```

**Verify user has this exact permission through ANY assigned role**

#### 4. **Platform Admin Access**

**If endpoint requires platform admin (`Policies.RequirePlatformAdmin`):**

Check JWT token at [jwt.ms](https://jwt.ms) for claim:
- Azure AD: `roles` claim contains `"Aonik.PlatformAdmin"`
- Auth0: Custom claim `https://aonik.com/roles` contains `"Aonik.PlatformAdmin"`

**If missing:**
- Azure AD: Assign App Role in Enterprise Applications
- Auth0: Assign role in Auth0 Dashboard and verify Action script

---

### Error: "No tenant context available"

**Cause:**
- `TenantResolver` couldn't extract tenant ID from request
- Wrong routing mode configured or claim missing

**Solutions:**

#### 1. **Verify Routing Mode (appsettings.json):**

```json
"Tenancy": {
  "RoutingMode": "Claim"  // Production: Claim, Development: Header
}
```

#### 2. **Claim Mode (Production):**

**Check JWT token for tenant claim:**
- Azure AD: Custom claim `extension_TenantId` or `tenantId`
- Auth0: Custom claim `https://aonik.com/tenantId`

**If missing:**
- Azure AD: Follow [Azure AD Setup Guide](authentication-azure-ad.md#step-3-configure-optional-claims)
- Auth0: Follow [Auth0 Setup Guide](authentication-auth0.md#step-2-create-auth0-action-for-custom-claims)

**Verify claim name in `AonikAuthenticationSetup.cs`:**
```csharp
var tenantIdClaim = context.Principal.FindFirst("extension_TenantId") // Azure AD
                 ?? context.Principal.FindFirst("https://aonik.com/tenantId"); // Auth0
```

#### 3. **Header Mode (Development Only):**

**Send X-Tenant-ID header:**
```bash
curl -H "Authorization: Bearer {token}" \
     -H "X-Tenant-ID: 11111111-1111-1111-1111-111111111111" \
     https://localhost:5001/billing/invoices
```

**Warning:** Never use Header mode in production (security risk)

#### 4. **Subdomain Mode (Advanced):**

**Verify tenant has subdomain configured:**
```sql
SELECT Id, Name, Subdomain FROM Tenants;
```

**Access API via subdomain:**
- `https://acme-corp.aonik.com/billing/invoices`
- Requires DNS/load balancer configuration

---

### Error: "Tenant is not active"

**Cause:**
- Tenant exists but `IsActive = false` in database
- Set during tenant creation or deactivation

**Solution:**

```sql
UPDATE Tenants
SET IsActive = 1
WHERE Id = 'your-tenant-id';
```

**Check why tenant was deactivated** (subscription expired, compliance issue, etc.)

---

### Error: "User identity not found or inactive"

**Cause:**
- User record exists but `IsActive = false`
- Or user was deleted

**Solution:**

```sql
-- Check user status
SELECT Email, IsActive FROM Users WHERE ExternalSubject = 'user-subject-from-token';

-- Reactivate if needed
UPDATE Users
SET IsActive = 1
WHERE ExternalSubject = 'user-subject-from-token'
  AND TenantId = 'your-tenant-id';
```

---

## 🛠️ Debugging Checklist

Work through this checklist systematically:

### Step 1: Token Acquisition
- [ ] Client app successfully acquires access token from IdP
- [ ] Token appears in network request (Dev Tools → Network → Headers)
- [ ] Token is not expired (check at jwt.ms)

### Step 2: Token Structure
- [ ] Token has `iss` claim matching Authority
- [ ] Token has `aud` claim matching Audience
- [ ] Token has `sub` claim (subject identifier)
- [ ] Token has tenant claim (if using Claim routing mode)

### Step 3: AONIK Configuration
- [ ] `appsettings.json` Authority matches token `iss`
- [ ] `appsettings.json` Audience matches token `aud`
- [ ] Routing mode is correct (Claim for production, Header for dev)
- [ ] AONIK server can reach IdP's JWKS endpoint

### Step 4: Database State
- [ ] Tenant exists and `IsActive = true`
- [ ] User exists in database (check by ExternalSubject)
- [ ] User `IsActive = true`
- [ ] User has at least one role assigned (check UserRoles table)
- [ ] Role has required permission (check RolePermissions table)

### Step 5: Endpoint Requirements
- [ ] Identify which permission endpoint requires (check endpoint code)
- [ ] Verify user has permission through assigned roles
- [ ] If platform admin endpoint, verify JWT has admin role claim

---

## 🧪 Testing Tools & Commands

### Decode JWT Token

**Online:**
- [jwt.ms](https://jwt.ms) - Microsoft's JWT decoder
- [jwt.io](https://jwt.io) - Auth0's JWT decoder (supports validation)

**Command Line (Linux/Mac):**
```bash
# Extract payload (second part of JWT)
echo "eyJhbGc..." | cut -d'.' -f2 | base64 -d | jq
```

**PowerShell (Windows):**
```powershell
# Install: Install-Module -Name JWT
Import-Module JWT
Get-JWTDetails -Token "eyJhbGc..."
```

---

### Test API with Token

**cURL:**
```bash
curl -X GET https://localhost:5001/billing/invoices \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "X-Tenant-ID: 11111111-1111-1111-1111-111111111111" \
  -k
```

**PowerShell:**
```powershell
$headers = @{
    "Authorization" = "Bearer eyJhbGc..."
    "X-Tenant-ID" = "11111111-1111-1111-1111-111111111111"
}
Invoke-RestMethod -Uri "https://localhost:5001/billing/invoices" -Headers $headers
```

**Postman:**
1. Create request: `GET https://localhost:5001/billing/invoices`
2. Authorization tab → Type: Bearer Token → Paste token
3. Headers tab → Add `X-Tenant-ID` (if using Header routing mode)
4. Send

---

### Check Database State

**User Identity:**
```sql
SELECT 
    u.Id,
    u.Email,
    u.ExternalIssuer,
    u.ExternalSubject,
    u.IsActive,
    t.Name as TenantName
FROM Users u
INNER JOIN Tenants t ON u.TenantId = t.Id
WHERE u.ExternalSubject = 'subject-from-token';
```

**User Roles:**
```sql
SELECT 
    u.Email,
    r.Name as RoleName,
    r.Description
FROM Users u
INNER JOIN UserRoles ur ON u.Id = ur.UserId
INNER JOIN Roles r ON ur.RoleId = r.Id
WHERE u.Email = 'user@example.com'
  AND u.TenantId = 'tenant-id';
```

**Role Permissions:**
```sql
SELECT 
    r.Name as RoleName,
    p.Name as PermissionName,
    p.Description
FROM Roles r
INNER JOIN RolePermissions rp ON r.Id = rp.RoleId
INNER JOIN Permissions p ON rp.PermissionId = p.Id
WHERE r.Name = 'Accountant'
  AND r.TenantId = 'tenant-id';
```

**All Permissions for User:**
```sql
SELECT DISTINCT
    u.Email,
    p.Name as PermissionName,
    r.Name as GrantedByRole
FROM Users u
INNER JOIN UserRoles ur ON u.Id = ur.UserId
INNER JOIN Roles r ON ur.RoleId = r.Id
INNER JOIN RolePermissions rp ON r.Id = rp.RoleId
INNER JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'user@example.com'
  AND u.TenantId = 'tenant-id'
ORDER BY p.Name;
```

---

## 🌐 Network & Infrastructure Issues

### HTTPS/TLS Issues

**Error:** "The SSL connection could not be established"

**Solutions:**
- **Development:** Accept self-signed certificate or use `-k` flag in curl
- **Production:** Ensure valid TLS certificate installed
- **Corporate Proxy:** Configure proxy settings in client app

---

### Firewall Blocking IdP

**Symptom:** "Unable to retrieve JWKS"

**Check:**
```bash
# Can AONIK server reach IdP?
curl https://login.microsoftonline.com/{tenant-id}/v2.0/.well-known/openid-configuration
curl https://{your-domain}.auth0.com/.well-known/openid-configuration
```

**Solutions:**
- Whitelist IdP domains in firewall
- Configure proxy settings if behind corporate proxy
- Check container/pod network policies (Kubernetes)

---

### CORS Issues (Browser)

**Symptom:** Console error "CORS policy blocked"

**Check:**
- Preflight OPTIONS request fails
- `Access-Control-Allow-Origin` header missing

**Fix in Program.cs:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "https://app.aonik.com",
            "https://acme-corp.aonik.com")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials(); // If using cookies
    });
});

// IMPORTANT: CORS must come before Authentication
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
```

---

## 🔒 Security Warnings

### Never Disable Security Features

**❌ DO NOT DO THIS (even in development):**
```csharp
// Disables signature validation - HUGE SECURITY RISK
options.TokenValidationParameters.ValidateIssuerSigningKey = false;

// Disables token expiration - tokens never expire
options.TokenValidationParameters.ValidateLifetime = false;

// Accepts any audience - anyone can call API
options.TokenValidationParameters.ValidateAudience = false;
```

**✅ Instead:**
- Fix your configuration to match tokens
- Use proper development/production environments
- Use Header routing mode for local testing only

---

### Header Routing Mode Security

**Development Only:**
```json
// appsettings.Development.json
"Tenancy": {
  "RoutingMode": "Header"
}
```

**Production Must Use Claim:**
```json
// appsettings.json
"Tenancy": {
  "RoutingMode": "Claim"
}
```

**Why:**
- Headers can be forged by client
- Claims come from trusted IdP signature
- Never trust user-supplied tenant ID in production

---

## 📞 Getting More Help

### Enable Detailed Logging

**appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.Authentication": "Debug",
      "Microsoft.AspNetCore.Authorization": "Debug",
      "Aonik.Infrastructure.Authentication": "Debug"
    }
  }
}
```

**Run API and watch logs:**
```bash
dotnet run --project src/Aonik.Api
```

Look for:
- Token validation success/failure
- Claim extraction details
- Authorization policy evaluation
- Tenant resolution attempts

---

### Check IdP Status Pages

**Azure AD:**
- [Azure Status](https://status.azure.com/status)
- Search for "Azure Active Directory"

**Auth0:**
- [Auth0 Status](https://status.auth0.com)

---

### Review Documentation

- [Authentication Overview](../features/authentication-authorization.md)
- [Azure AD Setup](authentication-azure-ad.md)
- [Auth0 Setup](authentication-auth0.md)
- [Permissions Reference](../reference/permissions.md)
- [Roles and Permissions Guide](roles-and-permissions.md)

---

### Common Gotchas

1. **Token caching:** Client apps cache tokens. Clear cache or wait for expiration.
2. **Environment mismatch:** Development token won't work against production API.
3. **Case sensitivity:** Permission names are case-sensitive (`invoices.create` ≠ `Invoices.Create`)
4. **Tenant context:** Same user identity = different User IDs per tenant
5. **JIT provisioning:** First login creates user with ZERO permissions
6. **Role changes:** Logout/login required after role/permission changes (token already issued)
7. **Middleware order:** Authentication must come before Authorization in `Program.cs`
8. **Time synchronization:** Server clock skew can cause token expiration issues

---

## ✅ Quick Reference: Error → Solution

| Error | Quick Fix |
|-------|-----------|
| IDX10205: Issuer validation failed | Match Authority to token `iss` claim |
| IDX10214: Audience validation failed | Match Audience to token `aud` claim |
| The signature is invalid | Verify Authority URL, restart API |
| 401: Missing Authorization header | Client must send `Authorization: Bearer {token}` |
| 401: Token expired | Get fresh token from IdP |
| 403: Forbidden | Assign role with required permission to user |
| No tenant context available | Add tenant claim to token (Claim mode) or X-Tenant-ID header (Header mode) |
| Tenant is not active | `UPDATE Tenants SET IsActive = 1` |
| User not found | Wait for JIT provisioning on first API call with valid token |

---

**Pro Tip:** When troubleshooting, work through authentication first (401 errors), then authorization (403 errors), then configuration (500 errors). Most issues are configuration mismatches between IdP and AONIK.
