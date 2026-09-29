import { useEffect, useRef } from 'react';
import { Card } from '@/components/ui/card';
import { useModules } from '@/modules';
import { ScreenLabelContext } from '@/modules/ScreenLabelContext';
import { useWorkspace } from '../useWorkspace';

interface DockviewPanelProps {
  params?: {
    panelId?: string;
  };
}

export function WorkspacePanel({ params }: DockviewPanelProps) {
  const panelId = params?.panelId;
  const { panels, panelComponents } = useModules();
  const config = panels.find((panel) => panel.id === panelId);
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
        <Card className="p-4 text-sm text-muted-foreground">
          This workspace panel is no longer available.
        </Card>
      </div>
    );
  }

  if (config.type === 'external' && !config.url) {
    return (
      <div className="p-4">
        <Card className="p-4 text-sm text-muted-foreground">
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

  const PanelComponent = config.componentKey ? panelComponents[config.componentKey] : undefined;
  if (!PanelComponent) {
    return (
      <div className="p-4">
        <Card className="p-4 text-sm text-muted-foreground">
          This panel is still being provisioned.
        </Card>
      </div>
    );
  }

  return <ScreenLabelContext.Provider value={config.category === 'page' ? config.title : undefined}><PanelComponent panelId={config.id} title={config.title} /></ScreenLabelContext.Provider>;
}
