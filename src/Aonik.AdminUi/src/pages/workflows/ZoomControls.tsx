// Zoom + fit-view controls — bottom-left overlay on the canvas.
// Mirrors ZoomControls in
// templates/aonik-admin-starterkit/screens/workflow-editor.jsx.

import { Maximize2, Minus, Plus } from 'lucide-react';
import type { ReactNode } from 'react';
import type { CanvasView } from './Minimap';

interface ButtonProps {
  children: ReactNode;
  onClick: () => void;
  title: string;
}

function Btn({ children, onClick, title }: ButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={title}
      className="inline-flex items-center justify-center rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-secondary)]"
      style={{ width: 28, height: 28, padding: 0, cursor: 'pointer' }}
    >
      {children}
    </button>
  );
}

export interface ZoomControlsProps {
  view: CanvasView;
  setView: (v: CanvasView) => void;
}

export function ZoomControls({ view, setView }: ZoomControlsProps) {
  return (
    <div
      className="absolute z-[5] flex items-center gap-1.5 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{
        left: 16,
        bottom: 16,
        padding: 4,
        boxShadow: '0 4px 14px -4px rgba(0,0,0,0.12)',
      }}
    >
      <Btn
        title="Zoom out"
        onClick={() => setView({ ...view, scale: Math.max(0.35, view.scale - 0.15) })}
      >
        <Minus size={12} />
      </Btn>
      <span
        className="text-center text-[11px] text-[var(--color-text-secondary)]"
        style={{ fontFamily: 'var(--font-mono)', minWidth: 38 }}
      >
        {Math.round(view.scale * 100)}%
      </span>
      <Btn
        title="Zoom in"
        onClick={() => setView({ ...view, scale: Math.min(2, view.scale + 0.15) })}
      >
        <Plus size={12} />
      </Btn>
      <span className="h-4 w-px bg-[var(--color-border-light)]" />
      <Btn title="Fit view" onClick={() => setView({ scale: 1, tx: 60, ty: 100 })}>
        <Maximize2 size={12} />
      </Btn>
    </div>
  );
}
