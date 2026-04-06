// ---------------------------------------------------------------------------
// Event Bus
// ---------------------------------------------------------------------------

/** A workspace event flowing through the event bus. */
export interface WorkspaceEvent {
  /** Event type identifier (e.g. 'invoice:selected', 'context:invoiceId'). */
  type: string;
  /** Arbitrary payload. */
  payload?: Record<string, unknown>;
  /** Panel ID that originated the event. */
  sourceId: string;
  /** Optional panel ID to target — omit for broadcast. */
  targetId?: string;
}

/** Handler for workspace events. */
export type WorkspaceEventHandler = (event: WorkspaceEvent) => void;

/** Unsubscribe function returned by subscribe / onContext / etc. */
export type Unsubscribe = () => void;

// ---------------------------------------------------------------------------
// Context Store
// ---------------------------------------------------------------------------

/** Handler for context value changes. */
export type ContextHandler<T = unknown> = (value: T, key: string) => void;

// ---------------------------------------------------------------------------
// Workspace Actions
// ---------------------------------------------------------------------------

/** Built-in workspace action types (matches existing AdminUI WorkspaceActionType). */
export type BuiltInActionType =
  | 'open-panel'
  | 'close-panel'
  | 'maximize-active'
  | 'exit-maximized'
  | 'save-layout'
  | 'load-layout'
  | 'reset-layout';

/** Action type — built-ins plus extensible string. */
export type WorkspaceActionType = BuiltInActionType | (string & {});

/** A workspace-level action request. */
export interface WorkspaceAction {
  type: WorkspaceActionType;
  payload?: Record<string, unknown>;
}

/** Handler for workspace actions. */
export type ActionHandler = (action: WorkspaceAction) => void;

// ---------------------------------------------------------------------------
// Panel Registry
// ---------------------------------------------------------------------------

/** Manifest describing a panel's capabilities. */
export interface PanelManifest {
  /** Unique panel identifier. */
  panelId: string;
  /** Human-readable title. */
  title: string;
  /** Optional description. */
  description?: string;
  /** Context keys this panel emits. */
  emitsContext?: string[];
  /** Context keys this panel consumes. */
  consumesContext?: string[];
  /** Workspace actions this panel may request. */
  actions?: string[];
  /** Arbitrary metadata for extensibility. */
  meta?: Record<string, unknown>;
}

// ---------------------------------------------------------------------------
// Wire Protocol (postMessage between host and iframe)
// ---------------------------------------------------------------------------

/** All wire message types for the postMessage protocol. */
export type WireMessageType =
  | 'aonik:workspace:init'
  | 'aonik:workspace:ready'
  | 'aonik:workspace:connected'
  | 'aonik:workspace:event'
  | 'aonik:workspace:action'
  | 'aonik:workspace:context-update'
  | 'aonik:workspace:context-sync'
  | 'aonik:workspace:register-panel'
  | 'aonik:workspace:disconnect';

/** A postMessage wire message. */
export interface WireMessage {
  type: WireMessageType;
  payload?: Record<string, unknown>;
}

// ---------------------------------------------------------------------------
// Iframe Connection (host-side tracking)
// ---------------------------------------------------------------------------

/** Tracks a connected iframe on the host side. */
export interface IframeConnection {
  panelId: string;
  origin: string;
  iframe: HTMLIFrameElement;
  connected: boolean;
}

// ---------------------------------------------------------------------------
// Workspace SDK Instance
// ---------------------------------------------------------------------------

/** The four primitives returned by createWorkspace(). */
export interface WorkspaceSdk {
  eventBus: import('./event-bus').EventBus;
  contextStore: import('./context-store').ContextStore;
  panelRegistry: import('./panel-registry').PanelRegistry;
  actions: import('./workspace-actions').WorkspaceActions;
}
