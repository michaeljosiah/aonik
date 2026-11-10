# Reproducing the serial-53 integrity audit

These are the exact read-only scripts and frozen catalogue used for the
10 November 2026 audit. They make no publishing, installed-library or cloud
configuration changes. The catalogue records already-public production URLs.
This check establishes byte integrity and Player compatibility, not rights
clearance or the capacity of AONIK's isolated verifier.

1. Check out ArkePlayer commit `bc2358a3baef408681397ac1baca87608b93f07c` and resolve
   its workspace with Flutter 3.41.3 / Dart 3.11.1 (`flutter pub get`).
2. Copy `hosted-catalogue.json`, `download.py` and `verify.dart` from this directory
   into an empty local audit directory. Keep downloads outside tracked folders;
   allow at least 2 GB free. Python 3 and Pillow are required for the image check.
3. Run `python <audit-directory>/download.py`. This reads the frozen input,
   anonymously fetches its exact HTTPS release URLs and stores files by SHA-256.
   It refuses excess length or hash mismatch and verifies picture decoding.
   On the original run every file was a fresh download. A rerun can reuse an
   exact cached file; only `freshDownload: true` independently demonstrates a
   network read in that run. The original public-pin report is retained in the
   adjacent consolidated evidence JSON.
4. Copy `verify.dart` into `build/baseline-audit/verify.dart` under that Player
   checkout (an ignored directory). From the Player workspace root, run:

   ```text
   dart run build/baseline-audit/verify.dart <absolute-audit-directory>
   ```

   It uses the workspace's actual `parseCatalogue`, `readWorldPackage`,
   `editionFingerprint`, `extractAsset` and `AssetCheck`, with no network or
   installed-library writes. It verifies all package structures and documents,
   exact outer and inner hashes, world/production IDs, edition identity and part
   continuity. Scratch extraction uses one fixed local filename, never an
   archive-supplied destination. A failed package is recorded and returns a
   nonzero exit code after the remaining packages are checked.
5. From `apps/arke_player`, run:

   ```text
   flutter test test/catalogue_test.dart test/catalogue_channel_test.dart test/catalogue_refresh_test.dart
   ```

   The audit run passed 29 tests. Normal development continues to select the
   isolated empty bundled baseline; production uses the existing public feed.
6. Compare a fresh anonymous hosted catalogue GET to the frozen input before
   claiming the audit describes the current feed. A higher serial needs a new
   audit; do not overwrite the retained evidence. The original run also matched
   every public pin against its GitHub release asset size and digest, recording
   release/asset IDs and timestamps. Those API observations and the final
   byte-identical readback are in the consolidated report.

The reported 13 archives, 18 artwork files and 1,812 embedded assets are counts
of verified references, not newly published works. The existing serial 53 had
already been merged and published through Player PR #72. This audit did not
raise the serial or upload a replacement. AONIK #428 tracks the separate
production-scale intake and verifier qualification.
