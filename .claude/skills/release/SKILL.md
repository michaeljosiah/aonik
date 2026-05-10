---
name: release
description: Cut an Aonik release. Bumps the Desktop package.json version, commits the bump, creates and pushes an annotated SemVer tag, then monitors both release workflows (API + CLI/Desktop) to completion. Reports the GitHub Release URL when done.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Bash(node *) Read Edit
---

Cut a tagged release of Aonik. The user has authorised you to bump
versions, commit, tag, push, and monitor the resulting GitHub Actions
workflows.

The user invokes this skill with the target version as an argument:

```
/release v0.2.0
/release 0.2.0          # 'v' prefix is optional — normalise either way
/release v0.2.0-rc.1    # pre-release tags are supported
```

If no version was supplied, ASK the user before doing anything else.
Never invent a version.

---

## Step 1 — Parse and validate the version

1. Take the user's argument and normalise it:
   - `TAG` = the version with `v` prefix (e.g. `v0.2.0`)
   - `VERSION` = the version WITHOUT `v` prefix (e.g. `0.2.0`)
2. Validate that `VERSION` matches SemVer: `MAJOR.MINOR.PATCH` with
   optional `-prerelease` suffix (e.g. `0.2.0`, `0.2.0-rc.1`,
   `1.0.0-beta.3`). Reject anything else with a clear error.
3. Confirm the parsed `TAG` and `VERSION` back to the user before
   proceeding (one short sentence).

## Step 2 — Pre-flight checks

Run these checks **in parallel** where possible. ALL must pass before
continuing. If any fails, stop and report the failure to the user — do
NOT attempt to auto-fix.

1. **On `master` branch:**
   ```bash
   git rev-parse --abbrev-ref HEAD
   ```
   Must print `master`. If on a feature branch, abort.
2. **Working tree clean:**
   ```bash
   git status --porcelain
   ```
   Must produce empty output. Uncommitted changes block the release —
   the user should either commit, stash, or discard before retrying.
3. **Up to date with `origin/master`:**
   ```bash
   git fetch origin master
   git rev-list HEAD..origin/master --count   # must be 0
   git rev-list origin/master..HEAD --count   # must be 0
   ```
   If `HEAD` is behind, abort and ask the user to `git pull`. If
   `HEAD` is ahead, abort and ask whether to push first.
4. **Tag doesn't already exist:**
   ```bash
   git tag --list "$TAG"               # local
   git ls-remote --tags origin "$TAG"  # remote
   ```
   Both must be empty. If the tag exists, abort with a pointer to the
   "Fixing a bad tag" section of `docs/deployment/github-release.md`.
5. **Latest CI on `master` is green:**
   ```bash
   gh run list --workflow=ci.yml --branch=master --limit=1 \
     --json status,conclusion,headSha,url
   ```
   The most recent run must be `status=completed` AND
   `conclusion=success`. If CI is still running or red, abort.

Show a short summary table of what was checked before moving on.

## Step 3 — Bump the Desktop package.json

The Desktop installer embeds the version from
`src/Aonik.AdminDesktop/package.json`. The release-clients.yml workflow
overrides it at build time, but keeping the source in sync prevents
drift and makes local `npm run dist:*` commands produce
correctly-versioned installers.

1. Read `src/Aonik.AdminDesktop/package.json` and capture the current
   `version` field.
2. If `current_version === VERSION` already, **skip the bump entirely**
   and jump to Step 5 (no commit needed) — but still print "Desktop
   already at $VERSION, skipping bump".
3. Otherwise, use the **Edit tool** (not `npm version` — we want to
   avoid creating a side-effect commit/tag from npm) to change the
   version field. Replace the line:
   ```
   "version": "<current_version>",
   ```
   with:
   ```
   "version": "<VERSION>",
   ```
4. Verify by re-reading the file and confirming the change.

If a `CHANGELOG.md` exists at the repo root with an `## [Unreleased]`
section, also rename that heading to `## [<VERSION>] - <YYYY-MM-DD>`
and insert a fresh empty `## [Unreleased]` heading above it. If there's
no CHANGELOG.md, skip this — don't create one.

## Step 4 — Commit and push the bump

Only run this step if Step 3 actually changed files.

```bash
git add src/Aonik.AdminDesktop/package.json
# Add CHANGELOG.md too if it was edited
git add CHANGELOG.md 2>/dev/null || true

git commit -m "$(cat <<'EOF'
chore(release): bump Desktop to <VERSION>

Prepare for tagged release <TAG>. Aligns the Desktop package.json
with the tag so local `npm run dist:*` builds match published
installer metadata.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
EOF
)"

git push origin master
```

If the push is rejected (e.g. someone else pushed to master between
Steps 2 and 4), abort and ask the user to retry — do NOT pull-and-retry
automatically. Re-tagging logic gets messy when commits sneak in.

## Step 5 — Create and push the annotated tag

The tag must point at the bump commit (or, if the bump was skipped, at
the current `master` HEAD).

```bash
git tag -a "$TAG" -m "Release $TAG"
git push origin "$TAG"
```

Tag pushes are NOT triggered by `git push` alone — the explicit
`git push origin <tag>` is required.

The push fires:
- `.github/workflows/release.yml`         — API tarball
- `.github/workflows/release-clients.yml` — CLI binaries + Desktop installers

## Step 6 — Monitor both workflows

Wait a few seconds for GitHub to register the runs, then:

1. Find the runs triggered by the tag push:
   ```bash
   gh run list --workflow=release.yml         --event=push --limit=3
   gh run list --workflow=release-clients.yml --event=push --limit=3
   ```
   The newest run on each that has `headBranch=$TAG` (or that started
   in the last minute) is the one we care about. Capture both run IDs.

2. Watch BOTH runs to completion. Run them in parallel — start one in
   the background and watch the other in the foreground:
   ```bash
   gh run watch <release-yml-run-id>          # foreground
   gh run watch <release-clients-yml-run-id>  # in background
   ```
   The `release-clients.yml` job is slow (Electron builds take 10-15
   minutes for the macOS leg).

3. If a run **fails**, fetch the failed-job logs:
   ```bash
   gh run view <run-id> --log-failed
   ```
   Common failure modes are listed in the troubleshooting table at the
   bottom of `docs/deployment/github-release.md`. Don't try to fix and
   re-run automatically — report the failure to the user with the
   suspected cause and ask how to proceed.

4. If a workflow needs manual re-run (e.g. flaky network), use:
   ```bash
   gh workflow run release-clients.yml -f tag=$TAG
   ```

## Step 7 — Verify and report

Once both workflows are green:

1. Inspect the release:
   ```bash
   gh release view "$TAG" --json name,tagName,url,assets,createdAt
   ```
2. Confirm the assets list contains:
   - `aonik-api-<TAG>.tar.gz`
   - `aonik-cli-<TAG>-{linux-x64,linux-arm64,win-x64,osx-x64,osx-arm64}.{tar.gz,zip}` (5 files)
   - At least one `.exe` (Windows NSIS installer)
   - At least one `.dmg` (macOS — usually 2: x64 and arm64)
   - At least one `.AppImage` (Linux)
   - `latest.yml`, `latest-mac*.yml`, `latest-linux.yml` (auto-update metadata)
   - `*.blockmap` files (delta updates)
3. Report to the user:
   - The release URL (`gh release view --json url -q .url`)
   - The full asset list as a markdown bullet list
   - Total artifact size
   - Workflow run URLs for both release.yml and release-clients.yml

If any expected asset is missing, flag it explicitly — that usually
means a matrix leg failed silently (e.g. icon files missing for the
Desktop build, or a code-sign step failed without erroring the job).

---

## Important reminders

- **Never reuse a tag that's been published.** If `v0.2.0` is broken,
  ship `v0.2.1`. Don't delete-and-retag — users may have already
  downloaded the bad assets.
- **Never bypass pre-flight checks.** Master must be green and the
  working tree must be clean. If the user pushes back, ask them to fix
  the underlying issue first; don't carve out exceptions silently.
- **Never `git push --tags`.** Always push the specific tag with
  `git push origin <tag>`. Pushing all tags can leak experimental
  local tags and accidentally trigger releases.
- **Never use `git tag` (lightweight) — always `git tag -a` (annotated)**
  with a message. GitHub uses the tag message as part of the release
  metadata.
- **Don't auto-fix bumps from `npm version`.** Use the Edit tool
  directly so we don't accumulate npm's auto-tag/auto-commit side
  effects.
- **Releases are NOT deployments.** Pushing a tag publishes artifacts;
  it does not roll anything out to dev/staging/prod. Use the
  `deploy-dev` skill for runtime deploys after the release lands.
- If the user asks you to release a hotfix, the same flow applies —
  pre-flight checks still hold (master green, clean tree). Reject
  attempts to bypass them.

## See also

- `docs/deployment/github-release.md` — full release runbook (versioning,
  signing roadmap, troubleshooting table)
- `.github/workflows/release.yml`         — API workflow
- `.github/workflows/release-clients.yml` — CLI + Desktop workflow
- `.claude/skills/deploy-dev/SKILL.md`    — runtime deploy (separate flow)
