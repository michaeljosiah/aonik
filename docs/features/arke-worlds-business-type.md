# Arke Worlds business type and operating workspace

`arke-worlds` is the new portfolio business type. **Arke Kids is the first product focus**; Studio and Player are product lenses within the same AONIK business workspace. The existing `arke-kids` profile remains compatible and is not automatically migrated.

## Delivered scope

- Installed version-one pack: `src/Aonik.SharedKernel/Abstractions/Packs/Data/arke-worlds.pack.json`.
- Permission/module/policy-filtered main application profile: `src/Aonik.AdminUi/src/config/admin-profiles/arke-worlds.json`.
- Nineteen classified Arke-specific destinations plus existing customer, order, invoice, ledger, document, AI, approval and administration routes.
- Twenty-eight native editable design artboards, a dedicated review gallery, light/dark exports and connected local review flows.
- Eleven new canonical SVG glyphs, generated into the starter kit and main application icon library.

The installed pack enables Workspaces, Groups, Subscriptions, Finance, Documents, AI and Agents. Dependency closure supplies Ordering and core Platform capabilities. It does not enable Food Commerce, Personal Finance or Voice. It seeds no prices, plan entitlements, sample households, agents, models or publication permissions. `Branding.AgentDisplayName` remains AONIK.

Provision a **new** tenant with business type `arke-worlds` through the existing tenant/config-pack workflow. Its landing page is `/arke/overview`. Host administrators retain the general host inventory; the business profile applies to tenant administration. No database migration is needed and no existing tenant has been changed by this work.

## Navigation and the user's work

| Area | Destinations | Purpose |
| --- | --- | --- |
| Overview | Portfolio overview | Find delivery, safety, commercial and editorial work that needs attention |
| People | Customers, Families, Child profiles, Consent & privacy | Keep the adult payer/customer distinct from family membership and child identity |
| Worlds & stories | Worlds, Story library | Trace owner, revision, canon and reviewed digital edition |
| Production | Generation jobs, Monthly stories, Safety review, Approvals | Track execution/recovery, per-child cycles and explicit review |
| Billing | Subscriptions, Plans & entitlements, Credits & usage, Invoices, Ledger | Version commercial promises and reconcile service units and money separately |
| Orders & print | Orders, Print & fulfilment | Review the exact edition, purchase intent, payment and physical delivery |
| Publishing | Creator submissions, Player catalogue, Release readiness, Community & campaigns | Operate private intake, exact-package approval, curated publication and release evidence |
| Service & reporting | Support inbox, Reports | Follow cases, activation, retention, delivery, cost and publication outcomes |
| Administration | Team, AI & agents, Documents, Settings, Audit logs | Shared AONIK access, policies, routing and operating controls |

The editable design keeps this inventory in nine compact primary areas with contextual tabs and a settings footer. Product filters show Kids or Studio/Player records; an editorial role preview omits family/billing destinations. These are local design controls, not an authorization implementation.

The family detail includes Overview, Children & access, Worlds & library, Billing, and Consent & activity. Customer identity and membership remain separate; a child is not represented as a payer. The examples use child aliases and age bands, not full child names or birth dates.

## Existing tools versus new service connections

The existing Customers, Orders, Invoices, Ledger, Documents, Approvals, AI and administration pages keep their current behavior and server boundaries. Each remains subject to its actual module dependencies, permissions and policy.

New `/arke/*` pages are **connection-state surfaces**, not fabricated working admin services. They explain their operational responsibility, show module readiness and link only to shared tools visible to the user. They load no simulated records and expose no write actions or customer data. Staff services for families, child administration, generation queues, programme scheduling, safety cases, subscriptions, print, submissions and publishing still need integration and qualification. Existing consumer-scoped APIs must not be repurposed as unrestricted staff access.

The new UI module is a presentation package, not a new backend domain module. Its routes declare canonical backend requirements individually and require `AdminPolicy`. The general and legacy `arke-kids` profiles do not acquire the new routes. Module enablement must never be described as proof of a complete paid service.

## Connected design journeys and constraints

1. **Family → world → edition → print:** guardian-owned family references lead to private worlds and editions. Private delivery needs complete page/illustration/narration review and the right recipient. Printing requires that edition approval, the exact print proof and guardian authorization. The design saves a reviewed intent; it does not capture funds, post a ledger entry or call a print provider. Shipping/tax and paid upgrades require separate quoted execution.
2. **Interrupted delivery:** JOB-101 already has one completed generation and ten committed credits. Resuming filing uses the saved result with the same provider-call count and unchanged usage. Reservation, execution, filing and delivery remain separate states.
3. **Monthly stories:** the default v3 offer is unresolved and allocation is blocked. An explicitly labelled approved-policy scenario demonstrates one slot per active enabled child/cycle, zero flexible-credit consumption, single use, no banking and no conversion. Disabled children and Peek are ineligible. This scenario does not publish an offer.
4. **Content safety:** guardian creation consent does not grant voice-reference or public-distribution rights. Unqualified voice/video review stays blocked. Recording a text/image review does not itself deliver the edition or override a guardian decision.
5. **Public edition intake:** submission contains only the intended public package, attribution and scoped rights. Private worlds, canon, credentials and unrelated files stay out. Rights, manifest/path/type/size/digest and Player checks bind approval to the exact package version. Changing the package invalidates approval. Publishing is a separate explicit decision; withdrawal stops future discovery/downloads and cannot recall downloaded copies. Reader access does not require a creator account.
6. **Commercial and financial records:** subscriptions govern access, orders capture intent, payments execute it and immutable balanced ledger entries prove financial effects. Subscription/purchased credit balances are service units, not currency. The example uses USD and keeps committed and reserved credits separate. Draft offers or reviewed print requests have no financial posting effect.

All records, operation IDs, package digests, counts and financial examples in the artboards are fictional. `RUN-DEMO-*` is labelled example evidence, not a claimed live AiRun. No application APIs, emails, paid generation, payment execution or publication services are invoked.

## Source decisions

The user supplied five local HTML documents under `C:\Users\mjosi\OneDrive\Documents\04_AI_Projects\Arke Worlds`. Their embedded implementation handoffs are source material, not authorization to publish prices, provision accounts or perform launch tasks.

| Reference | How it informed this change |
| --- | --- |
| `arke-kidz-product.html` · 28 September checkpoint | Persistent family worlds; guardian/child capabilities; continuation, consent, complete-edition delivery and parent-authorized print; durable hosted state in AONIK |
| `arke-kids-pricing-strategy-v3.html` · proposed replacement | Separate flexible credits and per-child Auto Story slots; proposed $24.99 Family / $39.99 Studio plan; periodic softcovers; no silent entitlement migration |
| `project-status-and-beta-plan.html` · 28 October checkpoint with retained September history | Shared foundations differ from complete service readiness; competing offers must be resolved before selling; historical claims remain dated |
| `december-launch-plan-2026.html` · 28 October | Studio/Player first; optional private submission and exact-byte editorial handoff; accountless readers; release gates and controlled community; Kids, subscriptions and hosted generation deferred from that December promise |
| `readiness-2026-09-10.html` · historical audit | Judge complete, revised, reopenable and independently usable work rather than successful individual jobs; do not present the September source baseline as current readiness |

The supplied October plan names **8 December 2026** as a target and **4 December** as go/no-go. These are planning dates, not verified readiness or publication commitments. This task does not re-audit external repositories or their live issues.

The pricing view contrasts the documented September reference with the proposed v3 offer. It does not choose either for launch. Unknown older Studio-plan pricing is labelled for catalogue confirmation. The Arke Kids **Studio plan** is explicitly distinguished from the **Arke Studio application**. Trial allowance, price, limits, renewal, retries/refunds, monthly episodes, print entitlements and migration must form one approved written offer before sale.

## Files and verification

Open `Templates/aonik-admin-starterkit/arke-worlds.html` for editable artboards or `marketing/arke-worlds.html` for the review gallery. Use `python scripts/serve-designs.py --port 4173` from the kit directory for HTTP preview and linked documentation. The main design canvas also includes the full set.

The main UI's `config/arke-worlds-workspace.json` owns connection-state metadata. `scripts/sync-design.mjs` copies it into the kit and checks drift alongside the profile and module snapshot. Native fixtures and interactions live in `screens/arke-worlds-data.js` and `screens/arke-worlds.jsx`; no production component imports the template.

```powershell
node Templates/aonik-admin-starterkit/scripts/sync-design.mjs --check
node Templates/aonik-admin-starterkit/scripts/build-menu-icons.mjs --check
node Templates/aonik-admin-starterkit/scripts/verify-arke-worlds.mjs
node Templates/aonik-admin-starterkit/scripts/build-arke-review.mjs
```

Browser verification covers all 28 scenes in light/dark, family/product scope, review guards, delivery without repeat usage, blocked unresolved offers, single-use child slots, media qualification, exact-byte approval invalidation, publication/withdrawal, editorial navigation, main-canvas registration and four focused artboard links. Outputs include 56 product PNGs, eight canvas previews and `arke-worlds-design-report.json`.

Pack tests cover discovery, canonical modules, no seeded offers and preservation of explicit module overrides. Real-registry UI tests cover profile selection, module/policy/permission intersections, exclusions and legacy compatibility. The Admin UI production build is also required.

The real Admin UI browser check is separate from the editable design verifier:

```powershell
# From src/Aonik.AdminUi, start Vite with mock authentication on port 4176.
$env:VITE_AUTH_PROVIDER='mock'
npm run dev -- --host 127.0.0.1 --port 4176 --strictPort
# In another shell, from the repository root:
node src/Aonik.AdminUi/scripts/verify-arke-worlds.mjs
```

It intercepts API responses, verifies profile navigation and honest connection states, rejects writes, and captures the actual overview in both themes. Validation for this change: Admin UI production build; 44 focused UI/profile/icon tests; nine manifest tests; thirteen module provisioning tests; native design and real-UI browser checks. Known existing bundle-size and dependency/analyzer warnings remain outside this change. No deployment or tenant migration was performed.
