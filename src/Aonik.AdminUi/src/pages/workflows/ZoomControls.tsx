// Zoom + fit-view controls — bottom-left overlay on the canvas.
// Mirrors ZoomControls in
// templates/aonik-admin-starterkit/screens/workflow-editor.jsx.

import { Maximize2, Minus, Plus } from 'lucide-react';
import type { ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import type { CanvasView } from './Minimap';

interface ButtonProps {
  children: ReactNode;
  onClick: () => void;
  title: string;
}

function Btn({ children, onClick, title }: ButtonProps) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          variant="outline"
          size="icon-sm"
          onClick={onClick}
          aria-label={title}
          className="size-7 bg-card text-muted-foreground"
        >
          {children}
        </Button>
      </TooltipTrigger>
      <TooltipContent>{title}</TooltipContent>
    </Tooltip>
  );
}

export interface ZoomControlsProps {
  view: CanvasView;
  setView: (v: CanvasView) => void;
  /**
   * Computes a view that fits all current canvas content into the viewport.
   * Wired by the parent so it has access to nodes + svg container size.
   * Returning null is a no-op (e.g. when nothing is on the canvas yet).
   */
  computeFitView?: () => CanvasView | null;
}

export function ZoomControls({ view, setView, computeFitView }: ZoomControlsProps) {
  const handleFit = () => {
    const next = computeFitView?.();
    if (next) setView(next);
    else setView({ scale: 1, tx: 60, ty: 100 });
  };

  return (
    <div
      className="absolute flex items-center gap-1.5 rounded-lg border border-border bg-card shadow-md"
      style={{
        left: 16,
        bottom: 16,
        padding: 4,
      }}
    >
      <Btn
        title="Zoom out"
        onClick={() => setView({ ...view, scale: Math.max(0.35, view.scale - 0.15) })}
      >
        <Minus size={12} />
      </Btn>
      <span
        className="text-center text-[11px] text-muted-foreground"
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
      <span className="h-4 w-px bg-border" />
      <Btn title="Fit view" onClick={handleFit}>
        <Maximize2 size={12} />
      </Btn>
    </div>
  );
}
