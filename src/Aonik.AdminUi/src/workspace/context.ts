import { createContext } from 'react';
import type { DockviewApi } from 'dockview';
import type { WorkspaceAction, WorkspaceLayoutRecord } from './types';
import type { WorkspaceEventBus } from './workspaceEventBus';

export interface WorkspaceContextValue {
  api: DockviewApi | null;
  setApi: (api: DockviewApi) => void;
  openPanel: (panelId: string) => void;
  closePanel: (panelId: string) => void;
  maximizeActivePanel: () => void;
  exitMaximizedGroup: () => void;
  layouts: WorkspaceLayoutRecord[];
  activeLayoutId: string;
  loadLayout: (layoutId: string) => void;
  saveActiveLayout: () => void;
  createLayoutFromActive: (name: string) => void;
  resetToDefaultLayout: () => void;
  eventBus: WorkspaceEventBus;
  dispatchAction: (action: WorkspaceAction) => void;
}

export const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);
