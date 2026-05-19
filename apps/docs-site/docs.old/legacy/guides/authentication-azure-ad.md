:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Azure AD (Microsoft Entra ID) Setup Guide

This guide walks you through configuring Azure Active Directory (now called **Microsoft Entra ID**) as the identity provider for AONIK.

## Prerequisites

Before you begin, make sure you have:

- ✅ An Azure account with an active subscription ([create one free](https://azure.microsoft.com/free/))
- ✅ **Application Developer** role or higher in your Azure AD tenant
- ✅ Access to the [Microsoft Entra admin center](https://entra.microsoft.com)
- ✅ Basic understanding of OAuth 2.0 and JWT tokens (see [Authentication Overview](../features/authentication-authorization.md))

---

## Step 1: Register the AONIK API Application

### 1.1 Create App Registration

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com)

2. Navigate to **Identity** > **Applications** > **App registrations**

3. Click **+ New registration**

4. Fill in the application details:
   
   **Name:** `AONIK API`
   
   **Supported account types:** Select based on your needs:
   - **Single tenant** (Recommended for most cases): Users only from your organization
   - **Multitenant**: Users from any Azure AD organization
   - **Multitenant + Personal accounts**: Includes Microsoft personal accounts (Outlook, Xbox)
   
   **Redirect URI:** Leave blank for now (APIs don't need redirect URIs)

5. Click **Register**

6. **Save these values** - you'll need them later:
   - **Application (client) ID**: Shows in the Overview page (e.g., `a1b2c3d4-...`)
   - **Directory (tenant) ID**: Also in Overview page (e.g., `e5f6g7h8-...`)

### 1.2 Create an Application ID URI

The Application ID URI is how your API identifies itself in tokens.

1. In your app registration, go to **Manage** > **Expose an API**

2. Next to **Application ID URI**, click **Add**

3. Accept the default value or customize it:
   - **Default**: `api://{client-id}`
   - **Custom**: `https://api.yourdomain.com` or `api://aonik-api`

4. Click **Save**

5. **Save this value** - this is your **Audience** for AONIK configuration

---

## Step 2: Define API Permissions (Scopes)

Scopes define what actions client applications can request to perform on your API.

### 2.1 Add Scopes

1. Still in **Expose an API**, scroll to **Scopes defined by this API**

2. Click **+ Add a scope**

3. Fill in scope details:
   
   **Scope name:** `access_as_user`
   
   **Who can consent?** `Admins and users`
   
   **Admin consent display name:** `Access AONIK API as a user`
   
   **Admin consent description:** `Allows the application to access the AONIK API on behalf of the signed-in user`
   
   **User consent display name:** `Access AONIK on your behalf`
   
   **User consent description:** `Allows the application to access your AONIK data`
   
   **State:** `Enabled`

4. Click **Add scope**

5. Your full scope will be: `api://{your-app-id}/access_as_user`

---

## Step 3: Configure Optional Claims (CRITICAL!)

AONIK needs specific claims in the JWT token. By default, Azure AD doesn't include all of them.

### 3.1 Add Optional Claims for Access Tokens

1. Go to **Manage** > **Token configuration**

2. Click **+ Add optional claim**

3. Select **Access** token type

4. Check these claims:
   - ✅ **email** - User's email address
   - ✅ **preferred_username** - User's username (usually email)

5. Click **Add**

6. If prompted to add Microsoft Graph permissions, click **Yes** (this allows reading user profile info)

### 3.2 Important Claims Reference

After configuration, your access tokens will contain:

```json
{
  "aud": "api://aonik-api",                      // Audience (your API)
  "iss": "https://login.microsoftonline.com/{tenant-id}/v2.0",  // Issuer
  "oid": "a1b2c3d4-...",                         // Object ID (user's unique ID)
  "sub": "a1b2c3d4-...",                         // Subject (usually same as oid)
  "tid": "e5f6g7h8-...",                         // Tenant ID
  "email": "john@example.com",                   // User's email
  "preferred_username": "john@example.com",      // Preferred username
  "scp": "access_as_user"                        // Scopes granted
}
```

**Key Points:**
- AONIK uses `oid` (object ID) as the user's unique identifier
- `email` is optional but useful for display
- `tid` (tenant ID) is the Azure AD tenant, NOT the AONIK tenant

---

## Step 4: Add Custom Claim for AONIK Tenant ID

AONIK needs to know which tenant the user belongs to. We'll add a custom claim `aonik_tenant_id`.

### Option A: Using App Roles (Recommended)

App roles allow you to assign users to specific AONIK tenants.

#### 4.1 Create App Role

1. Go to **Manage** > **App roles**

2. Click **+ Create app role**

3. Fill in the details:
   
   **Display name:** `Tenant Member`
   
   **Allowed member types:** `Users/Groups`
   
   **Value:** `{aonik-tenant-id}` (e.g., `550e8400-e29b-41d4-a716-446655440000`)
   
   **Description:** `Member of AONIK tenant`

4. Click **Apply**

5. Repeat for each AONIK tenant

#### 4.2 Assign Users to App Roles

1. Go to **Identity** > **Applications** > **Enterprise applications**

2. Find your AONIK API app

3. Go to **Manage** > **Users and groups**

4. Click **+ Add user/group**

5. Select user(s) and assign them to the appropriate tenant role

6. Click **Assign**

#### 4.3 Configure Claims Mapping

This is where we extract the app role and add it as `aonik_tenant_id` claim.

**Unfortunately, this requires custom claims mapping which needs a policy:**

1. Install Azure AD PowerShell module:
   ```powershell
   Install-Module AzureAD
   Connect-AzureAD
   ```

2. Create claims mapping policy:
   ```powershell
   $policy = New-AzureADPolicy -Definition @(
     '{
       "ClaimsMappingPolicy": {
         "Version": 1,
         "IncludeBasicClaimSet": "true",
         "ClaimsSchema": [
           {
             "Source": "user",
             "ID": "assignedroles",
             "SamlClaimType": "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
             "JwtClaimType": "roles"
           }
         ]
       }
     }'
   ) -DisplayName "AONIK Claims Mapping" -Type "ClaimsMappingPolicy"
   ```

3. Apply to your service principal:
   ```powershell
   $sp = Get-AzureADServicePrincipal -Filter "displayName eq 'AONIK API'"
   Add-AzureADServicePrincipalPolicy -Id $sp.ObjectId -RefObjectId $policy.Id
   ```

**Limitation:** This approach is complex. See Option B for a simpler alternative.

### Option B: Using Extension Attributes (Simpler)

If your organization allows editing user attributes, you can store the AONIK tenant ID directly on the user object.

#### 4.4 Register Extension Attribute

1. Go to **Manage** > **Manifest**

2. Find `optionalClaims` section

3. Add custom extension attribute (requires Azure AD Premium):

```json
"optionalClaims": {
  "accessToken": [
    {
      "name": "extension_aonik_tenant_id",
      "source": "user",
      "essential": false
    }
  ]
}
```

4. Assign values via Azure AD user management or Microsoft Graph API

**Limitation:** Requires Azure AD Premium and custom attribute management.

### Option C: Use Subdomain Routing (Easiest)

If custom claims are too complex, use subdomain-based routing instead:

- Users access `https://acme-corp.aonik.io`
- AONIK looks up `acme-corp` in the database
- No custom claims needed!

See [Tenant Routing](../features/authentication-authorization.md#tenant-routing) for details.

---

## Step 5: Register Client Application (Frontend)

Your frontend (web app, mobile app, SPA) needs its own app registration.

### 5.1 Create Client App Registration

1. In **App registrations**, click **+ New registration**

2. Fill in details:
   
   **Name:** `AONIK Web App` (or `AONIK Mobile App`, etc.)
   
   **Supported account types:** Same as API app
   
   **Redirect URI:**
   - **Web app**: `https://yourdomain.com/signin-oidc`
   - **SPA**: `https://yourdomain.com/auth/callback`
   - **Mobile**: `com.yourcompany.aonik://callback`

3. Click **Register**

4. **Save the Application (client) ID**

### 5.2 Grant API Permissions

1. In the client app, go to **Manage** > **API permissions**

2. Click **+ Add a permission**

3. Go to **My APIs** tab

4. Select **AONIK API**

5. Check **access_as_user** scope

6. Click **Add permissions**

7. Click **Grant admin consent for `{your-organization}`** (admin only)

### 5.3 Configure Authentication

For **Single Page Applications (React, Angular, Vue)**:

1. Go to **Manage** > **Authentication**

2. Under **Platform configurations**, add **Single-page application**

3. Add redirect URIs (e.g., `http://localhost:3000` for development)

4. Under **Implicit grant and hybrid flows**, check:
   - ✅ **ID tokens** (for sign-in flows)
   
5. Click **Save**

For **Web Applications (ASP.NET, Next.js SSR)**:

1. Under **Platform configurations**, add **Web**

2. Configure redirect URIs

3. Enable **ID tokens** under implicit grant

4. Click **Save**

### 5.4 Create Client Secret (Confidential Clients Only)

For server-side web apps (not SPAs):

1. Go to **Manage** > **Certificates & secrets**

2. Click **+ New client secret**

3. Add description: `AONIK Web App Secret`

4. Select expiration (recommend 6-12 months)

5. Click **Add**

6. **Copy the secret value immediately** - you won't see it again!

---

## Step 6: Configure AONIK API

Now configure AONIK to trust Azure AD tokens.

### 6.1 Update appsettings.json

Edit `src/Aonik.Api/appsettings.json`:

```json
{
  "Auth": {
    "Provider": "AzureAd",
    "TenantRoutingMode": "Subdomain",  // or "Claim" if you configured custom claims
    
    "AzureAd": {
      "Authority": "https://login.microsoftonline.com/{your-tenant-id}/v2.0",
      "Audience": "api://aonik-api",  // or your custom App ID URI
      "ValidateIssuer": true,
      "ValidateAudience": true,
      "ClockSkew": 300
    }
  },
  
  "PlatformAdmin": {
    "RoleName": "Aonik.PlatformAdmin",
    "ScopeClaimType": "aonik_platform_admin"
  }
}
```

**Replace:**
- `{your-tenant-id}`: Your Azure AD tenant ID from Step 1
- `api://aonik-api`: Your Application ID URI from Step 1

### 6.2 Configure Platform Admin (Optional)

If you want to grant platform admin access to specific Azure AD users:

#### Option 1: Using App Roles

1. Create app role with value `Aonik.PlatformAdmin`
2. Assign role to admin users
3. Azure AD will include `roles: ["Aonik.PlatformAdmin"]` in token

#### Option 2: Using Groups

1. Create Azure AD security group `AONIK Platform Admins`
2. Add admin users to the group
3. Configure app to include group claims
4. Check for group ID in `groups` claim

---

## Step 7: Test the Configuration

### 7.1 Get a Test Token

Use a tool like Postman or curl to get an access token:

**Request:**
```http
POST https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

grant_type=password
&client_id={client-app-id}
&scope=api://aonik-api/.default
&username=john@example.com
&password={user-password}
```

**Important:** Password grant is only for testing. Production apps should use:
- Authorization Code flow (web apps)
- PKCE flow (SPAs, mobile apps)

### 7.2 Inspect the Token

Copy the access token and paste it into [jwt.ms](https://jwt.ms) to inspect claims:

**Verify:**
- ✅ `aud` matches your configured Audience
- ✅ `iss` matches your Authority with tenant ID
- ✅ `oid` or `sub` is present (user ID)
- ✅ `email` is present (if configured)
- ✅ `scp` contains `access_as_user`

### 7.3 Call AONIK API

```http
GET https://localhost:5001/billing/invoices
Authorization: Bearer {your-access-token}
X-Tenant-Id: {aonik-tenant-guid}  // Only needed if TenantRoutingMode is "Header" in Development
```

**Expected Responses:**
- `200 OK` with invoice data = Success! ✅
- `401 Unauthorized` = Token validation failed (check Authority/Audience config)
- `403 Forbidden` = User authenticated but lacks permissions (assign roles in AONIK)
- `404 Not Found` = Tenant not found or user not in tenant

---

## Common Issues & Solutions

### Issue: "IDX10205: Issuer validation failed"

**Cause:** Token issuer doesn't match configured Authority

**Solution:**
1. Check token's `iss` claim value
2. Verify Authority in appsettings.json includes `/v2.0` for v2 tokens
3. Ensure tenant ID in Authority matches token's `tid` claim

### Issue: "IDX10214: Audience validation failed"

**Cause:** Token audience doesn't match configured Audience

**Solution:**
1. Check token's `aud` claim value
2. Verify Audience in appsettings.json matches your App ID URI exactly
3. Ensure client requested correct scope (e.g., `api://aonik-api/access_as_user`)

### Issue: "The signature is invalid"

**Cause:** Token not signed by trusted authority or signing keys changed

**Solution:**
1. Verify token was issued by Azure AD (check `iss` claim)
2. Restart AONIK API (forces reload of signing keys)
3. Check firewall allows access to `login.microsoftonline.com`

### Issue: Custom claims not appearing in token

**Cause:** Optional claims not configured or not granted consent

**Solution:**
1. Verify claims in **Token configuration**
2. Click **Grant admin consent** in API permissions
3. Request new token (old tokens don't get new claims)

### Issue: User gets 403 after first login

**Cause:** JIT provisioning creates user with zero permissions

**Expected behavior!**

**Solution:**
1. Admin logs into AONIK
2. Finds new user in tenant
3. Assigns role with appropriate permissions
4. User can now access endpoints

---

## Production Checklist

Before going to production:

- [ ] Use **single tenant** app registration (unless you need multitenant)
- [ ] Set **token lifetime** appropriately (default 1 hour)
- [ ] Enable **Conditional Access** policies for sensitive operations
- [ ] Configure **certificate-based credentials** instead of secrets (for web apps)
- [ ] Set up **monitoring and alerts** for failed authentications
- [ ] Test **token refresh** flows in client apps
- [ ] Enable **Azure AD audit logs** for compliance
- [ ] Configure **subdomain routing** with proper DNS/SSL (if using subdomains)
- [ ] Set `TenantRoutingMode` to `"Claim"` or `"Subdomain"` (NOT `"Header"`)
- [ ] Remove any test users/apps from production tenant

---

## Additional Resources

- [Microsoft Entra ID Documentation](https://learn.microsoft.com/en-us/entra/identity-platform/)
- [Access Tokens in Microsoft Identity Platform](https://learn.microsoft.com/en-us/entra/identity-platform/access-tokens)
- [Optional Claims Configuration](https://learn.microsoft.com/en-us/entra/identity-platform/optional-claims)
- [AONIK Authentication Overview](../features/authentication-authorization.md)
- [AONIK Permissions Reference](../reference/permissions.md)
- [Troubleshooting Authentication](authentication-troubleshooting.md)

---

**Last Updated:** January 9, 2025  
**Tested With:** Microsoft Entra ID (Azure AD) v2.0 endpoints
