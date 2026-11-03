# Chat interface design references

The starter kit contains six native, editable chat artboards in both the main canvas and `Templates/aonik-admin-starterkit/chat.html`. These are design prototypes with fictional data and local interactions for review and marketing. They do not call application APIs, models, messaging services or payment services.

## Surfaces

| Scene | Design |
| --- | --- |
| Slide-out chat | A right-side panel beside invoice INV-2048, with an evidence-backed agent response, page context, a one-time draft-invoice permission card and composer. |
| Permission cards | Expanded read scopes and the specific proposed invoice change, with request/run references, approve-once and decline states. |
| Collaborative chat | A full workspace with shared/private conversation navigation, a shared room, multiple named human and agent contributions, mentions, participant permissions, linked records and a payment proposal. |
| Chat notifications | Mixed human/agent inbox with dedicated AONIK and Finance portraits, unread status, mention/message labels, room context and open/read/dismiss actions. |
| Mentions inbox | A mentions-only inbox with read and unread examples; opening a notification marks it read and highlights its linked room message. |
| Human proposal review | Maria's Finance lead view, showing the exact amount, customer, order, funding intent, proposal/run references and explicit approval before queued execution. |

The original collaboration example room has Oliver Reed (Operations), Maria Gomez (Finance lead), Billing Agent, Ledger Agent and Compliance Agent. Human and Agent badges identify each contribution; agents use the shared CGI headshots, with domain SVGs retained for tools and linked records. Selecting a participant reveals their role or tool scope. Inviting Sam Chen demonstrates an additional human participant as an Observer.

## Approval and audience behavior

- Reading page context is identified separately from permission to change a record.
- The draft-invoice card represents a medium-risk, single-use approval bound to a specific request. Approve/decline resolves that local card; it cannot be applied twice.
- Payment capture appears as a durable proposal, with an order (business intent), payment intent (funding) and AI run reference. Oliver can view it but cannot approve it. Maria must confirm the exact proposal before approval. Rejection does not require the amount confirmation.
- Approval is shown as **Approved · queued**, with no claim that capture succeeded or a ledger posting exists. Existing ledger evidence remains a separate record. This follows Specs 030/032 and the platform rule that agents propose and systems execute.
- Private and shared messages use separate local state. Opening a team room requires an explicit audience confirmation and shares only the selected invoice reference. Private chat messages are not copied.
- The composer identifies its audience. Asking Billing Agent in a shared room remains visible to everyone in that room. Switching to a private conversation does not reveal room messages.
- Invites explain that a new participant can see the room's history; their workspace/domain permissions still apply.

The notification artboards use the runtime AONIK/Finance identities and human initials. The inbox filters All, Unread, Mentions and Chat. Local actions support read, dismiss, mark-all and opening the linked message without executing agent tools.

These are UX demonstrations, not an authorization boundary. Production permissions and mutation approval must continue to be enforced by the server; approving a visual card must never substitute for the server decision or proposal dispatcher.

## Source and previews

- Editable React source: `Templates/aonik-admin-starterkit/screens/ai-chat.jsx`.
- Scoped styles: `Templates/aonik-admin-starterkit/design-system/chat.css`.
- Canonical icons: `Templates/aonik-admin-starterkit/assets/menu-icons.js`.
- Focused canvas: `Templates/aonik-admin-starterkit/chat.html`.
- Main canvas section: **Chat · People & agents**. Existing `ai-chat-slideout` and `ai-chat-fullpage` artboard IDs are retained.
- Gallery: `Templates/aonik-admin-starterkit/marketing/index.html`.

The designs use production white/charcoal surfaces and teal accents. Coral is reserved for applying agent-proposed changes. The canvas retains focus, theme switching and editable labels; scene inventory and mock participants are defined in the JSX source.

Serve the starter-kit directory on port 4173, then open `http://127.0.0.1:4173/chat.html`. For a clean product preview, use `chat.html?capture=1&scene=collaboration&theme=light` (also `slideout`, `permissions`, `proposal-review`, `notifications`, `mentions`, and `theme=dark`).

Run from the repository root:

```powershell
node Templates/aonik-admin-starterkit/scripts/verify-chat-artboards.mjs
```

The script writes twelve clean light/dark marketing PNGs at 3200 × 2200, twelve canvas previews at 3200 × 2560, and `chat-design-report.json`. It checks author identities, shared icons, horizontal layout, one-time permissions, human approval roles, queued execution, invitation/recipient choice, private/shared message isolation, composition, search, both canvas registrations, notification filters/read/dismiss/message links, and absence of application API calls.

## Production implementation boundary

The application now implements shared rooms alongside private chat. Persisted room membership, human/agent authors, history-aware invitations, roles, mentions, presence, live updates, linked references and server-owned approval cards follow these layout references. Shared rooms use registered runtime agents; the starter-kit personas remain design examples. Existing requester-only approval policy is preserved. See [Collaborative chat rooms](collaborative-chat.md) for the production contracts, migration, scope and verification.

Shared headshot inventory, original assets and generation prompts are documented in [Shared CGI agent portraits](agent-portraits.md). The Actor/Agent labels and permission boundaries stay visible when portraits are shown.
