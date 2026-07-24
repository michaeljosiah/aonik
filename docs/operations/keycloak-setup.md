# Keycloak setup for Aonik (operator runbook)

This runbook walks an operator through configuring a Keycloak realm so an Aonik
deployment can use it as its identity provider. The architectural decisions
behind these instructions are captured in
[ADR-007](../decisions/007-keycloak-as-auth-provider.md) and
[Spec 029](../specifications/029.keycloak-auth-provider.html).

## What you'll set up

A single Keycloak realm (`aonik` is the conventional name) containing two clients:

| Client | Type | Purpose | Aonik setting |
|---|---|---|---|
| `aonik-spa` | Public (PKCE) | End-user login for the admin UI, Payabo mobile, CLI | `Auth.Keycloak.ClientId` |
| `aonik-admin` | Confidential, service-accounts on | Aonik's Admin API client (provisioning, deletes, password resets) | `Auth.Keycloak.AdminClientId` + `Auth.Keycloak.AdminClientSecret` |

## Prerequisites

- A reachable Keycloak instance (version 26 or newer). Self-hosted or managed;
  Aonik does not ship Keycloak itself.
- Realm SMTP configured if you want Keycloak to send verify-email and
  password-reset mails. (Aonik never sees those mails — they go straight from
  Keycloak's configured SMTP to the user.)
- Aonik deployed with `Auth.Provider=Keycloak`.

## Step 1 — Create the realm

```text
Master realm UI → Add realm → Name: aonik
```

Set:

- **Display name**: `Aonik` (whatever you want to surface on the login page)
- **User registration**: off (Aonik provisions users via the Admin API)
- **Forgot password**: on
- **Remember me**: optional
- **Verify email**: optional (recommended on for production)
- **Login with email**: on
- **Duplicate emails**: off

In **Realm settings → Tokens**:

- **Access Token Lifespan**: 30 min (Aonik defaults assume 30 min; longer is
  fine but adjust `ClockSkewSeconds` if you change it dramatically)
- **SSO Session Idle**: 30 min
- **SSO Session Max**: 10 hours

## Step 2 — Configure SMTP (recommended)

`Realm settings → Email` — point at your transactional mail provider. Without
this, password-reset and verify-email actions silently no-op from the
end-user's perspective.

## Step 3 — Create the `aonik-spa` client

```text
Clients → Create client
  Client type:     OpenID Connect
  Client ID:       aonik-spa
  Name:            Aonik SPA (Admin UI / Payabo)
  Description:     End-user login client for the Aonik platform
```

Next page:

- **Client authentication**: OFF (public client, uses PKCE)
- **Authorization**: OFF
- **Standard flow**: ON
- **Direct access grants**: ON (only if you want `KeycloakAuthTokenService.ExchangeAsync` to work — see below)
- **Implicit flow**: OFF
- **Service accounts roles**: OFF

Final page (Login settings):

- **Root URL**: your admin UI / Payabo origin (e.g. `https://admin.aonik.com`)
- **Valid redirect URIs**: `https://admin.aonik.com/*`, `https://payabo.aonik.com/*` (add all client origins)
- **Web origins**: `+` (mirror the redirect URIs)
- **Post logout redirect URIs**: `+`

### Direct-grant warning

Direct-grant (Resource Owner Password Credentials) bypasses Keycloak's hosted
login flows — MFA, terms acceptance, identity-broker challenges. **Only enable
it if Aonik's `IAuthTokenService.ExchangeAsync` callers genuinely need
password-grant token exchange.** For most deployments that authenticate via
the hosted login page, leave it off.

### Audience mapper (REQUIRED)

Aonik's API validates `aud` claims with `ValidateAudience=true`. Without this
mapper, every Keycloak-issued token will be rejected with `audience invalid`.

```text
Clients → aonik-spa → Client scopes → aonik-spa-dedicated → Add mapper → By configuration
  Mapper type:                    Audience
  Name:                           audience-aonik-api
  Included Client Audience:       aonik-api
  Add to ID token:                OFF
  Add to access token:            ON
```

The value of "Included Client Audience" must match `Auth.Keycloak.Audience` in
your Aonik settings.

### Roles mapper (REQUIRED)

Aonik's `ClaimsRoleMapper.ExtractRoles` reads a top-level `roles` claim.
Keycloak emits roles under `realm_access.roles` by default; add this mapper
to flatten them into a top-level claim:

```text
Clients → aonik-spa → Client scopes → aonik-spa-dedicated → Add mapper → By configuration
  Mapper type:                       User Realm Role
  Name:                              roles-flatten
  Token Claim Name:                  roles
  Claim JSON Type:                   String
  Multivalued:                       ON
  Add to ID token:                   ON
  Add to access token:               ON
  Add to userinfo:                   ON
```

## Step 4 — Create realm roles

Create one realm role per Aonik role that your tenant uses. The minimum set:

```text
Realm roles → Create role
  - Aonik.PlatformAdmin
  - TenantAdmin
  - PersonalUser
  - Operations
  - ReadOnly
  - Compliance
```

Assign roles to users via **Users → {user} → Role mappings → Assign role**.

## Step 5 — Create the `aonik-admin` service-account client

```text
Clients → Create client
  Client type:     OpenID Connect
  Client ID:       aonik-admin
  Name:            Aonik Admin API client
```

Next page:

- **Client authentication**: ON (confidential client, requires secret)
- **Authorization**: OFF
- **Standard flow**: OFF
- **Direct access grants**: OFF
- **Implicit flow**: OFF
- **Service accounts roles**: ON

Final page:

- Leave URL fields blank (this client never logs a user in).

### Grant the three required realm-management roles

This is the only step where it's easy to get the security posture wrong. Do
NOT grant `realm-admin` — that gives Aonik permission to modify the realm
configuration itself, federation, other clients, etc.

```text
Clients → aonik-admin → Service accounts roles → Assign role
  Filter by client:    realm-management
  Select:
    - manage-users
    - view-users
    - view-realm
```

Apply.

### Retrieve the client secret

```text
Clients → aonik-admin → Credentials → Client Secret
```

Copy the value; this goes into Aonik's `Auth.Keycloak.AdminClientSecret`
setting. Treat it like any other production secret — Aonik flags it
`IsEncrypted` in `SettingDefinitions` so it's persisted through
`ISettingValueProtector`, but the operator still needs to inject it through a
secure path (Key Vault, environment variable from a secret store, etc.).

## Step 6 — Wire Aonik

Set the following Aonik settings. `Auth.*` is a configuration-managed prefix —
`SettingService` refuses writes to it through the settings APIs and reads it
from configuration only — so these go in `appsettings.json`, environment
variables, or your operator's secret store, not the admin UI:

| Setting | Value | Source |
|---|---|---|
| `Auth.Provider` | `Keycloak` | — |
| `Auth.Keycloak.Authority` | `https://<keycloak-host>/realms/aonik` | — |
| `Auth.Keycloak.Audience` | `aonik-api` | Must match the audience mapper above |
| `Auth.Keycloak.ClientId` | `aonik-spa` | Step 3 |
| `Auth.Keycloak.ClientSecret` | empty (public client) | Leave blank unless you flipped `aonik-spa` to confidential |
| `Auth.Keycloak.Realm` | `aonik` | Step 1 |
| `Auth.Keycloak.AdminClientId` | `aonik-admin` | Step 5 |
| `Auth.Keycloak.AdminClientSecret` | the client secret from Step 5 | Secret store |

Restart Aonik (settings are read at startup for the JwtBearer middleware).
Subsequent settings changes (e.g. flipping `Auth.Provider`) are picked up
on the next token validation without a restart, but the JwtBearer schemes
themselves are registered once at boot.

## Step 7 — Smoke test

1. Log in to the admin UI. You should be redirected to Keycloak's hosted
   login page, then back to the admin UI with an Aonik session.
2. Hit a tenant-scoped endpoint (e.g. `GET /v1/me`). Should return 200 with
   your identity.
3. From the admin UI, **Settings → Users → Invite user**. Confirm the new
   user appears in Keycloak's **Users** list with `emailVerified=false`.
4. Click "Forgot password" on the login page (or trigger from admin UI).
   Confirm the user receives the Keycloak reset email at the configured
   SMTP.

## Federation playbook

Aonik talks one OIDC dialect. Upstream IdPs (Okta, AD FS, SAML, social) are
configured **inside Keycloak**; Aonik gains zero code for any of them.

### Okta

```text
Realm settings → Identity providers → Add provider → OpenID Connect v1.0
  Alias:                        okta
  Display name:                 Okta
  Authorization URL:            https://<okta-domain>/oauth2/default/v1/authorize
  Token URL:                    https://<okta-domain>/oauth2/default/v1/token
  Userinfo URL:                 https://<okta-domain>/oauth2/default/v1/userinfo
  JWKS URL:                     https://<okta-domain>/oauth2/default/v1/keys
  Client ID:                    <Okta application's client id>
  Client secret:                <Okta application's client secret>
  Default scopes:               openid profile email
```

End-user sees a "Sign in with Okta" button on the Keycloak login page,
clicks through, gets redirected back. Aonik sees a Keycloak-issued JWT.

### Azure AD / AD FS

Same shape as Okta — Identity providers → Add provider → OpenID Connect v1.0,
pointing at Microsoft's well-known endpoint:
`https://login.microsoftonline.com/<tenant-id>/v2.0/.well-known/openid-configuration`.

### Google / GitHub (social)

Built-in social providers under Identity providers → Add provider → Google / GitHub.
Provide OAuth client id + secret from the respective developer console.

### Enterprise SAML 2.0

Identity providers → Add provider → SAML v2.0. Upload the IdP metadata XML.
Keycloak handles the SAML round-trip and issues Aonik a standard OIDC JWT.

## Troubleshooting

### `401 audience invalid`

The audience mapper isn't configured on `aonik-spa`, or the audience value
doesn't match `Auth.Keycloak.Audience`. See Step 3.

### `403 forbidden` on every authenticated request

Roles aren't flowing through. Either the roles-flatten mapper isn't
configured, or the user has no realm roles assigned. See Step 3 + Step 4.

### `Token issuer not allowed for active provider`

Two distinct causes.

**The authority doesn't match the token's `iss`.** Most common reason:
`KC_HOSTNAME` not set on the Keycloak container, so the issuer comes back as
the container's internal hostname instead of the external authority. Set
`KC_HOSTNAME` to the externally-reachable host.

**The provider is only half-switched.** Two configuration keys resolve the
active provider and both have to say `Keycloak`: `Auth:Provider` binds
`AuthOptions` (JWT scheme selection), while `Settings:Auth.Provider` is what
`GetActiveProviderAsync` reads when it compares the token's issuer against the
active provider. Set only the first and a valid Keycloak token is rejected with
this message.

Note the dot in `Settings:Auth.Provider`: the environment-variable spelling
would be `Settings__Auth.Provider`, which a POSIX shell refuses as an invalid
identifier (`export Settings__Auth.Provider=Keycloak` is a syntax error, and
`Settings__Auth__Provider` maps to a different key the code never reads). Pass
both on the command line instead — the colon form needs no escaping anywhere:

```bash
dotnet run --project src/Aonik.Api -- --Auth:Provider=Keycloak --Settings:Auth.Provider=Keycloak
```

Or persist them for local work:

```bash
dotnet user-secrets set "Settings:Auth.Provider" Keycloak --project src/Aonik.Api
```

If the deployment target only accepts environment variables, quote the whole
assignment so the shell never parses the name — `env 'Settings__Auth.Provider=Keycloak'
'Auth__Provider=Keycloak' dotnet …`, or in PowerShell `${env:Settings__Auth.Provider} =
'Keycloak'`. The AppHost's `AONIK_AUTH_PROVIDER=Keycloak` path needs none of
this: it sets both through the process API, where the dot is unremarkable.

### `Direct-grant disabled` on `KeycloakAuthTokenService.ExchangeAsync`

Flip **Direct access grants** to ON on `aonik-spa` (Step 3). Note the
security trade-off — direct grant skips the hosted login flow.

### Admin-API operations return `403`

The `aonik-admin` service-account client is missing one of the three required
realm-management roles. Re-check Step 5 — needs `manage-users`, `view-users`,
and `view-realm`. Do **not** grant `realm-admin`.

## See also

- [ADR-007 — Keycloak as auth provider](../decisions/007-keycloak-as-auth-provider.md)
- [Spec 029 — full specification](../specifications/029.keycloak-auth-provider.html)
- [Keycloak Admin REST API reference (v26)](https://www.keycloak.org/docs-api/latest/rest-api/index.html)
- [`infra/keycloak/compose.keycloak.yml`](../../infra/keycloak/compose.keycloak.yml) — local-dev profile (DO NOT use in production)
- [`infra/keycloak/realm-export.json`](../../infra/keycloak/realm-export.json) — pre-seeded local-dev realm export
