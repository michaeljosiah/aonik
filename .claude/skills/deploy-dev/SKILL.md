---
name: deploy-dev
description: Commit code changes, push to remote, and deploy to the dev environment. Triggers CI, dispatches the CD deploy workflow, approves all environment gates, and verifies completed deployment.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Bash(dotnet build *) Bash(dotnet test *)
---

Commit all staged and unstaged changes, push to the remote, then deploy to the dev environment. You are authorised to approve all GitHub environment protection gates.

## Step 1 — Commit and Push

1. Run `git status` and `git diff` to review all changes.
2. Run `git log --oneline -5` to match the repository's commit message style.
3. Stage relevant files (prefer named files over `git add -A`).
4. Create a commit with a clear message summarising what changed and why. End with:
   ```
   Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
   ```
5. Push the branch to the remote:
   ```bash
   git push
   ```
   If the branch has no upstream, use `git push -u origin HEAD`.

## Step 2 — Wait for CI to Complete

The CI workflow runs automatically on push to `master` (or on PRs targeting `master`).

1. Find the CI run triggered by the push:
   ```bash
   gh run list --workflow=ci.yml --limit=5
   ```
2. Watch the run until it completes:
   ```bash
   gh run watch <run-id>
   ```
3. If CI fails, investigate the logs with `gh run view <run-id> --log-failed`, fix the issue, commit, push, and repeat from Step 1.

## Step 3 — Trigger the Dev Deployment

Once CI passes and images are published, dispatch the deploy workflow:

```bash
gh workflow run "CD: Deploy" \
  -f environment=dev \
  -f mode=deploy \
  -f use_digest_references=true
```

The `image_version` defaults to the current SHA so you do not need to specify it after a fresh CI build.

## Step 4 — Monitor the Deployment

1. Wait a few seconds for the run to register, then find it:
   ```bash
   gh run list --workflow=cd-deploy.yml --limit=5
   ```
2. Watch the deployment run:
   ```bash
   gh run watch <run-id>
   ```

## Step 5 — Approve All Gates

GitHub environment protection rules may pause the workflow awaiting approval.

1. Check for pending deployments:
   ```bash
   gh run view <run-id>
   ```
   Look for "waiting" or "pending" status on environment deployments.
2. If a review is required, approve it:
   ```bash
   gh api repos/{owner}/{repo}/actions/runs/<run-id>/pending_deployments \
     --method POST \
     -f 'environment_ids[]=<env-id>' \
     -f state=approved \
     -f comment="Approved by Claude — authorised by user"
   ```
   To get the pending environment IDs:
   ```bash
   gh api repos/{owner}/{repo}/actions/runs/<run-id>/pending_deployments
   ```
3. **Approve every gate** — do not skip any. The user has explicitly authorised all approvals.

## Step 6 — Verify Completed Deployment

After approval, continue watching the run until it finishes:

```bash
gh run watch <run-id>
```

Once the run completes successfully:

1. Confirm the run status is `completed` with conclusion `success`:
   ```bash
   gh run view <run-id>
   ```
2. Review the full pipeline — every job should show a green checkmark. If any job failed, investigate with:
   ```bash
   gh run view <run-id> --log-failed
   ```
3. Report the final deployment status to the user including the run URL.

## Important Reminders

- **Approve ALL gates** — not just the first one. Multiple approval steps may exist in the pipeline.
- **Do not skip CI** — always wait for CI to pass before triggering deployment.
- **Review the entire pipeline** — after the run completes, verify every job succeeded. A run can show "completed" while individual jobs have failed.
- If the deployment fails, provide the failure logs and suggest next steps rather than silently retrying.
