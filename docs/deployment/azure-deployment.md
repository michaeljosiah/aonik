# Azure Deployment

This guide provides a practical Azure deployment baseline for AONIK.

## Supported Deployment Profiles

- **Primary:** Azure Container Apps (ACA) for API + Worker + Admin UI
- **Fallback:** Azure App Service for API + Admin UI (worker can remain on ACA/Jobs later)

> AKS is intentionally out of scope for the current phase.

## Target Architecture (ACA Primary)

- **API**: Azure Container Apps (`Aonik.Api`) with external ingress
- **Worker**: Azure Container Apps (`Aonik.Worker`) without public ingress
- **Admin UI**: Azure Container Apps (`Aonik.AdminUi`) with external ingress
- **Database**: Azure SQL Database
- **Secrets**: Azure Key Vault + managed identity access
- **Container registry**: Azure Container Registry (ACR)
- **Observability**: Log Analytics + Application Insights

## Infrastructure as Code (Bicep)

IaC assets live under `infra/`:

- `infra/profiles/aca/main.bicep`
- `infra/profiles/appservice/main.bicep`
- `infra/environments/dev|staging|prod/*.parameters.json`

See `infra/README.md` for structure, deployment commands, and required parameter substitutions.

## Required Configuration

- `ConnectionStrings:DefaultConnection` (required outside Development)
- Auth provider configuration under `Auth:*`

## Recommended Deployment Flow

1. Build and publish immutable container tags for API, Worker, and Admin UI.
2. Provision/upgrade infrastructure with Bicep.
3. Run EF Core migrations as a controlled deployment task/job.
4. Roll out API, Worker, and Admin UI revisions.
5. Execute post-deploy smoke checks and monitor telemetry.

## GitHub Actions CD (Step-by-Step)

A dedicated workflow is available at `.github/workflows/azure-iac-cd.yml` for manual, controlled infrastructure rollout.

### What the workflow does

- Supports both profiles: `aca` and `appservice`
- Supports `what-if` mode for safe preview
- Uses Azure authentication via OIDC by default (`azure/login`), with optional client-secret fallback
- Deploys using profile/environment parameter files in `infra/environments/*`
- Fails early when parameter files still contain `REPLACE_WITH_*` placeholders (for example image references)

### 1) Prepare Azure once (OIDC for GitHub)

1. In Azure, create or choose a service principal / app registration for GitHub deployments.
2. Grant it permissions on the target subscription/resource group (typically `Contributor` at minimum scope required).
3. Configure a federated credential that trusts your GitHub repo and environment.
4. Collect values for:
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `AZURE_CLIENT_SECRET` (optional fallback when OIDC cannot be used)

### 2) Prepare environment parameter files in the repo

For each environment, update image placeholders before deployment:

- ACA: `infra/environments/<env>/aca.parameters.json`
  - `apiImage`
  - `workerImage`
  - `adminUiImage`
- App Service: `infra/environments/<env>/appservice.parameters.json`
  - `apiImage`
  - `adminUiImage`

Example image format:

- `myregistry.azurecr.io/aonik-api:staging-2026-01-30`
- `myregistry.azurecr.io/aonik-worker:staging-2026-01-30`
- `myregistry.azurecr.io/aonik-adminui:staging-2026-01-30`

Commit these parameter updates to your branch before running the workflow.

### 3) Create GitHub environments

In GitHub:

1. Open your repository.
2. Go to **Settings** → **Environments**.
3. Create environments named exactly:
   - `dev`
   - `staging`
   - `prod`
4. (Optional but recommended) configure protection rules/required reviewers for `staging` and `prod`.

### 4) Add required GitHub environment secrets

For each environment (`dev`, `staging`, `prod`), add:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_CLIENT_SECRET` (optional)
- `SQL_ADMIN_PASSWORD`

Path in GitHub UI:

- **Settings** → **Environments** → `<environment>` → **Secrets and variables** → **Actions** → **New environment secret**.

### 5) Ensure the target Azure Resource Group exists

The workflow deploys at resource-group scope and requires an existing RG.

If needed, create it first:

```bash
az group create --name <rg-name> --location <azure-region>
```

### 6) Run a safe preview (what-if)

In GitHub:

1. Go to **Actions**.
2. Open **Azure IaC CD** workflow.
3. Click **Run workflow**.
4. Select inputs:
   - `profile`: `aca` or `appservice`
   - `environment`: `dev` / `staging` / `prod`
   - `resource_group`: existing RG name
   - `location`: optional override (or leave empty)
   - `mode`: `what-if`
5. Run and review output to confirm planned changes.

### 7) Execute deployment

Repeat the same steps as above, but set:

- `mode`: `deploy`

Monitor the job logs until completion.

### 8) Verify deployment outputs and health

After a successful deployment:

1. In Azure Portal, check deployed resources under the target RG.
2. Confirm runtime endpoints are up:
   - API URL (`apiUrl` output)
   - Admin UI URL (`adminUiUrl` output)
3. Validate logs/telemetry in Application Insights and Log Analytics.
4. Run smoke tests against API and UI.

### 9) Promote between environments

Recommended sequence:

1. Deploy `dev` first (`what-if` → `deploy`).
2. Deploy `staging` after validation.
3. Deploy `prod` with approval gates enabled in GitHub environments.

## Notes

- The app fails fast in non-Development environments if `ConnectionStrings:DefaultConnection` is missing.
- Avoid embedding secrets in `appsettings.json`; use environment variables and Key Vault references.
- Financially material processing should keep audit/trace telemetry enabled in all non-local environments.
- If `AZURE_CLIENT_SECRET` is configured in the GitHub environment, the IaC workflow authenticates with service principal secret; otherwise it uses OIDC federation.
