import type {
  ContextHandler,
  PanelManifest,
  Unsubscribe,
  WireMessage,
  WorkspaceAction,
  WorkspaceEvent,
  WorkspaceEventHandler,
} from '../core/types';

// ---------------------------------------------------------------------------
// Options
// ---------------------------------------------------------------------------

export interface IframeClientOptions {
  /**
   * Origins trusted to send messages to this iframe.
   * If omitted, all origins are accepted (not recommended for production).
   */
  trustedOrigins?: string[];
}

// ---------------------------------------------------------------------------
// IframeClient
// ---------------------------------------------------------------------------

/**
 * Client-side SDK for external micro-apps loaded inside iframes.
 *
 * Provides the same API surface as the host-side SDK (subscribe, publish,
 * context, actions) but proxies all calls through `postMessage` to the
 * host workspace.
 *
 * @example
 * ```ts
 * import { createClient } from '@aonik/workspace-sdk/client';
 *
 * const client = createClient({ trustedOrigins: ['https://admin.aonik.com'] });
 *
 * client.onReady(() => {
 *   console.log('Connected as panel:', client.getPanelId());
 *
 *   client.subscribe('invoice:selected', (event) => {
 *     console.log('Invoice selected:', event.payload?.invoiceId);
 *   });
 *
 *   client.updateContext('activeTool', 'aml-scanner');
 * });
 * ```
 */
export class IframeClient {
  private trustedOrigins: Set<string> | null;
  private panelId: string | null = null;
  private connected = false;
  private disposed = false;

  // Local event handlers (keyed by event type)
  private eventHandlers = new Map<string, Set<WorkspaceEventHandler>>();

  // Context cache (populated on context-sync, updated on context: events)
  private contextCache = new Map<string, unknown>();
  private contextHandlers = new Map<string, Set<ContextHandler>>();

  // Ready callbacks
  private readyCallbacks = new Set<() => void>();

  private boundHandleMessage: (event: MessageEvent) => void;

  constructor(options?: IframeClientOptions) {
    this.trustedOrigins = options?.trustedOrigins
      ? new Set(options.trustedOrigins)
      : null;

    this.boundHandleMessage = this.handleMessage.bind(this);
    window.addEventListener('message', this.boundHandleMessage);
  }

  // ── Lifecycle ──────────────────────────────────────────────────────

  /**
   * Register a callback that fires once the handshake completes.
   * If already connected, fires immediately.
   */
  onReady(callback: () => void): Unsubscribe {
    if (this.connected) {
      callback();
    }
    this.readyCallbacks.add(callback);
    return () => this.readyCallbacks.delete(callback);
  }

  /** Returns the panel ID assigned by the host, or null if not yet connected. */
  getPanelId(): string | null {
    return this.panelId;
  }

  /** Whether the handshake has completed. */
  isConnected(): boolean {
    return this.connected;
  }

  /** Clean up all listeners. */
  dispose(): void {
    this.disposed = true;
    window.removeEventListener('message', this.boundHandleMessage);
    this.eventHandlers.clear();
    this.contextHandlers.clear();
    this.readyCallbacks.clear();
    this.contextCache.clear();
  }

  // ── Event Bus ──────────────────────────────────────────────────────

  /** Subscribe to workspace events forwarded from the host. */
  subscribe(type: string, handler: WorkspaceEventHandler): Unsubscribe {
    const existing = this.eventHandlers.get(type) ?? new Set();
    existing.add(handler);
    this.eventHandlers.set(type, existing);
    return () => {
      existing.delete(handler);
      if (existing.size === 0) this.eventHandlers.delete(type);
    };
  }

  /** Publish a workspace event to the host (which broadcasts to other panels). */
  publish(event: Omit<WorkspaceEvent, 'sourceId'>): void {
    this.postToHost({
      type: 'aonik:workspace:event',
      payload: {
        type: event.type,
        payload: event.payload,
        targetId: event.targetId,
      } as unknown as Record<string, unknown>,
    });
  }

  // ── Context Store ──────────────────────────────────────────────────

  /** Update a context value (syncs to host and other panels). */
  updateContext(key: string, value: unknown): void {
    this.contextCache.set(key, value);
    this.postToHost({
      type: 'aonik:workspace:context-update',
      payload: { key, value },
    });
  }

  /** Read the current value for a context key from the local cache. */
  getContext<T = unknown>(key: string): T | undefined {
    return this.contextCache.get(key) as T | undefined;
  }

  /** Subscribe to changes on a specific context key. */
  onContext<T = unknown>(key: string, handler: ContextHandler<T>): Unsubscribe {
    const existing = this.contextHandlers.get(key) ?? new Set<ContextHandler>();
    existing.add(handler as ContextHandler);
    this.contextHandlers.set(key, existing);
    return () => {
      existing.delete(handler as ContextHandler);
      if (existing.size === 0) this.contextHandlers.delete(key);
    };
  }

  // ── Workspace Actions ──────────────────────────────────────────────

  /** Request a workspace-level action from the host. */
  callWorkspace(action: WorkspaceAction): void {
    this.postToHost({
      type: 'aonik:workspace:action',
      payload: action as unknown as Record<string, unknown>,
    });
  }

  // ── Panel Registration ─────────────────────────────────────────────

  /** Register this panel's manifest with the host workspace. */
  registerPanel(manifest: PanelManifest): void {
    this.postToHost({
      type: 'aonik:workspace:register-panel',
      payload: manifest as unknown as Record<string, unknown>,
    });
  }

  // ── Internal: Message Handling ─────────────────────────────────────

  private handleMessage(event: MessageEvent): void {
    if (this.disposed) return;

    // Origin check
    if (this.trustedOrigins && !this.trustedOrigins.has(event.origin)) return;

    const message = event.data as Partial<WireMessage>;
    if (!message || typeof message.type !== 'string') return;

    switch (message.type) {
      case 'aonik:workspace:init':
        this.handleInit(message.payload);
        break;

      case 'aonik:workspace:connected':
        this.handleConnected();
        break;

      case 'aonik:workspace:context-sync':
        this.handleContextSync(message.payload);
        break;

      case 'aonik:workspace:event':
        this.handleIncomingEvent(message.payload);
        break;

      case 'aonik:workspace:disconnect':
        this.handleDisconnect();
        break;

      default:
        break;
    }
  }

  private handleInit(payload: Record<string, unknown> | undefined): void {
    if (!payload || typeof payload.panelId !== 'string') return;
    this.panelId = payload.panelId;

    // Respond with ready
    this.postToHost({ type: 'aonik:workspace:ready' });
  }

  private handleConnected(): void {
    this.connected = true;
    for (const cb of this.readyCallbacks) {
      cb();
    }
  }

  private handleContextSync(payload: Record<string, unknown> | undefined): void {
    if (!payload) return;
    // Payload is a flat key-value map of the entire context
    for (const [key, value] of Object.entries(payload)) {
      this.contextCache.set(key, value);
      this.notifyContextHandlers(key, value);
    }
  }

  private handleIncomingEvent(payload: Record<string, unknown> | undefined): void {
    if (!payload || typeof payload.type !== 'string') return;

    const event: WorkspaceEvent = {
      type: payload.type,
      payload: (payload.payload as Record<string, unknown>) ?? undefined,
      sourceId: (payload.sourceId as string) ?? 'host',
      targetId: (payload.targetId as string) ?? undefined,
    };

    // Update context cache if this is a context event
    if (event.type.startsWith('context:') && event.payload) {
      const key = event.payload.key as string;
      const value = event.payload.value;
      if (key) {
        this.contextCache.set(key, value);
        this.notifyContextHandlers(key, value);
      }
    }

    // Dispatch to local subscribers
    this.dispatchEvent(event);
  }

  private handleDisconnect(): void {
    this.connected = false;
  }

  // ── Internal: Helpers ──────────────────────────────────────────────

  private postToHost(message: WireMessage): void {
    // The iframe posts to its parent. Security is enforced host-side.
    window.parent.postMessage(message, '*');
  }

  private dispatchEvent(event: WorkspaceEvent): void {
    const direct = this.eventHandlers.get(event.type);
    const wildcard = this.eventHandlers.get('*');
    direct?.forEach((handler) => handler(event));
    wildcard?.forEach((handler) => handler(event));
  }

  private notifyContextHandlers(key: string, value: unknown): void {
    const handlers = this.contextHandlers.get(key);
    handlers?.forEach((handler) => handler(value, key));
  }
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

/**
 * Create a new iframe client instance.
 *
 * Typically called once at the top level of an external micro-app.
 */
export function createClient(options?: IframeClientOptions): IframeClient {
  return new IframeClient(options);
}
