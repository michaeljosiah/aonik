import { useCallback, useContext } from 'react';
import { WorkspaceContext } from './context';
import type { WorkspaceAction, WorkspaceEvent, WorkspaceEventHandler } from './types';

export function useWorkspace() {
  const context = useContext(WorkspaceContext);
  if (!context) {
    throw new Error('useWorkspace must be used within WorkspaceProvider');
  }
  return context;
}

export function useWorkspaceEvents(panelId: string) {
  const { eventBus, dispatchAction } = useWorkspace();

  const emit = useCallback(
    (event: Omit<WorkspaceEvent, 'sourceId'>) => {
      eventBus.emit({ ...event, sourceId: panelId });
    },
    [eventBus, panelId]
  );

  const onEvent = useCallback(
    (type: string, handler: WorkspaceEventHandler) =>
      eventBus.on(type, (event) => {
        if (event.targetId && event.targetId !== panelId) return;
        handler(event);
      }),
    [eventBus, panelId]
  );

  const callWorkspace = useCallback(
    (action: WorkspaceAction) => dispatchAction(action),
    [dispatchAction]
  );

  return {
    emit,
    onEvent,
    callWorkspace,
  };
}
