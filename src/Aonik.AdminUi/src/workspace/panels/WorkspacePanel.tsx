import { useEffect, useMemo, useRef } from 'react';
import { Card } from '@/components/ui/card';
import { getWorkspacePanelConfig } from '../registry';
import { workspacePanelComponents } from '../panelComponents';
import { useWorkspace } from '../useWorkspace';
import type { WorkspaceAction } from '../types';

interface DockviewPanelProps {
  params?: {
    panelId?: string;
  };
}

export function WorkspacePanel({ params }: DockviewPanelProps) {
  const panelId = params?.panelId;
  const config = useMemo(
    () => (panelId ? getWorkspacePanelConfig(panelId) : undefined),
    [panelId]
  );
  const { eventBus, dispatchAction } = useWorkspace();
  const iframeRef = useRef<HTMLIFrameElement | null>(null);

  useEffect(() => {
    if (!config || config.type !== 'external') return;

    const handleMessage = (event: MessageEvent) => {
      if (event.source !== iframeRef.current?.contentWindow) return;
      if (!event.data || typeof event.data !== 'object') return;

      const message = event.data as { type?: string; payload?: Record<string, unknown> };
      if (message.type === 'aonik:workspace:event' && message.payload) {
        const workspaceEvent = message.payload as { type?: string; payload?: Record<string, unknown>; targetId?: string };
        eventBus.emit({
          type: String(workspaceEvent.type ?? 'external:event'),
          payload: workspaceEvent.payload,
          sourceId: panelId ?? 'external',
          targetId: workspaceEvent.targetId,
        });
      }

      if (message.type === 'aonik:workspace:action' && message.payload) {
        const action = message.payload as Partial<WorkspaceAction>;
        if (action && typeof action.type === 'string') {
          dispatchAction(action as WorkspaceAction);
        }
      }
    };

    const unsubscribe = eventBus.on('*', (workspaceEvent) => {
      if (!iframeRef.current?.contentWindow) return;
      if (workspaceEvent.targetId && workspaceEvent.targetId !== panelId) return;

      iframeRef.current.contentWindow.postMessage(
        {
          type: 'aonik:workspace:event',
          payload: workspaceEvent,
        },
        '*'
      );
    });

    window.addEventListener('message', handleMessage);

    return () => {
      window.removeEventListener('message', handleMessage);
      unsubscribe();
    };
  }, [config, dispatchAction, eventBus, panelId]);

  if (!config) {
    return (
      <div className="p-4">
        <Card className="p-4 text-sm text-[var(--color-text-secondary)]">
          This workspace panel is no longer available.
        </Card>
      </div>
    );
  }

  if (config.type === 'external' && !config.url) {
    return (
      <div className="p-4">
        <Card className="p-4 text-sm text-[var(--color-text-secondary)]">
          This external panel is missing a URL.
        </Card>
      </div>
    );
  }

  if (config.type === 'external') {
    return (
      <div className="h-full w-full">
        <iframe
          ref={iframeRef}
          title={config.title}
          src={config.url}
          className="h-full w-full border-0"
          onLoad={() => {
            iframeRef.current?.contentWindow?.postMessage(
              { type: 'aonik:workspace:init', payload: { panelId: config.id } },
              '*'
            );
          }}
        />
      </div>
    );
  }

  const PanelComponent = config.componentKey ? workspacePanelComponents[config.componentKey] : undefined;
  if (!PanelComponent) {
    return (
      <div className="p-4">
        <Card className="p-4 text-sm text-[var(--color-text-secondary)]">
          This panel is still being provisioned.
        </Card>
      </div>
    );
  }

  return <PanelComponent panelId={config.id} title={config.title} />;
}
