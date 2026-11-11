import copy
import json
import unittest
import io
import urllib.error

from mirror import Catalogue, GitHubRelease, Refusal, Transport, reconcile


def feed(serial, marker="test"):
    return json.dumps({"version": 2, "serial": serial, "worlds": [], "band": marker}).encode()


class Release:
    named = staticmethod(GitHubRelease.named)

    def __init__(self, current=None):
        self.assets = []
        self.bytes = {}
        self.fail = None
        self.renames = 0
        self.readback_fails = False
        if current:
            self.ensure_asset("catalogue.json", current)

    def list_assets(self):
        return copy.deepcopy(self.assets)

    def download(self, asset):
        return self.bytes[asset["id"]]

    def ensure_asset(self, name, data):
        old = self.named(self.assets, name)
        if old:
            if self.download(old) != data:
                raise Refusal("staged-asset-mismatch")
            return old
        asset = {"name": name, "id": len(self.assets) + 1}
        self.assets.append(asset)
        self.bytes[asset["id"]] = data
        return copy.deepcopy(asset)

    def rename(self, asset, name):
        if self.named(self.assets, name):
            raise Refusal("name-conflict")
        original = next(x for x in self.assets if x["id"] == asset["id"])
        original["name"] = name
        self.renames += 1
        if self.fail == self.renames:
            raise Refusal("lost-rename-response")

    def public_readback(self, expected):
        current = self.named(self.assets, "catalogue.json")
        if self.readback_fails or current is None or self.download(current) != expected.data:
            raise Refusal("public-readback-pending-retry")


class MirrorTests(unittest.TestCase):
    def run_mirror(self, release, data, baseline=None):
        floor = Catalogue.read(baseline or feed(10))
        return reconcile(release, lambda: data, floor.serial, floor.sha256)

    def test_promotes_exact_bytes_retaining_fence_and_old_copy(self):
        release = Release(feed(10))
        result = self.run_mirror(release, feed(11))
        self.assertEqual(result["serial"], 11)
        self.assertEqual(len(release.assets), 3)
        self.assertIn(feed(10), release.bytes.values())
        self.assertEqual(release.renames, 2)

    def test_replay_never_duplicates_or_regresses(self):
        release = Release(feed(10))
        self.run_mirror(release, feed(11))
        result = self.run_mirror(release, feed(11))
        self.assertFalse(result["promoted"])
        self.assertEqual(release.renames, 2)
        with self.assertRaisesRegex(Refusal, "remote-ahead"):
            self.run_mirror(release, feed(10))

    def test_unknown_outcomes_at_both_rename_steps_recover(self):
        for point in (1, 2):
            with self.subTest(point=point):
                release = Release(feed(10))
                release.fail = point
                with self.assertRaisesRegex(Refusal, "lost-rename-response"):
                    self.run_mirror(release, feed(11))
                self.assertIn(feed(10), release.bytes.values())
                release.fail = None
                self.run_mirror(release, feed(11))
                release.public_readback(Catalogue.read(feed(11)))
                self.assertEqual(len(release.assets), 3)

    def test_missing_live_feed_does_not_erase_serial_fence(self):
        release = Release(feed(10))
        release.fail = 1
        with self.assertRaises(Refusal):
            self.run_mirror(release, feed(12))
        release.fail = None
        with self.assertRaisesRegex(Refusal, "remote-ahead"):
            self.run_mirror(release, feed(11))
        self.assertEqual(release.renames, 1)
        self.run_mirror(release, feed(12))

    def test_newer_head_recovers_missing_feed_without_lower_restore(self):
        release = Release(feed(10))
        release.fail = 1
        with self.assertRaises(Refusal):
            self.run_mirror(release, feed(11))
        release.fail = None
        self.run_mirror(release, feed(12))
        release.public_readback(Catalogue.read(feed(12)))

    def test_same_serial_conflict_is_held(self):
        release = Release(feed(11, "other"))
        with self.assertRaisesRegex(Refusal, "same-serial-conflict"):
            self.run_mirror(release, feed(11))
        self.assertEqual(release.renames, 0)

    def test_wrong_baseline_refused_before_any_write(self):
        release = Release(feed(10, "different"))
        with self.assertRaisesRegex(Refusal, "same-serial-conflict"):
            self.run_mirror(release, feed(11))
        self.assertEqual(len(release.assets), 1)

    def test_canonical_changes_before_swap_keep_old_feed(self):
        release = Release(feed(10))
        sources = iter((feed(11), feed(12)))
        with self.assertRaisesRegex(Refusal, "canonical-head-changed-retry"):
            floor = Catalogue.read(feed(10))
            reconcile(release, lambda: next(sources), 10, floor.sha256)
        self.assertEqual(release.renames, 0)
        self.run_mirror(release, feed(12))

    def test_failed_readback_does_not_roll_back_and_retry_verifies(self):
        release = Release(feed(10))
        release.readback_fails = True
        with self.assertRaisesRegex(Refusal, "public-readback-pending"):
            self.run_mirror(release, feed(11))
        release.readback_fails = False
        self.run_mirror(release, feed(11))
        self.assertEqual(release.renames, 2)

    def test_staged_bytes_and_serial_fence_are_verified(self):
        release = Release(feed(10))
        asset = release.ensure_asset(Catalogue.read(feed(11)).snapshot_name, feed(10))
        with self.assertRaisesRegex(Refusal, "invalid-serial-fence"):
            self.run_mirror(release, feed(11))
        self.assertEqual(release.renames, 0)

    def test_conflicting_fences_refused_even_when_live_asset_missing(self):
        release = Release()
        for marker in ("one", "two"):
            head = Catalogue.read(feed(11, marker))
            release.ensure_asset(head.snapshot_name, head.data)
        with self.assertRaisesRegex(Refusal, "conflicting-serial-fences"):
            self.run_mirror(release, feed(12))

    def test_invalid_or_oversized_catalogue_refused(self):
        for data in (b"{}", b"null", feed(True), feed(-1), feed(9007199254740991), b" " * 1048577):
            with self.subTest(data=data[:40]), self.assertRaises(Refusal):
                self.run_mirror(Release(feed(10)), data)

    def test_external_writer_change_detected_before_rename(self):
        release = Release(feed(10))
        calls = 0
        def source():
            nonlocal calls
            calls += 1
            if calls == 2:
                release.bytes[1] = feed(12)
            return feed(11)
        with self.assertRaisesRegex(Refusal, "external-writer-detected"):
            reconcile(release, source, 10, Catalogue.read(feed(10)).sha256)
        self.assertEqual(release.renames, 0)


class TransportTests(unittest.TestCase):
    def test_redirect_strips_api_token(self):
        transport = Transport()
        requests = []
        class Response(io.BytesIO):
            status = 200
        class Opener:
            def open(self, request, timeout):
                requests.append(request)
                if len(requests) == 1:
                    raise urllib.error.HTTPError(request.full_url, 302, "redirect", {
                        "Location": "https://release-assets.githubusercontent.com/asset?signature=test"}, None)
                return Response(b"verified bytes")
        transport.opener = Opener()
        result = transport.request("https://api.github.com/repos/test/repo/releases/assets/1", token="test-secret", redirects=True)
        self.assertEqual(result, b"verified bytes")
        self.assertEqual(requests[0].get_header("Authorization"), "Bearer test-secret")
        self.assertIsNone(requests[1].get_header("Authorization"))

    def test_untrusted_credential_destination_refused_before_request(self):
        transport = Transport()
        with self.assertRaisesRegex(Refusal, "credential-destination-refused"):
            transport.request("https://example.test/", token="test-secret")

    def test_redirect_cannot_escape_github_download_storage(self):
        transport = Transport()
        class Opener:
            def open(self, request, timeout):
                raise urllib.error.HTTPError(request.full_url, 302, "redirect", {"Location": "https://example.test/private"}, None)
        transport.opener = Opener()
        with self.assertRaisesRegex(Refusal, "download-redirect-refused"):
            transport.request("https://github.com/test/repo/releases/download/test/catalogue.json", redirects=True)


if __name__ == "__main__":
    unittest.main()
