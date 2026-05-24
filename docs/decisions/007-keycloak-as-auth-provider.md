# ADR-007: Keycloak as a First-Class Operator-Choice Auth Provider

**Status**: Accepted (Phase 1-4 landed; admin-UI OIDC client landed 2026-05-24)
**Date**: 2026-05-21
**Decision Makers**: Development Team
**Related**: [ADR-005](005-adopt-module-first-modular-monolith.md), [Spec 029](../specifications/029.keycloak-auth-provider.html), Spec 026 (introduced the IdP factory pattern this ADR extends)

## Context

Aonik already supported two auth providers (Auth0, Azure AD) behind a thoughtful factory-per-capability abstraction in `src/Aonik.Infrastructure/Authentication/`. Six capability surfaces — JWT validation, IdP management client, user provisioning, password reset, account service, token exchange — each have:

- a single interface in `Aonik.Platform.Contracts.Services.Authentication`,
- two parallel implementations (`Auth0*` and `AzureAd*`),
- a factory keyed on the `Auth.Provider` setting that dispatches to the active provider.

The third operator-choice option — **Keycloak** — is high-payoff for three reasons:

1. **Self-hostable IdP without paid Auth0**: products targeting cost-sensitive or data-sovereignty-regulated markets (MyBillAfrica, RemitExchange) benefit from deploying without a paid IdP.
2. **Federation lives in Keycloak, not Aonik**: Keycloak natively federates upstream to Okta / AD FS / SAML / social IdPs, so Aonik gains "BYO upstream IdP" without growing its own multi-IdP surface.
3. **Local-dev ergonomics**: `docker compose -f infra/keycloak/compose.keycloak.yml up` replaces the Auth0 tenant configuration step that currently gates contributor onboarding.

## Decision

Add Keycloak as a third occupant of the existing IdP factory pattern. Operator selects between Auth0 / Azure AD / Keycloak per deployment via the `Auth.Provider` setting. Federation to upstream IdPs (Okta, AD FS, SAML, social) is configured **inside Keycloak**, not in Aonik.

### Architectural Guarantees

1. **Operator-choice, not tenant-choice.** `Auth.Provider` stays platform-level. A tenant inside a deployment cannot pick a different IdP than its host's. Per-tenant federated IdP is intentionally deferred.
2. **No new abstraction layer.** Six existing interfaces and five existing factories each gain one new implementation / switch arm. The pattern is right-sized; resisting a registry / strategy refactor.
3. **OIDC discovery, not hard-coded paths.** Aonik never assembles `/realms/{realm}/protocol/openid-connect/token` by hand at the JWT validation layer. The JwtBearer middleware fetches `/.well-known/openid-configuration` and resolves the JWKS / token endpoints from it.
4. **Roles via Keycloak protocol mapper, not custom Aonik-side claim path.** `ClaimsRoleMapper.ExtractRoles` stays provider-agnostic; Keycloak emits a flat `roles` claim via a configured protocol mapper.
5. **Admin client uses least-privilege role grants.** The `aonik-admin` service-account client requires only `manage-users`, `view-users`, `view-realm` from the built-in `realm-management` client. `realm-admin` is **not** granted.
6. **Federation is Keycloak's job.** Aonik talks one OIDC dialect; the upstream zoo (Okta, AD FS, SAML) is Keycloak's problem to translate.

### What Lands in This ADR

**Phase 1 — Authentication only** ✅ Landed.
- `KeycloakOptions` POCO + `Auth.Keycloak.*` settings (7 keys).
- `AonikAuthenticationSetup` third `JwtBearer` scheme + issuer routing.
- `appsettings.json` + `appsettings.Development.json` defaults.

**Phase 2 — Management plane** ✅ Landed.
- `KeycloakManagementClient`, `KeycloakUserProvisioner`, `KeycloakPasswordResetService`, `KeycloakAccountService`, `KeycloakAuthTokenService`.
- Shared `KeycloakTokenHelper` + `KeycloakUrls` helpers.
- Five factory switch-arm edits (`IdentityProviderManagementClientFactory`, `IdpUserProvisionerFactory`, `IdpPasswordResetServiceFactory`, `IdpAccountServiceFactory`, `AuthTokenServiceFactory`).
- DI registration in `Aonik.Infrastructure.DependencyInjection`.

**Phase 3 — Settings + Admin UI form** ✅ Landed.
- `KeycloakSettingsSnapshot` / `KeycloakSettingsUpdate` records + matching API contracts (`KeycloakSettingsResponse`, `KeycloakSettingsUpdateRequest`, `PublicKeycloakSettingsResponse`).
- `AuthProviderSettingsService.GetAsync` returns the Keycloak section.
- Three settings endpoints (Public / Get admin / Update admin) surface and round-trip the Keycloak section.
- `AuthProviderSettingsUpdateRequestValidator` accepts `"Keycloak"` as a valid `ActiveProvider`.
- Admin UI `SettingsAuthenticationPage.tsx` gains a Keycloak form section mirroring Auth0 / Azure AD; `types/index.ts` extends `AuthProviderType` to `'AzureAd' | 'Auth0' | 'Keycloak'`.

**Phase 4 — Local-dev + docs** ✅ Landed.
- `infra/keycloak/compose.keycloak.yml` — single-service docker-compose with Keycloak 26 in `start-dev` mode.
- `infra/keycloak/realm-export.json` — pre-seeded realm (`aonik`) with `aonik-spa` + `aonik-admin` clients, three users (admin / regular / service-account), audience + roles flattening mappers.
- This ADR + the operator runbook at `docs/operations/keycloak-setup.md`.

### Admin-UI OIDC client ✅ Landed (2026-05-24)

The admin UI now logs in against Keycloak when `VITE_AUTH_PROVIDER=keycloak`. Implementation:

- `oidc-client-ts` + `react-oidc-context` added as runtime dependencies (the React wrapper mirrors `@azure/msal-react` / `@auth0/auth0-react` patterns the codebase already uses).
- `authConfig.ts` extends the `AuthProvider` union to `'azure-ad' | 'auth0' | 'keycloak' | 'mock'`; `validateAuthConfig` checks `VITE_KEYCLOAK_AUTHORITY` + `VITE_KEYCLOAK_CLIENT_ID`; `keycloakConfig` exposes the OIDC client config.
- `useAuth.tsx` gains `useKeycloakAuth` + `KeycloakAuthContextProvider`. Roles extraction looks at `profile.roles` first (flattened by the realm protocol mapper, the spec default) then falls back to `profile.realm_access.roles`. Silent renew runs through `signinSilent`; `getAccessToken` checks freshness against `expires_at` with a 60-second skew margin.
- `AuthProvider.tsx` wraps the tree in `<AuthProvider>` from `react-oidc-context`, configured with PKCE, `automaticSilentRenew`, `localStorage`-backed `WebStorageStateStore`, and an `onSigninCallback` that strips OIDC response params from the URL after the redirect.

Required env vars (set in `.env.local` or the deployment environment):
- `VITE_AUTH_PROVIDER=keycloak`
- `VITE_KEYCLOAK_AUTHORITY=https://keycloak.example.com/realms/aonik`
- `VITE_KEYCLOAK_CLIENT_ID=aonik-spa`
- Optional: `VITE_KEYCLOAK_REDIRECT_URI`, `VITE_KEYCLOAK_POST_LOGOUT_REDIRECT_URI`, `VITE_KEYCLOAK_CLIENT_SECRET` (not recommended for browser clients).

### Configuration shape

```jsonc
{
  "Auth": {
    "Provider": "Keycloak",          // or "Auth0" or "AzureAd"
    "Keycloak": {
      "Authority": "https://keycloak.example.com/realms/aonik",
      "Audience": "aonik-api",
      "ValidateIssuer": true,
      "ClockSkewSeconds": 300
    }
  },
  "Settings": {
    "Auth.Provider": "Keycloak",
    "Auth.Keycloak.Authority": "https://keycloak.example.com/realms/aonik",
    "Auth.Keycloak.Audience": "aonik-api",
    "Auth.Keycloak.ClientId": "aonik-spa",
    "Auth.Keycloak.ClientSecret": "<from-secret-store>",
    "Auth.Keycloak.Realm": "aonik",
    "Auth.Keycloak.AdminClientId": "aonik-admin",
    "Auth.Keycloak.AdminClientSecret": "<from-secret-store>"
  }
}
```

Both `ClientSecret` and `AdminClientSecret` are flagged `IsEncrypted` in `SettingDefinitions`, so they're persisted through `ISettingValueProtector` and never round-trip through the public admin-UI snapshot (the response carries `hasClientSecret` / `hasAdminClientSecret` booleans only).

### Realm setup

Operators provision a single realm with two clients:

| Client | Access type | Purpose |
|---|---|---|
| `aonik-spa` | Public (PKCE) | Admin UI / Payabo SPA login. Audience mapper emits `aud: ["aonik-api"]`. Roles mapper flattens `realm_access.roles` → top-level `roles` claim. |
| `aonik-admin` | Confidential, service-accounts enabled | Aonik's Admin API client. Required `realm-management` roles: `manage-users`, `view-users`, `view-realm`. |

See [docs/operations/keycloak-setup.md](../operations/keycloak-setup.md) for the step-by-step runbook.

## Consequences

### Positive

- Operators can deploy Aonik with a self-hosted IdP at no additional licensing cost.
- Federation playbook ("configure inside Keycloak") gives operators a clean BYO upstream IdP story without growing Aonik's surface area.
- Adding a third occupant to the IdP factory pattern proves the abstraction is real, not aspirational. Any residual Auth0-shaped assumptions in `ClaimsRoleMapper` / `ClaimsEmailResolver` / `HandleTokenValidatedAsync` would have surfaced here — they didn't, which is itself evidence the abstraction is solid.
- Local-dev onboarding: `docker compose -f infra/keycloak/compose.keycloak.yml up` replaces the Auth0 tenant configuration step.

### Trade-offs

- **Three providers is one more than two.** Each upgrade-compatible IdP release, each claim-name surprise, each refresh-token-rotation quirk multiplies maintenance work.
- **Keycloak Admin API ≠ Auth0 Management API ≠ Microsoft Graph.** The interface shape held; the implementations are real engineering with subtle semantics (e.g. Keycloak's `username` vs Auth0's email-as-username, Keycloak's `reset-password` endpoint vs Auth0's PATCH).
- **Direct-grant is off by default in Keycloak.** Operators using `KeycloakAuthTokenService.ExchangeAsync` must enable "Direct Access Grants Enabled" on the `aonik-spa` client. Documented prominently in the runbook.
- **Three SPA login codepaths to maintain.** `@azure/msal-react`, `@auth0/auth0-react`, and `react-oidc-context` each have their own session-restore semantics, silent-renew quirks, and error shapes. The thin `AuthContextType` interface that `useAuth` exposes papers over the differences for the rest of the SPA, but the three hook implementations diverge in detail.

### Risks

| Risk | Mitigation |
|---|---|
| Keycloak Admin API contract drift between versions (26 → 27 etc.). | Pin runbook target version; treat the Admin API as a versioned external dependency. |
| Roles claim path differs from Auth0 / Azure AD → users authenticate but have no roles. | Realm export includes a `roles-flatten` protocol mapper; runbook documents the requirement explicitly. |
| Admin-client credentials over-scoped to `realm-admin`. | Runbook lists exactly three required roles and rejects `realm-admin`. |
| Local-dev `realm-export.json` drifts from production reality. | The export is checked in; reviewers compare against the production realm during operator-runbook updates. |

## See Also

- [Spec 029](../specifications/029.keycloak-auth-provider.html) — full specification.
- [docs/operations/keycloak-setup.md](../operations/keycloak-setup.md) — production realm setup runbook.
- [`src/Aonik.Infrastructure/Authentication/AonikAuthenticationSetup.cs`](../../src/Aonik.Infrastructure/Authentication/AonikAuthenticationSetup.cs) — policy-scheme dispatcher.
- [`src/Aonik.Platform/Services/Settings/AuthSettingNames.cs`](../../src/Aonik.Platform/Services/Settings/AuthSettingNames.cs) — setting-key registry.
