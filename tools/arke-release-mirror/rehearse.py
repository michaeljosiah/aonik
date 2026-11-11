"""Live test: own run-scoped empty-catalogue release only. Never a shipped feed."""
import json
import os
from pathlib import Path

from mirror import Catalogue, GitHubRelease, Refusal, Transport, reconcile


class FaultRelease(GitHubRelease):
    fail_after_rename = None
    renames = 0
    fail_readback = False

    def rename(self, asset, name):
        super().rename(asset, name)
        self.renames += 1
        if self.fail_after_rename == self.renames:
            raise Refusal("injected-lost-rename-response")

    def public_readback(self, expected):
        if self.fail_readback:
            raise Refusal("injected-readback-unavailable")
        super().public_readback(expected)


def main():
    repository = os.environ["GITHUB_REPOSITORY"]
    run = os.environ["GITHUB_RUN_ID"]
    attempt = os.environ["GITHUB_RUN_ATTEMPT"]
    if (os.environ.get("GITHUB_ACTIONS") != "true" or repository != "michaeljosiah/aonik"
            or not run.isdigit() or not attempt.isdigit()):
        raise Refusal("isolated-actions-rehearsal-only")
    transport = Transport()
    token = os.environ["GH_TOKEN"]
    api = f"https://api.github.com/repos/{repository}"
    tag = f"aonik-mirror-rehearsal-{run}-{attempt}"
    def request(path, method="GET", payload=None):
        return transport.request(api + path, method=method, token=token,
                                 data=None if payload is None else json.dumps(payload).encode())
    def latest():
        try:
            release = json.loads(request("/releases/latest"))
            return {"id": release["id"], "tag": release["tag_name"]}
        except Refusal as error:
            if str(error) == "github-http-404":
                return None
            raise
    original_latest = latest()
    created = json.loads(request("/releases", "POST", {
        "tag_name": tag, "target_commitish": os.environ["GITHUB_SHA"],
        "name": "Temporary AONIK mirror recovery test", "prerelease": True, "make_latest": "false",
        "body": "Automated isolated #431 qualification. Empty synthetic catalogues only; removed after testing."}))
    if created["tag_name"] != tag or not created["prerelease"]:
        raise Refusal("created-release-identity-mismatch")
    results = {"repository": repository, "tag": tag, "releaseId": created["id"],
               "runId": run, "sourceCommit": os.environ["GITHUB_SHA"], "checks": []}
    try:
        remote = FaultRelease(repository, tag, token, transport)
        def feed(serial, band="isolated-test"):
            return json.dumps({"version": 2, "serial": serial, "worlds": [], "band": band}).encode()
        floor = Catalogue.read(feed(1))
        def mirror(data):
            return reconcile(remote, lambda: data, floor.serial, floor.sha256)
        def check(name, serial, data=None):
            result = mirror(data or feed(serial))
            if result["serial"] != serial:
                raise Refusal("unexpected-test-result")
            results["checks"].append({"name": name, **result})
        def refused(name, expected, data):
            try:
                mirror(data)
            except Refusal as error:
                if str(error) != expected:
                    raise
                results["checks"].append({"name": name, "code": expected})
            else:
                raise Refusal("expected-test-refusal-missing")

        check("initial-create-and-anonymous-readback", 1)
        check("higher-serial-promotes-exact-bytes", 2)
        check("duplicate-trigger-no-rewrite", 2)
        refused("stale-trigger-refused", "remote-ahead", feed(1))
        refused("same-serial-conflict-held", "same-serial-conflict", feed(2, "conflict"))
        remote.fail_after_rename = remote.renames + 1
        refused("lost-retirement-response", "injected-lost-rename-response", feed(3))
        remote.fail_after_rename = None
        refused("missing-live-asset-still-fenced", "remote-ahead", feed(2))
        check("recover-interrupted-promotion", 3)
        remote.fail_after_rename = remote.renames + 2
        refused("lost-promotion-response", "injected-lost-rename-response", feed(4))
        remote.fail_after_rename = None
        check("recover-committed-unknown-outcome", 4)
        remote.fail_readback = True
        refused("failed-public-readback", "injected-readback-unavailable", feed(5))
        remote.fail_readback = False
        check("retry-confirms-without-republishing", 5)
        after = latest()
        if after != original_latest:
            raise Refusal("application-latest-changed")
        results["latestUnchanged"] = True
        results["finalAssetCount"] = len(remote.list_assets())
        results["passed"] = True
    finally:
        # Only this run's release/tag, captured from its successful create call.
        request(f"/releases/{int(created['id'])}", "DELETE")
        request(f"/git/refs/tags/{tag}", "DELETE")
        results["isolatedReleaseRemoved"] = True
        Path("mirror-rehearsal.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps(results))


if __name__ == "__main__":
    main()
