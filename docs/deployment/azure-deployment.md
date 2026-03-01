# Azure Deployment

This guide defines AONIK's Azure container delivery model with strict separation of concerns:

1. **Platform Bootstrap** (`cd-infra.yml`) provisions/updates shared Azure infrastructure only.
2. **Image Release** (`cd-images.yml`) builds and pushes a cohesive runtime image set.
3. **Runtime Deploy** (`cd-deploy.yml`) deploys app runtimes using those exact image references.



## Deployment Architecture

```text
┌──────────────────────────┐
│ 1) Azure Platform        │
│    Bootstrap             │
│    (infra only)          │
└────────────┬─────────────┘
             │ outputs infra foundations (ACR, SQL, KV, ACA)
             ▼
┌──────────────────────────┐
│ 2) CD: Container Images  │
│    (build/tag/push)      │
│    aonik-api             │
│    aonik-worker          │
│    aonik-adminui         │
└────────────┬─────────────┘
             │ emits release manifest + immutable refs
             ▼
┌──────────────────────────┐
│ 3) CD: Deploy            │
│    (rollout)             │
│    fail-fast preflight   │
│    no mixed versions     │
└──────────────────────────┘
```

## First-Run Playbook (bootstrap-first)

Run order for a fresh environment:

1. **Platform bootstrap** (`mode=deploy`) for target `environment`.
2. **Image release** with default tag (`git SHA`) or explicit immutable tag.
3. **Runtime deploy** using the same image version.

This removes first-run ambiguity; no skip flags are required for normal bootstrap. Bootstrap uses per-service image defaults so API/Admin UI runtime port assumptions remain valid in first-run ACA deployments.

## Normal Release Playbook

1. Run **CD: Container Images** with:
   - `environment`: target env credential scope
   - `image_tag`: optional override (default `github.sha`)
   - `semver_alias`: optional mutable alias (e.g. `v1.5.0`)
2. Capture release artifact `image-release-<version>/image-release-manifest.json`.
3. Run **CD: Deploy** with:
   - same `environment`
   - `image_version=<version from image release>`
   - `use_digest_references=true` (recommended)


### Optional Operator Orchestrator

For teams that want a single entry point, use `cd-pipeline.yml`:

- Set `build_images=true` to run image release first and automatically pass the resolved immutable `release_version` into runtime deployment.
- Set `build_images=false` to skip image build/push and provide `image_version` explicitly (required).

This preserves the standard split between build/release and deployment while reducing operator handoff errors.

This orchestrator intentionally exposes a compact input set to satisfy GitHub's `workflow_dispatch` 10-input limit; use `cd-images.yml` or `cd-deploy.yml` directly for advanced overrides such as `semver_alias`, explicit `acr_name` / `acr_login_server`, or `location`.

## Rollback Playbook

1. Identify a prior successful image release version.
2. Re-run **CD: Deploy** with:
   - `image_version=<previous immutable version>`
   - unchanged environment
3. Runtime deploy preflight validates that all required service images exist for that version before rollout.

## Runtime Consistency Rules

- Default release tag is **git SHA**.
- Optional semver alias is supported for operator convenience.
- Deployment always resolves one cohesive version set across `aonik-api`, `aonik-worker`, and `aonik-adminui`.
- If any required image is missing, runtime deploy fails with actionable guidance.
- `skip_image_validation` exists only as an advanced/emergency bypass.

## Security and Azure Authentication

All three workflows use:

- **OIDC (`azure/login`) by default**.
- **Service principal secret fallback** only when `AZURE_CLIENT_SECRET` is configured.

Required environment secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `SQL_ADMIN_PASSWORD` (bootstrap/runtime deployment)
- `ACS_CONNECTION_STRING` (optional; required for ACS email/SMS dispatch)
- `VERIFICATION_HASH_KEY` (optional; required for verification hash protection)
- `AZURE_CLIENT_SECRET` (optional fallback)

Required environment variables for Admin UI image build:

- `VITE_AUTH_PROVIDER` (`azure-ad`, `auth0`, or `mock`)
- `VITE_API_BASE_URL`

If `VITE_AUTH_PROVIDER` is omitted, image release defaults it to `azure-ad` during Admin UI build.
- If provider is `azure-ad`: `VITE_AZURE_AD_CLIENT_ID`, `VITE_AZURE_AD_TENANT_ID`
- If provider is `auth0`: `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`
- Optional overrides: `VITE_AZURE_AD_REDIRECT_URI`, `VITE_AZURE_AD_API_SCOPE`, `VITE_AUTH0_REDIRECT_URI`, `VITE_AUTH0_AUDIENCE`

Runtime app settings are injected through JSON payloads:

- `API_APP_SETTINGS_JSON`
- `WORKER_APP_SETTINGS_JSON`

You can define these as GitHub Environment **Secrets** (recommended when they include credentials) or **Variables**. The deploy workflow prefers secrets when both are set.

Each payload must be a JSON object where keys are final .NET configuration environment variable names.

Auth0 example (`API_APP_SETTINGS_JSON`):

```json
{
  "Settings__Auth.Provider": "Auth0",
  "Settings__Auth.Auth0.Domain": "aonik.uk.auth0.com",
  "Settings__Auth.Auth0.Audience": "https://api.aonik.com",
  "Settings__Auth.Auth0.ClientId": "<spa-client-id>",
  "Settings__Auth.Auth0.ManagementClientId": "<m2m-client-id>",
  "Settings__Auth.Auth0.ManagementClientSecret": "<m2m-client-secret>",
  "Settings__Auth.Auth0.Connection": "Username-Password-Authentication",
  "Settings__Auth.Auth0.ManagementAudience": "https://aonik.uk.auth0.com/api/v2/"
}
```

You can include any other runtime settings in the same payload (for example `PlatformAdmin__*`, `BlobStorage__*`, `Communication__Azure__Email__FromAddress`, `FeatureManagement__*`).

Recommended least-privilege role scoping:

- **Platform Bootstrap**: Contributor on target resource group/subscription scope used for infra.
- **Image Release**: `AcrPush` on target ACR + read access to subscription metadata.
- **Runtime Deploy**: Contributor on target resource group + pull access via managed identity/runtime config.

## Troubleshooting Missing Tags

If runtime deploy fails with missing tags:

1. Confirm `image_version` matches image release output.
2. Re-run **CD: Container Images** for that version.
3. Verify all repositories exist in ACR:
   - `aonik-api`
   - `aonik-worker`
   - `aonik-adminui`
4. Re-run **CD: Deploy**.

Avoid mixing ad-hoc service tags. Runtime deploy intentionally blocks partial version sets.

## Operations Runbooks

- `docs/runbooks/bootstrap.md`
- `docs/runbooks/build-and-push.md`
- `docs/runbooks/deploy-runtime.md`
- `docs/runbooks/release-and-deploy.md`
