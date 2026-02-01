import { DockviewReact } from 'dockview-react';
import { themeAbyss } from 'dockview';
import { WorkspacePanel } from './panels/WorkspacePanel';
import { useWorkspace } from './useWorkspace';
import type { DockviewReadyEvent } from 'dockview';

const dockComponents = {
  'workspace-panel': WorkspacePanel,
};

export function WorkspaceDock() {
  const { setApi } = useWorkspace();

  return (
    <div className="flex-1 min-h-0">
      <DockviewReact
        components={dockComponents}
        theme={themeAbyss}
        onReady={(event: DockviewReadyEvent) => setApi(event.api)}
        className="h-full"
      />
    </div>
  );
}
