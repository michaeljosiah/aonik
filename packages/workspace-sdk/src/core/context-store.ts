import type { EventBus } from './event-bus';
import type { ContextHandler, Unsubscribe } from './types';

/** Sentinel sourceId used for context-originated events. */
const CONTEXT_SOURCE = '__context-store__';

/** Prefix for context change events on the bus. */
function contextEventType(key: string): string {
  return `context:${key}`;
}

/**
 * Shared key-value context store backed by the {@link EventBus}.
 *
 * Every call to {@link updateContext} stores the value locally **and**
 * publishes a `context:{key}` event on the bus, so existing wildcard
 * listeners (including the iframe bridge) automatically see mutations.
 */
export class ContextStore {
  private store = new Map<string, unknown>();
  private bus: EventBus;

  constructor(eventBus: EventBus) {
    this.bus = eventBus;
  }

  /**
   * Set or update a context value and notify all subscribers.
   *
   * Emits a `context:{key}` event on the bus with `{ key, value, previousValue }`.
   */
  updateContext<T = unknown>(key: string, value: T): void {
    const previousValue = this.store.get(key);
    this.store.set(key, value);

    this.bus.publish({
      type: contextEventType(key),
      payload: { key, value, previousValue },
      sourceId: CONTEXT_SOURCE,
    });
  }

  /** Synchronous read of the current value for a context key. */
  getContext<T = unknown>(key: string): T | undefined {
    return this.store.get(key) as T | undefined;
  }

  /**
   * Subscribe to changes on a specific context key.
   *
   * The handler receives the new value and the key name.
   *
   * @returns An unsubscribe function.
   */
  onContext<T = unknown>(key: string, handler: ContextHandler<T>): Unsubscribe {
    return this.bus.subscribe(contextEventType(key), (event) => {
      handler(event.payload?.value as T, key);
    });
  }

  /** Return all current context keys. */
  keys(): string[] {
    return Array.from(this.store.keys());
  }

  /** Return a shallow snapshot of all context entries. */
  snapshot(): Record<string, unknown> {
    return Object.fromEntries(this.store);
  }

  /** Clear all stored context values without emitting events. */
  clear(): void {
    this.store.clear();
  }
}
