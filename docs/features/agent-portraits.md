# Shared CGI agent portraits

AONIK has a shared set of sixteen fictional, human-like CGI headshots. The style uses centered head-and-shoulders framing, soft studio light, a neutral background, charcoal/ivory clothing and a teal fabric accent. Names and Agent labels remain visible; the portraits represent software agents, not real employees.

The portraits were generated individually with the **built-in image_gen tool** on 2 November 2026. The exact final prompt set is preserved in `Templates/aonik-admin-starterkit/assets/agent-portraits/prompts.json`. Original PNGs are retained without code-based resizing, retouching or compositing.

## Inventory

| Identity | Coverage |
| --- | --- |
| AONIK | Main assistant / `master-orchestrator`; mixed Black and Chinese woman in her thirties |
| Finance Agent | `finance-agent` |
| Platform Agent | `platform-agent` |
| Commerce Agent | `commerce-agent` |
| Personal Finance Agent | `personal-finance-agent` |
| Classification Agent | `pf-classify` |
| Forecast Agent | `pf-forecast` |
| Insights Agent | `pf-insights`; also the starter-kit Insights persona |
| Financial Life Graph Agent | `financial-life-graph-agent` |
| Compass Planner Agent | `pf-compass-planner` |
| Ledger Agent | Starter-kit design persona |
| Billing Agent | Starter-kit design persona |
| FX Agent | Starter-kit design persona |
| Compliance Agent | Starter-kit design persona |
| Close Agent | Starter-kit design persona |
| Dunning Agent | Starter-kit design persona |

The six design personas are not newly registered runtime agents. The gallery labels them separately. The nine current domain descriptors and the orchestrator all have mappings, while the eight starter-kit roster entries reuse the same identities. Personal Finance is the visible English label; legacy aliases are only lookup compatibility.

## Assets and consumers

- Canonical PNGs, catalog, prompts and dimension/hash manifest: `Templates/aonik-admin-starterkit/assets/agent-portraits/`.
- Browser resolver: `Templates/aonik-admin-starterkit/assets/agent-portraits.js`.
- Application copies: `src/Aonik.AdminUi/public/images/agents/cgi-<identity>.png`.
- Application resolver: `src/Aonik.AdminUi/src/lib/agentPortraits.generated.ts`.
- Download/size-preview gallery: `Templates/aonik-admin-starterkit/agent-portraits.html`.

The native starter-kit roster, detail/topology portraits, and editable chat scenes use the shared catalog. The application's `AgentPortrait` and `AiAgentSelector` use the generated React mapping. SVG `<image>` rendering preserves compatibility with portraits inside topology `<g>` elements. Glyph swatches explicitly use `forceGlyph`; unknown/custom agent names retain the existing geometric fallback. Aliases match normalized full names, not broad keyword guesses.

The dedicated AONIK portrait is `aonik.png`. Its exact built-in generation prompt is preserved in `aonik-prompt.json`. Main-assistant aliases (including `master-orchestrator` and `orch`) resolve to this portrait. The earlier `orchestrator.png` concept and its original prompt are retained as unused source material.

Live application chat carries the resolved server agent's name on `TEXT_MESSAGE_START`. The same identity is stored on the assistant message and retained through thread hydration, history reconstruction, and AG-UI snapshots. `ChatMessageList`, the slide-out chat header and full chat navigation use shared portraits with visible AI labels. Delegated tool calls do not imply a separate message author. Legacy messages without an author keep a generic agent fallback.

`AgentAvatar` is the shared application primitive; `AonikCommentary` presents the main assistant's page commentary. My Space uses it for the existing source-backed counts and freshness summary. This deterministic workspace brief does not introduce a new generated narrative or model call. Proposal cards use their recorded agent name before domain fallback.

Agent notifications use an explicit `agentName` string in the existing `MetadataJson` object, for example `{"agentName":"finance-agent","proposalId":"…"}`, supplied by a notification producer with `Source = "Agent"`. Existing notification contracts already persist and stream this metadata; no schema change is needed. The UI does not infer identity from titles, body text, AiRunId or URLs. Unknown or missing authors use a generic agent icon; system notifications ignore agent-name metadata. Severity and unread state remain visible and notification actions are unchanged. This work does not create new notification producers.

Matching editable My Space and Notification Center artboards are in the starter kit's main canvas; the marketing gallery includes both light and dark captures.

The existing agent icon-picker manifest includes the new public PNGs after a Vite build. No agent definitions, permissions, policies, model routes or database records are changed by these presentation assets.

## Refresh and verification

```powershell
node Templates/aonik-admin-starterkit/scripts/build-agent-portraits.mjs
node Templates/aonik-admin-starterkit/scripts/build-agent-portraits.mjs --check
node Templates/aonik-admin-starterkit/scripts/verify-agent-portraits.mjs
node Templates/aonik-admin-starterkit/scripts/verify-chat-artboards.mjs
```

From `src/Aonik.AdminUi`, the first two commands also have `npm run sync:agent-portraits` and `npm run check:agent-portraits` aliases. The generator copies originals byte-for-byte, emits both resolvers, validates square PNGs and detects missing runtime/design mappings or duplicate aliases. Check mode verifies hashes and generated outputs without modifying them.

For a new agent, generate a distinct matching headshot, save its original PNG beside the other canonical assets, add its stable ID/name aliases to `catalog.json`, preserve its prompt in `prompts.json`, then regenerate. Do not assign an existing identity to an unrelated agent through a substring match.

The portrait browser verifier checks all sixteen original downloads, runtime/design filters, small avatar previews, light/dark layouts, the starter-kit roster and profile rendering, and absence of application API calls. It exports contact sheets and roster/detail screenshots into `marketing/screenshots/` with `agent-portraits-report.json`. The chat verifier refreshes all chat previews with the generated faces and checks explicit Agent labels and the three domain identities in the collaborative room.

The portrait verifier also captures and checks AONIK dashboard commentary and agent notification artboards. To verify real shared application components with fictional props, start Vite with mock auth on port 4175 and run `node scripts/verify-agent-identity.mjs` from `src/Aonik.AdminUi`. It verifies decoded images, explicit authors, system/legacy fallbacks, severity, and the mark-read callback, without sending application API requests. Its two runtime captures and JSON report are written to the same screenshots folder.
