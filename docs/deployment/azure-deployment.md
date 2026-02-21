# Azure Deployment

This guide defines AONIK's Azure container delivery model with strict separation of concerns:

1. **Platform Bootstrap** (`azure-platform-bootstrap.yml`) provisions/updates shared Azure infrastructure only.
2. **Image Release** (`azure-image-release.yml`) builds and pushes a cohesive runtime image set.
3. **Runtime Deploy** (`azure-runtime-deploy.yml`) deploys app runtimes using those exact image references.

> Legacy path: `.github/workflows/azure-iac-cd.yml` remains available during migration, but is now considered deprecated for day-to-day operations.

## Deployment Architecture

```text
┌──────────────────────────┐
│ 1) Azure Platform        │
│    Bootstrap             │
│    (infra only)          │
└────────────┬─────────────┘
             │ outputs infra foundations (ACR, SQL, KV, ACA/AppService)
             ▼
┌──────────────────────────┐
│ 2) Azure Image Release   │
│    (build/tag/push)      │
│    aonik-api             │
│    aonik-worker          │
│    aonik-adminui         │
└────────────┬─────────────┘
             │ emits release manifest + immutable refs
             ▼
┌──────────────────────────┐
│ 3) Azure Runtime Deploy  │
│    (rollout)             │
│    fail-fast preflight   │
│    no mixed versions     │
└──────────────────────────┘
```

## First-Run Playbook (bootstrap-first)

Run order for a fresh environment:

1. **Platform bootstrap** (`mode=deploy`) for `profile=aca|appservice` and target `environment`.
2. **Image release** with default tag (`git SHA`) or explicit immutable tag.
3. **Runtime deploy** using the same image version.

This removes first-run ambiguity; no skip flags are required for normal bootstrap. Bootstrap uses per-service image defaults so API/Admin UI runtime port assumptions remain valid in first-run ACA deployments.

## Normal Release Playbook

1. Run **Azure Image Release** with:
   - `environment`: target env credential scope
   - `image_tag`: optional override (default `github.sha`)
   - `semver_alias`: optional mutable alias (e.g. `v1.5.0`)
2. Capture release artifact `image-release-<version>/image-release-manifest.json`.
3. Run **Azure Runtime Deploy** with:
   - same `environment`
   - same `profile`
   - `image_version=<version from image release>`
   - `use_digest_references=true` (recommended)


### Optional Operator Orchestrator

For teams that want a single entry point, use `azure-release-and-deploy.yml`:

- Set `build_images=true` to run image release first and automatically pass the resolved immutable `release_version` into runtime deployment.
- Set `build_images=false` to skip image build/push and provide `image_version` explicitly (required).

This preserves the standard split between build/release and deployment while reducing operator handoff errors.

This orchestrator intentionally exposes a compact input set to satisfy GitHub's `workflow_dispatch` 10-input limit; use `azure-image-release.yml` or `azure-runtime-deploy.yml` directly for advanced overrides such as `semver_alias`, explicit `acr_name` / `acr_login_server`, or `location`.

## Rollback Playbook

1. Identify a prior successful image release version.
2. Re-run **Azure Runtime Deploy** with:
   - `image_version=<previous immutable version>`
   - unchanged profile/environment
3. Runtime deploy preflight validates that all required service images exist for that version before rollout.

## Runtime Consistency Rules

- Default release tag is **git SHA**.
- Optional semver alias is supported for operator convenience.
- Deployment always resolves one cohesive version set across `aonik-api`, `aonik-worker`, and `aonik-adminui` (or API/AdminUI for `appservice`).
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
- `AZURE_CLIENT_SECRET` (optional fallback)

Required environment variables for Admin UI image build:

- `VITE_AUTH_PROVIDER` (`azure-ad`, `auth0`, or `mock`)
- `VITE_API_BASE_URL`
- If provider is `azure-ad`: `VITE_AZURE_AD_CLIENT_ID`, `VITE_AZURE_AD_TENANT_ID`
- If provider is `auth0`: `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`
- Optional overrides: `VITE_AZURE_AD_REDIRECT_URI`, `VITE_AZURE_AD_API_SCOPE`, `VITE_AUTH0_REDIRECT_URI`, `VITE_AUTH0_AUDIENCE`

Optional environment variables for API/Worker runtime settings overrides:

- `API_AUTH_PROVIDER` → `Auth__Provider`
- `API_AUTH_TENANT_ROUTING` → `Auth__TenantRouting`
- `API_AUTH_AUTH0_AUTHORITY` → `Auth__Auth0__Authority`
- `API_AUTH_AUTH0_AUDIENCE` → `Auth__Auth0__Audience`
- `API_AUTH_AZUREAD_AUTHORITY` → `Auth__AzureAd__Authority`
- `API_AUTH_AZUREAD_AUDIENCE` → `Auth__AzureAd__Audience`
- `API_PLATFORM_ADMIN_ROLE_CLAIM_TYPE` → `PlatformAdmin__RoleClaimType`
- `API_PLATFORM_ADMIN_ROLE_VALUE` → `PlatformAdmin__RoleValue`
- `API_PLATFORM_ADMIN_SCOPE_CLAIM_TYPE` → `PlatformAdmin__ScopeClaimType`
- `API_PLATFORM_ADMIN_ADMIN_EMAIL_0` → `PlatformAdmin__AdminEmails__0`
- `API_BLOB_STORAGE_PROVIDER` → `BlobStorage__Provider`
- `API_BLOB_STORAGE_AZURE_ACCOUNT_NAME` → `BlobStorage__Azure__AccountName`
- `API_BLOB_STORAGE_PROFILE_PHOTOS_PUBLIC_BASE_URL` → `BlobStorage__ProfilePhotos__PublicBaseUrl`
- `API_BLOB_STORAGE_PRODUCT_IMAGES_PUBLIC_BASE_URL` → `BlobStorage__ProductImages__PublicBaseUrl`
- `API_BLOB_STORAGE_DOCUMENTS_PUBLIC_BASE_URL` → `BlobStorage__Documents__PublicBaseUrl`
- `WORKER_BLOB_STORAGE_PROVIDER` → `BlobStorage__Provider`
- `WORKER_BLOB_STORAGE_AZURE_ACCOUNT_NAME` → `BlobStorage__Azure__AccountName`

Backward-compatible JSON bundle variables are still supported:

- `API_APP_SETTINGS_JSON`
- `WORKER_APP_SETTINGS_JSON`

If both explicit variables and JSON bundles are present, explicit variables take precedence for duplicate keys.

Recommended least-privilege role scoping:

- **Platform Bootstrap**: Contributor on target resource group/subscription scope used for infra.
- **Image Release**: `AcrPush` on target ACR + read access to subscription metadata.
- **Runtime Deploy**: Contributor on target resource group + pull access via managed identity/runtime config.

## Troubleshooting Missing Tags

If runtime deploy fails with missing tags:

1. Confirm `image_version` matches image release output.
2. Re-run **Azure Image Release** for that version.
3. Verify all repositories exist in ACR:
   - `aonik-api`
   - `aonik-worker`
   - `aonik-adminui`
4. Re-run **Azure Runtime Deploy**.

Avoid mixing ad-hoc service tags. Runtime deploy intentionally blocks partial version sets.

## Migration: old way → new way

### Old way

- Run one mixed workflow (`azure-iac-cd.yml`) for infra + image resolution/validation.
- Optional fallback/skip behaviors created first-run and consistency friction.

### New way

- `azure-platform-bootstrap.yml` for infra baseline only.
- `azure-image-release.yml` for image build/tag/push and release manifest publication.
- `azure-runtime-deploy.yml` for deterministic runtime rollout using a single release version.

During migration, legacy workflow remains available for manual users, but new environments should follow the 3-workflow architecture.

## Operations Runbooks

- `docs/runbooks/bootstrap.md`
- `docs/runbooks/build-and-push.md`
- `docs/runbooks/deploy-runtime.md`
- `docs/runbooks/release-and-deploy.md`
