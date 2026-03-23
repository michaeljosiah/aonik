# Azure Deployment

This guide defines AONIK's Azure container delivery model with strict separation of concerns:

1. **Platform Bootstrap** (`cd-infra.yml`) provisions/updates shared Azure infrastructure only.
2. **CI** (`ci.yml`) validates pull requests and, on pushes to `master`, builds and pushes a cohesive runtime image set.
3. **Runtime Deploy** (`cd-deploy.yml`) deploys app runtimes using those exact image references with environment approval gates.



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
│ 2) CI                    │
│    (build/test)          │
│    push to master =>     │
│    build/tag/push        │
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
2. Push the desired commit to `master` so **CI** publishes images with the commit SHA.
3. Run **CD: Deploy** using that same image version.

This removes first-run ambiguity; no skip flags are required for normal bootstrap. Bootstrap uses per-service image defaults so API/Admin UI runtime port assumptions remain valid in first-run ACA deployments.

## Normal Release Playbook

1. Merge or push the desired commit to `master`.
2. Wait for **CI** to complete:
   - PRs run build/test only for fast feedback.
   - `master` pushes run build/test and publish `aonik-api`, `aonik-worker`, and `aonik-adminui` images tagged with the commit SHA.
3. Capture release artifact `image-release-<sha>/image-release-manifest.json` from the CI run if you need an audit record.
4. Run **CD: Deploy** with:
   - target `environment`
   - `image_version=<master commit SHA>`
   - `use_digest_references=true` (recommended)

### Manual image rebuild fallback

`cd-images.yml` remains available as a manual fallback if you need to rebuild/push runtime images outside the normal `master` CI path.

### Current caveat

The Admin UI still uses build-time `VITE_*` settings. That means the automatically published CI image reflects the repository-level defaults used during the build. If staging or prod need different Admin UI auth/API values, either:

- rebuild with `cd-images.yml` for that environment, or
- move the Admin UI to runtime-injected configuration so one image can be promoted unchanged across environments.

## Rollback Playbook

1. Identify a prior successful CI image release version.
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

The delivery workflows use:

- **OIDC (`azure/login`) by default**.
- **Service principal secret fallback** only when `AZURE_CLIENT_SECRET` is configured.

Repository variables used by CI image publishing:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `WORKLOAD_NAME`
- `VITE_AUTH_PROVIDER`
- `VITE_API_BASE_URL`
- Provider-specific `VITE_*` values used for the Admin UI build

Environment secrets used by infrastructure/runtime deployment:

- `SQL_ADMIN_PASSWORD` (bootstrap/runtime deployment)
- `BOOTSTRAP_SETUP_SECRET` (runtime deployment; injected as `Bootstrap__SetupSecret`)
- `ACS_CONNECTION_STRING` (optional; required for ACS email/SMS dispatch)
- `VERIFICATION_HASH_KEY` (optional; required for verification hash protection)
- `AZURE_CLIENT_SECRET` (optional fallback)

Environment variables used by deployment workflows:

- `AZURE_RESOURCE_GROUP`
- Optional `WORKLOAD_NAME` override if you need deployment-specific naming

Repository variables used by the Admin UI image build:

- `VITE_AUTH_PROVIDER` (`azure-ad`, `auth0`, or `mock`)
- `VITE_API_BASE_URL`

If `VITE_AUTH_PROVIDER` is omitted, CI/manual image release defaults it to `azure-ad` during the Admin UI build.
- If provider is `azure-ad`: `VITE_AZURE_AD_CLIENT_ID`, `VITE_AZURE_AD_TENANT_ID`
- If provider is `auth0`: `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`
- Optional overrides: `VITE_AZURE_AD_REDIRECT_URI`, `VITE_AZURE_AD_API_SCOPE`, `VITE_AUTH0_REDIRECT_URI`, `VITE_AUTH0_AUDIENCE`

Bootstrap install code is injected with a dedicated GitHub Environment secret:

- `BOOTSTRAP_SETUP_SECRET` -> `Bootstrap__SetupSecret`

Use the JSON payloads only for other runtime app settings:

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

You can include other runtime settings in the same payload (for example `PlatformAdmin__*`, `BlobStorage__*`, `Communication__Azure__Email__FromAddress`, `FeatureManagement__*`). Avoid placing `Bootstrap__SetupSecret` in the JSON bundle now that the deploy flow supports a dedicated secret.

Recommended least-privilege role scoping:

- **Platform Bootstrap**: Contributor on target resource group/subscription scope used for infra.
- **Image Release**: `AcrPush` on target ACR + read access to subscription metadata.
- **Runtime Deploy**: Contributor on target resource group + pull access via managed identity/runtime config.

## Troubleshooting Missing Tags

If runtime deploy fails with missing tags:

1. Confirm `image_version` matches a successful `master` CI run output.
2. Re-run CI from the desired commit or use **CD: Container Images** as a manual fallback.
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
