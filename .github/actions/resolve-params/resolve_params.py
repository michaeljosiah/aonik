"""Resolve effective Bicep deployment parameters.

This script replaces ~400 lines of inline Python that was duplicated
across cd-infra.yml, cd-deploy.yml, and
azure-iac-cd.yml (now deleted).

It reads a base parameter file, applies image references, app settings
overrides, and workload name resolution, then writes an effective
parameter file ready for `az deployment group create`.

Inputs are passed via environment variables (set by the composite action).
"""

import json
import os
import pathlib
import subprocess
import sys


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def env(name: str, default: str = "") -> str:
    return os.environ.get(name, default).strip()


def env_bool(name: str) -> bool:
    return env(name).lower() == "true"


def run_az(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["az", *args],
        capture_output=True,
        text=True,
    )


def resolve_acr_login_server(
    acr_name_override: str,
    workload_name: str,
    environment_name: str,
) -> tuple[str, str]:
    """Return (acr_name, acr_login_server)."""
    if acr_name_override:
        if "." in acr_name_override:
            # Full login server provided
            return acr_name_override.split(".", 1)[0], acr_name_override
        # Short name provided
        result = run_az("acr", "show", "--name", acr_name_override, "--query", "loginServer", "-o", "tsv")
        login_server = (result.stdout or "").strip() if result.returncode == 0 else ""
        return acr_name_override, login_server

    derived = f"{workload_name.lower()}-{environment_name.lower()}acr".replace("-", "")
    result = run_az("acr", "show", "--name", derived, "--query", "loginServer", "-o", "tsv")
    login_server = (result.stdout or "").strip() if result.returncode == 0 else ""
    return derived, login_server


def image_exists_in_acr(acr_name: str, repository: str, tag: str) -> tuple[bool, str]:
    """Check if image:tag exists. Returns (exists, error_detail)."""
    result = run_az(
        "acr", "repository", "show",
        "--name", acr_name,
        "--image", f"{repository}:{tag}",
        "-o", "json",
    )
    if result.returncode == 0:
        return True, result.stdout
    detail = (result.stderr or result.stdout or "unknown error").strip()
    return False, detail


def is_not_found_error(detail: str) -> bool:
    markers = (
        "manifest unknown", "name unknown", "not found",
        "does not exist", "repositorynotfound", "manifestunknown",
    )
    lower = detail.lower()
    return any(m in lower for m in markers)


def resolve_digest(acr_name: str, repository: str, tag: str) -> str:
    """Resolve the digest for an image:tag."""
    result = run_az(
        "acr", "repository", "show",
        "--name", acr_name,
        "--image", f"{repository}:{tag}",
        "-o", "json",
    )
    if result.returncode == 0:
        metadata = json.loads(result.stdout)
        return metadata.get("digest", "")
    return ""


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> None:
    base_param_file = pathlib.Path(env("INPUT_BASE_PARAM_FILE"))
    output_param_file = pathlib.Path(env("INPUT_OUTPUT_PARAM_FILE"))
    workload_name = env("INPUT_WORKLOAD_NAME")
    acr_override = env("INPUT_ACR_OVERRIDE")
    environment_name = env("INPUT_ENVIRONMENT_NAME")
    release_version = env("INPUT_RELEASE_VERSION")
    use_digest = env_bool("INPUT_USE_DIGEST_REFERENCES")
    skip_validation = env_bool("INPUT_SKIP_IMAGE_VALIDATION")
    mode = env("INPUT_MODE", "runtime")  # "bootstrap" or "runtime"
    bootstrap_images_json = env("INPUT_BOOTSTRAP_IMAGES")
    app_settings_json = env("INPUT_APP_SETTINGS")
    image_repos_json = env("INPUT_IMAGE_REPOS")
    admin_ui_target_port = env("INPUT_ADMIN_UI_TARGET_PORT")

    # --- Load base parameters ---
    if not base_param_file.exists():
        print(f"Base parameter file not found: {base_param_file}")
        sys.exit(1)

    document = json.loads(base_param_file.read_text())
    params = document.get("parameters", {})

    # --- Resolve workload name ---
    workload_wrapper = params.get("workloadName")
    if not workload_name:
        if isinstance(workload_wrapper, dict):
            candidate = workload_wrapper.get("value")
            if isinstance(candidate, str) and candidate.strip():
                workload_name = candidate.strip()
    if not workload_name:
        workload_name = "aonik"

    if not isinstance(workload_wrapper, dict):
        params["workloadName"] = {"value": workload_name}
    else:
        workload_wrapper["value"] = workload_name

    print(f"Workload name: {workload_name}")

    # --- Bootstrap mode: apply bootstrap images and write ---
    if mode == "bootstrap":
        bootstrap_images = json.loads(bootstrap_images_json) if bootstrap_images_json else {}
        for key, image_ref in bootstrap_images.items():
            wrapper = params.get(key)
            if isinstance(wrapper, dict) and image_ref:
                wrapper["value"] = image_ref

        if admin_ui_target_port:
            port_wrapper = params.get("adminUiTargetPort")
            if isinstance(port_wrapper, dict):
                port_wrapper["value"] = int(admin_ui_target_port)

        output_param_file.write_text(json.dumps(document, indent=2) + "\n")
        print(f"Bootstrap parameters written to {output_param_file}")
        return

    # --- Runtime mode: resolve ACR, images, app settings ---
    acr_name, acr_login_server = resolve_acr_login_server(
        acr_override, workload_name, environment_name,
    )

    if not acr_login_server or not acr_name:
        print("Unable to resolve ACR registry. Run 'CD: Infrastructure' first or provide acr_override.")
        sys.exit(1)

    print(f"ACR name: {acr_name}")
    print(f"ACR login server: {acr_login_server}")

    # --- Resolve image references ---
    repos: dict[str, str] = {}
    if image_repos_json:
        repos = json.loads(image_repos_json)
    else:
        repos = {
            "apiImage": "aonik-api",
            "workerImage": "aonik-worker",
            "adminUiImage": "aonik-adminui",
        }
        if "appservice" in str(base_param_file):
            repos.pop("workerImage", None)

    resolved: dict[str, str] = {}
    missing: list[tuple[str, str]] = []
    query_failures: list[tuple[str, str]] = []

    for param_key, repository in repos.items():
        if skip_validation:
            resolved[param_key] = f"{acr_login_server}/{repository}:{release_version}"
            continue

        exists, detail = image_exists_in_acr(acr_name, repository, release_version)
        if exists:
            if use_digest:
                digest = resolve_digest(acr_name, repository, release_version)
                if digest:
                    resolved[param_key] = f"{acr_login_server}/{repository}@{digest}"
                else:
                    resolved[param_key] = f"{acr_login_server}/{repository}:{release_version}"
            else:
                resolved[param_key] = f"{acr_login_server}/{repository}:{release_version}"
        elif is_not_found_error(detail):
            missing.append((repository, detail))
        else:
            query_failures.append((repository, detail))

    if query_failures:
        print("Deploy blocked: unable to verify images in ACR due to registry query failures.")
        for repo, detail in query_failures:
            print(f"  - {repo}:{release_version} ({detail})")
        print("Grant the deploy principal AcrPull or resolve transient registry/network issues.")
        print("Use skip_image_validation=true only for emergency recovery.")
        sys.exit(1)

    if missing:
        print(f"Deploy blocked: release version '{release_version}' is incomplete.")
        print("Missing images:")
        for repo, detail in missing:
            print(f"  - {repo}:{release_version} ({detail})")
        print("Run 'CD: Container Images' with the same release version first.")
        print("Use skip_image_validation=true only for emergency recovery.")
        sys.exit(1)

    for key, value in resolved.items():
        wrapper = params.get(key)
        if isinstance(wrapper, dict):
            wrapper["value"] = value

    print("Resolved images:")
    for key, value in resolved.items():
        print(f"  - {key}={value}")

    # --- Merge app settings ---
    if app_settings_json:
        try:
            all_settings = json.loads(app_settings_json)
        except json.JSONDecodeError as e:
            print(f"Invalid app settings JSON: {e}")
            sys.exit(1)

        if not isinstance(all_settings, dict):
            print("App settings must be a JSON object with 'apiAppSettings' and/or 'workerAppSettings' keys.")
            sys.exit(1)

        for param_name in ("apiAppSettings", "workerAppSettings"):
            settings = all_settings.get(param_name)
            if settings and isinstance(settings, dict):
                clean = {str(k): str(v) for k, v in settings.items() if str(v).strip()}
                if clean:
                    params[param_name] = {"value": clean}

    # --- Write effective parameters ---
    output_param_file.write_text(json.dumps(document, indent=2) + "\n")
    print(f"Effective parameters written to {output_param_file}")

    # --- Set outputs for GitHub Actions ---
    github_output = os.environ.get("GITHUB_OUTPUT")
    if github_output:
        with open(github_output, "a") as f:
            f.write(f"acr_name={acr_name}\n")
            f.write(f"acr_login_server={acr_login_server}\n")
            f.write(f"release_version={release_version}\n")


if __name__ == "__main__":
    main()
