import { useMemo } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  type Node,
  type Edge,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import dagre from 'dagre';
import { Database, Globe, Server } from 'lucide-react';

import type { TopologyResponse, TopologyNode, TopologyEdge } from '@/services/observabilityService';

const STATUS_BORDER: Record<string, string> = {
  healthy: 'border-emerald-500',
  degraded: 'border-amber-500',
  critical: 'border-red-500',
  unknown: 'border-gray-400',
};

const STATUS_BG: Record<string, string> = {
  healthy: 'bg-emerald-50 dark:bg-emerald-950/30',
  degraded: 'bg-amber-50 dark:bg-amber-950/30',
  critical: 'bg-red-50 dark:bg-red-950/30',
  unknown: 'bg-gray-50 dark:bg-gray-900/30',
};

function KindIcon({ kind }: { kind: string }) {
  if (kind === 'datastore') return <Database className="h-3.5 w-3.5" />;
  if (kind === 'external') return <Globe className="h-3.5 w-3.5" />;
  return <Server className="h-3.5 w-3.5" />;
}

function NodeCard({ data }: { data: { node: TopologyNode } }) {
  const n = data.node;
  return (
    <div
      className={`rounded-md border-2 ${STATUS_BORDER[n.status] ?? STATUS_BORDER.unknown} ${STATUS_BG[n.status] ?? STATUS_BG.unknown} px-3 py-2 shadow-sm min-w-[160px]`}
    >
      <div className="flex items-center gap-1.5 text-xs font-semibold text-[var(--color-text-primary)]">
        <KindIcon kind={n.kind} />
        <span className="truncate">{n.label}</span>
      </div>
      <div className="mt-1 flex items-center justify-between gap-2 text-[10px] text-[var(--color-text-tertiary)]">
        <span>{n.calls.toLocaleString()} calls</span>
        <span>{n.errorRatePct.toFixed(1)}% err</span>
        <span>{n.p95LatencyMs >= 1000 ? `${(n.p95LatencyMs / 1000).toFixed(1)}s` : `${Math.round(n.p95LatencyMs)}ms`} p95</span>
      </div>
    </div>
  );
}

const nodeTypes = { service: NodeCard };

function layoutGraph(nodes: TopologyNode[], edges: TopologyEdge[]) {
  const g = new dagre.graphlib.Graph();
  g.setDefaultEdgeLabel(() => ({}));
  g.setGraph({ rankdir: 'LR', nodesep: 40, ranksep: 80 });

  const NODE_W = 200;
  const NODE_H = 64;

  for (const n of nodes) g.setNode(n.id, { width: NODE_W, height: NODE_H });
  for (const e of edges) g.setEdge(e.source, e.target);

  dagre.layout(g);

  const flowNodes: Node[] = nodes.map((n) => {
    const pos = g.node(n.id);
    return {
      id: n.id,
      type: 'service',
      data: { node: n },
      position: { x: (pos?.x ?? 0) - NODE_W / 2, y: (pos?.y ?? 0) - NODE_H / 2 },
    };
  });

  const flowEdges: Edge[] = edges.map((e, i) => ({
    id: `${e.source}__${e.target}__${i}`,
    source: e.source,
    target: e.target,
    label: `${e.calls.toLocaleString()} · ${e.p95LatencyMs >= 1000 ? `${(e.p95LatencyMs / 1000).toFixed(1)}s` : `${Math.round(e.p95LatencyMs)}ms`}`,
    animated: e.errorRatePct > 5,
    style: {
      stroke:
        e.errorRatePct > 10
          ? '#ef4444'
          : e.errorRatePct > 2
            ? '#f59e0b'
            : 'var(--color-border-medium, #94a3b8)',
      strokeWidth: Math.min(4, 1 + Math.log10(Math.max(1, e.calls))),
    },
    labelStyle: { fontSize: 10, fill: 'var(--color-text-tertiary)' },
    labelBgStyle: { fill: 'var(--color-surface)' },
  }));

  return { flowNodes, flowEdges };
}

export function TopologyTab({ data }: { data: TopologyResponse }) {
  const { flowNodes, flowEdges } = useMemo(
    () => layoutGraph(data.nodes, data.edges),
    [data.nodes, data.edges],
  );

  if (data.nodes.length === 0) {
    return (
      <div className="rounded-md border border-dashed border-[var(--color-border-light)] p-10 text-center text-sm text-[var(--color-text-tertiary)]">
        No topology data yet. Services appear here once App Insights receives requests/dependencies traffic.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4 text-xs text-[var(--color-text-tertiary)]">
        <span className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full bg-emerald-500" /> Healthy
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full bg-amber-500" /> Degraded
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full bg-red-500" /> Critical
        </span>
        <span className="ml-auto">
          Generated {new Date(data.generatedAt).toLocaleTimeString()}
        </span>
      </div>
      <div
        style={{ height: '70vh' }}
        className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]"
      >
        <ReactFlow
          nodes={flowNodes}
          edges={flowEdges}
          nodeTypes={nodeTypes}
          fitView
          proOptions={{ hideAttribution: true }}
        >
          <Background gap={16} />
          <Controls showInteractive={false} />
          <MiniMap pannable zoomable />
        </ReactFlow>
      </div>
    </div>
  );
}
