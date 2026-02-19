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

### 2) Understand placeholders and choose how to supply values

The committed parameter files intentionally keep generic image placeholders such as:

- `REPLACE_WITH_ACR_LOGIN_SERVER/aonik-api:<tag>`
- `REPLACE_WITH_ACR_LOGIN_SERVER/aonik-worker:<tag>`
- `REPLACE_WITH_ACR_LOGIN_SERVER/aonik-adminui:<tag>`

You can supply the ACR login server explicitly, but it is no longer mandatory for standard naming.

Use one of these options:

1. **Automatic default (new):** the workflow derives the ACR login server from workload name + environment using the same naming convention as IaC: `<workload>-<environment>acr` (hyphens removed) + `.azurecr.io`.
2. **Preferred explicit config:** set `ACR_LOGIN_SERVER` as a GitHub environment variable.
3. **Per-run override:** pass workflow input `acr_login_server` when clicking **Run workflow**.

To control deterministic naming for non-`aonik` deployments, optionally set workflow input `workload_name` (or environment variable `WORKLOAD_NAME`). If omitted, workflow falls back to `workloadName` in the parameter file, then to `aonik`.

The workflow builds an effective parameter file at runtime and replaces `REPLACE_WITH_ACR_LOGIN_SERVER` automatically.

> Important: this replaces only the registry host. Keep image tags in parameter files current for each release (for example `:dev-2026-02-18`) so deployments are predictable and immutable.

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

### 4a) (Optional) Add the ACR login server variable (step-by-step)

If you want explicit control (or your naming differs from the default convention), do this:

1. In GitHub, open **Settings** → **Environments** → `<environment>` → **Secrets and variables** → **Actions**.
2. Under **Variables**, click **New environment variable**.
3. Name: `ACR_LOGIN_SERVER`
4. Value: your registry host only (example: `myregistry.azurecr.io`, without `https://`).
5. Save and re-run **Azure IaC CD** workflow.

You can skip this variable and rely on automatic derivation, or provide `acr_login_server` directly in the workflow run form for one-off deployments.

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
   - `workload_name`: optional workload override for naming (fallback: parameter `workloadName`, then `aonik`)
   - `acr_login_server`: optional per-run ACR host override (example: `myregistry.azurecr.io`)
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
