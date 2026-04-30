// Workflow canvas — SVG diagram with drag/zoom/pan, port-wiring, marquee
// select, snap-to-grid, palette drop, edge rendering, and trace overlay.
//
// 1:1 port of WorkflowCanvas from
// templates/aonik-admin-starterkit/screens/workflow-editor.jsx. State is
// lifted into the parent screen so the inspector and trace bar can reach
// the same selection/run-trace data.

import { useCallback, useEffect, useRef, useState } from 'react';
import { NODE_KIND } from './stepKindCatalog';
import {
  GRID,
  NODE_H,
  NODE_W,
  bezierPath,
  inPortPos,
  outPortPos,
  outputCount,
  snap,
} from './editorGeometry';
import { NodeShape } from './NodeShape';
import { Minimap } from './Minimap';
import type { CanvasView } from './Minimap';
import { ZoomControls } from './ZoomControls';
import type {
  WorkflowComment,
  WorkflowEdge,
  WorkflowNode,
} from './workflowTypes';

export interface Selection {
  nodes: string[];
  edges: string[];
}

export interface TraceState {
  runId: string;
  current: string;
  completed: string[];
}

export interface ValidationError {
  nodeId: string | null;
  message: string;
}

export interface WorkflowCanvasProps {
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  view: CanvasView;
  setView: (v: CanvasView) => void;
  selection: Selection;
  setSelection: (s: Selection) => void;
  hoveredEdge: string | null;
  setHoveredEdge: (id: string | null) => void;
  onMoveNode: (id: string, x: number, y: number) => void;
  onAddEdge: (e: { from: string; to: string; fromIdx: number }) => void;
  onDropPaletteItem: (kind: string, x: number, y: number) => void;
  trace?: TraceState | null;
  comments?: WorkflowComment[];
  validationErrors?: ValidationError[];
  showGrid?: boolean;
  showMinimap?: boolean;
}

type DragState =
  | null
  | { kind: 'pan'; start: { x: number; y: number }; view0: CanvasView }
  | {
      kind: 'node';
      ids: string[];
      start: { x: number; y: number };
      origins: Record<string, { x: number; y: number }>;
    }
  | {
      kind: 'wire';
      fromNode: string;
      fromIdx: number;
      fromPt: { x: number; y: number };
      to: { x: number; y: number };
    }
  | {
      kind: 'marquee';
      start: { x: number; y: number };
      end: { x: number; y: number };
    };

declare global {
  interface Window {
    __aonikEditorSpaceDown?: boolean;
  }
}

export function WorkflowCanvas({
  nodes,
  edges,
  view,
  setView,
  selection,
  setSelection,
  hoveredEdge,
  setHoveredEdge,
  onMoveNode,
  onAddEdge,
  onDropPaletteItem,
  trace = null,
  comments = [],
  validationErrors = [],
  showGrid = true,
  showMinimap = true,
}: WorkflowCanvasProps) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [drag, setDrag] = useState<DragState>(null);

  // Compute a view that frames every node in the viewport with a small
  // padding. Used by ZoomControls' Fit-view button and once on mount so a
  // fresh editor opens with the whole graph visible instead of the
  // top-left corner.
  const computeFitView = useCallback((): CanvasView | null => {
    const r = svgRef.current?.getBoundingClientRect();
    if (!r || nodes.length === 0) return null;

    const xs = nodes.map((n) => n.x);
    const ys = nodes.map((n) => n.y);
    const x0 = Math.min(...xs);
    const y0 = Math.min(...ys);
    const x1 = Math.max(...xs.map((x) => x + NODE_W));
    const y1 = Math.max(...ys.map((y) => y + NODE_H));

    const graphW = x1 - x0;
    const graphH = y1 - y0;
    const padding = 64;
    const availW = Math.max(1, r.width - padding * 2);
    const availH = Math.max(1, r.height - padding * 2);

    // Scale to fit, capped at 1.0 so small graphs don't render gigantic.
    const scale = Math.min(1, Math.min(availW / graphW, availH / graphH));

    // Centre the graph within the viewport.
    const tx = (r.width - graphW * scale) / 2 - x0 * scale;
    const ty = (r.height - graphH * scale) / 2 - y0 * scale;

    return { scale, tx, ty };
  }, [nodes]);

  // Auto-fit once when the node count first becomes non-zero (the page
  // navigates to the editor and the API resolves). Subsequent edits don't
  // re-fit so the user's scroll/zoom is preserved.
  const hasFitRef = useRef(false);
  useEffect(() => {
    if (hasFitRef.current || nodes.length === 0) return;
    const fit = computeFitView();
    if (fit) {
      setView(fit);
      hasFitRef.current = true;
    }
  }, [nodes.length, computeFitView, setView]);

  // Convert client coords → world coords (account for pan + zoom).
  const clientToWorld = useCallback(
    (cx: number, cy: number) => {
      const r = svgRef.current?.getBoundingClientRect();
      if (!r) return { x: 0, y: 0 };
      return {
        x: (cx - r.left - view.tx) / view.scale,
        y: (cy - r.top - view.ty) / view.scale,
      };
    },
    [view],
  );

  // Wheel zoom (centered on cursor).
  const handleWheel = (e: React.WheelEvent<SVGSVGElement>) => {
    e.preventDefault();
    const r = svgRef.current?.getBoundingClientRect();
    if (!r) return;
    const mx = e.clientX - r.left;
    const my = e.clientY - r.top;
    const delta = -e.deltaY * 0.0018;
    const nextScale = Math.max(0.35, Math.min(2.0, view.scale * (1 + delta)));
    const wx = (mx - view.tx) / view.scale;
    const wy = (my - view.ty) / view.scale;
    setView({
      ...view,
      scale: nextScale,
      tx: mx - wx * nextScale,
      ty: my - wy * nextScale,
    });
  };

  // Pointer down on canvas background → pan or marquee.
  const handleBgDown = (e: React.MouseEvent<SVGSVGElement>) => {
    if (e.button !== 0) return;
    const isSpace = window.__aonikEditorSpaceDown;
    if (isSpace) {
      setDrag({ kind: 'pan', start: { x: e.clientX, y: e.clientY }, view0: view });
    } else {
      const w = clientToWorld(e.clientX, e.clientY);
      setDrag({ kind: 'marquee', start: w, end: w });
      if (!e.shiftKey) setSelection({ nodes: [], edges: [] });
    }
  };

  // Pointer down on a node → select + start drag.
  const handleNodeDown = (
    e: React.MouseEvent<SVGElement>,
    node: WorkflowNode,
  ) => {
    e.stopPropagation();
    let nextSel: Selection = selection;
    if (e.shiftKey) {
      const has = selection.nodes.includes(node.id);
      nextSel = {
        nodes: has
          ? selection.nodes.filter((i) => i !== node.id)
          : [...selection.nodes, node.id],
        edges: [],
      };
    } else if (!selection.nodes.includes(node.id)) {
      nextSel = { nodes: [node.id], edges: [] };
    }
    setSelection(nextSel);

    const ids = nextSel.nodes.length ? nextSel.nodes : [node.id];
    const origins: Record<string, { x: number; y: number }> = {};
    ids.forEach((id) => {
      const n = nodes.find((x) => x.id === id);
      if (n) origins[id] = { x: n.x, y: n.y };
    });
    setDrag({
      kind: 'node',
      ids,
      start: { x: e.clientX, y: e.clientY },
      origins,
    });
  };

  // Pointer down on output port → start a wire.
  const handleOutPortDown = (
    e: React.MouseEvent<SVGCircleElement>,
    node: WorkflowNode,
    outIdx: number,
  ) => {
    e.stopPropagation();
    const total = outputCount(node);
    const fromPt = outPortPos(node, outIdx, total);
    const w = clientToWorld(e.clientX, e.clientY);
    setDrag({ kind: 'wire', fromNode: node.id, fromIdx: outIdx, fromPt, to: w });
  };

  // Pointer up on input port → finalise wire.
  const handleInPortUp = (
    e: React.MouseEvent<SVGCircleElement>,
    node: WorkflowNode,
  ) => {
    if (drag && drag.kind === 'wire' && drag.fromNode !== node.id) {
      onAddEdge({ from: drag.fromNode, fromIdx: drag.fromIdx, to: node.id });
    }
    setDrag(null);
    e.stopPropagation();
  };

  const handleMove = (e: React.MouseEvent<SVGSVGElement>) => {
    if (!drag) return;
    if (drag.kind === 'pan') {
      const dx = e.clientX - drag.start.x;
      const dy = e.clientY - drag.start.y;
      setView({ ...drag.view0, tx: drag.view0.tx + dx, ty: drag.view0.ty + dy });
    } else if (drag.kind === 'node') {
      const dx = (e.clientX - drag.start.x) / view.scale;
      const dy = (e.clientY - drag.start.y) / view.scale;
      drag.ids.forEach((id) => {
        const o = drag.origins[id];
        if (o) onMoveNode(id, snap(o.x + dx), snap(o.y + dy));
      });
    } else if (drag.kind === 'wire') {
      setDrag({ ...drag, to: clientToWorld(e.clientX, e.clientY) });
    } else if (drag.kind === 'marquee') {
      setDrag({ ...drag, end: clientToWorld(e.clientX, e.clientY) });
    }
  };

  const handleUp = () => {
    if (drag && drag.kind === 'marquee') {
      const x0 = Math.min(drag.start.x, drag.end.x);
      const x1 = Math.max(drag.start.x, drag.end.x);
      const y0 = Math.min(drag.start.y, drag.end.y);
      const y1 = Math.max(drag.start.y, drag.end.y);
      const hits = nodes
        .filter(
          (n) =>
            n.x + NODE_W >= x0 && n.x <= x1 && n.y + NODE_H >= y0 && n.y <= y1,
        )
        .map((n) => n.id);
      if (hits.length) setSelection({ nodes: hits, edges: [] });
    }
    setDrag(null);
  };

  // Track Space key for pan-mode (n8n-style).
  useEffect(() => {
    const dn = (e: KeyboardEvent) => {
      if (e.code === 'Space') window.__aonikEditorSpaceDown = true;
    };
    const up = (e: KeyboardEvent) => {
      if (e.code === 'Space') window.__aonikEditorSpaceDown = false;
    };
    window.addEventListener('keydown', dn);
    window.addEventListener('keyup', up);
    return () => {
      window.removeEventListener('keydown', dn);
      window.removeEventListener('keyup', up);
    };
  }, []);

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    const kind = e.dataTransfer.getData('application/x-node-kind');
    if (!kind) return;
    const w = clientToWorld(e.clientX, e.clientY);
    onDropPaletteItem(
      kind,
      snap(w.x - NODE_W / 2),
      snap(w.y - NODE_H / 2),
    );
  };

  const cursor = drag?.kind === 'pan'
    ? 'grabbing'
    : window.__aonikEditorSpaceDown
      ? 'grab'
      : 'default';

  return (
    <div
      className="relative flex-1 min-w-0 overflow-hidden bg-[var(--color-surface-inset)]"
      style={{ height: '100%', cursor }}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
    >
      <svg
        ref={svgRef}
        width="100%"
        height="100%"
        onMouseDown={handleBgDown}
        onMouseMove={handleMove}
        onMouseUp={handleUp}
        onMouseLeave={handleUp}
        onWheel={handleWheel}
        style={{ display: 'block', userSelect: 'none' }}
      >
        <defs>
          <pattern
            id="dotgrid"
            x={0}
            y={0}
            width={GRID * 2}
            height={GRID * 2}
            patternUnits="userSpaceOnUse"
          >
            <circle cx={1} cy={1} r={1} fill="rgba(0,0,0,0.07)" />
          </pattern>
          <marker
            id="arrow"
            viewBox="0 0 10 10"
            refX={9}
            refY={5}
            markerWidth={7}
            markerHeight={7}
            orient="auto-start-reverse"
          >
            <path d="M 0 0 L 10 5 L 0 10 z" fill="#9aa3ad" />
          </marker>
          <marker
            id="arrow-active"
            viewBox="0 0 10 10"
            refX={9}
            refY={5}
            markerWidth={7}
            markerHeight={7}
            orient="auto-start-reverse"
          >
            <path d="M 0 0 L 10 5 L 0 10 z" fill="#055a60" />
          </marker>
          <marker
            id="arrow-trace"
            viewBox="0 0 10 10"
            refX={9}
            refY={5}
            markerWidth={7}
            markerHeight={7}
            orient="auto-start-reverse"
          >
            <path d="M 0 0 L 10 5 L 0 10 z" fill="#3ab795" />
          </marker>
        </defs>

        {/* Grid (in screen space, but offset by pan so it tracks). */}
        {showGrid && (
          <rect
            x={(view.tx % (GRID * 2 * view.scale)) - GRID * 2 * view.scale}
            y={(view.ty % (GRID * 2 * view.scale)) - GRID * 2 * view.scale}
            width="200%"
            height="200%"
            fill="url(#dotgrid)"
          />
        )}

        {/* World group — applies pan + zoom. */}
        <g transform={`translate(${view.tx} ${view.ty}) scale(${view.scale})`}>
          {/* Edges */}
          {edges.map((e) => {
            const a = nodes.find((n) => n.id === e.from);
            const b = nodes.find((n) => n.id === e.to);
            if (!a || !b) return null;
            const total = NODE_KIND[a.kind].outputs ?? 1;
            const p1 = outPortPos(a, e.fromIdx ?? 0, total);
            const p2 = inPortPos(b);
            const isSel = selection.edges.includes(e.id);
            const isHover = hoveredEdge === e.id;
            const isTraced =
              !!trace &&
              trace.completed.includes(e.from) &&
              (trace.completed.includes(e.to) || trace.current === e.to);

            let stroke = '#9aa3ad';
            if (isTraced) stroke = '#3ab795';
            else if (isSel) stroke = 'var(--color-brand-primary)';
            else if (isHover) stroke = '#055a60';
            const sw = isSel || isTraced ? 2.5 : isHover ? 2 : 1.5;
            const markerEnd = isTraced
              ? 'url(#arrow-trace)'
              : isSel
                ? 'url(#arrow-active)'
                : 'url(#arrow)';

            return (
              <g key={e.id}>
                {/* Fat invisible hit path for easier clicking. */}
                <path
                  d={bezierPath(p1.x, p1.y, p2.x, p2.y)}
                  stroke="transparent"
                  strokeWidth={14}
                  fill="none"
                  onMouseEnter={() => setHoveredEdge(e.id)}
                  onMouseLeave={() => setHoveredEdge(null)}
                  onMouseDown={(ev) => {
                    ev.stopPropagation();
                    setSelection({ nodes: [], edges: [e.id] });
                  }}
                  style={{ cursor: 'pointer' }}
                />
                <path
                  d={bezierPath(p1.x, p1.y, p2.x, p2.y)}
                  stroke={stroke}
                  strokeWidth={sw}
                  fill="none"
                  markerEnd={markerEnd}
                  style={{ pointerEvents: 'none' }}
                />
                {e.label && (
                  <g pointerEvents="none">
                    <rect
                      x={(p1.x + p2.x) / 2 - 18}
                      y={(p1.y + p2.y) / 2 - 9}
                      width={36}
                      height={16}
                      rx={3}
                      fill="var(--color-surface)"
                      stroke="var(--color-border-light)"
                      strokeWidth={1}
                    />
                    <text
                      x={(p1.x + p2.x) / 2}
                      y={(p1.y + p2.y) / 2 + 3}
                      textAnchor="middle"
                      fontSize={10}
                      fontFamily="var(--font-mono)"
                      fill="var(--color-text-secondary)"
                    >
                      {e.label}
                    </text>
                  </g>
                )}
              </g>
            );
          })}

          {/* In-progress wire */}
          {drag?.kind === 'wire' && (
            <path
              d={bezierPath(drag.fromPt.x, drag.fromPt.y, drag.to.x, drag.to.y)}
              stroke="var(--color-brand-primary)"
              strokeWidth={2}
              strokeDasharray="5 4"
              fill="none"
              style={{ pointerEvents: 'none' }}
            />
          )}

          {/* Nodes */}
          {nodes.map((n) => (
            <NodeShape
              key={n.id}
              node={n}
              selected={selection.nodes.includes(n.id)}
              traceCurrent={trace?.current === n.id}
              traceDone={trace?.completed.includes(n.id) ?? false}
              hasError={validationErrors.some((v) => v.nodeId === n.id)}
              onMouseDown={(e) => handleNodeDown(e, n)}
              onPortOutDown={(e, idx) => handleOutPortDown(e, n, idx)}
              onPortInUp={(e) => handleInPortUp(e, n)}
            />
          ))}

          {/* Comments */}
          {comments.map((c) => (
            <g key={c.id} transform={`translate(${c.x}, ${c.y})`}>
              <rect
                x={0}
                y={0}
                width={200}
                height={56}
                rx={6}
                fill="#fff8df"
                stroke="#d4a843"
                strokeWidth={1}
              />
              <text x={10} y={20} fontSize={11} fontWeight={600} fill="#7d5a0e">
                {c.author}
              </text>
              <foreignObject x={10} y={24} width={180} height={28}>
                <div
                  style={{
                    fontSize: 10.5,
                    color: '#5a4308',
                    lineHeight: 1.4,
                    fontFamily: 'var(--font-sans)',
                  }}
                >
                  {c.body}
                </div>
              </foreignObject>
            </g>
          ))}

          {/* Marquee selection */}
          {drag?.kind === 'marquee' && (
            <rect
              x={Math.min(drag.start.x, drag.end.x)}
              y={Math.min(drag.start.y, drag.end.y)}
              width={Math.abs(drag.end.x - drag.start.x)}
              height={Math.abs(drag.end.y - drag.start.y)}
              fill="var(--color-brand-primary-10)"
              stroke="var(--color-brand-primary)"
              strokeWidth={1}
              strokeDasharray="4 3"
              pointerEvents="none"
            />
          )}
        </g>
      </svg>

      {showMinimap && <Minimap nodes={nodes} view={view} />}
      <ZoomControls view={view} setView={setView} computeFitView={computeFitView} />
    </div>
  );
}
