import type { WorkspaceEvent, WorkspaceEventHandler } from './types';

export class WorkspaceEventBus {
  private handlers = new Map<string, Set<WorkspaceEventHandler>>();

  on(type: string, handler: WorkspaceEventHandler) {
    const existing = this.handlers.get(type) ?? new Set<WorkspaceEventHandler>();
    existing.add(handler);
    this.handlers.set(type, existing);

    return () => this.off(type, handler);
  }

  off(type: string, handler: WorkspaceEventHandler) {
    const existing = this.handlers.get(type);
    if (!existing) return;
    existing.delete(handler);
    if (existing.size === 0) {
      this.handlers.delete(type);
    }
  }

  emit(event: WorkspaceEvent) {
    const direct = this.handlers.get(event.type);
    const wildcard = this.handlers.get('*');

    direct?.forEach((handler) => handler(event));
    wildcard?.forEach((handler) => handler(event));
  }
}
