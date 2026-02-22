# Runbook: Release and Deploy Runtime

Workflow: `.github/workflows/cd-pipeline.yml`

## Purpose
Provide a single operator entry point that can either:
- build/push images and deploy in one run (`build_images=true`), or
- deploy an existing image version without rebuilding (`build_images=false`).

## Inputs
- `profile`: `aca` or `appservice`
- `environment`: `dev|staging|prod`
- `build_images`: `true|false`
- `image_version`: required when `build_images=false`
- `image_tag`: optional tag override when `build_images=true`
- `resource_group` (optional override)
- `workload_name` (optional override)
- `use_digest_references` (`true` recommended)
- `mode`: `what-if` or `deploy`
- `skip_image_validation` (advanced/emergency only)

## Behavior
1. Validates orchestrator input requirements.
2. If `build_images=true`, runs image release and uses the emitted immutable release version for deploy.
3. If `build_images=false`, skips image release and uses provided `image_version`.
4. Runs runtime deploy with existing fail-fast image validation and deployment mode controls.

## Notes
- Recommended default: keep `build_images=true` for normal release operations.
- Use `build_images=false` for controlled rollback or environment promotion with a known image version.

- Advanced overrides (`semver_alias`, `acr_name`, `acr_login_server`, `location`) are intentionally excluded from this orchestrator to keep `workflow_dispatch` within GitHub's 10-input limit; use the underlying workflows directly when those overrides are needed.
