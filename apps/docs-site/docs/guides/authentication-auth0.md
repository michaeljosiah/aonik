# Auth0 Setup Guide

This guide walks you through configuring **Auth0** as the identity provider for AONIK. Auth0 is a flexible authentication service that supports multiple identity sources (Google, GitHub, Microsoft, etc.) with minimal setup.

## Prerequisites

Before you begin, make sure you have:

- ✅ An Auth0 account ([sign up free](https://auth0.com/signup))
- ✅ Access to the [Auth0 Dashboard](https://manage.auth0.com)
- ✅ Basic understanding of OAuth 2.0 and JWT tokens (see [Authentication Overview](../features/authentication-authorization.md))

---

## Step 1: Create an Auth0 API

The Auth0 API represents your AONIK backend.

### 1.1 Create the API

1. Log into the [Auth0 Dashboard](https://manage.auth0.com)

2. Navigate to **Applications** > **APIs**

3. Click **+ Create API**

4. Fill in the details:
   
   **Name:** `AONIK API`
   
   **Identifier:** `https://api.yourdomain.com` or `https://aonik-api`
   
   > **Important:** This identifier becomes your **Audience** claim in JWT tokens. Use a URL format even if it doesn't need to resolve. This cannot be changed later!
   
   **Signing Algorithm:** `RS256` (recommended for security)

5. Click **Create**

### 1.2 Configure API Settings

1. In your new API, go to **Settings** tab

2. Verify/configure these settings:
   
   **Enable RBAC:** `ON` (enables role-based access control)
   
   **Add Permissions in the Access Token:** `ON` (includes permissions in JWT)
   
   **Allow Skipping User Consent:** `OFF` (users should know what apps access)
   
   **Token Expiration:** `86400` seconds (24 hours) - adjust as needed
   
   **Token Expiration For Browser Flows:** `7200` seconds (2 hours)

3. Scroll down and click **Save**

### 1.3 Define Permissions (Scopes)

Permissions in Auth0 are called "scopes". Let's add one for API access:

1. Still in your API, go to **Permissions** tab

2. Add a permission:
   
   **Permission (Scope):** `read:all`
   
   **Description:** `Read access to AONIK API`

3. Click **Add**

4. Add more permissions as needed (optional - AONIK handles fine-grained permissions in database):
   - `write:all` - Write access to AONIK API
   - `delete:all` - Delete access to AONIK API

**Note:** These Auth0 permissions are coarse-grained. AONIK's database-backed permissions (like `Invoice.Create`, `Payment.Read`) provide fine-grained control.

---

## Step 2: Configure Auth0 Actions (Custom Claims)

AONIK needs custom claims in the JWT token, specifically `aonik_tenant_id`. Auth0 Actions let you add these claims.

### 2.1 Create a Custom Action

1. Navigate to **Actions** > **Library**

2. Click **+ Build Custom**

3. Fill in details:
   
   **Name:** `Add AONIK Claims`
   
   **Trigger:** `Login / Post Login`
   
   **Runtime:** Latest Node.js version

4. Click **Create**

### 2.2 Add Action Code

Replace the default code with this:

```javascript
/**
 * Handler that will be called during the execution of a PostLogin flow.
 *
 * @param {Event} event - Details about the user and the context in which they are logging in.
 * @param {PostLoginAPI} api - Interface whose methods can be used to change the behavior of the login.
 */
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://aonik.io';
  
  // Get AONIK tenant ID from user metadata
  const aonikTenantId = event.user.app_metadata?.aonik_tenant_id;
  
  if (aonikTenantId) {
    // Add custom claim to access token
    api.accessToken.setCustomClaim(`${namespace}/aonik_tenant_id`, aonikTenantId);
    
    // Optionally add to ID token for client apps
    api.idToken.setCustomClaim(`${namespace}/aonik_tenant_id`, aonikTenantId);
  }
  
  // Add email claim to access token (important for AONIK user identification)
  if (event.user.email) {
    api.accessToken.setCustomClaim('email', event.user.email);
  }
  
  // Add platform admin flag if present
  const isPlatformAdmin = event.user.app_metadata?.platform_admin === true;
  if (isPlatformAdmin) {
    api.accessToken.setCustomClaim(`${namespace}/platform_admin`, true);
    api.idToken.setCustomClaim(`${namespace}/platform_admin`, true);
  }
  
  // Log for debugging (remove in production)
  console.log(`User ${event.user.email} logging in to tenant: ${aonikTenantId || 'none'}`);
};
```

**Key Points:**
- Custom claims must use a **namespaced format** (`https://aonik.io/aonik_tenant_id`)
- We read `aonik_tenant_id` from `app_metadata` (set per user)
- Email claim is added to access token for user identification in AONIK
- Platform admin flag is optional (for AONIK system administrators)

**Important Note About Email Claim:**
AONIK relies on the email claim for user identification and JWT provisioning. By default, Auth0 may not include the email claim in access tokens. The action above explicitly adds the email claim to ensure it's available to AONIK APIs.

### 2.3 Deploy the Action

1. Click **Deploy** (top right)

2. Navigate to **Actions** > **Flows**

3. Select **Login** flow

4. Drag **Add AONIK Claims** action from the right sidebar into the flow (between **Start** and **Complete**)

5. Click **Apply**

Now every login will add these custom claims to tokens!

---

## Step 3: Assign Tenant IDs to Users

Users need the `aonik_tenant_id` in their metadata. There are two ways to set this:

### Option A: Manually via Dashboard (For Testing)

1. Navigate to **User Management** > **Users**

2. Click on a user

3. Scroll down to **Metadata** section

4. In **app_metadata**, click **Edit**

5. Add JSON:
   ```json
   {
     "aonik_tenant_id": "550e8400-e29b-41d4-a716-446655440000"
   }
   ```

6. Click **Save**

### Option B: Programmatically via Management API (Production)

For production, update user metadata via Auth0 Management API when users sign up or when admins assign them to tenants.

**Example using Auth0 Management API:**

```javascript
const ManagementClient = require('auth0').ManagementClient;

const management = new ManagementClient({
  domain: 'your-domain.auth0.com',
  clientId: 'YOUR_CLIENT_ID',
  clientSecret: 'YOUR_CLIENT_SECRET',
});

// Assign user to AONIK tenant
await management.updateAppMetadata(
  { id: 'auth0|user-id' },
  {
    aonik_tenant_id: '550e8400-e29b-41d4-a716-446655440000'
  }
);
```

**When to Update:**
- After user signs up (via signup hook or post-registration flow)
- When admin assigns user to a tenant in AONIK admin portal
- When user switches tenants (if your app supports this)

### Option C: Use Subdomain Routing (Alternative)

If managing user metadata is complex, use subdomain-based routing instead:

- Users access `https://acme-corp.aonik.io`
- AONIK looks up `acme-corp` subdomain in database
- No custom claims needed!

See [Tenant Routing](../features/authentication-authorization.md#tenant-routing) for details.

---

## Step 4: Create Client Application

Your frontend (web app, mobile app, SPA) needs an Auth0 application.

### 4.1 Create Application

1. Navigate to **Applications** > **Applications**

2. Click **+ Create Application**

3. Fill in details:
   
   **Name:** `AONIK Web App` (or appropriate name)
   
   **Application Type:** Select based on your frontend:
   - **Single Page Web Applications** (React, Angular, Vue)
   - **Regular Web Applications** (ASP.NET, Next.js with server)
   - **Native** (iOS, Android, mobile apps)

4. Click **Create**

### 4.2 Configure Application Settings

1. In your new application, go to **Settings** tab

2. Configure these fields:
   
   **Application URIs:**
   
   - **Allowed Callback URLs:** `https://yourdomain.com/callback, http://localhost:3000/callback`
   - **Allowed Logout URLs:** `https://yourdomain.com/, http://localhost:3000/`
   - **Allowed Web Origins:** `https://yourdomain.com, http://localhost:3000` (CORS)
   
   **Application Properties:**
   
   - **Token Endpoint Authentication Method:** 
     - `None` for SPAs (public client)
     - `Post` for web apps (confidential client)

3. Scroll down and click **Save Changes**

4. **Save these values** - you'll need them for your frontend:
   - **Domain:** `your-domain.auth0.com`
   - **Client ID:** `abc123...`
   - **Client Secret:** (only for server-side web apps, not SPAs)

### 4.3 Enable API Access

1. Still in application settings, scroll to **APIs** section

2. Enable **API Authorization**

3. In **API Settings**, ensure your AONIK API is authorized

Alternatively, in the **APIs** tab:

1. Click **APIs** in top navigation

2. Select your AONIK API

3. Go to **Machine to Machine Applications** tab

4. Toggle **ON** for your web app

5. Select permissions: `read:all` (or all permissions)

6. Click **Update**

---

## Step 5: Configure AONIK API

Now configure AONIK to trust Auth0 tokens.

### 5.1 Update appsettings.json

Edit `src/Aonik.Api/appsettings.json`:

```json
{
  "Auth": {
    "Provider": "Auth0",
    "TenantRoutingMode": "Claim",  // or "Subdomain" if you prefer
    
    "Auth0": {
      "Authority": "https://your-domain.auth0.com/",
      "Audience": "https://aonik-api",  // Must match your API Identifier from Step 1
      "ValidateIssuer": true,
      "ValidateAudience": true,
      "ClockSkew": 300
    }
  },
  
  "PlatformAdmin": {
    "RoleName": "Aonik.PlatformAdmin",
    "ScopeClaimType": "https://aonik.io/platform_admin"
  }
}
```

**Replace:**
- `your-domain.auth0.com`: Your Auth0 domain (from Step 4)
- `https://aonik-api`: Your API Identifier (from Step 1)

**Important:** 
- Authority URL must end with `/`
- Audience must match API Identifier exactly (case-sensitive)

---

## Step 6: Test the Configuration

### 6.1 Get a Test Token

#### Option 1: Using Auth0 Dashboard

1. Navigate to **Applications** > **APIs**

2. Select your AONIK API

3. Click **Test** tab

4. Copy the curl command or use the built-in tester

5. Response includes `access_token` - copy this

#### Option 2: Using Postman

**Request:**
```http
POST https://your-domain.auth0.com/oauth/token
Content-Type: application/json

{
  "grant_type": "password",
  "username": "john@example.com",
  "password": "user-password",
  "audience": "https://aonik-api",
  "client_id": "your-client-id",
  "client_secret": "your-client-secret",
  "scope": "read:all openid profile email"
}
```

**Important:** Password grant is only for testing. Production should use:
- Authorization Code + PKCE (SPAs, mobile)
- Authorization Code (web apps)
- Client Credentials (service-to-service)

### 6.2 Inspect the Token

Copy the access token and paste it into [jwt.ms](https://jwt.ms) to inspect claims:

**Verify:**
- ✅ `aud` matches your configured Audience
- ✅ `iss` matches your Authority (e.g., `https://your-domain.auth0.com/`)
- ✅ `sub` is present (user ID - looks like `auth0|abc123...`)
- ✅ `https://aonik.io/aonik_tenant_id` is present (if you configured it)
- ✅ `email` claim is present (required for AONIK user identification)
- ✅ `scope` or `permissions` contains granted scopes

**Example Token:**
```json
{
  "iss": "https://your-domain.auth0.com/",
  "sub": "auth0|507f1f77bcf86cd799439011",
  "aud": "https://aonik-api",
  "iat": 1704844800,
  "exp": 1704931200,
  "azp": "your-client-id",
  "scope": "read:all",
  "permissions": ["read:all"],
  "https://aonik.io/aonik_tenant_id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john@example.com",
  "email_verified": true
}
```

### 6.3 Call AONIK API

```http
GET https://localhost:5001/billing/invoices
Authorization: Bearer {your-access-token}
```

**Expected Responses:**
- `200 OK` with invoice data = Success! ✅
- `401 Unauthorized` = Token validation failed (check Authority/Audience config)
- `403 Forbidden` = User authenticated but lacks permissions (assign roles in AONIK)
- `404 Not Found` = Tenant not found or user not in tenant

---

## Step 7: Configure Social Connections (Optional)

Auth0 makes it easy to allow users to sign in with Google, GitHub, Microsoft, etc.

### 7.1 Enable Social Connection

1. Navigate to **Authentication** > **Social**

2. Select a provider (e.g., **Google**)

3. Configure with your OAuth credentials or use Auth0's dev keys (for testing only)

4. Click **Save**

### 7.2 Enable for Your Application

1. Go to **Applications** > **Applications**

2. Select your web app

3. Go to **Connections** tab

4. Enable the social connections you want

5. Disable **Username-Password-Authentication** if you only want social logins

---

## Advanced Configuration

### Add Roles to Tokens (Optional)

If you want to use Auth0 roles in addition to AONIK database permissions:

#### 7.3 Create Roles

1. Navigate to **User Management** > **Roles**

2. Click **+ Create Role**

3. Name: `AONIK User` (or `AONIK Admin`, etc.)

4. Assign permissions from your API

5. Click **Create**

#### 7.4 Assign Users to Roles

1. Go to **User Management** > **Users**

2. Select a user

3. Go to **Roles** tab

4. Click **Assign Roles**

5. Select roles

6. Click **Assign**

#### 7.5 Add Roles to Token (Action)

Update your "Add AONIK Claims" action:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://aonik.io';
  
  // Add tenant ID
  const aonikTenantId = event.user.app_metadata?.aonik_tenant_id;
  if (aonikTenantId) {
    api.accessToken.setCustomClaim(`${namespace}/aonik_tenant_id`, aonikTenantId);
  }
  
  // Add Auth0 roles to token
  if (event.authorization && event.authorization.roles) {
    api.accessToken.setCustomClaim(`${namespace}/roles`, event.authorization.roles);
  }
  
  // Add platform admin flag
  const isPlatformAdmin = event.user.app_metadata?.platform_admin === true;
  if (isPlatformAdmin) {
    api.accessToken.setCustomClaim(`${namespace}/platform_admin`, true);
  }
};
```

---

## Common Issues & Solutions

### Issue: "Audience (aud) claim mismatch"

**Cause:** Token audience doesn't match configured Audience

**Solution:**
1. Check token's `aud` claim in jwt.ms
2. Verify Audience in appsettings.json matches API Identifier exactly
3. Ensure client requests token with correct `audience` parameter

### Issue: "Issuer (iss) validation failed"

**Cause:** Token issuer doesn't match configured Authority

**Solution:**
1. Check token's `iss` claim value
2. Verify Authority in appsettings.json matches exactly (including trailing `/`)
3. Auth0 issuer is always `https://{your-domain}.auth0.com/`

### Issue: Custom claim `aonik_tenant_id` not in token

**Cause:** Action not deployed or user metadata not set

**Solution:**
1. Verify Action is deployed: **Actions** > **Library** > "Add AONIK Claims" should show "Deployed"
2. Verify Action is in Login flow: **Actions** > **Flows** > **Login**
3. Check user's `app_metadata` contains `aonik_tenant_id`
4. Request a new token (old tokens won't have new claims)

### Issue: Email claim not in token

**Cause:** Email not included in access token by default

**Symptoms:**
- User authentication succeeds but AONIK can't identify user
- JWT provisioning fails with "missing email" error
- API logs show missing email claim

**Solution:**
1. Ensure your Auth0 Action includes the email claim (see Step 2.2 above)
2. Verify the Action code has: `api.accessToken.setCustomClaim('email', event.user.email);`
3. Redeploy the Action and request a new token
4. Check token at jwt.ms to verify `email` claim is present

### Issue: "User gets 403 after first login"

**Cause:** JIT provisioning creates user with zero permissions

**Expected behavior!**

**Solution:**
1. Admin logs into AONIK
2. Finds new user in tenant
3. Assigns role with appropriate permissions
4. User can now access endpoints

### Issue: "Invalid signature"

**Cause:** Token not signed by Auth0 or AONIK can't reach Auth0

**Solution:**
1. Verify token was issued by Auth0 (check `iss` claim)
2. Restart AONIK API (forces reload of signing keys)
3. Check firewall allows access to `{your-domain}.auth0.com`
4. Check Auth0 status page for outages

---

## Production Checklist

Before going to production:

- [ ] Use **production Auth0 tenant** (not development tenant)
- [ ] Configure **custom domain** for Auth0 (e.g., `auth.yourdomain.com`)
- [ ] Enable **Brute-force Protection** and **Breached Password Detection**
- [ ] Configure **MFA** (Multi-Factor Authentication) for sensitive operations
- [ ] Set up **monitoring and logs** in Auth0 dashboard
- [ ] Use **production social connection credentials** (not dev keys)
- [ ] Configure **email templates** for password resets, welcome emails, etc.
- [ ] Set appropriate **token lifetimes** (shorter for sensitive apps)
- [ ] Enable **Anomaly Detection** for suspicious login attempts
- [ ] Set up **Auth0 Log Streaming** to your monitoring system
- [ ] Test **token refresh** flows in client apps
- [ ] Remove **http://localhost** URLs from callback/origin settings
- [ ] Set `TenantRoutingMode` to `"Claim"` or `"Subdomain"` (NOT `"Header"`)
- [ ] Document **user metadata management** process for tenant assignment

---

## Additional Resources

- [Auth0 Documentation](https://auth0.com/docs)
- [Auth0 Actions](https://auth0.com/docs/customize/actions)
- [Auth0 APIs](https://auth0.com/docs/get-started/apis)
- [Auth0 Management API](https://auth0.com/docs/api/management/v2)
- [AONIK Authentication Overview](../features/authentication-authorization.md)
- [AONIK Permissions Reference](../reference/permissions.md)
- [Troubleshooting Authentication](authentication-troubleshooting.md)

---

**Last Updated:** January 9, 2025  
**Tested With:** Auth0 Latest (January 2025)
