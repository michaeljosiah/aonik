import type { EventBus } from '../core/event-bus';
import type { ContextStore } from '../core/context-store';
import type { PanelRegistry } from '../core/panel-registry';
import type { WorkspaceActions } from '../core/workspace-actions';
import type {
  IframeConnection,
  PanelManifest,
  WireMessage,
  WorkspaceAction,
  WorkspaceEvent,
} from '../core/types';

// ---------------------------------------------------------------------------
// Options
// ---------------------------------------------------------------------------

export interface IframeBridgeHostOptions {
  eventBus: EventBus;
  contextStore: ContextStore;
  actions: WorkspaceActions;
  panelRegistry: PanelRegistry;
  /** Origins that are allowed to communicate via postMessage. */
  allowedOrigins: string[];
}

// ---------------------------------------------------------------------------
// IframeBridgeHost
// ---------------------------------------------------------------------------

/**
 * Host-side bridge that manages secure postMessage communication with
 * external micro-app iframes.
 *
 * Replaces the raw `postMessage('*')` handling previously inlined in
 * `WorkspacePanel.tsx` with origin validation and a three-step handshake.
 */
export class IframeBridgeHost {
  private eventBus: EventBus;
  private contextStore: ContextStore;
  private actions: WorkspaceActions;
  private panelRegistry: PanelRegistry;
  private allowedOrigins: Set<string>;

  private connections = new Map<string, IframeConnection>();
  private busUnsubscribe: (() => void) | null = null;
  private boundHandleMessage: ((event: MessageEvent) => void) | null = null;

  constructor(options: IframeBridgeHostOptions) {
    this.eventBus = options.eventBus;
    this.contextStore = options.contextStore;
    this.actions = options.actions;
    this.panelRegistry = options.panelRegistry;
    this.allowedOrigins = new Set(options.allowedOrigins);
  }

  // ── Lifecycle ──────────────────────────────────────────────────────

  /** Start listening for postMessage events and forwarding bus events. */
  start(): void {
    if (this.boundHandleMessage) return; // already started

    this.boundHandleMessage = this.handleMessage.bind(this);
    window.addEventListener('message', this.boundHandleMessage);

    // Forward all bus events to connected iframes
    this.busUnsubscribe = this.eventBus.subscribe('*', (event) => {
      this.forwardEventToIframes(event);
    });
  }

  /** Stop listening and clean up all connections. */
  stop(): void {
    if (this.boundHandleMessage) {
      window.removeEventListener('message', this.boundHandleMessage);
      this.boundHandleMessage = null;
    }
    this.busUnsubscribe?.();
    this.busUnsubscribe = null;
    this.connections.clear();
  }

  // ── Connection Management ──────────────────────────────────────────

  /**
   * Register an iframe for communication.
   * Call this when an external panel mounts in the workspace.
   */
  registerIframe(panelId: string, iframe: HTMLIFrameElement, origin: string): void {
    this.connections.set(panelId, {
      panelId,
      origin,
      iframe,
      connected: false,
    });
  }

  /** Unregister an iframe. Call this when the panel unmounts. */
  unregisterIframe(panelId: string): void {
    const conn = this.connections.get(panelId);
    if (conn) {
      this.postToIframe(conn, { type: 'aonik:workspace:disconnect' });
      this.connections.delete(panelId);
    }
  }

  /**
   * Initiate the handshake by sending `aonik:workspace:init` to the iframe.
   * Call this in the iframe's `onLoad` handler.
   */
  sendInit(panelId: string): void {
    const conn = this.connections.get(panelId);
    if (!conn) return;
    this.postToIframe(conn, {
      type: 'aonik:workspace:init',
      payload: { panelId } as unknown as Record<string, unknown>,
    });
  }

  /** Update the allowed origins list at runtime. */
  setAllowedOrigins(origins: string[]): void {
    this.allowedOrigins = new Set(origins);
  }

  // ── Internal: Message Handling ─────────────────────────────────────

  private handleMessage(event: MessageEvent): void {
    // Security: reject messages from untrusted origins
    if (!this.isOriginAllowed(event.origin)) return;

    // Find the connection by matching event.source to a registered iframe
    const conn = this.findConnectionBySource(event.source);
    if (!conn) return;

    const message = event.data as Partial<WireMessage>;
    if (!message || typeof message.type !== 'string') return;

    switch (message.type) {
      case 'aonik:workspace:ready':
        this.handleReady(conn);
        break;

      case 'aonik:workspace:event':
        this.handleIncomingEvent(conn, message.payload);
        break;

      case 'aonik:workspace:action':
        this.handleIncomingAction(message.payload);
        break;

      case 'aonik:workspace:context-update':
        this.handleContextUpdate(message.payload);
        break;

      case 'aonik:workspace:register-panel':
        this.handleRegisterPanel(message.payload);
        break;

      default:
        // Unknown message type — ignore
        break;
    }
  }

  private handleReady(conn: IframeConnection): void {
    conn.connected = true;

    // Confirm connection
    this.postToIframe(conn, { type: 'aonik:workspace:connected' });

    // Send full context snapshot so the iframe starts with current state
    this.postToIframe(conn, {
      type: 'aonik:workspace:context-sync',
      payload: this.contextStore.snapshot() as Record<string, unknown>,
    });
  }

  private handleIncomingEvent(
    conn: IframeConnection,
    payload: Record<string, unknown> | undefined,
  ): void {
    if (!payload) return;

    const workspaceEvent: WorkspaceEvent = {
      type: String(payload.type ?? 'external:event'),
      payload: (payload.payload as Record<string, unknown>) ?? undefined,
      sourceId: conn.panelId,
      targetId: payload.targetId ? String(payload.targetId) : undefined,
    };

    this.eventBus.publish(workspaceEvent);
  }

  private handleIncomingAction(payload: Record<string, unknown> | undefined): void {
    if (!payload || typeof payload.type !== 'string') return;
    this.actions.callWorkspace(payload as unknown as WorkspaceAction);
  }

  private handleContextUpdate(payload: Record<string, unknown> | undefined): void {
    if (!payload || typeof payload.key !== 'string') return;
    this.contextStore.updateContext(String(payload.key), payload.value);
  }

  private handleRegisterPanel(payload: Record<string, unknown> | undefined): void {
    if (!payload || typeof payload.panelId !== 'string') return;
    this.panelRegistry.registerPanel(payload as unknown as PanelManifest);
  }

  // ── Internal: Event Forwarding ─────────────────────────────────────

  private forwardEventToIframes(event: WorkspaceEvent): void {
    for (const conn of this.connections.values()) {
      if (!conn.connected) continue;

      // Don't echo events back to the originating iframe
      if (event.sourceId === conn.panelId) continue;

      // Respect targeted events
      if (event.targetId && event.targetId !== conn.panelId) continue;

      this.postToIframe(conn, {
        type: 'aonik:workspace:event',
        payload: event as unknown as Record<string, unknown>,
      });
    }
  }

  // ── Internal: Helpers ──────────────────────────────────────────────

  private postToIframe(conn: IframeConnection, message: WireMessage): void {
    conn.iframe.contentWindow?.postMessage(message, conn.origin);
  }

  private isOriginAllowed(origin: string): boolean {
    return this.allowedOrigins.has(origin);
  }

  private findConnectionBySource(
    source: MessageEventSource | null,
  ): IframeConnection | undefined {
    if (!source) return undefined;
    for (const conn of this.connections.values()) {
      if (conn.iframe.contentWindow === source) return conn;
    }
    return undefined;
  }
}
