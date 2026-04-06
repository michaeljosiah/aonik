import { useCallback, useContext, useEffect, useState } from 'react';
import { WorkspaceSdkContext } from './context';
import type { WorkspaceSdkContextValue } from './context';
import type {
  PanelManifest,
  Unsubscribe,
  WorkspaceAction,
  WorkspaceEvent,
  WorkspaceEventHandler,
} from '../core/types';

// ---------------------------------------------------------------------------
// Internal helper
// ---------------------------------------------------------------------------

function useSdk(): WorkspaceSdkContextValue {
  const ctx = useContext(WorkspaceSdkContext);
  if (!ctx) {
    throw new Error(
      'useWorkspaceSdk hooks must be used within a <WorkspaceSdkProvider>.',
    );
  }
  return ctx;
}

// ---------------------------------------------------------------------------
// useWorkspaceEvents — enhanced version of the existing hook
// ---------------------------------------------------------------------------

/**
 * React hook for workspace event bus, context, and actions.
 *
 * Drop-in enhancement of the existing `useWorkspaceEvents(panelId)` hook.
 * Returns the same `emit`/`onEvent`/`callWorkspace` plus `updateContext`
 * and `getContext`.
 */
export function useWorkspaceEvents(panelId: string) {
  const { eventBus, contextStore, actions } = useSdk();

  /** Publish an event with `sourceId` set to this panel. */
  const emit = useCallback(
    (event: Omit<WorkspaceEvent, 'sourceId'>) => {
      eventBus.publish({ ...event, sourceId: panelId });
    },
    [eventBus, panelId],
  );

  /**
   * Subscribe to events of a given type.
   * Automatically filters out events targeted at other panels.
   */
  const onEvent = useCallback(
    (type: string, handler: WorkspaceEventHandler): Unsubscribe =>
      eventBus.subscribe(type, (event) => {
        if (event.targetId && event.targetId !== panelId) return;
        handler(event);
      }),
    [eventBus, panelId],
  );

  /** Dispatch a workspace-level action. */
  const callWorkspace = useCallback(
    (action: WorkspaceAction) => actions.callWorkspace(action),
    [actions],
  );

  /** Update a shared context value. */
  const updateContext = useCallback(
    <T = unknown>(key: string, value: T) => contextStore.updateContext(key, value),
    [contextStore],
  );

  /** Read a shared context value synchronously. */
  const getContext = useCallback(
    <T = unknown>(key: string): T | undefined => contextStore.getContext<T>(key),
    [contextStore],
  );

  return { emit, onEvent, callWorkspace, updateContext, getContext };
}

// ---------------------------------------------------------------------------
// useWorkspaceContext — reactive context binding
// ---------------------------------------------------------------------------

/**
 * React hook that subscribes to a shared context key and returns a
 * `[value, setValue]` tuple similar to `useState`.
 *
 * @example
 * ```tsx
 * const [selectedInvoice, setSelectedInvoice] = useWorkspaceContext<string>('selectedInvoice');
 * ```
 */
export function useWorkspaceContext<T = unknown>(
  key: string,
): [T | undefined, (value: T) => void] {
  const { contextStore } = useSdk();

  const [value, setLocal] = useState<T | undefined>(() =>
    contextStore.getContext<T>(key),
  );

  useEffect(() => {
    // Sync initial value in case it changed between render and effect
    setLocal(contextStore.getContext<T>(key));

    return contextStore.onContext<T>(key, (newValue) => {
      setLocal(newValue);
    });
  }, [contextStore, key]);

  const setValue = useCallback(
    (newValue: T) => {
      contextStore.updateContext(key, newValue);
    },
    [contextStore, key],
  );

  return [value, setValue];
}

// ---------------------------------------------------------------------------
// usePanel — read a panel manifest
// ---------------------------------------------------------------------------

/**
 * React hook to look up a panel manifest from the registry.
 *
 * Returns `undefined` if the panel is not registered.
 */
export function usePanel(panelId: string): PanelManifest | undefined {
  const { panelRegistry } = useSdk();
  // Synchronous read — manifests are registered at config time
  return panelRegistry.getPanel(panelId);
}
