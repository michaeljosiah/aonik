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
