import type { SerializedDockview } from 'dockview';

export type WorkspacePanelType = 'internal' | 'external';

/**
 * Panel category — drives how the panel is surfaced and how it behaves.
 *
 * **Strategy: When is a page a micro-app?**
 *
 * Use `'page'` (the default) when the panel is a standard CRUD / list / detail
 * view that was designed as a full-page component and is simply wrapped for
 * workspace rendering via `wrapPage()`. Pages are self-contained — they don't
 * publish or subscribe to workspace events and can function identically whether
 * opened as a standalone route or inside the workspace dock.
 *
 * Use `'micro-app'` when the panel is purpose-built for the workspace and
 * meets **one or more** of these criteria:
 *   1. **Cross-panel communication** — it publishes or subscribes to workspace
 *      events (e.g. `job:selected`, `invoice:matched`).
 *   2. **Shared context** — it writes to or reads from the workspace context
 *      store so sibling panels can react.
 *   3. **Workspace-native UI** — the component was designed specifically for
 *      panel-sized rendering (compact layout, no page chrome, dense data).
 *   4. **Paired composition** — it is designed to work alongside one or more
 *      companion panels (e.g. Job Monitor ↔ Audit Trail).
 *
 * Micro-apps appear on the MySpace dashboard as installable "app cards" and
 * can be grouped into pre-built workspace templates.
 */
export type WorkspacePanelCategory = 'page' | 'micro-app';

export interface WorkspacePanelRenderProps {
  panelId: string;
  title: string;
}

export interface WorkspacePanelConfig {
  id: string;
  title: string;
  description?: string;
  type: WorkspacePanelType;
  /** Distinguishes workspace-native micro-apps from wrapped pages. Defaults to `'page'`. */
  category?: WorkspacePanelCategory;
  componentKey?: string;
  url?: string;
  route?: string;
  appCardId?: string;
  defaultWidth?: number;
  defaultHeight?: number;
}

/**
 * A pre-built workspace template contributed by a module.
 *
 * Templates define a named, curated arrangement of panels that can be
 * instantiated as a new workspace layout with a single click. They appear
 * in the Workspace sidebar menu alongside user-saved layouts.
 *
 * Example: "Job Auditor" pairs the Background Jobs panel with the Audit
 * Trail panel so operators can monitor scheduled jobs and immediately
 * cross-reference audit logs.
 */
export interface WorkspaceTemplate {
  /** Unique template identifier (e.g. `'job-auditor'`) */
  id: string;
  /** Display name shown in the workspace menu */
  name: string;
  /** Short description shown as a subtitle or tooltip */
  description?: string;
  /** Lucide icon name */
  icon?: string;
  /** Ordered list of panel IDs to open. First panel is the base; subsequent
   *  panels are added according to `layout`. */
  panels: string[];
  /** How panels are arranged when the template is instantiated.
   *  - `'tabs'` — all panels as tabs in a single group (default)
   *  - `'split-horizontal'` — panels side-by-side (left → right)
   *  - `'split-vertical'` — panels stacked (top → bottom)
   */
  layout?: 'tabs' | 'split-horizontal' | 'split-vertical';
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
