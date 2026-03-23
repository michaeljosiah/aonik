# Runbook: Build and Push Runtime Images

Primary workflow: `.github/workflows/ci.yml`

Manual fallback workflow: `.github/workflows/cd-images.yml`

## Purpose
Build and push `aonik-api`, `aonik-worker`, and `aonik-adminui` images to ACR using one cohesive release version.

## Primary path
- Open a pull request to `master` for fast build/test feedback only.
- Merge or push to `master` to trigger CI image publishing.
- CI tags images with the commit SHA and uploads `image-release-<sha>` artifacts.

## Manual fallback inputs (`cd-images.yml`)
- `environment`: `dev|staging|prod`
- `image_tag` (optional; defaults to `github.sha`)
- `semver_alias` (optional)
- `workload_name` (optional)
- `acr_name` (optional explicit override)

## Required GitHub configuration
- Repository vars: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `WORKLOAD_NAME`
- Repository vars: `VITE_AUTH_PROVIDER`, `VITE_API_BASE_URL`
- Repository vars when `VITE_AUTH_PROVIDER=azure-ad`: `VITE_AZURE_AD_CLIENT_ID`, `VITE_AZURE_AD_TENANT_ID`
- Repository vars when `VITE_AUTH_PROVIDER=auth0`: `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`
- Optional repo vars: `VITE_AZURE_AD_REDIRECT_URI`, `VITE_AZURE_AD_API_SCOPE`, `VITE_AUTH0_REDIRECT_URI`, `VITE_AUTH0_AUDIENCE`
- Optional secret fallback for manual image builds: `AZURE_CLIENT_SECRET`

If `VITE_AUTH_PROVIDER` is not set, the workflow defaults it to `azure-ad` for the Admin UI build.

## Steps
1. Preferred: wait for the `master` CI run to finish publishing images.
2. Optional fallback: trigger `cd-images.yml` and set `image_tag` if needed.
3. Wait for all three images to build and push.
4. Download artifact `image-release-<version>`.
5. Record `image-release-manifest.json` for deployment/audit.

## Notes
- OIDC is default; avoid static registry credentials.
- Optional `semver_alias` is mutable and should not be used as deployment source of truth.
- The Admin UI still reads auth/API settings from build-time `VITE_*` values. The automatic CI build therefore uses the repository defaults; use manual `cd-images.yml` if you need environment-specific Admin UI settings until runtime config is externalized.
