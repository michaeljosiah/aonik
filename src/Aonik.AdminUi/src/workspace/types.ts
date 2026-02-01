import type { SerializedDockview } from 'dockview';

export type WorkspacePanelType = 'internal' | 'external';

export interface WorkspacePanelRenderProps {
  panelId: string;
  title: string;
}

export interface WorkspacePanelConfig {
  id: string;
  title: string;
  description?: string;
  type: WorkspacePanelType;
  componentKey?: string;
  url?: string;
  route?: string;
  appCardId?: string;
  defaultWidth?: number;
  defaultHeight?: number;
}

export interface WorkspaceEvent {
  type: string;
  payload?: Record<string, unknown>;
  sourceId: string;
  targetId?: string;
}

export type WorkspaceEventHandler = (event: WorkspaceEvent) => void;

export type WorkspaceActionType =
  | 'open-panel'
  | 'close-panel'
  | 'maximize-active'
  | 'exit-maximized'
  | 'save-layout'
  | 'load-layout'
  | 'reset-layout';

export interface WorkspaceAction {
  type: WorkspaceActionType;
  payload?: Record<string, unknown>;
}

export type WorkspaceLayoutSnapshot = SerializedDockview;

export interface WorkspaceLayoutRecord {
  id: string;
  name: string;
  isDefault: boolean;
  updatedAt: string;
  layout: WorkspaceLayoutSnapshot;
}
