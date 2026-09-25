import { DockviewReact } from 'dockview-react';
import type { DockviewReadyEvent, DockviewTheme } from 'dockview';
import { WorkspacePanel } from './panels/WorkspacePanel';
import { useWorkspace } from './useWorkspace';

const dockComponents = {
  'workspace-panel': WorkspacePanel,
};

// One theme for both modes: `.dockview-theme-aonik` in index.css maps
// Dockview's variables onto the design tokens, which flip with data-theme.
const aonikDockTheme: DockviewTheme = {
  name: 'aonik',
  className: 'dockview-theme-aonik',
  gap: 6,
};

export function WorkspaceDock() {
  const { setApi } = useWorkspace();

  return (
    <div className="flex-1 min-h-0 bg-muted/50 p-1.5">
      <DockviewReact
        components={dockComponents}
        theme={aonikDockTheme}
        onReady={(event: DockviewReadyEvent) => setApi(event.api)}
        className="h-full"
      />
    </div>
  );
}
