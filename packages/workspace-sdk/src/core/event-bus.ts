import type { Unsubscribe, WorkspaceEvent, WorkspaceEventHandler } from './types';

/**
 * Lightweight pub/sub event bus for workspace events.
 *
 * Drop-in replacement for the existing `WorkspaceEventBus` in the Admin UI.
 * Provides both the new primary API (`subscribe`/`publish`) and backward-
 * compatible aliases (`on`/`off`/`emit`).
 */
export class EventBus {
  private handlers = new Map<string, Set<WorkspaceEventHandler>>();

  // ── Primary API ──────────────────────────────────────────────────────

  /**
   * Subscribe to events of a given type.
   * Use `'*'` to receive all events (wildcard).
   *
   * @returns An unsubscribe function.
   */
  subscribe(type: string, handler: WorkspaceEventHandler): Unsubscribe {
    const existing = this.handlers.get(type) ?? new Set<WorkspaceEventHandler>();
    existing.add(handler);
    this.handlers.set(type, existing);
    return () => this.unsubscribe(type, handler);
  }

  /** Remove a previously registered handler. */
  unsubscribe(type: string, handler: WorkspaceEventHandler): void {
    const existing = this.handlers.get(type);
    if (!existing) return;
    existing.delete(handler);
    if (existing.size === 0) {
      this.handlers.delete(type);
    }
  }

  /** Publish an event to all matching subscribers (direct + wildcard). */
  publish(event: WorkspaceEvent): void {
    const direct = this.handlers.get(event.type);
    const wildcard = this.handlers.get('*');
    direct?.forEach((handler) => handler(event));
    wildcard?.forEach((handler) => handler(event));
  }

  /** Remove all registered handlers. */
  dispose(): void {
    this.handlers.clear();
  }

  // ── Backward-compatible aliases ──────────────────────────────────────

  /** Alias for {@link subscribe}. */
  on(type: string, handler: WorkspaceEventHandler): Unsubscribe {
    return this.subscribe(type, handler);
  }

  /** Alias for {@link unsubscribe}. */
  off(type: string, handler: WorkspaceEventHandler): void {
    this.unsubscribe(type, handler);
  }

  /** Alias for {@link publish}. */
  emit(event: WorkspaceEvent): void {
    this.publish(event);
  }
}
