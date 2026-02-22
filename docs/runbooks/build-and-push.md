# Runbook: Build and Push Runtime Images

Workflow: `.github/workflows/cd-images.yml`

## Purpose
Build and push `aonik-api`, `aonik-worker`, and `aonik-adminui` images to ACR using one cohesive release version.

## Inputs
- `environment`: `dev|staging|prod`
- `image_tag` (optional; defaults to `github.sha`)
- `semver_alias` (optional)
- `workload_name` (optional)
- `acr_name` (optional explicit override)

## Required GitHub Environment Secrets/Vars
- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- Optional secret fallback: `AZURE_CLIENT_SECRET`
- Vars: `VITE_AUTH_PROVIDER`, `VITE_API_BASE_URL`
- Vars when `VITE_AUTH_PROVIDER=azure-ad`: `VITE_AZURE_AD_CLIENT_ID`, `VITE_AZURE_AD_TENANT_ID`
- Vars when `VITE_AUTH_PROVIDER=auth0`: `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`
- Optional vars: `VITE_AZURE_AD_REDIRECT_URI`, `VITE_AZURE_AD_API_SCOPE`, `VITE_AUTH0_REDIRECT_URI`, `VITE_AUTH0_AUDIENCE`

If `VITE_AUTH_PROVIDER` is not set, the workflow defaults it to `azure-ad` for the Admin UI build.

## Steps
1. Trigger workflow and set `image_tag` if needed.
2. Wait for all three images to build and push.
3. Download artifact `image-release-<version>`.
4. Record `image-release-manifest.json` for deployment/audit.

## Notes
- OIDC is default; avoid static registry credentials.
- Optional `semver_alias` is mutable and should not be used as deployment source of truth.


## Optional Combined Execution

If you want one manual trigger for release + deploy, use `.github/workflows/cd-pipeline.yml` with `build_images=true`.
