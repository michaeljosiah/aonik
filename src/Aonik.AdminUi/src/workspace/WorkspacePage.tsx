import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { WorkspaceProvider } from './WorkspaceProvider';
import { WorkspaceToolbar } from './WorkspaceToolbar';
import { WorkspaceDock } from './WorkspaceDock';
import { useWorkspace } from './useWorkspace';

function WorkspacePageContent() {
  const { openPanel, loadLayout } = useWorkspace();
  const [searchParams, setSearchParams] = useSearchParams();

  useEffect(() => {
    const panelId = searchParams.get('panel');
    const layoutId = searchParams.get('layout');

    if (layoutId) {
      loadLayout(layoutId);
    }

    if (panelId) {
      openPanel(panelId);
    }

    if (panelId || layoutId) {
      const nextParams = new URLSearchParams(searchParams);
      nextParams.delete('panel');
      nextParams.delete('layout');
      setSearchParams(nextParams, { replace: true });
    }
  }, [loadLayout, openPanel, searchParams, setSearchParams]);

  return (
    <div className="flex h-full flex-col">
      <WorkspaceToolbar />
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
