import type { ActionHandler, Unsubscribe, WorkspaceAction } from './types';

/**
 * Thin routing layer for workspace-level actions.
 *
 * The primary handler is typically `WorkspaceProvider.dispatchAction` on the
 * host side, or a postMessage proxy on the iframe client side. Custom commands
 * can be registered to extend the built-in action set.
 */
export class WorkspaceActions {
  private handler: ActionHandler | null = null;
  private commands = new Map<string, ActionHandler>();

  /** Set the primary action dispatcher (e.g. WorkspaceProvider.dispatchAction). */
  setHandler(handler: ActionHandler): void {
    this.handler = handler;
  }

  /**
   * Register a custom command handler for a specific action type.
   * Custom commands take precedence over the primary handler.
   *
   * @returns An unregister function.
   */
  registerCommand(actionType: string, handler: ActionHandler): Unsubscribe {
    this.commands.set(actionType, handler);
    return () => this.commands.delete(actionType);
  }

  /**
   * Dispatch a workspace action.
   *
   * Checks custom commands first, then falls back to the primary handler.
   */
  callWorkspace(action: WorkspaceAction): void {
    const customHandler = this.commands.get(action.type);
    if (customHandler) {
      customHandler(action);
      return;
    }

    if (this.handler) {
      this.handler(action);
      return;
    }

    if (typeof console !== 'undefined') {
      console.warn(
        `[@aonik/workspace-sdk] No handler for action "${action.type}". ` +
          'Call actions.setHandler() or registerCommand() first.',
      );
    }
  }
}
