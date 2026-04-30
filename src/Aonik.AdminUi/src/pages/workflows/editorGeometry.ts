// Geometry constants and helpers for the workflow editor canvas.
// Mirrors the constants block at the top of
// templates/aonik-admin-starterkit/screens/workflow-editor.jsx so the
// canvas, palette-drop logic, and inspector stay in sync.

import type { WorkflowNode } from './workflowTypes';
import { NODE_KIND } from './stepKindCatalog';

export const NODE_W = 220;
export const NODE_H = 76;
export const PORT_R = 6;
export const GRID = 16;
export const SNAP = 8;
export const HEADER_H = 30;

export const snap = (v: number): number => Math.round(v / SNAP) * SNAP;

export interface Point {
  x: number;
  y: number;
}

export const inPortPos = (n: { x: number; y: number }): Point => ({
  x: n.x,
  y: n.y + NODE_H / 2,
});

export function outPortPos(
  n: { x: number; y: number },
  idx = 0,
  total = 1,
): Point {
  if (total <= 1) return { x: n.x + NODE_W, y: n.y + NODE_H / 2 };
  // Decision/loop: 2 outputs split vertically
  const offset = (idx === 0 ? -1 : 1) * (NODE_H / 4);
  return { x: n.x + NODE_W, y: n.y + NODE_H / 2 + offset };
}

export function bezierPath(x1: number, y1: number, x2: number, y2: number): string {
  const dx = Math.max(40, Math.abs(x2 - x1) * 0.4);
  return `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`;
}

/** Resolve a node's output port count from the kind catalog. */
export function outputCount(n: Pick<WorkflowNode, 'kind'>): number {
  return NODE_KIND[n.kind].outputs ?? 1;
}
