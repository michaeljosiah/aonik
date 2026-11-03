# AI-assisted support design references

Four native editable artboards are available in `Templates/aonik-admin-starterkit/support.html` and the main canvas under **Support · Customer conversations**. They use the production neutral/teal semantic tokens, shared SVG icons, human initials and AONIK's approved CGI portrait.

| Artboard | Layout and examples |
| --- | --- |
| Email inbox | Application rail, support queues, connected product inboxes, case list, selected conversation and AONIK copilot. Fictional Remittance, Billing and Personal Finance enquiries show priority, assignment, response deadline and unread state. |
| Conversation | Full email thread, internal-only notes, assignment control, linked record evidence, reply composer and case summary. |
| Reply review | Editable draft, saved customer recipient, tone guidance, referenced sources, facts to check and explicit send confirmation. Funding received is distinguished from payout completion. |
| Escalations | Deadline-sorted review queue, receiving teams, existing handoffs, customer update commitments and a service-level summary. |

## AI and human decisions

AONIK offers a case brief and suggested topic, priority and receiving team. Opening a suggestion does not apply it. A human applies triage after reviewing the case classification. Source cards open inspectable fictional record, guide and conversation excerpts.

Each customer has a separate draft and internal-note history. Selecting another case never reuses Nadia's transfer narrative in another customer's reply. Internal notes are labelled team-only. The reply screen remains editable and blocks empty messages. Tone selection changes editorial guidance in this prototype; it does not call a model or silently rewrite the operator's edits.

The send dialog shows the exact recipient, subject and edited body. Its action requires confirmation of recipient, facts and the follow-up commitment. The resulting local state is **Queued for delivery**, with no claim of delivery. Merely opening or cancelling the dialog has no send effect. Operational handoffs similarly require review of evidence, team and the next customer update; the support operator retains ownership of communicating with the customer.

All metrics, inbox counts, people, emails, sources and case records are fictional design data. Summary counts illustrate a larger workspace; the inbox renders six example conversations. `.test` addresses are examples. No external email or invitation is sent.

## Production boundary

This turn adds design assets and local interactions only. There is no new Support module registration, mailbox connector, support API, email dispatch, AI execution or database migration. The live admin application's routes and business profiles do not gain a support inbox from these prototypes. The new canonical vectors are generated into the application's shared icon library for consistent future use.

An implementation must authorize tenant/staff/case access, bind customer contacts on the server, sanitize incoming HTML and attachments, deduplicate provider deliveries, preserve thread/message ownership, and keep internal notes separate from outbound email. Human-triggered sends require durable request identity and truthful queued/provider/delivery outcomes. Any agent-invoked mutation must be classified and governed by the existing server approval gate.

AI must resolve through AiRoutePolicy, record AiRuns and reference selected case IDs and authorized evidence. A support email is not permission to project a Personal Finance User Brief or unrelated private account data. Prompts must treat incoming messages as untrusted content.

Orders remain business intent; payment/payout services provide execution evidence; the ledger remains financial truth. A support status enquiry or handoff cannot authorize a payout retry, capture, refund, account-email mutation or ledger posting. Financial changes continue through their own permissions and durable proposal/approval workflows.

## Sources and verification

- Editable React: `Templates/aonik-admin-starterkit/screens/support.jsx`.
- Scoped styles: `Templates/aonik-admin-starterkit/design-system/support.css`.
- Focused canvas: `Templates/aonik-admin-starterkit/support.html`.
- Canonical icon additions: Support, Email, Inbox, Knowledge and Search in `assets/menu-icons.js`.
- Gallery: `Templates/aonik-admin-starterkit/marketing/index.html`.

Run from the repository root:

```powershell
node Templates/aonik-admin-starterkit/scripts/build-menu-icons.mjs --check
node Templates/aonik-admin-starterkit/scripts/verify-support-artboards.mjs
```

The browser verifier checks search/product filtering, per-customer draft isolation, reviewed triage, internal notes, empty-draft prevention, send/handoff confirmation and cancellation, queue wording, source evidence, shared assets, layout overflow and both canvas registrations. It asserts no application API calls or runtime exceptions. Twenty PNGs are exported: eight clean scene captures, four confirmation-state captures and eight focused-artboard previews. The machine-readable result is `marketing/screenshots/support-design-report.json`.

Serve the kit over HTTP as described in its README. A clean preview is `support.html?capture=1&scene=inbox&theme=light`; also use `conversation`, `reply-review`, `escalations` and `theme=dark`. Canvas annotations retain the host's existing focus, reorder and editable-label behavior.
