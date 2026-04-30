// Left-rail palette — drag-source for the workflow editor canvas.
// Mirrors EditorPalette + PaletteItem from
// templates/aonik-admin-starterkit/screens/workflow-editor-chrome.jsx.

import {
  Bolt,
  Check,
  ChevronLeft,
  ChevronRight,
  Clock,
  GitFork,
  Info,
  MoreHorizontal,
  RefreshCw,
  Send,
  Sparkles,
  Users,
  Wrench,
  Zap,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { NODE_KIND } from './stepKindCatalog';
import type { EditorNodeKind } from './workflowMockData';

const ICON_BY_NAME: Record<string, LucideIcon> = {
  Wrench,
  Sparkles,
  GitFork,
  Users,
  Clock,
  Check,
  Send,
  Zap,
  RefreshCw,
  Bolt,
};

interface PaletteGroup {
  name: string;
  kinds: EditorNodeKind[];
}

const GROUPS: PaletteGroup[] = [
  { name: 'Triggers', kinds: ['trigger'] },
  { name: 'Actions', kinds: ['tool', 'agent', 'notify', 'emit'] },
  { name: 'Logic', kinds: ['decision', 'loop', 'wait'] },
  { name: 'Coordination', kinds: ['human', 'end'] },
];

interface PaletteItemProps {
  kind: EditorNodeKind;
}

function PaletteItem({ kind }: PaletteItemProps) {
  const meta = NODE_KIND[kind];
  const Icon = ICON_BY_NAME[meta.icon] ?? Bolt;

  const handleDragStart = (e: React.DragEvent<HTMLDivElement>) => {
    e.dataTransfer.setData('application/x-node-kind', kind);
    e.dataTransfer.effectAllowed = 'copy';
  };

  return (
    <div
      draggable
      onDragStart={handleDragStart}
      className="group flex select-none items-center gap-2.5 rounded-md border border-transparent hover:border-[var(--color-border-light)] hover:bg-[var(--color-surface-inset)]"
      style={{ padding: '7px 8px', cursor: 'grab' }}
    >
      <span
        className="inline-flex flex-none items-center justify-center text-white"
        style={{ width: 22, height: 22, borderRadius: 5, background: meta.tint }}
      >
        <Icon size={11} />
      </span>
      <div className="min-w-0 flex-1">
        <div className="text-[12px] font-medium text-[var(--color-text-primary)]">
          {meta.label}
        </div>
        <div className="overflow-hidden text-ellipsis whitespace-nowrap text-[10px] text-[var(--color-text-tertiary)]">
          {meta.desc}
        </div>
      </div>
      <MoreHorizontal size={10} className="text-[var(--color-text-tertiary)]" />
    </div>
  );
}

export interface EditorPaletteProps {
  collapsed: boolean;
  setCollapsed: (v: boolean) => void;
}

export function EditorPalette({ collapsed, setCollapsed }: EditorPaletteProps) {
  return (
    <div
      className="flex flex-none flex-col border-r border-[var(--color-border-light)] bg-[var(--color-surface)] transition-[width] duration-150"
      style={{ width: collapsed ? 48 : 240 }}
    >
      <div
        className="flex items-center border-b border-[var(--color-border-light)]"
        style={{ padding: collapsed ? '12px 0' : '12px 14px' }}
      >
        {!collapsed && (
          <span className="flex-1 text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-secondary)]">
            Nodes
          </span>
        )}
        <button
          type="button"
          onClick={() => setCollapsed(!collapsed)}
          className="rounded p-1.5 text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface-inset)]"
          style={{ margin: collapsed ? '0 auto' : 0 }}
          aria-label={collapsed ? 'Expand palette' : 'Collapse palette'}
        >
          {collapsed ? <ChevronRight size={12} /> : <ChevronLeft size={12} />}
        </button>
      </div>

      {!collapsed && (
        <div className="flex-1 overflow-y-auto px-2 py-2">
          {GROUPS.map((g) => (
            <div key={g.name} className="mb-3.5">
              <div
                className="text-[9.5px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-tertiary)]"
                style={{ padding: '6px 8px' }}
              >
                {g.name}
              </div>
              <div className="flex flex-col gap-0.5">
                {g.kinds.map((k) => (
                  <PaletteItem key={k} kind={k} />
                ))}
              </div>
            </div>
          ))}
          <div
            className="mt-1.5 flex gap-2 rounded-md bg-[var(--color-brand-primary-10)] text-[11px] text-[var(--color-text-secondary)]"
            style={{ margin: '6px 8px 0', padding: 10, lineHeight: 1.5 }}
          >
            <Info size={11} className="flex-none text-[var(--color-brand-primary)]" />
            <span>
              Drag any node onto the canvas. Hold <b>Space</b> to pan.
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
