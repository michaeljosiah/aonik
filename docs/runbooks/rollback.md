# Runbook: Rollback

## Purpose

Roll back a failed or problematic deployment to a previously known-good release version.

## When to Use

- A deployment causes errors, degraded performance, or broken functionality
- A critical bug is discovered in the newly deployed version
- Health probes or monitoring alerts fire after deployment

## Prerequisites

- Know the **previous release version** (image tag) that was stable. Check the `image-release-manifest.json` artifact from a prior successful **CD: Container Images** run, or inspect the ACR tags with `az acr repository show-tags`.
- Access to the target GitHub environment secrets (same as any deploy).

## Rollback Steps

### Option A: Re-deploy via CD: Deploy (Recommended)

This is the safest approach — it uses the same deployment pipeline as normal releases.

1. Go to **Actions > CD: Deploy** (`cd-deploy.yml`).
2. Click **Run workflow**.
3. Set inputs:
   - `profile`: same as original (e.g. `aca`)
   - `environment`: the affected environment
   - `image_version`: the **previous stable version** (e.g. `abc1234`)
   - `mode`: `what-if` first, then `deploy`
   - Leave `skip_image_validation` as `false`
4. Run `what-if` and verify the diff shows the expected image rollback.
5. Run `deploy`.
6. Verify:
   - API endpoint returns 200 (`curl -s -o /dev/null -w "%{http_code}" https://<api-url>/health`)
   - Check Application Insights for error rate returning to baseline
   - Confirm admin UI loads correctly

### Option B: Re-deploy via CD: Pipeline

Use when you also want to rebuild images from a known-good commit.

1. Go to **Actions > CD: Pipeline** (`cd-pipeline.yml`).
2. Set `build_images` to `true`.
3. Set `ref` to the known-good Git commit SHA or tag.
4. Run through `what-if` then `deploy` for the target environment.

### Option C: Azure CLI Direct Revision Switch (Emergency)

Use only when GitHub Actions is unavailable or too slow.

```bash
# List active revisions
az containerapp revision list \
  --name aonik-<env>-api \
  --resource-group <rg> \
  --output table

# Activate a previous revision and route 100% traffic
az containerapp ingress traffic set \
  --name aonik-<env>-api \
  --resource-group <rg> \
  --revision-weight <previous-revision-name>=100
```

Repeat for `worker` and `adminui` container apps as needed.

## Post-Rollback

1. **Communicate** the rollback to the team (channel/incident thread).
2. **Investigate** the root cause of the failed deployment.
3. **Do not re-deploy** the broken version until the fix is verified in a lower environment.
4. If resource locks are enabled (prod), they do not affect container image updates — only resource deletion.

## Finding Previous Versions

```bash
# List recent tags for a repository in ACR
az acr repository show-tags \
  --name <acr-name> \
  --repository aonik-api \
  --orderby time_desc \
  --top 10

# Download a prior release manifest artifact from GitHub Actions
gh run download <run-id> -n image-release-manifest
cat image-release-manifest.json
```
