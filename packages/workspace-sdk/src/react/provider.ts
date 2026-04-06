import { useMemo, createElement } from 'react';
import type { ReactNode } from 'react';
import { WorkspaceSdkContext } from './context';
import type { WorkspaceSdkContextValue } from './context';
import { createWorkspace } from '../core/index';

export interface WorkspaceSdkProviderProps {
  children: ReactNode;
  /**
   * Optionally inject pre-existing SDK instances.
   * When provided, the provider uses these instead of creating new ones.
   * This is the primary integration path for the Admin UI, where
   * `WorkspaceProvider` controls the lifecycle.
   */
  value?: WorkspaceSdkContextValue;
}

/**
 * Provides the workspace SDK primitives to the React component tree.
 *
 * If no `value` is supplied, creates a new SDK instance internally.
 */
export function WorkspaceSdkProvider({ children, value }: WorkspaceSdkProviderProps) {
  const sdk = useMemo<WorkspaceSdkContextValue>(() => {
    if (value) return value;
    const { eventBus, contextStore, panelRegistry, actions } = createWorkspace();
    return { eventBus, contextStore, panelRegistry, actions };
  }, [value]);

  return createElement(WorkspaceSdkContext.Provider, { value: sdk }, children);
}
