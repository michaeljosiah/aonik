export { EventBus } from './event-bus';
export { ContextStore } from './context-store';
export { PanelRegistry } from './panel-registry';
export { WorkspaceActions } from './workspace-actions';

export type {
  // Event Bus
  WorkspaceEvent,
  WorkspaceEventHandler,
  Unsubscribe,
  // Context Store
  ContextHandler,
  // Workspace Actions
  BuiltInActionType,
  WorkspaceActionType,
  WorkspaceAction,
  ActionHandler,
  // Panel Registry
  PanelManifest,
  // Wire Protocol
  WireMessageType,
  WireMessage,
  IframeConnection,
  // SDK Instance
  WorkspaceSdk,
} from './types';

// ---------------------------------------------------------------------------
// Convenience factory
// ---------------------------------------------------------------------------

import { EventBus } from './event-bus';
import { ContextStore } from './context-store';
import { PanelRegistry } from './panel-registry';
import { WorkspaceActions } from './workspace-actions';
import type { WorkspaceSdk } from './types';

/**
 * Create a new workspace SDK instance with all four primitives wired together.
 *
 * The returned `eventBus` is shared by the `contextStore`, so context changes
 * automatically flow as events.
 */
export function createWorkspace(): WorkspaceSdk {
  const eventBus = new EventBus();
  const contextStore = new ContextStore(eventBus);
  const panelRegistry = new PanelRegistry();
  const actions = new WorkspaceActions();
  return { eventBus, contextStore, panelRegistry, actions };
}
