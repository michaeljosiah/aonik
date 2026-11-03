# Collaborative chat rooms

The Admin UI now provides persisted team conversations at **AI Chat > People & agents > Shared rooms** (`/ai/chat?view=rooms`). A room link includes `room=<id>` and is accessible only to current participants in the same tenant. The existing private chat and AG-UI endpoints continue to use owner-only threads.

## Workspace

The workspace follows the editable chat designs: room navigation, a named human/agent conversation, an audience-labelled composer, participants and their scopes, linked references, permission cards and a confirmation dialog. Agent portraits come from the canonical shared portrait catalog. Human authors use directory identities and initials. A mobile sheet exposes participants and linked context on narrower screens.

Staff can create a room with optional invoice, order, payment-intent, ledger-entry, proposal or customer references. Creation starts a fresh history; private threads and personal memory are not copied. References are labels, not a claim that a record exists or that the viewer has domain access. Domain tools must verify records and apply their normal permissions.

Human messages are saved before a selected agent is invoked. Structured mentions identify current participants. Selecting an agent asks it to respond to that saved message; mentions can also tag colleagues without invoking a model. The composer always identifies the shared audience. Messages, agent responses, approval states and membership changes refresh across visible clients every three seconds. Presence uses a persisted heartbeat every twenty seconds and a sixty-second online window; agents do not display fabricated human presence. Hidden tabs pause polling and resume when visible.

## Message and mention notifications

New human messages and persisted agent replies enqueue a reference-only `ChatRoomMessagePostedEvent` in the same transaction as the message. The existing **Worker outbox processor must run** to deliver these notifications. No schema change is required beyond the existing room and notification/outbox migrations.

The original audience is captured at commit. Delivery rechecks tenant, active room membership and active staff eligibility; late invitees do not receive historical alerts. Human authors are excluded from their own posts. Agent replies notify the room's human participants, including the requester. Observers can receive notifications. A structured human mention (`Mention someone` in the composer) takes priority over the ordinary message notification; typing a name in free text alone does not create a mention. Agents do not receive inbox notifications.

The notification writer passes a stable per-message idempotency key to the existing per-tenant/per-user unique index. Retrying partial fan-out cannot create a second alert for that message. System activity, private threads and failed/empty agent responses do not create new-message alerts. A committed agent response carries its agent identity and AiRunId; concurrency retries discard the rolled-back notification event and never rerun the model or tools.

Notifications use the existing staff inbox and top-bar count, with **All / Unread / Mentions / Chat** filters, shared CGI agent portraits and human initials. Opening an unread item first records its read state, then opens and highlights the linked message. Earlier messages load through the authorized paged room endpoint; unloaded gaps remain visible and can be filled. Read failure keeps the inbox open with retry feedback. Mark read, dismiss and mark-all affect only the current user's notifications; they do not execute approvals or mark a room's entire history read.

These alerts are **in-app only**: no email or mobile push is sent. Message text remains in the room; notification bodies use generic wording and metadata identifies the author, room and message. The room endpoint reauthorizes every link, including after removal. Existing historical alerts are not a grant of access.

The SSE publisher is process-local. A visible client therefore reconciles the persisted inbox every ten seconds, and on focus/visibility changes, in addition to existing SSE updates. Tenant/identity changes clear the displayed inbox and ignore responses from the prior scope. Staff inbox endpoints and the notification bell use AdminReadPolicy; read-only staff may manage their own notification state but gain no domain mutation authority.

## Access and roles

- **Owner:** creates the room, manages its members and agents, and contributes. The owner must remain a member.
- **Participant:** reads the shared history and contributes. Agent actions additionally require a writable workspace staff role.
- **Observer:** reads history; cannot post, invoke agents or decide approvals.

All endpoints require a staff policy, tenant context, an authenticated user and active room membership. The directory includes active, provisioned PlatformAdmin, TenantAdmin, Operations and ReadOnly staff in the current tenant. PersonalUser accounts and unaccepted invitations are excluded. A room role never upgrades a workspace role.

Invitations and agent additions require an explicit acknowledgement that the participant can read the existing room history. Only the owner manages other members. A non-owner may leave. Membership changes are recorded as room activity. Removal immediately denies subsequent reads and writes. The client clears the conversation and open room dialogs when access is revoked.

## Agents and approvals

Only registered, active agents from enabled modules can be added or invoked. Starter-kit design personas are not registered as new executable agents. Personal-finance agents and descriptors that require a private User Brief are excluded from shared rooms.

AONIK is the main room assistant and uses her dedicated CGI portrait. In shared rooms she summarises supplied room context with a versioned `collaborative-chat.v1` prompt. She has no private memory, MCP, reminder or domain-delegation tools in this transport. A human explicitly addresses a registered domain agent for live evidence or proposals. Domain agents resolve through the existing configuration/model-routing path and retain their registered, server-gated tools.

Each room turn has an AiRun before a model is invoked. Room messages, tool approval requests and high-risk proposals reference that run. Existing chat-client audit middleware still records model calls. The new context guard rechecks the requesting human's active membership, writable staff role, agent membership and active turn before a classified mutation reaches its normal approval gate.

Room-origin authority is also checked through the existing global tool-approval and proposal decision routes. A copied link cannot bypass room removal, observer restrictions or the requesting-human requirement. Medium approvals are bound to their room and cannot be consumed from another room or private chat.

Approval cards show the recorded arguments and durable references. Decisions use the existing ToolApprovalService and proposal dispatcher. Medium actions require approval bound to the exact arguments, followed by an explicit **Continue this approved action** request; the existing gate consumes the approval once. High-risk actions remain durable proposals executed through the dispatcher. The UI distinguishes approval from execution and does not assert a payment or ledger posting succeeded.

**Existing approval policy remains requester-only.** Another human in the room can review a proposal, but room ownership or participation does not grant authority to approve someone else's request. A future B2B approver policy is a separate policy change; this feature does not infer financial authority from room roles.

## Persistence, ordering and recovery

The Agents module owns `ChatRoom`, `ChatRoomMember`, `ChatRoomMessage` and `ChatRoomTurn`. All entities remain anemic and tenant-scoped. Configurations live in the module; only the canonical AonikDbContext migration stream is used.

SQL transactions, room revision concurrency, unique message sequence indexes and request references protect ordering and retries. A duplicate message or agent request returns its existing result; a changed message reusing a request reference is rejected. A conflicting writer refreshes and retries with the same reference. Only one agent run may be active in a room. If another human posts while an agent reply is being committed, the service reloads the room revision and retries only the database commit; it never reruns the model or tools. Agent context is reconstructed from persisted room messages and typed references, never client-supplied assistant history.

Runs are durably claimed before models or tools execute, have a three-minute execution timeout, and record an Unconfirmed outcome after interruption. Disconnects do not trigger automatic execution retries. If a process dies before releasing its claim, an owner may close a run after ten minutes without rerunning it. Users must inspect underlying records and approvals before submitting a fresh request. Read-only room activity cannot infer that financial execution completed.

Message history is paged in batches of 100 using the `before` sequence. Recent approval metadata is refreshed from its server-owned records. EF InMemory can verify access and version conflicts but does not implement transactional rollback; SQL Server is the authority for concurrent sequence/rollback validation.

## HTTP contracts

| Operation | Endpoint |
| --- | --- |
| List / create rooms | `GET /ai/rooms`, `POST /ai/rooms` |
| Read room and history | `GET /ai/rooms/{roomId}?before=<sequence>` |
| Eligible colleagues / agents | `GET /ai/rooms/people?search=...`, `GET /ai/rooms/agents` |
| Add or update membership | `POST /ai/rooms/{roomId}/members` |
| Remove or leave | `DELETE /ai/rooms/{roomId}/members/{actorKey}` |
| Presence heartbeat | `POST /ai/rooms/{roomId}/presence` |
| Persist human message | `POST /ai/rooms/{roomId}/messages` |
| Request agent response | `POST /ai/rooms/{roomId}/turns` |
| Decide a turn-bound approval | `POST /ai/rooms/{roomId}/approvals/decide` |
| Close interrupted claim | `POST /ai/rooms/{roomId}/turns/{turnId}/close` |

The service derives human authors and AI run references on the server. Actor keys use `human:<userId>` and `agent:<registered-name>`. Room operations do not accept a caller-supplied author. Agent invocation and approval endpoints require AdminWritePolicy in addition to service-level membership and workspace-role checks.

## Migration and verification

`20261102192745_AddCollaborativeChatRooms` and `20261102201401_AddChatRoomRunOriginIndex` were generated using the EF CLI against AonikDbContext. They create the four room tables, their ordering/idempotency indexes and an indexed lookup for room-origin approval authority. The canonical migration chain was applied successfully to a fresh isolated local SQL database.

```powershell
dotnet test tests/Aonik.Application.Tests --filter "FullyQualifiedName~ChatRoomServiceTests|FullyQualifiedName~ToolApproval|FullyQualifiedName~ChatThreadServiceOwnershipTests"
dotnet test tests/Aonik.Api.Tests --filter FullyQualifiedName~ChatRoomEndpointsTests
```

For SQL-backed service tests, first migrate a dedicated verification database, set `AONIK_CHATROOM_TEST_SQL_CONNECTION` to that database's connection string, then run ChatRoomServiceTests. This exercises actual transaction rollback and retry ordering as well as membership, agent turns and approval visibility. Do not point test fixtures at a live application database.

Start the Admin UI with mock authentication on port 4175 and run `node scripts/verify-collaborative-chat.mjs` from `src/Aonik.AdminUi`. It drives two browser clients against fictional intercepted APIs, checks live human/agent updates, explicit approval confirmation and continuation, invitation consent, observers, revocation, portrait decoding, mobile participants, worker-style notification polling, inbox actions and earlier-message navigation. It sends no live agent calls, invitations, notifications or financial operations.

Light/dark production UI captures and `collaborative-chat-runtime-report.json` are written to `Templates/aonik-admin-starterkit/marketing/screenshots/`. Six editable artboards, including notifications and mentions, remain in the starter-kit canvas and `chat.html`; see [chat design references](chat-designs.md).
