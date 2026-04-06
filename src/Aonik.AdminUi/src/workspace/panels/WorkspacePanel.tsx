import { useEffect, useMemo, useRef } from 'react';
import { Card } from '@/components/ui/card';
import { getWorkspacePanelConfig } from '../registry';
import { workspacePanelComponents } from '../panelComponents';
import { useWorkspace } from '../useWorkspace';

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
  const { iframeBridge } = useWorkspace();
  const iframeRef = useRef<HTMLIFrameElement | null>(null);

  // Register / unregister the iframe with the bridge
  useEffect(() => {
    if (!config || config.type !== 'external' || !config.url || !panelId) return;

    let origin: string;
    try {
      origin = new URL(config.url).origin;
    } catch {
      return;
    }

    // Wait for the iframe ref to be set
    const iframe = iframeRef.current;
    if (!iframe) return;

    iframeBridge.registerIframe(panelId, iframe, origin);

    return () => {
      iframeBridge.unregisterIframe(panelId);
    };
  }, [config, iframeBridge, panelId]);

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
            if (panelId) {
              iframeBridge.sendInit(panelId);
            }
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
