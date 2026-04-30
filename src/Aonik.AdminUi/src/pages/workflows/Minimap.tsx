// Minimap — bottom-right overview pane that scales the whole node graph
// into a 180×110 SVG with each node represented as a tinted dot.
// Mirrors the Minimap component in
// templates/aonik-admin-starterkit/screens/workflow-editor.jsx.

import { NODE_KIND } from './stepKindCatalog';
import { NODE_H, NODE_W } from './editorGeometry';
import type { WorkflowNode } from './workflowTypes';

export interface CanvasView {
  scale: number;
  tx: number;
  ty: number;
}

export interface MinimapProps {
  nodes: WorkflowNode[];
  view: CanvasView;
}

export function Minimap({ nodes }: MinimapProps) {
  if (!nodes.length) return null;

  const xs = nodes.map((n) => n.x);
  const ys = nodes.map((n) => n.y);
  const x0 = Math.min(...xs) - 40;
  const y0 = Math.min(...ys) - 40;
  const x1 = Math.max(...xs.map((x) => x + NODE_W)) + 40;
  const y1 = Math.max(...ys.map((y) => y + NODE_H)) + 40;
  const w = x1 - x0;
  const h = y1 - y0;
  const W = 180;
  const H = 110;
  const s = Math.min(W / w, H / h);

  return (
    <div
      className="absolute z-[5] rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{
        right: 16,
        bottom: 16,
        width: W + 12,
        height: H + 12,
        padding: 6,
        boxShadow: '0 4px 14px -4px rgba(0,0,0,0.12)',
      }}
    >
      <svg
        width={W}
        height={H}
        className="block rounded-[3px] bg-[var(--color-surface-inset)]"
      >
        {nodes.map((n) => (
          <rect
            key={n.id}
            x={(n.x - x0) * s}
            y={(n.y - y0) * s}
            width={NODE_W * s}
            height={NODE_H * s}
            rx={1.5}
            fill={NODE_KIND[n.kind].tint}
            fillOpacity={0.8}
          />
        ))}
      </svg>
    </div>
  );
}
