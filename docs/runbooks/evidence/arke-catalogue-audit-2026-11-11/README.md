# Serial-54 follow-up audit

The live catalogue advanced during the handoff checks. Player PR #78 had already
published the 11 November Na Love or Juju world and guide. This directory freezes
that input without replacing the historical serial-53 evidence.

The adjacent `arke-catalogue-baseline-2026-11-11.json` records all 31 pin hashes,
13 packages and 1,833 embedded asset checks. Only the world and guide pins changed.
Both were freshly downloaded anonymously; the other 29 references were rehashed
from the previous verified cache. GitHub size/digest metadata agrees for both new
assets. The final hosted bytes match the current Player's bundled serial 54.

Use the previous audit's `download.py` and reproduction instructions in
`../arke-catalogue-audit-2026-11-10/README.md`, but use this frozen catalogue and
`verify.dart`. The current-reader checkout is
`d47c39492afad805974050120c64fee1cf716f8f`. The unchanged previous audit executable,
compiled against `bc2358a3baef408681397ac1baca87608b93f07c`, independently passed
the same complete input. Both reports are retained in the consolidated evidence.
This checks reader/asset integrity, not native decoding of every media file.

The largest package is 1,118,528,131 bytes with 1,165 assets; its largest asset is
47,926,047 bytes. These dimensions remain within the separately qualified
`large-v1` envelope. The public packages were not admitted to AONIK or approved for
distribution by this audit. The new ownership projection keeps Party and private
rights-reference fields null. Re-audit any higher live serial before cutover;
never restore the earlier serial 53 as the production head.
