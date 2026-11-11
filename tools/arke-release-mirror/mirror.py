"""Serialize this process with every writer to the target GitHub release.

The immutable snapshot is a durable serial fence. GitHub has no atomic release
asset swap: a killed process may leave catalogue.json absent until the next run.
No payload, token, signed download URL or private API response is logged.
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass

LIMIT = 1024 * 1024
SNAPSHOT = re.compile(r"^aonik-catalogue-(\d+)-([a-f0-9]{64})\.json$")


class Refusal(RuntimeError):
    pass


@dataclass(frozen=True)
class Catalogue:
    data: bytes
    serial: int
    sha256: str

    @classmethod
    def read(cls, data):
        if len(data) > LIMIT:
            raise Refusal("catalogue-too-large")
        try:
            root = json.loads(data.decode("utf-8"))
            serial = root["serial"]
            if (root["version"] != 2 or type(serial) is not int
                    or not 0 <= serial < 9007199254740991
                    or not isinstance(root["worlds"], list)):
                raise ValueError()
        except (ValueError, KeyError, TypeError):
            raise Refusal("invalid-catalogue") from None
        return cls(data, serial, hashlib.sha256(data).hexdigest())

    @property
    def snapshot_name(self):
        return f"aonik-catalogue-{self.serial}-{self.sha256}.json"

    @property
    def pending_name(self):
        return f"aonik-pending-{self.serial}-{self.sha256}.json"


def same(left, right):
    return left.serial == right.serial and left.sha256 == right.sha256


def advance(source, previous):
    if source.serial < previous.serial:
        raise Refusal("remote-ahead")
    if source.serial == previous.serial and source.sha256 != previous.sha256:
        raise Refusal("same-serial-conflict")


def reconcile(remote, source, baseline_serial, baseline_sha256):
    """No event payload supplies bytes: every run obtains today's canonical head."""
    head = Catalogue.read(source())
    floor = Catalogue(b"", baseline_serial, baseline_sha256)
    advance(head, floor)
    assets = remote.list_assets()
    current = remote.named(assets, "catalogue.json")
    current_head = Catalogue.read(remote.download(current)) if current else None
    if current_head:
        advance(current_head, floor)
        advance(head, current_head)

    anchors = []
    for asset in assets:
        match = SNAPSHOT.fullmatch(asset["name"])
        if match:
            anchors.append((int(match[1]), match[2], asset))
    if anchors:
        highest = max(x[0] for x in anchors)
        candidates = [x for x in anchors if x[0] == highest]
        if len({x[1] for x in candidates}) != 1:
            raise Refusal("conflicting-serial-fences")
        serial, digest, asset = candidates[0]
        anchor = Catalogue.read(remote.download(asset))
        if anchor.serial != serial or anchor.sha256 != digest:
            raise Refusal("invalid-serial-fence")
        advance(anchor, floor)
        advance(head, anchor)
        if current_head and current_head.serial == anchor.serial:
            advance(current_head, anchor)

    if current_head and same(head, current_head):
        remote.public_readback(head)
        return {"status": "synced", "serial": head.serial, "sha256": head.sha256, "promoted": False}

    # These two readbacks occur before renaming the live feed. Never rename the
    # immutable serial fence: it must survive a crash at any subsequent step.
    remote.ensure_asset(head.snapshot_name, head.data)
    pending = remote.ensure_asset(head.pending_name, head.data)
    fresh = Catalogue.read(source())
    if not same(head, fresh):
        raise Refusal("canonical-head-changed-retry")
    latest = remote.named(remote.list_assets(), "catalogue.json")
    if (latest or {}).get("id") != (current or {}).get("id"):
        raise Refusal("external-writer-detected")
    if latest:
        observed = Catalogue.read(remote.download(latest))
        if not same(observed, current_head):
            raise Refusal("external-writer-detected")
        remote.rename(latest, f"aonik-retired-{observed.serial}-{observed.sha256}-{latest['id']}.json")
    # Re-running after an uncertain PATCH reads asset identities and never
    # deletes/reuploads catalogue.json. A completed rename is recognised above.
    remote.rename(pending, "catalogue.json")
    remote.public_readback(head)
    return {"status": "synced", "serial": head.serial, "sha256": head.sha256, "promoted": True}


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Transport:
    def __init__(self):
        self.opener = urllib.request.build_opener(NoRedirect)

    def request(self, url, *, method="GET", data=None, token=None, limit=LIMIT,
                content_type="application/json", accept="application/vnd.github+json", redirects=False):
        # Authorisation is restricted to GitHub's API/upload endpoints. Downloads
        # use separate unauthenticated requests, including every redirect hop.
        for _ in range(4):
            uri = urllib.parse.urlsplit(url)
            if uri.scheme != "https" or uri.username or uri.password or uri.fragment or uri.port not in (None, 443):
                raise Refusal("invalid-https-endpoint")
            if token and uri.hostname not in ("api.github.com", "uploads.github.com"):
                raise Refusal("credential-destination-refused")
            headers = {"User-Agent": "Aonik-Release-Mirror/1.0", "Accept": accept,
                       "Cache-Control": "no-cache"}
            # A JSON content type on a bodyless GET makes FastEndpoints attempt
            # to deserialize an empty body before serving the public catalogue.
            if data is not None:
                headers["Content-Type"] = content_type
            if uri.hostname in ("api.github.com", "uploads.github.com"):
                headers["X-GitHub-Api-Version"] = "2022-11-28"
            if token:
                headers["Authorization"] = f"Bearer {token}"
            request = urllib.request.Request(url, data=data, headers=headers, method=method)
            try:
                response = self.opener.open(request, timeout=45)
            except urllib.error.HTTPError as error:
                if redirects and error.code in (301, 302, 303, 307, 308):
                    next_url = urllib.parse.urljoin(url, error.headers.get("Location", ""))
                    host = urllib.parse.urlsplit(next_url).hostname
                    error.close()
                    if host not in ("github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com"):
                        raise Refusal("download-redirect-refused") from None
                    url, token = next_url, None
                    continue
                code = error.code
                error.close()
                raise Refusal(f"github-http-{code}") from None
            except (OSError, TimeoutError):
                raise Refusal("transport-uncertain-retry") from None
            with response:
                if response.status not in (200, 201, 204):
                    raise Refusal("unexpected-http-status")
                body = response.read(limit + 1)
                if len(body) > limit:
                    raise Refusal("response-too-large")
                return body
        raise Refusal("redirect-limit")


class GitHubRelease:
    def __init__(self, repository, tag, token, transport):
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
            raise Refusal("invalid-repository")
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,127}", tag):
            raise Refusal("invalid-tag")
        if not token:
            raise Refusal("missing-repository-token")
        self.api = f"https://api.github.com/repos/{repository}"
        self.public = f"https://github.com/{repository}/releases/download/{tag}/"
        self.transport, self.token = transport, token
        release = self.json(self.api + "/releases/tags/" + tag)
        if release["draft"]:
            raise Refusal("public-release-required")
        self.release_id = release["id"]
        # Never create/edit a release or change make_latest. This target must
        # have been deliberately provisioned and its sole-writer policy set.
        self.upload = f"https://uploads.github.com/repos/{repository}/releases/{self.release_id}/assets"

    def json(self, url, *, method="GET", payload=None):
        data = None if payload is None else json.dumps(payload).encode()
        return json.loads(self.transport.request(url, method=method, data=data, token=self.token))

    def list_assets(self):
        assets = []
        for page in range(1, 21):
            batch = self.json(f"{self.api}/releases/{self.release_id}/assets?per_page=100&page={page}")
            if not isinstance(batch, list):
                raise Refusal("invalid-asset-list")
            assets.extend(batch)
            if len(batch) < 100:
                return assets
        raise Refusal("asset-retention-review-required")

    @staticmethod
    def named(assets, name):
        matches = [asset for asset in assets if asset["name"] == name]
        if len(matches) > 1:
            raise Refusal("duplicate-asset-name")
        return matches[0] if matches else None

    def download(self, asset):
        if asset.get("state") != "uploaded" or asset.get("size", LIMIT + 1) > LIMIT:
            raise Refusal("asset-not-readable")
        # IDs are immutable, so a renamed asset is read through its API identity,
        # not an old cached release filename. Redirects strip credentials.
        return self.transport.request(f"{self.api}/releases/assets/{int(asset['id'])}",
                                      token=self.token, accept="application/octet-stream", redirects=True)

    def ensure_asset(self, name, data):
        asset = self.named(self.list_assets(), name)
        if asset is None:
            result = self.transport.request(self.upload + "?name=" + urllib.parse.quote(name, safe=""),
                                            method="POST", data=data, token=self.token)
            asset = json.loads(result)
        if self.download(asset) != data:
            raise Refusal("staged-asset-mismatch")
        return asset

    def rename(self, asset, name):
        self.json(f"{self.api}/releases/assets/{int(asset['id'])}", method="PATCH", payload={"name": name})

    def public_readback(self, expected):
        for attempt in range(4):
            try:
                data = self.transport.request(self.public + "catalogue.json?_arke_check=" + str(time.time_ns()), accept="application/octet-stream", redirects=True)
                if data == expected.data:
                    return
            except Refusal:
                pass
            if attempt < 3:
                time.sleep(5)
        raise Refusal("public-readback-pending-retry")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--canonical-url", required=True)
    parser.add_argument("--baseline-serial", required=True, type=int)
    parser.add_argument("--baseline-sha256", required=True)
    parser.add_argument("--evidence", required=True)
    args = parser.parse_args()
    # CLI is deliberately Actions-only: a local shell is not a global lock.
    if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("ARKE_MIRROR_SINGLE_WRITER") != "true":
        raise Refusal("serialized-actions-writer-required")
    if args.repository != os.environ.get("GITHUB_REPOSITORY"):
        raise Refusal("cross-repository-write-refused")
    if not re.fullmatch(r"[a-f0-9]{64}", args.baseline_sha256) or not 0 <= args.baseline_serial < 9007199254740991:
        raise Refusal("qualified-baseline-required")
    url = urllib.parse.urlsplit(args.canonical_url)
    if url.query or not re.fullmatch(r"/publishing/channels/[a-z0-9][a-z0-9-]{0,79}/catalogue\.json", url.path):
        raise Refusal("canonical-channel-url-required")
    transport = Transport()
    source = lambda: transport.request(args.canonical_url, accept="application/json")
    remote = GitHubRelease(args.repository, args.tag, os.environ.get("GH_TOKEN"), transport)
    result = reconcile(remote, source, args.baseline_serial, args.baseline_sha256)
    result.update({"repository": args.repository, "tag": args.tag,
                   "runId": os.environ.get("GITHUB_RUN_ID"), "sourceCommit": os.environ.get("GITHUB_SHA")})
    with open(args.evidence, "w", encoding="utf-8") as evidence:
        json.dump(result, evidence, indent=2)
    print(json.dumps(result))


if __name__ == "__main__":
    try:
        main()
    except Refusal as error:
        print(json.dumps({"status": "held-or-retry-required", "code": str(error)}), file=sys.stderr)
        sys.exit(1)
