import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { WorkspaceProvider } from './WorkspaceProvider';
import { WorkspaceDock } from './WorkspaceDock';
import { useWorkspace } from './useWorkspace';

function WorkspacePageContent() {
  const { api, openPanel, loadLayout, resetToDefaultLayout, createLayoutFromActive, renameLayout, removeLayout } = useWorkspace();
  const [searchParams, setSearchParams] = useSearchParams();

  useEffect(() => {
    if (!api) {
      return;
    }

    const panelId = searchParams.get('panel');
    const layoutId = searchParams.get('layout');

    if (!panelId && !layoutId) {
      return;
    }

    const timerId = window.setTimeout(() => {
      if (layoutId) {
        loadLayout(layoutId);
      }

      if (panelId) {
        openPanel(panelId);
      }

      const nextParams = new URLSearchParams(searchParams);
      nextParams.delete('panel');
      nextParams.delete('layout');
      setSearchParams(nextParams, { replace: true });
    }, 0);

    return () => window.clearTimeout(timerId);
  }, [api, loadLayout, openPanel, searchParams, setSearchParams]);

  useEffect(() => {
    const handleReset = () => resetToDefaultLayout();
    window.addEventListener('aonik:workspace:reset', handleReset);
    return () => window.removeEventListener('aonik:workspace:reset', handleReset);
  }, [resetToDefaultLayout]);

  useEffect(() => {
    const handleLoad = (event: Event) => {
      const detail = (event as CustomEvent).detail as { layoutId?: string } | undefined;
      if (detail?.layoutId) {
        loadLayout(String(detail.layoutId));
      }
    };

    const handleCreate = (event: Event) => {
      const detail = (event as CustomEvent).detail as { name?: string } | undefined;
      if (detail?.name) {
        createLayoutFromActive(String(detail.name));
      }
    };

    const handleRename = (event: Event) => {
      const detail = (event as CustomEvent).detail as { layoutId?: string; name?: string } | undefined;
      if (detail?.layoutId && detail?.name) {
        renameLayout(String(detail.layoutId), String(detail.name));
      }
    };

    const handleRemove = (event: Event) => {
      const detail = (event as CustomEvent).detail as { layoutId?: string } | undefined;
      if (detail?.layoutId) {
        removeLayout(String(detail.layoutId));
      }
    };

    window.addEventListener('aonik:workspace:load', handleLoad);
    window.addEventListener('aonik:workspace:create', handleCreate);
    window.addEventListener('aonik:workspace:rename', handleRename);
    window.addEventListener('aonik:workspace:remove', handleRemove);

    return () => {
      window.removeEventListener('aonik:workspace:load', handleLoad);
      window.removeEventListener('aonik:workspace:create', handleCreate);
      window.removeEventListener('aonik:workspace:rename', handleRename);
      window.removeEventListener('aonik:workspace:remove', handleRemove);
    };
  }, [createLayoutFromActive, loadLayout, removeLayout, renameLayout]);

  return (
    <div className="flex h-full flex-col">
      <WorkspaceDock />
    </div>
  );
}

export function WorkspacePage() {
  return (
    <WorkspaceProvider>
      <WorkspacePageContent />
    </WorkspaceProvider>
  );
}
