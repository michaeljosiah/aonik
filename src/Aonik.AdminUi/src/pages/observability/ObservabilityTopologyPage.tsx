import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Background,
  Controls,
  MiniMap,
  ReactFlow,
  type Edge,
  type NodeMouseHandler,
  type Node,
  type NodeProps,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import dagre from 'dagre';
import { Activity, Database, Globe, Loader2, Play, RefreshCw, Server } from 'lucide-react';
import { toast } from 'sonner';

import { PageHeader } from '@/components/layout/aonik/PageHeader';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  observabilityService,
  type RuntimeServiceActionResponse,
  type RuntimeServiceStatus,
  type TopologyEdge,
  type TopologyNode,
  type TopologyResponse,
} from '@/services/observabilityService';

const TIME_RANGE_OPTIONS = [
  { value: '1h', label: 'Last hour' },
  { value: '24h', label: 'Last 24 hours' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
];

const STATUS_BORDER: Record<string, string> = {
  healthy: 'border-emerald-500',
  degraded: 'border-amber-500',
  critical: 'border-red-500',
  unknown: 'border-slate-400',
};

const STATUS_BG: Record<string, string> = {
  healthy: 'bg-emerald-50 dark:bg-emerald-950/30',
  degraded: 'bg-amber-50 dark:bg-amber-950/30',
  critical: 'bg-red-50 dark:bg-red-950/30',
  unknown: 'bg-slate-50 dark:bg-slate-900/30',
};

type FlowNodeData = { node: TopologyNode; selected: boolean };

function KindIcon({ kind }: { kind: string }) {
  if (kind === 'datastore') return <Database className="h-3.5 w-3.5" />;
  if (kind === 'external') return <Globe className="h-3.5 w-3.5" />;
  return <Server className="h-3.5 w-3.5" />;
}

function getRuntimeBadgeVariant(runtimeState: string | null | undefined): 'success' | 'warning' | 'error' | 'outline' | 'pending' {
  switch ((runtimeState ?? '').toLowerCase()) {
    case 'running':
      return 'success';
    case 'processing':
      return 'pending';
    case 'degraded':
    case 'failed':
      return 'error';
    case 'scaled-to-zero':
    case 'stopped':
      return 'warning';
    default:
      return 'outline';
  }
}

function formatRuntimeLabel(runtimeState: string | null | undefined): string {
  switch ((runtimeState ?? '').toLowerCase()) {
    case 'scaled-to-zero':
      return 'Scaled to zero';
    case 'running':
      return 'Running';
    case 'processing':
      return 'Starting';
    case 'degraded':
      return 'Degraded';
    case 'failed':
      return 'Failed';
    case 'stopped':
      return 'Stopped';
    case 'missing':
      return 'Missing';
    default:
      return 'Unknown';
  }
}

function formatRelativeTime(iso: string | null): string {
  if (!iso) return '--';
  const diffMs = Date.now() - new Date(iso).getTime();
  const diffMin = Math.floor(Math.abs(diffMs) / 60_000);

  if (diffMin < 1) return diffMs < 0 ? 'in <1m' : 'just now';
  if (diffMin < 60) return diffMs < 0 ? `in ${diffMin}m` : `${diffMin}m ago`;

  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return diffMs < 0 ? `in ${diffHr}h` : `${diffHr}h ago`;

  const diffDay = Math.floor(diffHr / 24);
  return diffMs < 0 ? `in ${diffDay}d` : `${diffDay}d ago`;
}

function formatLatency(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.round(ms)}ms`;
}

function NodeCard({ data }: NodeProps<Node<FlowNodeData>>) {
  const node = data.node;
  const runtime = node.runtime;

  return (
    <div
      className={`min-w-[220px] rounded-md border-2 px-3 py-2 shadow-sm ${STATUS_BORDER[node.status] ?? STATUS_BORDER.unknown} ${STATUS_BG[node.status] ?? STATUS_BG.unknown} ${data.selected ? 'ring-2 ring-[var(--color-brand-primary)] ring-offset-2' : ''}`}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-1.5 text-xs font-semibold text-[var(--color-text-primary)]">
            <KindIcon kind={node.kind} />
            <span className="truncate">{node.label}</span>
          </div>
          <div className="mt-1 text-[10px] uppercase tracking-[0.12em] text-[var(--color-text-tertiary)]">
            {node.kind}
          </div>
        </div>
        {runtime ? (
          <Badge variant={getRuntimeBadgeVariant(runtime.runtimeState)} className="capitalize">
            {formatRuntimeLabel(runtime.runtimeState)}
          </Badge>
        ) : null}
      </div>
      <div className="mt-2 grid grid-cols-3 gap-2 text-[10px] text-[var(--color-text-tertiary)]">
        <div>
          <div>Calls</div>
          <div className="font-semibold text-[var(--color-text-primary)]">{node.calls.toLocaleString()}</div>
        </div>
        <div>
          <div>Error</div>
          <div className="font-semibold text-[var(--color-text-primary)]">{node.errorRatePct.toFixed(1)}%</div>
        </div>
        <div>
          <div>P95</div>
          <div className="font-semibold text-[var(--color-text-primary)]">{formatLatency(node.p95LatencyMs)}</div>
        </div>
      </div>
      {runtime ? (
        <div className="mt-2 text-[10px] text-[var(--color-text-tertiary)]">
          Replicas {runtime.activeRevisionReplicas ?? 0} / min {runtime.minReplicas ?? 0}
        </div>
      ) : null}
    </div>
  );
}

const nodeTypes = { serviceNode: NodeCard };

function layoutGraph(nodes: TopologyNode[], edges: TopologyEdge[], selectedNodeId: string | null) {
  const graph = new dagre.graphlib.Graph();
  graph.setDefaultEdgeLabel(() => ({}));
  graph.setGraph({ rankdir: 'LR', nodesep: 40, ranksep: 100 });

  const nodeWidth = 240;
  const nodeHeight = 112;

  for (const node of nodes) {
    graph.setNode(node.id, { width: nodeWidth, height: nodeHeight });
  }

  for (const edge of edges) {
    graph.setEdge(edge.source, edge.target);
  }

  dagre.layout(graph);

  const flowNodes: Node<FlowNodeData>[] = nodes.map((node) => {
    const pos = graph.node(node.id);
    return {
      id: node.id,
      type: 'serviceNode',
      data: { node, selected: node.id === selectedNodeId },
      position: { x: (pos?.x ?? 0) - nodeWidth / 2, y: (pos?.y ?? 0) - nodeHeight / 2 },
    };
  });

  const flowEdges: Edge[] = edges.map((edge, index) => ({
    id: `${edge.source}__${edge.target}__${index}`,
    source: edge.source,
    target: edge.target,
    label: `${edge.calls.toLocaleString()} · ${formatLatency(edge.p95LatencyMs)}`,
    animated: edge.errorRatePct > 5,
    style: {
      stroke: edge.errorRatePct > 10 ? '#ef4444' : edge.errorRatePct > 2 ? '#f59e0b' : '#94a3b8',
      strokeWidth: Math.min(4, 1 + Math.log10(Math.max(1, edge.calls))),
    },
    labelStyle: { fontSize: 10, fill: 'var(--color-text-tertiary)' },
    labelBgStyle: { fill: 'var(--color-surface)' },
  }));

  return { flowNodes, flowEdges };
}

function mergeRuntimeIntoTopology(topology: TopologyResponse, runtimeStatuses: RuntimeServiceStatus[]): TopologyResponse {
  if (runtimeStatuses.length === 0) {
    return topology;
  }

  const runtimeByService = new Map(runtimeStatuses.map((status) => [status.serviceName, status]));
  const existingIds = new Set(topology.nodes.map((node) => node.id));

  const mergedNodes = topology.nodes.map((node) => ({
    ...node,
    runtime: runtimeByService.get(node.id) ?? node.runtime ?? null,
  }));

  for (const runtime of runtimeStatuses) {
    if (existingIds.has(runtime.serviceName)) {
      continue;
    }

    mergedNodes.push({
      id: runtime.serviceName,
      label: runtime.displayName,
      kind: runtime.serviceType as TopologyNode['kind'],
      status: 'unknown',
      calls: 0,
      errorRatePct: 0,
      p95LatencyMs: 0,
      lastSeen: runtime.lastActiveTime,
      runtime,
    });
  }

  return {
    ...topology,
    nodes: mergedNodes,
  };
}

export function ObservabilityTopologyPage() {
  const [timeRange, setTimeRange] = useState('24h');
  const [topology, setTopology] = useState<TopologyResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [actionInFlight, setActionInFlight] = useState<string | null>(null);

  const loadTopology = useCallback(async (range: string) => {
    const [topologyResult, runtimeStatuses] = await Promise.all([
      observabilityService.getTopology(range),
      observabilityService.getRuntimeServices(),
    ]);

    const merged = mergeRuntimeIntoTopology(topologyResult, runtimeStatuses);
    setTopology(merged);
    setSelectedNodeId((current) => current ?? merged.nodes[0]?.id ?? null);
  }, []);

  useEffect(() => {
    let active = true;

    const run = async () => {
      setLoading(true);
      try {
        await loadTopology(timeRange);
      } catch (error) {
        console.error('Failed to load topology:', error);
        if (active) {
          toast.error('Failed to load service topology.');
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    };

    void run();

    return () => {
      active = false;
    };
  }, [timeRange, loadTopology]);

  const selectedNode = topology?.nodes.find((node) => node.id === selectedNodeId) ?? null;

  const { flowNodes, flowEdges } = useMemo(() => {
    if (!topology) {
      return { flowNodes: [] as Node<FlowNodeData>[], flowEdges: [] as Edge[] };
    }

    return layoutGraph(topology.nodes, topology.edges, selectedNodeId);
  }, [topology, selectedNodeId]);

  const handleNodeClick = useCallback<NodeMouseHandler<Node<FlowNodeData>>>((_event, node) => {
    setSelectedNodeId(node.id);
  }, []);

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await loadTopology(timeRange);
      toast.success('Topology refreshed.');
    } catch (error) {
      console.error('Failed to refresh topology:', error);
      toast.error('Failed to refresh service topology.');
    } finally {
      setRefreshing(false);
    }
  };

  const applyRuntimeUpdate = useCallback((response: RuntimeServiceActionResponse) => {
    setTopology((current) => {
      if (!current || !response.runtime) {
        return current;
      }

      return {
        ...current,
        nodes: current.nodes.map((node) =>
          node.id === response.runtime?.serviceName
            ? { ...node, runtime: response.runtime }
            : node),
      };
    });
  }, []);

  const handleStartService = async (serviceName: string) => {
    setActionInFlight(serviceName);
    try {
      const result = await observabilityService.startRuntimeService(serviceName);
      if (result.success) {
        toast.success(result.message);
      } else {
        toast.error(result.message);
      }

      applyRuntimeUpdate(result);
      await loadTopology(timeRange);
    } catch (error) {
      console.error('Failed to start service:', error);
      toast.error('Failed to start runtime service.');
    } finally {
      setActionInFlight(null);
    }
  };

  return (
    <div className="flex h-full flex-col overflow-hidden">
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)] px-6 py-5">
        <PageHeader
          eyebrow="Observability"
          title="Service Topology"
          subtitle="Visualize platform dependencies and wake scaled-to-zero dev services from the same operational map."
          actions={(
            <>
              <Select value={timeRange} onValueChange={setTimeRange}>
                <SelectTrigger className="w-[180px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TIME_RANGE_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button variant="outline" size="sm" onClick={() => void handleRefresh()} disabled={refreshing}>
                <RefreshCw className={`mr-2 h-4 w-4 ${refreshing ? 'animate-spin' : ''}`} />
                Refresh
              </Button>
            </>
          )}
        />
      </div>

      <div className="flex-1 overflow-auto p-6">
        {loading ? (
          <div className="flex items-center justify-center py-20 text-sm text-[var(--color-text-secondary)]">
            <Loader2 className="mr-2 h-5 w-5 animate-spin" />
            Loading topology...
          </div>
        ) : !topology ? (
          <div className="rounded-md border border-dashed border-[var(--color-border-light)] p-10 text-center text-sm text-[var(--color-text-tertiary)]">
            Topology data is unavailable.
          </div>
        ) : (
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Activity className="h-4 w-4 text-[var(--color-brand-primary)]" />
                  Runtime dependency map
                </CardTitle>
                <CardDescription>
                  Traffic and dependency edges come from Application Insights. Runtime badges come from live Azure Container Apps state.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="mb-3 flex flex-wrap items-center gap-3 text-xs text-[var(--color-text-tertiary)]">
                  <span className="flex items-center gap-1.5"><span className="h-2 w-2 rounded-full bg-emerald-500" /> Healthy</span>
                  <span className="flex items-center gap-1.5"><span className="h-2 w-2 rounded-full bg-amber-500" /> Degraded</span>
                  <span className="flex items-center gap-1.5"><span className="h-2 w-2 rounded-full bg-red-500" /> Critical</span>
                  <span className="ml-auto">Generated {new Date(topology.generatedAt).toLocaleTimeString()}</span>
                </div>
                <div className="h-[72vh] rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]">
                  <ReactFlow
                    nodes={flowNodes}
                    edges={flowEdges}
                    nodeTypes={nodeTypes}
                    fitView
                    onNodeClick={handleNodeClick}
                    proOptions={{ hideAttribution: true }}
                  >
                    <Background gap={16} />
                    <Controls showInteractive={false} />
                    <MiniMap pannable zoomable />
                  </ReactFlow>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Selected service</CardTitle>
                <CardDescription>
                  Inspect runtime status and use dev runtime controls for services that are allowed to wake from zero.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {!selectedNode ? (
                  <p className="text-sm text-[var(--color-text-tertiary)]">Select a node in the topology to inspect it.</p>
                ) : (
                  <>
                    <div>
                      <div className="flex items-center gap-2">
                        <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{selectedNode.label}</h2>
                        {selectedNode.runtime ? (
                          <Badge variant={getRuntimeBadgeVariant(selectedNode.runtime.runtimeState)}>
                            {formatRuntimeLabel(selectedNode.runtime.runtimeState)}
                          </Badge>
                        ) : null}
                      </div>
                      <p className="mt-1 text-sm text-[var(--color-text-secondary)]">
                        {selectedNode.kind === 'service'
                          ? 'Platform runtime service visible in the Container Apps environment.'
                          : selectedNode.kind === 'datastore'
                            ? 'Connected datastore or internal backing service observed from telemetry.'
                            : 'External dependency discovered from telemetry.'}
                      </p>
                    </div>

                    <div className="grid grid-cols-2 gap-3 text-sm">
                      <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                        <div className="text-xs text-[var(--color-text-tertiary)]">Calls</div>
                        <div className="mt-1 font-medium text-[var(--color-text-primary)]">{selectedNode.calls.toLocaleString()}</div>
                      </div>
                      <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                        <div className="text-xs text-[var(--color-text-tertiary)]">P95 latency</div>
                        <div className="mt-1 font-medium text-[var(--color-text-primary)]">{formatLatency(selectedNode.p95LatencyMs)}</div>
                      </div>
                      <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                        <div className="text-xs text-[var(--color-text-tertiary)]">Error rate</div>
                        <div className="mt-1 font-medium text-[var(--color-text-primary)]">{selectedNode.errorRatePct.toFixed(1)}%</div>
                      </div>
                      <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                        <div className="text-xs text-[var(--color-text-tertiary)]">Last seen</div>
                        <div className="mt-1 font-medium text-[var(--color-text-primary)]">{formatRelativeTime(selectedNode.lastSeen)}</div>
                      </div>
                    </div>

                    {selectedNode.runtime ? (
                      <div className="space-y-3 rounded-md border border-[var(--color-border-light)] p-4">
                        <div className="flex items-center justify-between gap-2">
                          <div>
                            <div className="text-sm font-medium text-[var(--color-text-primary)]">Runtime state</div>
                            <div className="text-xs text-[var(--color-text-tertiary)]">
                              Provisioning {selectedNode.runtime.provisioningState}
                              {selectedNode.runtime.latestRevisionName ? ` · ${selectedNode.runtime.latestRevisionName}` : ''}
                            </div>
                          </div>
                          {selectedNode.runtime.message ? (
                            <span className="text-xs text-[var(--color-text-tertiary)]">{selectedNode.runtime.message}</span>
                          ) : null}
                        </div>

                        <div className="grid grid-cols-2 gap-3 text-sm">
                          <div>
                            <div className="text-xs text-[var(--color-text-tertiary)]">Replicas</div>
                            <div className="font-medium text-[var(--color-text-primary)]">{selectedNode.runtime.activeRevisionReplicas ?? 0}</div>
                          </div>
                          <div>
                            <div className="text-xs text-[var(--color-text-tertiary)]">Scale range</div>
                            <div className="font-medium text-[var(--color-text-primary)]">
                              {selectedNode.runtime.minReplicas ?? 0} to {selectedNode.runtime.maxReplicas ?? '--'}
                            </div>
                          </div>
                          <div>
                            <div className="text-xs text-[var(--color-text-tertiary)]">Revision health</div>
                            <div className="font-medium text-[var(--color-text-primary)]">{selectedNode.runtime.revisionHealthState ?? '--'}</div>
                          </div>
                          <div>
                            <div className="text-xs text-[var(--color-text-tertiary)]">Last active</div>
                            <div className="font-medium text-[var(--color-text-primary)]">{formatRelativeTime(selectedNode.runtime.lastActiveTime)}</div>
                          </div>
                        </div>

                        {selectedNode.runtime.isStartable ? (
                          <Button
                            onClick={() => void handleStartService(selectedNode.runtime!.serviceName)}
                            disabled={actionInFlight === selectedNode.runtime.serviceName || selectedNode.runtime.isRunning}
                            className="w-full"
                          >
                            {actionInFlight === selectedNode.runtime.serviceName ? (
                              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            ) : (
                              <Play className="mr-2 h-4 w-4" />
                            )}
                            {selectedNode.runtime.isRunning ? 'Service already running' : `Start ${selectedNode.runtime.displayName}`}
                          </Button>
                        ) : null}
                      </div>
                    ) : (
                      <div className="rounded-md border border-dashed border-[var(--color-border-light)] p-4 text-sm text-[var(--color-text-tertiary)]">
                        No direct runtime control is available for this node because it is telemetry-only and not mapped to a managed Container App service.
                      </div>
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          </div>
        )}
      </div>
    </div>
  );
}
