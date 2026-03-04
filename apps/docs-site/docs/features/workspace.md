# Workspace (Dockview)

The AONIK Workspace is a Dockview-powered layout where users open micro apps as panels.
It supports internal React components and external iframe apps, persistent layouts, and
cross-panel messaging.

## Goals

- Provide a dedicated route for multi-panel work (`/workspace`).
- Allow contextual launches from the dashboard into the workspace.
- Support internal panels today and external micro apps later.
- Persist multiple named layouts in localStorage.
- Enable cross-panel communication and workspace actions.

## Entry Points

- Dashboard launch buttons open `/workspace?panel={panelId}`.
- Workspace route can also be opened directly.
- AI chat continues to use the existing `/ai/chat` route.

## Architecture Overview

```
WorkspacePage
  └─ WorkspaceProvider
     ├─ WorkspaceToolbar (layout actions)
     └─ WorkspaceDock (DockviewReact instance)
          └─ WorkspacePanel (internal / external panel renderer)
```

## Key Files

- Workspace route: `src/Aonik.AdminUi/src/workspace/WorkspacePage.tsx`
- Provider + state: `src/Aonik.AdminUi/src/workspace/WorkspaceProvider.tsx`
- Dock container: `src/Aonik.AdminUi/src/workspace/WorkspaceDock.tsx`
- Panel renderer: `src/Aonik.AdminUi/src/workspace/panels/WorkspacePanel.tsx`
- Panel registry: `src/Aonik.AdminUi/src/workspace/registry.ts`
- Panel components: `src/Aonik.AdminUi/src/workspace/panelComponents.ts`
- Event bus: `src/Aonik.AdminUi/src/workspace/workspaceEventBus.ts`
- Hooks: `src/Aonik.AdminUi/src/workspace/useWorkspace.ts`
- Layout storage: `src/Aonik.AdminUi/src/workspace/storage.ts`

## Panel Registry

Panels are registered statically in `workspacePanelRegistry` and referenced by `panelId`.
Each entry defines the panel type and optional render component or URL.

```ts
export interface WorkspacePanelConfig {
  id: string;
  title: string;
  description?: string;
  type: 'internal' | 'external';
  componentKey?: string; // internal
  url?: string;          // external
  appCardId?: string;    // dashboard linkage
  defaultWidth?: number;
  defaultHeight?: number;
}
```

Mapping to dashboard cards is done via `appCardId`.

## Internal Panels

Internal panels are React components registered in `workspacePanelComponents`.
They receive a minimal render contract:

```ts
export interface WorkspacePanelRenderProps {
  panelId: string;
  title: string;
}
```

Example: `InvoiceManagerPanel` emits a selection event that `ReconciliationHubPanel`
listens to.

## External Panels (iframes)

External panels use an iframe with a postMessage bridge. No sandbox restrictions
are enforced yet.

When the iframe loads, the workspace posts:

```json
{ "type": "aonik:workspace:init", "payload": { "panelId": "..." } }
```

The iframe can send two kinds of messages:

1. Cross-panel events

```json
{
  "type": "aonik:workspace:event",
  "payload": {
    "type": "invoice:selected",
    "payload": { "invoiceId": "INV-3921" },
    "targetId": "reconciliation-hub"
  }
}
```

2. Workspace actions

```json
{
  "type": "aonik:workspace:action",
  "payload": {
    "type": "open-panel",
    "payload": { "panelId": "fraud-detection" }
  }
}
```

## Cross-Panel Messaging (Event Bus)

The Workspace event bus is a simple pub/sub dispatcher. It supports targeted events
and broadcast events.

### Event Shape

```ts
export interface WorkspaceEvent {
  type: string;
  payload?: Record<string, unknown>;
  sourceId: string;
  targetId?: string;
}
```

### Internal Panel Usage

Emit events:

```ts
const { emit } = useWorkspaceEvents(panelId);
emit({ type: 'invoice:selected', payload: { invoiceId: 'INV-3921' } });
```

Listen to events:

```ts
const { onEvent } = useWorkspaceEvents(panelId);
useEffect(() => onEvent('invoice:selected', handler), [onEvent]);
```

If `targetId` is set, only that panel receives the event.

### External Panel Usage

External apps can broadcast the same event payload using `postMessage`.

## Workspace Actions

Panels can request workspace-level actions via `dispatchAction` or `callWorkspace`.
These are handled in the provider to keep logic centralized.

Supported actions:

- `open-panel`
- `close-panel`
- `maximize-active`
- `exit-maximized`
- `save-layout`
- `load-layout`
- `reset-layout`

## Layout Persistence

Layouts are stored in localStorage under `aonik:workspace:layouts`.

```ts
export interface WorkspaceLayoutRecord {
  id: string;
  name: string;
  isDefault: boolean;
  updatedAt: string;
  layout: Record<string, unknown>; // Dockview serialized state
}
```

The provider listens to `onDidLayoutChange` and saves the active layout automatically.

## Toolbar Actions

The Workspace toolbar provides:

- Layout selector
- Save layout
- Save as (new named layout)
- Reset to default layout
- Maximize active panel

## Adding a New Panel

1. Create the React panel in `src/Aonik.AdminUi/src/workspace/apps/`.
2. Register the component key in `workspacePanelComponents`.
3. Register a panel entry in `workspacePanelRegistry`.
4. (Optional) Link the panel to a dashboard AppCard via `appCardId`.

## Known Limitations (Current)

- External panels do not enforce sandbox or origin validation.
- Panel titles are static from the registry.
- Layout storage is local to the browser only.
