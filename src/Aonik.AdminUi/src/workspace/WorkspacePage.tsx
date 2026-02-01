import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { WorkspaceProvider } from './WorkspaceProvider';
import { WorkspaceDock } from './WorkspaceDock';
import { useWorkspace } from './useWorkspace';

function WorkspacePageContent() {
  const { openPanel, loadLayout, resetToDefaultLayout } = useWorkspace();
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

  useEffect(() => {
    const handleReset = () => resetToDefaultLayout();
    window.addEventListener('aonik:workspace:reset', handleReset);
    return () => window.removeEventListener('aonik:workspace:reset', handleReset);
  }, [resetToDefaultLayout]);

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
