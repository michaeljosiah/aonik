import { DockviewReact } from 'dockview-react';
import { themeAbyss, themeLight } from 'dockview';
import { WorkspacePanel } from './panels/WorkspacePanel';
import { useWorkspace } from './useWorkspace';
import type { DockviewReadyEvent } from 'dockview';
import { useTheme } from '@/contexts';

const dockComponents = {
  'workspace-panel': WorkspacePanel,
};

export function WorkspaceDock() {
  const { setApi } = useWorkspace();
  const { resolvedTheme } = useTheme();
  const dockTheme = resolvedTheme === 'dark' ? themeAbyss : themeLight;

  return (
    <div className="flex-1 min-h-0">
      <DockviewReact
        components={dockComponents}
        theme={dockTheme}
        onReady={(event: DockviewReadyEvent) => setApi(event.api)}
        className="h-full"
      />
    </div>
  );
}
