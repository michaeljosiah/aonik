import { createContext } from 'react';
import type { DockviewApi } from 'dockview';
import type { WorkspaceAction, WorkspaceLayoutRecord } from './types';
import type { EventBus } from '@aonik/workspace-sdk';
import type { IframeBridgeHost } from '@aonik/workspace-sdk/host';

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
  applyTemplate: (templateId: string) => void;
  renameLayout: (layoutId: string, newName: string) => void;
  removeLayout: (layoutId: string) => void;
  eventBus: EventBus;
  dispatchAction: (action: WorkspaceAction) => void;
  iframeBridge: IframeBridgeHost;
}

export const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);
