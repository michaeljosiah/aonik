---
name: fix-flutter-locked-file
description: Fix Payabo mobile Flutter build failures on Windows caused by locked files that cannot be moved to the build output directory. Copies the source asset to the safe intermediate target locations.
argument-hint: <filename or path from error message>
allowed-tools: Bash(cp *) Bash(rm *) Bash(find *) Bash(ls *) Bash(mkdir *)
---

# Fix Flutter Locked File Build Failure (Windows)

On Windows, Flutter builds sometimes fail because an asset file (e.g. a `.wav`, `.png`, or other asset) is locked by another process and cannot be moved to the build intermediates directory. The fix is to manually copy the source file to the intermediate target locations where the build expects raw (uncompressed) assets.

## Instructions

1. **Identify the locked file** from the error message or from `$ARGUMENTS`. Extract the filename (e.g. `simi_thinking_loop.wav`).

2. **Find the source file** under the Payabo assets directory:
   ```bash
   find apps/payabo_mobile/assets -name "<filename>" 2>/dev/null
   ```

3. **Find all build target locations** that expect this file:
   ```bash
   find apps/payabo_mobile/build -name "<filename>" 2>/dev/null
   ```

4. **Copy the source file to every SAFE target**, creating parent directories as needed:
   ```bash
   mkdir -p "<target_dir>" && cp "<source_path>" "<target_path>"
   ```

   The safe intermediate paths to copy raw assets into are:
   - `build/app/intermediates/flutter/debug/flutter_assets/assets/<subdir>/<filename>`
   - `build/app/intermediates/flutter/release/flutter_assets/assets/<subdir>/<filename>`
   - `build/app/intermediates/assets/debug/mergeDebugAssets/flutter_assets/assets/<subdir>/<filename>`
   - `build/app/intermediates/assets/release/mergeReleaseAssets/flutter_assets/assets/<subdir>/<filename>`
   - `build/unit_test_assets/assets/<subdir>/<filename>`

   Where `<subdir>` matches the source directory structure (e.g. `audio/`, `images/`).

   **NEVER copy raw assets into `compressed_assets` directories.** Those paths
   (`compressDebugAssets/out/`, `compressReleaseAssets/out/`) contain Gradle-compressed
   archives. Placing a raw file there causes `PackageAndroidArtifact` to fail with
   "Could not find EOCD" because it expects a zip-like format. Gradle regenerates
   compressed assets automatically from the merge intermediates above. If a stale raw
   file already exists in a `compressed_assets` path, **delete it**:
   ```bash
   rm -f "apps/payabo_mobile/build/app/intermediates/compressed_assets/.../simi_thinking_loop.wav"
   ```

5. **Retry the Flutter build** by running the same build command that failed.

6. Report which file was copied and to how many target locations.

## Important

- Always copy from the canonical source under `apps/payabo_mobile/assets/`, never between build directories.
- The working directory for all commands is the repo root: `C:\Users\mjosi\source\repos\aonik`.
- If the file is not an asset (e.g. a generated Dart file or Gradle artifact), investigate what process holds the lock instead — `handle.exe` or Task Manager can help identify it.
