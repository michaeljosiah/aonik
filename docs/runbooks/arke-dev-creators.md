# Invited creators in dev

Scope: AONIK #436. Dev remains the delivery environment; this is not production activation.

## Access

In the Arke Kidz dev tenant, an authorised tenant administrator invites the creator's email
using Users → Invite. Leave additional roles empty. The default PersonalUser role and active
party membership permit creator submission; they do not grant editorial or release authority.
Use real private inboxes for actual creators. Mailinator is only the disposable acceptance fixture.

The invitation must contain an absolute URL to the dev admin site's `/signin` page. Configure
`UserLifecycle:AdminUiBaseUrl` with the dev admin origin. The URL carries the one-shot invitation
and tenant context. The creator chooses **Sign in or create account**, signs up with the invited
email if needed, verifies that email and returns to accept the invitation. Expired invitations
must be resent through the normal admin action. Never mark an email verified in the database.

For an unaccepted Auth0 invitation, AONIK checks the validated token's subject and the invited
mailbox against Auth0's authenticated `/userinfo` response before identity linking or local
authority is granted. This also covers registration that has already linked the subject. Missing,
unverified or mismatching mailbox evidence and expired invitations fail closed; tokens need not
carry a custom `email_verified` claim. Accepted existing users keep their normal sign-in path.

After acceptance, the creator signs into **Arke Studio Dev** using that same account. A user
without an invitation or active party membership must remain unable to submit. A creator must
not approve or publish their own submission.

## Declaration

Both the active channel and Studio profile use `dev-creator-v1`. The exact text is in
`iac/azure/environments/dev/arke-studio.large.json`, also embedded in Studio Dev. It covers
creator-owned or appropriately licensed work, required attribution/licence information,
private upload and review, and subsequent separately approved release through publicly
accessible dev catalogue/artifact URLs. Withdrawal does not recall downloaded/offline copies.
No production distribution is authorised.

Keep the standard and large profiles' declaration text/version identical. Historical receipts
retain their original version and recorded consent; this change does not approve any edition.
Existing installations must receive the updated profile/build before submitting a new revision.

## Builds

- Studio: **Package Studio Dev (Windows)** in `michaeljosiah/ArkeStudio`, artifact
  `ArkeStudioDev-Windows-x64`. It contains the Windows x64 installer, source commit and SHA-256.
  The app is named Arke Studio Dev, defaults to `%USERPROFILE%/ArkeStudioDev`, and uses
  `%APPDATA%/ArkeStudioDev`. It has no production updater. Install later Dev builds over it.
- Player: **Package Player Dev (Android)** in `michaeljosiah/ArkePlayer`, artifact
  `ArkePlayerDev-Android`. It contains a universal APK for phones/emulators, source commit and
  SHA-256. The stable test key signs `studio.arke.player.dev`; data and jobs are separate from
  production. Install updates over the existing Dev app without uninstalling or clearing data.

Earlier locally built rehearsal APKs may have the Android debug certificate instead of the
stable cohort key. Android correctly refuses to replace those packages. Keep their data intact
and use a separate device/emulator for the cohort build; do not uninstall them as an upgrade fix.
The distributable workflow records an increasing version code as well as its signing provenance.

Both builds include public dev configuration only. No passwords, tokens or client secrets are
distributed. Player requires no account to discover released works. GitHub Actions artifact
downloads require repository access and expire after 30 days; keep the delivered copies and
checksum files available to the invited cohort.

## Submission and review

1. In Studio Dev, create/open a world and export a frozen **Arke Player** edition.
2. Connect the invited Arke account. Select the export and submit with its metadata,
   attribution, licences and the displayed declaration. Track upload, verification and review
   using the submission receipt. An interrupted upload resumes against the same receipt.
3. In AONIK dev, the separately authorised reviewer inspects the verified package and exact
   revision digest, records the required checks, and approves or requests changes/rejects.
4. A separately authorised release action makes the approved edition available on
   `arke-worlds-dev`. Approval alone does not publish it.
5. Player Dev refreshes the catalogue, downloads and verifies the package, then reads/plays it.
   Exercise saved progress and cold offline access. Withdraw disposable acceptance editions
   after the rehearsal and verify they disappear from discovery while installed copies remain.

## Deployment and evidence

Build and deploy the tested API/Admin UI revision using immutable images. Preserve the live
dev channel enablement, reviewer/publisher IDs, qualified large-capacity settings, mounts and
secret references; the checked-in baseline parameter file intentionally stages a closed channel.
Update the declaration version on both API and Worker, and the admin URL on API. Keep the legacy
mirror disabled. Read back effective settings and health before sending a fresh invitation.

Record the installed build hashes, invitation/acceptance outcome, new creator/party IDs,
receipt/revision and archive digest, verification and review evidence, release/withdrawal serials,
and native Player progress/offline observations. Exclude invitation tokens, credentials, bearer
tokens and private Auth0 callback data. #436 remains open until this live acceptance is complete.

### 2026-11-12 acceptance — full dev flow passed

The tested application commit `eeaeee963ef629006e61cfd140c0a4f89e9aa97b` is deployed using
the immutable manifest from Actions run `43111938410`: API revision `0000393`, Worker `0000383`
and Admin UI `0000374`. All three became ready; the API health check and empty dev catalogue
returned 200. The declaration is `dev-creator-v1`, the 1.5 GiB limit is preserved, and the mirror
remains disabled. AONIK PR #437 is green and reviewed by Codex, but awaits the repository's
required approving review. Studio PRs #2989/#2991, Player PRs #79/#80/#81 and private specs
PRs #157/#159 merged. This checkpoint completes live technical acceptance; #436 remains open
until the final AONIK merge requirement is satisfied.

The disposable creator completed real Auth0 registration and mailbox verification. Its AONIK
user ID is `564b82bf-a7f1-4084-a7be-80ad9951c986`, party ID
`4f839a49-934e-4641-9326-93f8bd854a1e`, with only `PersonalUser`. The API correctly reported
`Invited` after identity linking. Resending succeeded and Mailinator displayed the fresh
invitation with the expected tenant, role and expiry. The creator switched from the existing
reviewer browser account, returned to the invitation, and explicitly accepted it. At 23:15 UTC
the API reported `Active` with only `PersonalUser`; the invitation token was removed from the
browser URL. Native Studio Dev sign-in then succeeded and retained its encrypted account locally.
Using that session, `/v1/me` and creator submissions returned 200; editorial queue and channel
management returned 403. No reviewer or publisher role was granted.

Installed artifacts (SHA-256):

- Windows Studio Dev 0.5.116, packaging run `43120088742`, source
  `ac975f3c27f71d94403fe37ec8da53df754774b2`:
  `4f363e079a6e8166f85e4292a9204871857d5d9dcec501941cd42a81b969a30e`.
  Installer exited 0 and retained the signed-in account. The installed build imported the
  original fixture, exported it and submitted the frozen edition through the native UI.
- Android Player Dev versionCode 10003, packaging run `43127092051`, source
  `546686f05fff0152a2ab1fa48202795f6ed17fef`:
  `22667e809f9c139a46666d1df13773561f7245bdaaac228069d9a469531e5492`.
  It installed over 10002 without uninstalling, retaining the book and existing saved checkpoint.
  Its stable CI signing certificate
  is `c161b145275983926c2de54f2f6392a22c12774d012db1614f300696da784475`.
  The earlier local-debug installation was preserved after Android refused a certificate mismatch.

The original synthetic **Creator Garden Dev 436 / The Seed Library** has three chapters
and geometric artwork. Native import exposed an existing bug: the empty `.commit/staging`
parent left by a completed save was mistaken for pending work. Studio PR #2991 and specs
PR #159 merged after seven import regressions and full CI passed. The corrected installed
build imported the untouched source, with the empty staging parent still present.

Native submission produced receipt `9dda5e26-d72f-4c70-9c41-189fd9ada802`, revision
`7dfa00ff-8595-41f7-b66a-3d6d8a4e8a02` (1), archive SHA-256
`f37af7340903cbbaf1b107cd49cd5bd7007505092fc4cffbf294ac153267c5e3`, 2,358,836 bytes.
AONIK verified the archive; the reviewer downloaded and checked the same server copy.
The creator's own review and publication endpoints returned 403. The owner-authorised
automated dev decision `85772277-d4d3-4abd-90de-da9e9e7358ae` approved review digest
`7ed155e5b5906d780907e6a6ae576663158c352c22c6514ef5e10f42cf89ed68`.
Approval alone left the catalogue empty at serial 26. Separate publication operation
`5ee8b6c1-abd2-4bba-9ece-7369faaac470` committed on its first delivery attempt, moving
the dev catalogue to serial 27. The public artifact also matched the archive digest.

Player Dev discovered, downloaded and opened the book. The offline restart test found a
reader defect: Home retained 67%, but the renderer had saved the preceding chapter's CFI.
Native traces identified a 0.95 CSS-pixel overlap at Android's 2.625 device scale. A second
defect hid the installed book-only world's poster behind audio-only chapter content. Player
PR #81 fixes both and merged as `cc148475f2dc4693268a2d019f89a289e3a8827d`.
Validation includes eight pagination boundary regressions, strict analysis, design validation,
440 passing app tests (13 existing fixture-dependent skips), and green production/dev builds.

The final signed APK retained the installed book across the 10002 → 10003 update, restored
its Story poster and opened its production page. A fresh chapter-two checkpoint was saved in
the corrected reader; reinstalling that same APK retained 67%, and a cold start with both
Wi-Fi and mobile data disabled opened **The Rainy Afternoon**, not the previous chapter.
Earlier incorrectly recorded CFIs are not reconstructed by the fix; this check explicitly
uses a newly saved checkpoint after the correction.

Withdrawal operation `9695d694-6b2a-4a23-8076-a08d8fc07f41` committed once at 00:36:38 UTC,
moving the public catalogue to empty serial **28**. Native Player's **Check for updates**
completed and Worlds discovery became empty. After another cold offline restart, the
installed copy still opened chapter two with its saved 67% progress. Studio's final receipt
refresh showed **Withdrawn · complete**. Device networking was restored and the temporary
reviewer helper was signed out; the invited creator remains available for further dev work.

These device checks ran on the isolated Android 16 emulator. The 2 GB diagnostic run hit
Android-wide memory/I/O pressure during a swipe; its data was preserved while the emulator
was restarted with 4 GB for the final signed-app acceptance. No physical-device claim is made.
The mirror remains disabled and production was unchanged.

The handoff contains the tested Windows installer, Android APK, build identities,
`SHA256SUMS.txt` and `START-HERE.txt`. The Windows and Android packaging runs above also
provide the repository-access-controlled artifacts. No account credentials, invitation tokens
or reviewer session data are included in either build or the handoff.
