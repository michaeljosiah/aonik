import { createContext } from 'react';
import type { EventBus } from '../core/event-bus';
import type { ContextStore } from '../core/context-store';
import type { PanelRegistry } from '../core/panel-registry';
import type { WorkspaceActions } from '../core/workspace-actions';

/** Shape of the SDK context provided to React components. */
export interface WorkspaceSdkContextValue {
  eventBus: EventBus;
  contextStore: ContextStore;
  panelRegistry: PanelRegistry;
  actions: WorkspaceActions;
}

export const WorkspaceSdkContext = createContext<WorkspaceSdkContextValue | null>(null);
