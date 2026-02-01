import { useMemo } from 'react';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useWorkspace } from './useWorkspace';

export function WorkspaceToolbar() {
  const {
    layouts,
    activeLayoutId,
    loadLayout,
    saveActiveLayout,
    createLayoutFromActive,
    resetToDefaultLayout,
    maximizeActivePanel,
  } = useWorkspace();

  const activeLayout = useMemo(
    () => layouts.find((layout) => layout.id === activeLayoutId),
    [activeLayoutId, layouts]
  );

  const handleSaveAs = () => {
    const name = window.prompt('Name this layout', activeLayout?.name ?? 'New Layout');
    if (!name) return;
    createLayoutFromActive(name);
  };

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--color-border)] bg-[var(--color-surface)] px-4 py-3">
      <div>
        <p className="text-sm font-semibold text-[var(--color-text-primary)]">Workspace</p>
        <p className="text-xs text-[var(--color-text-secondary)]">Arrange your active micro apps and agents.</p>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <Select
          value={activeLayoutId}
          onValueChange={(value) => {
            if (!value) return;
            loadLayout(value);
          }}
        >
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder="Select layout" />
          </SelectTrigger>
          <SelectContent>
            {layouts.map((layout) => (
              <SelectItem key={layout.id} value={layout.id}>
                {layout.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button
          variant="secondary"
          size="sm"
          onClick={saveActiveLayout}
          disabled={!activeLayoutId}
        >
          Save layout
        </Button>
        <Button variant="outline" size="sm" onClick={handleSaveAs}>
          Save as
        </Button>
        <Button variant="ghost" size="sm" onClick={resetToDefaultLayout}>
          Reset
        </Button>
        <Button variant="ghost" size="sm" onClick={maximizeActivePanel}>
          Maximize
        </Button>
      </div>
    </div>
  );
}
