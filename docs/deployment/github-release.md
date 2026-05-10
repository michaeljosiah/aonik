# GitHub Release Runbook

Aonik ships releases through a **tag-driven** workflow on GitHub. Pushing
a SemVer tag like `v0.2.0` triggers two workflows that build, package,
and attach all release artifacts to a single GitHub Release.

## What gets shipped

A single tagged release attaches the following assets, all built and
uploaded automatically by GitHub Actions:

| Component   | Workflow              | Artifacts attached                                               |
|-------------|-----------------------|------------------------------------------------------------------|
| API         | `release.yml`         | `aonik-api-<tag>.tar.gz`                                         |
| CLI         | `release-clients.yml` | `aonik-cli-<tag>-{linux-x64,linux-arm64,win-x64,osx-x64,osx-arm64}.{tar.gz,zip}` |
| Desktop app | `release-clients.yml` | NSIS `.exe` (Windows), `.dmg` (macOS x64+arm64), `.AppImage` (Linux), plus `latest*.yml` and `*.blockmap` for auto-updates |

Both workflows attach to the **same** release using
`softprops/action-gh-release@v2`, which is idempotent on tag — files
from one job don't overwrite files from the other.

The download URLs follow a stable pattern:

```
https://github.com/<org>/aonik/releases/download/<tag>/<filename>
https://github.com/<org>/aonik/releases/latest/download/<filename>   # always latest
```

The `latest/download/<filename>` URL is what installers and update tools
should pin against.

## Pre-release checklist

Run through this before tagging:

1. `master` is green — CI on the head commit shows a successful `ci.yml`
   run.
2. The change you want shipped is merged to `master` (releases cut from
   master only — feature branches don't release).
3. **Bump `src/Aonik.AdminDesktop/package.json` `version`** to match the
   tag you intend to push (drop the `v` prefix — `v0.2.0` →
   `"version": "0.2.0"`). The workflow overrides it for the build, but
   keeping the source value in sync prevents drift.
4. Update `CHANGELOG.md` if the repo has one (release notes are
   auto-generated from PR titles since the previous tag, so a
   well-curated changelog isn't strictly required, but it helps).
5. Decide whether you're shipping a **prerelease** (`v0.2.0-rc.1`,
   `v0.2.0-beta.1`) or a **stable** release (`v0.2.0`). Prerelease tags
   build the same artifacts but should be marked "prerelease" on the
   GitHub Release page after the fact.

## Cutting a release

```bash
# 1. Make sure local master is up to date.
git checkout master
git pull origin master

# 2. Create an ANNOTATED tag (-a) with a short release message.
#    Annotated tags carry an author + date + message and are what
#    `git describe` and GitHub use as the release source.
git tag -a v0.2.0 -m "Release v0.2.0"

# 3. Push the tag. `git push` alone does NOT push tags.
git push origin v0.2.0
```

That's it. Within a few seconds:

- `release.yml` starts building the API tarball (~3-5 min).
- `release-clients.yml` starts building the CLI matrix (5 RIDs in
  parallel, ~3-4 min) and the Desktop matrix (3 OS-specific runners in
  parallel, ~10-15 min — Electron is slow).
- Once both finish, the release page contains every artifact.

Watch progress:

```bash
gh run list --workflow=release.yml         --limit=3
gh run list --workflow=release-clients.yml --limit=3
gh run watch <run-id>
```

## Manual trigger (workflow_dispatch)

If a tag already exists but the workflow didn't run (e.g. you cut a tag
before `release-clients.yml` was added, or a build failed and you fixed
the workflow), you can re-run against an existing tag without retagging:

```bash
gh workflow run release.yml         -f tag=v0.2.0
gh workflow run release-clients.yml -f tag=v0.2.0
```

Or in the GitHub UI: **Actions → [workflow] → Run workflow → enter tag**.

## Versioning

Stick to **SemVer** with a `v` prefix:

- `v<MAJOR>.<MINOR>.<PATCH>`             — stable releases
- `v<MAJOR>.<MINOR>.<PATCH>-rc.<N>`      — release candidates
- `v<MAJOR>.<MINOR>.<PATCH>-beta.<N>`    — betas
- `v<MAJOR>.<MINOR>.<PATCH>-alpha.<N>`   — alphas

`MAJOR` increments on breaking changes, `MINOR` on new features (no
breaking changes), `PATCH` on bug fixes. Pre-release suffixes order
naturally: `0.2.0-alpha.1` < `0.2.0-beta.1` < `0.2.0-rc.1` < `0.2.0`.

The CLI and API embed the tag-stripped version (`0.2.0`) into the
binary via `dotnet publish /p:Version=…`. The Desktop app uses
`electron-builder`'s `extraMetadata.version` override so the installer
metadata matches the tag.

## Fixing a bad tag

If you tag the wrong commit, or push a tag with a typo:

```bash
# Delete locally.
git tag -d v0.2.0

# Delete on the remote (this triggers nothing — releases stay alive
# until you also delete the GitHub Release explicitly).
git push origin :refs/tags/v0.2.0

# If a release was created, delete it too.
gh release delete v0.2.0 --yes

# Re-tag and push correctly.
git tag -a v0.2.0 -m "Release v0.2.0"
git push origin v0.2.0
```

**Never reuse a tag** that's already been published to users. If
`v0.2.0` is broken, ship `v0.2.1` instead.

## Workflow internals

### `release.yml` — API

- Triggers on `push: tags: 'v*'` and `workflow_dispatch`.
- One job: builds `Aonik.sln` on `ubuntu-latest`, runs all tests,
  publishes `src/Aonik.Api`, tars it as `aonik-api-<tag>.tar.gz`.
- Validates the canonical migration stream
  (`dotnet ef migrations has-pending-model-changes`) before building —
  so a model/snapshot drift will fail the release.

### `release-clients.yml` — CLI + Desktop

- Same trigger as `release.yml` (push tag `v*` or manual).
- **`prepare`** job extracts the tag and the version-without-`v` once
  and exposes them as outputs.
- **`build-cli`** matrix on `ubuntu-latest` — 5 RIDs (`linux-x64`,
  `linux-arm64`, `win-x64`, `osx-x64`, `osx-arm64`). All cross-compile
  fine from Linux because we're not yet code-signing. Output: a
  self-contained single-file binary per RID, packaged as `.tar.gz` (or
  `.zip` for Windows).
- **`build-desktop`** matrix on `windows-latest` / `macos-latest` /
  `ubuntu-latest` — Electron installers MUST be built on their target
  OS. Each runner first builds `Aonik.AdminUi` (whose `dist/` folder is
  the renderer), then runs `electron-builder` with
  `--publish=never -c.extraMetadata.version=<version>`.
- **`attach-to-release`** job downloads every matrix output and hands
  it to `softprops/action-gh-release@v2`.

The workflow file is annotated heavily — read
`.github/workflows/release-clients.yml` directly for line-by-line
explanations.

## Code signing (deferred)

The current workflow ships **unsigned** artifacts. Expect:

- **Windows** — SmartScreen warns "Windows protected your PC. Microsoft
  Defender SmartScreen prevented an unrecognized app from starting."
  Users have to click "More info → Run anyway."
- **macOS** — Gatekeeper refuses to launch the app outright. Users have
  to right-click → Open → confirm.
- **Linux** — generally fine.

When you're ready to ship to public users, wire signing in this order
(easiest payoff first):

1. **Windows code-signing certificate** (~$100-400/year, EV is more
   trusted but pricier). Store the `.pfx` as a base64 secret
   (`WINDOWS_CERT_PFX_BASE64`), unpack it on the runner, sign with
   `signtool` after the NSIS step. electron-builder has a built-in
   hook.
2. **Apple Developer Program** ($99/year). Generate a Developer ID
   Application certificate, store it as `MAC_CERT_P12_BASE64` plus
   `MAC_CERT_PASSWORD`, set `APPLE_ID` /
   `APPLE_APP_SPECIFIC_PASSWORD` / `APPLE_TEAM_ID`. electron-builder
   handles codesign + notarization end-to-end if those env vars are set.
3. **Linux** — generally not signed. Optionally GPG-sign the AppImage
   if your audience verifies upstream signatures.

Slot names referenced above are reserved — when you wire signing, use
exactly those secret names so the workflow only needs the env-var
plumbing added, not renaming.

## Auto-updates (deferred)

`electron-builder` already produces `latest.yml`,
`latest-mac{,-arm64}.yml`, and `latest-linux.yml` files alongside the
installers, and `release-clients.yml` attaches them to the release.

To enable in-app auto-update for the Desktop app:

1. Switch `src/Aonik.AdminDesktop/electron-builder.yml`'s `publish`
   block from `provider: generic` to:
   ```yaml
   publish:
     provider: github
     owner: <github-org>
     repo: aonik
   ```
2. Add `electron-updater` as a runtime dependency in
   `src/Aonik.AdminDesktop/package.json`.
3. Wire `autoUpdater.checkForUpdatesAndNotify()` into the main process
   (`src/main/index.ts`).

The installed app will then poll
`https://api.github.com/repos/<org>/aonik/releases/latest`, compare the
version, and offer updates with delta downloads (using the `.blockmap`
files we already attach).

## Releases vs. environment deployments

Releases are **source-of-truth artifacts** — they don't deploy
themselves. Pushing `v0.2.0`:

- ✅ Builds and publishes API tarball + CLI binaries + Desktop
  installers to a GitHub Release.
- ❌ Does NOT deploy the API to dev/staging/prod. That's a separate
  flow via `cd-images.yml` (image build/push) and `cd-deploy.yml`
  (rollout to ACA).

To deploy a tagged build, see
[`docs/runbooks/deploy-runtime.md`](../runbooks/deploy-runtime.md) and
[`docs/runbooks/build-and-push.md`](../runbooks/build-and-push.md).

The `.claude/skills/deploy-dev` skill encapsulates the dev-environment
deploy flow end-to-end.

## Troubleshooting

| Symptom                                           | Likely cause                                                | Fix |
|---------------------------------------------------|-------------------------------------------------------------|-----|
| `release-clients.yml` didn't fire                 | Workflow not yet on the tagged commit                       | Either re-tag from a commit that includes the workflow, or trigger manually with `gh workflow run release-clients.yml -f tag=…` |
| Desktop build fails on `icon not found`           | `src/Aonik.AdminDesktop/resources/` is missing icons        | Add `icon.ico` (Windows), `icon.icns` (macOS), `icon.png` (Linux) at those paths, or remove the `icon:` lines from `electron-builder.yml` to fall back to defaults |
| CLI publish fails with reflection error           | `PublishTrimmed=true` was enabled                            | Leave trimming OFF until Spectre.Console + System.CommandLine surfaces are trim-annotated |
| `softprops/action-gh-release` says "Resource not accessible" | Workflow lacks `permissions: contents: write`         | Already set at the top of both release workflows — confirm you didn't accidentally change it |
| Installer version says `0.1.0` instead of `0.2.0` | `package.json` version not bumped AND the override flag failed | Verify the `-c.extraMetadata.version=<version>` flag is still in the workflow's "Build Desktop installer" step |
| Two releases created for one tag                  | `release.yml` and `release-clients.yml` raced at create-time | Harmless — the `softprops` action merges. If you really got two distinct GitHub Releases, delete the empty one |
| `Sign tool not found` on Windows runner           | Code signing was wired but signtool path changed             | Use `windows-sdk-installer` action or hardcode the SDK path; signtool ships with the Windows 10 SDK on `windows-latest` runners |

## See also

- `.github/workflows/release.yml` — API workflow (annotated)
- `.github/workflows/release-clients.yml` — CLI + Desktop workflow (annotated)
- `src/Aonik.AdminDesktop/electron-builder.yml` — Desktop packaging config
- [`docs/runbooks/deploy-runtime.md`](../runbooks/deploy-runtime.md) — runtime deployment (separate from releases)
- [`docs/runbooks/rollback.md`](../runbooks/rollback.md) — rolling back a bad release
