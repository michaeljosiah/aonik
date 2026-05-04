import { useState, useEffect, useCallback, useMemo } from 'react';
import ReactFlow, {
  Background,
  Controls,
  MiniMap,
  Handle,
  Position,
  useNodesState,
  useEdgesState,
  type Node,
  type Edge,
  type NodeProps,
  MarkerType,
} from 'reactflow';
import 'reactflow/dist/style.css';
import { RefreshCw, Network, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import {
  personalFinanceService,
  type FinancialLifeGraphNode,
  type FinancialLifeGraphEdge,
  type FinancialLifeGraphSummary,
} from '@/services/personalFinanceService';

// ── Node Type Configuration ─────────────────────────────────────────

interface NodeTypeConfig {
  label: string;
  color: string;
  bg: string;
  border: string;
  icon: string;
}

const NODE_TYPE_CONFIG: Record<string, NodeTypeConfig> = {
  UserRoot:              { label: 'User',          color: '#055a60', bg: '#e8f5f6', border: '#055a60', icon: '\u{1F464}' },
  Household:             { label: 'Household',     color: '#6d28d9', bg: '#f3e8ff', border: '#6d28d9', icon: '\u{1F3E0}' },
  HouseholdMember:       { label: 'Member',        color: '#7c3aed', bg: '#f3e8ff', border: '#7c3aed', icon: '\u{1F465}' },
  Party:                 { label: 'Party',         color: '#0369a1', bg: '#e0f2fe', border: '#0369a1', icon: '\u{1F91D}' },
  PersonalAccount:       { label: 'Account',       color: '#047857', bg: '#ecfdf5', border: '#047857', icon: '\u{1F3E6}' },
  PersonalLinkedAccount: { label: 'Linked Acct',   color: '#059669', bg: '#ecfdf5', border: '#059669', icon: '\u{1F517}' },
  PersonalTransaction:   { label: 'Transaction',   color: '#d97706', bg: '#fffbeb', border: '#d97706', icon: '\u{1F4B3}' },
  Bill:                  { label: 'Bill',          color: '#dc2626', bg: '#fef2f2', border: '#dc2626', icon: '\u{1F4C4}' },
  Goal:                  { label: 'Goal',          color: '#2563eb', bg: '#eff6ff', border: '#2563eb', icon: '\u{1F3AF}' },
  Subscription:          { label: 'Subscription',  color: '#9333ea', bg: '#faf5ff', border: '#9333ea', icon: '\u{1F504}' },
  FxQuote:               { label: 'FX Rate',       color: '#0891b2', bg: '#ecfeff', border: '#0891b2', icon: '\u{1F4B1}' },
  OrderRef:              { label: 'Order',         color: '#ea580c', bg: '#fff7ed', border: '#ea580c', icon: '\u{1F4E6}' },
  InvoiceRef:            { label: 'Invoice',       color: '#ca8a04', bg: '#fefce8', border: '#ca8a04', icon: '\u{1F9FE}' },
  PaymentIntentRef:      { label: 'Payment',       color: '#16a34a', bg: '#f0fdf4', border: '#16a34a', icon: '\u{1F4B8}' },
  NativeAnnotation:      { label: 'Annotation',    color: '#64748b', bg: '#f8fafc', border: '#64748b', icon: '\u{1F4CC}' },
  RelationshipAnnotation:{ label: 'Rel. Note',     color: '#64748b', bg: '#f8fafc', border: '#64748b', icon: '\u{1F4CC}' },
  InferredAnnotation:    { label: 'AI Inferred',   color: '#a855f7', bg: '#faf5ff', border: '#a855f7', icon: '\u{2728}' },
};

const DEFAULT_CONFIG: NodeTypeConfig = {
  label: 'Node', color: '#6b7280', bg: '#f9fafb', border: '#6b7280', icon: '\u{2B55}',
};

function getNodeConfig(nodeType: string): NodeTypeConfig {
  return NODE_TYPE_CONFIG[nodeType] ?? DEFAULT_CONFIG;
}

// ── Predicate Display Names ─────────────────────────────────────────

const PREDICATE_LABELS: Record<string, string> = {
  OWNS_ACCOUNT: 'owns',
  HAS_TRANSACTION: 'has txn',
  USES_ACCOUNT: 'uses',
  USES_LINKED_ACCOUNT: 'linked to',
  HAS_BILL: 'has bill',
  HAS_GOAL: 'has goal',
  HAS_SUBSCRIPTION: 'subscribes',
  BELONGS_TO_HOUSEHOLD: 'belongs to',
  HOUSEHOLD_HAS_MEMBER: 'has member',
  HOUSEHOLD_HAS_ACCOUNT: 'has account',
  RELATED_TO_PARTY: 'related to',
  FUNDED_BY_ACCOUNT: 'funded by',
  HAS_FX_CONTEXT: 'fx context',
  ANNOTATED_AS: 'annotated',
  LINKED_TO_ORDER: 'order',
  LINKED_TO_INVOICE: 'invoice',
  LINKED_TO_PAYMENT_INTENT: 'payment',
};

// ── Custom Graph Node ───────────────────────────────────────────────

function GraphNodeComponent({ data }: NodeProps) {
  const config = data.config as NodeTypeConfig;
  const isInferred = data.isInferred as boolean;

  return (
    <div
      className="group relative"
      style={{
        minWidth: 160,
        maxWidth: 220,
      }}
    >
      <Handle type="target" position={Position.Top} className="!bg-transparent !border-0 !w-3 !h-3" />

      <div
        className="rounded-xl px-3.5 py-2.5 shadow-sm transition-shadow hover:shadow-md"
        style={{
          background: config.bg,
          border: `1.5px solid ${config.border}`,
        }}
      >
        {/* Type badge */}
        <div className="flex items-center gap-1.5 mb-1">
          <span className="text-sm leading-none">{config.icon}</span>
          <span
            className="text-[10px] font-semibold uppercase tracking-wider"
            style={{ color: config.color }}
          >
            {config.label}
          </span>
          {isInferred && (
            <Sparkles className="w-3 h-3 ml-auto" style={{ color: '#a855f7' }} />
          )}
        </div>

        {/* Display name */}
        <p
          className="text-xs font-medium leading-snug truncate"
          style={{ color: 'var(--color-text-primary, #1e293b)' }}
          title={data.label as string}
        >
          {data.label as string}
        </p>
      </div>

      <Handle type="source" position={Position.Bottom} className="!bg-transparent !border-0 !w-3 !h-3" />
    </div>
  );
}

const nodeTypes = { graphNode: GraphNodeComponent };

// ── Layout Engine ───────────────────────────────────────────────────

/** Simple layered layout: group by node type, then arrange in concentric rings. */
function layoutNodes(
  graphNodes: FinancialLifeGraphNode[],
  graphEdges: FinancialLifeGraphEdge[],
): { nodes: Node[]; edges: Edge[] } {
  // Priority order for rings (centre → outer)
  const ringOrder = [
    'UserRoot',
    'PersonalAccount', 'PersonalLinkedAccount',
    'Household', 'HouseholdMember', 'Party',
    'Bill', 'Goal', 'Subscription',
    'PersonalTransaction',
    'FxQuote', 'OrderRef', 'InvoiceRef', 'PaymentIntentRef',
    'NativeAnnotation', 'RelationshipAnnotation', 'InferredAnnotation',
  ];

  // Exclude transactions from the visual if there are too many (keep graph readable)
  const transactionCount = graphNodes.filter(n => n.nodeType === 'PersonalTransaction').length;
  const skipTransactions = transactionCount > 50;

  const filteredNodes = skipTransactions
    ? graphNodes.filter(n => n.nodeType !== 'PersonalTransaction')
    : graphNodes;

  const filteredEdges = skipTransactions
    ? graphEdges.filter(e => {
        const isTransactionEdge = filteredNodes.every(n => n.nodeId !== e.fromNodeId) ||
          filteredNodes.every(n => n.nodeId !== e.toNodeId);
        return !isTransactionEdge;
      })
    : graphEdges;

  // Group nodes by type
  const groups = new Map<string, FinancialLifeGraphNode[]>();
  for (const node of filteredNodes) {
    const list = groups.get(node.nodeType) ?? [];
    list.push(node);
    groups.set(node.nodeType, list);
  }

  // Place each ring
  const centerX = 0;
  const centerY = 0;
  const baseRadius = 200;
  const radiusStep = 200;
  const flowNodes: Node[] = [];

  let ringIndex = 0;
  for (const nodeType of ringOrder) {
    const group = groups.get(nodeType);
    if (!group || group.length === 0) continue;
    groups.delete(nodeType);

    if (nodeType === 'UserRoot') {
      // Centre node
      for (const gn of group) {
        flowNodes.push(makeFlowNode(gn, centerX, centerY));
      }
    } else {
      ringIndex++;
      const radius = baseRadius + (ringIndex - 1) * radiusStep;
      const angleStep = (2 * Math.PI) / group.length;
      const startAngle = -Math.PI / 2; // top

      group.forEach((gn, i) => {
        const angle = startAngle + i * angleStep;
        const x = centerX + radius * Math.cos(angle);
        const y = centerY + radius * Math.sin(angle);
        flowNodes.push(makeFlowNode(gn, x, y));
      });
    }
  }

  // Any remaining types not in ringOrder
  for (const [, group] of groups) {
    ringIndex++;
    const radius = baseRadius + (ringIndex - 1) * radiusStep;
    const angleStep = (2 * Math.PI) / group.length;
    group.forEach((gn, i) => {
      const angle = -Math.PI / 2 + i * angleStep;
      const x = centerX + radius * Math.cos(angle);
      const y = centerY + radius * Math.sin(angle);
      flowNodes.push(makeFlowNode(gn, x, y));
    });
  }

  // Build edges (only for nodes that exist in the filtered set)
  const nodeIdSet = new Set(flowNodes.map(n => n.id));
  const flowEdges: Edge[] = filteredEdges
    .filter(e => nodeIdSet.has(e.fromNodeId) && nodeIdSet.has(e.toNodeId))
    .map((e, i) => {
      const predLabel = PREDICATE_LABELS[e.predicate] ?? e.predicate.toLowerCase().replace(/_/g, ' ');
      return {
        id: `e-${i}`,
        source: e.fromNodeId,
        target: e.toNodeId,
        label: predLabel,
        type: 'default',
        animated: e.predicate === 'HAS_TRANSACTION',
        style: { stroke: '#94a3b8', strokeWidth: 1.5 },
        labelStyle: {
          fontSize: 9,
          fontWeight: 500,
          fill: '#64748b',
        },
        labelBgStyle: {
          fill: 'var(--color-surface, #ffffff)',
          fillOpacity: 0.85,
        },
        labelBgPadding: [4, 2] as [number, number],
        labelBgBorderRadius: 3,
        markerEnd: { type: MarkerType.ArrowClosed, width: 12, height: 12, color: '#94a3b8' },
      };
    });

  return { nodes: flowNodes, edges: flowEdges };
}

function makeFlowNode(gn: FinancialLifeGraphNode, x: number, y: number): Node {
  const config = getNodeConfig(gn.nodeType);
  return {
    id: gn.nodeId,
    type: 'graphNode',
    position: { x, y },
    data: {
      label: gn.displayName,
      nodeType: gn.nodeType,
      config,
      isInferred: false,
      metadataJson: gn.metadataJson,
      sourceType: gn.sourceType,
      sourceId: gn.sourceId,
    },
  };
}

// ── Summary Stats Bar ───────────────────────────────────────────────

function SummaryBar({ summary, nodeCount, edgeCount, skippedTransactions }: {
  summary: FinancialLifeGraphSummary;
  nodeCount: number;
  edgeCount: number;
  skippedTransactions: boolean;
}) {
  const stats = [
    { label: 'Accounts', value: summary.accountsCount + summary.linkedAccountsCount },
    { label: 'Bills', value: summary.billsCount },
    { label: 'Goals', value: summary.goalsCount },
    { label: 'Subscriptions', value: summary.subscriptionsCount },
    { label: 'Transactions', value: summary.transactionsCount },
    { label: 'Nodes', value: nodeCount },
    { label: 'Edges', value: edgeCount },
  ].filter(s => s.value > 0);

  return (
    <div className="flex items-center gap-4 flex-wrap">
      {stats.map(s => (
        <div key={s.label} className="flex items-center gap-1.5">
          <span className="text-xs font-semibold text-[var(--color-text-primary)]">{s.value}</span>
          <span className="text-xs text-[var(--color-text-tertiary)]">{s.label}</span>
        </div>
      ))}
      {skippedTransactions && (
        <span className="text-[10px] text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/20 px-2 py-0.5 rounded-full">
          Transactions hidden (50+ nodes)
        </span>
      )}
    </div>
  );
}

// ── Legend ───────────────────────────────────────────────────────────

function Legend({ visibleTypes }: { visibleTypes: Set<string> }) {
  const items = Object.entries(NODE_TYPE_CONFIG).filter(([type]) => visibleTypes.has(type));
  if (items.length === 0) return null;

  return (
    <div className="absolute bottom-4 left-4 z-10 bg-[var(--color-surface)]/90 backdrop-blur-sm border border-[var(--color-border-light)] rounded-lg px-3 py-2 shadow-sm">
      <p className="text-[10px] font-semibold text-[var(--color-text-tertiary)] uppercase tracking-wider mb-1.5">Legend</p>
      <div className="flex flex-wrap gap-x-3 gap-y-1">
        {items.map(([type, config]) => (
          <div key={type} className="flex items-center gap-1.5">
            <span
              className="w-2.5 h-2.5 rounded-full border"
              style={{ background: config.bg, borderColor: config.border }}
            />
            <span className="text-[10px] text-[var(--color-text-secondary)]">{config.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Main Component ──────────────────────────────────────────────────

interface FinancialGraphSubTabProps {
  userId: string;
}

export function FinancialGraphSubTab({ userId }: FinancialGraphSubTabProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [summary, setSummary] = useState<FinancialLifeGraphSummary | null>(null);
  const [rawNodeCount, setRawNodeCount] = useState(0);
  const [rawEdgeCount, setRawEdgeCount] = useState(0);
  const [skippedTransactions, setSkippedTransactions] = useState(false);

  const loadGraph = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await personalFinanceService.admin.getFinancialLifeGraph(userId);
      setSummary(response.summary);
      setRawNodeCount(response.nodes.length);
      setRawEdgeCount(response.edges.length);

      const txCount = response.nodes.filter(n => n.nodeType === 'PersonalTransaction').length;
      setSkippedTransactions(txCount > 50);

      const { nodes: flowNodes, edges: flowEdges } = layoutNodes(response.nodes, response.edges);
      setNodes(flowNodes);
      setEdges(flowEdges);
    } catch (err) {
      console.error('Failed to load financial graph:', err);
      setError('Failed to load the financial life graph.');
    } finally {
      setLoading(false);
    }
  }, [userId, setNodes, setEdges]);

  useEffect(() => {
    void loadGraph();
  }, [loadGraph]);

  const visibleTypes = useMemo(() => {
    const types = new Set<string>();
    for (const node of nodes) {
      if (node.data.nodeType) types.add(node.data.nodeType as string);
    }
    return types;
  }, [nodes]);

  if (loading) {
    return <PageLoadingScreen message="Loading financial graph" />;
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <p className="text-sm text-[var(--color-error)] mb-3">{error}</p>
        <Button size="sm" variant="outline" onClick={() => void loadGraph()}>
          <RefreshCw className="w-3.5 h-3.5 mr-1" />
          Retry
        </Button>
      </div>
    );
  }

  if (nodes.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <Network className="w-10 h-10 text-[var(--color-text-tertiary)] mb-3 opacity-40" />
        <p className="text-sm text-[var(--color-text-tertiary)]">No financial graph data for this customer.</p>
        <p className="text-xs text-[var(--color-text-tertiary)] mt-1">The graph populates as accounts, transactions, and financial data are added.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {/* Header bar */}
      <div className="flex items-center justify-between">
        {summary && (
          <SummaryBar
            summary={summary}
            nodeCount={rawNodeCount}
            edgeCount={rawEdgeCount}
            skippedTransactions={skippedTransactions}
          />
        )}
        <Button size="sm" variant="outline" onClick={() => void loadGraph()}>
          <RefreshCw className="w-3.5 h-3.5 mr-1" />
          Refresh
        </Button>
      </div>

      {/* Graph canvas */}
      <div
        className="rounded-lg border border-[var(--color-border-light)] overflow-hidden relative"
        style={{ height: 600 }}
      >
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          nodeTypes={nodeTypes}
          fitView
          fitViewOptions={{ padding: 0.2, maxZoom: 1.2 }}
          minZoom={0.1}
          maxZoom={2}
          proOptions={{ hideAttribution: true }}
          defaultEdgeOptions={{
            style: { strokeWidth: 1.5 },
          }}
        >
          <Background
            gap={20}
            size={1}
            color="var(--color-border-light, #e2e8f0)"
          />
          <Controls
            showInteractive={false}
            className="!bg-[var(--color-surface)] !border-[var(--color-border-light)] !shadow-sm [&>button]:!bg-[var(--color-surface)] [&>button]:!border-[var(--color-border-light)] [&>button:hover]:!bg-[var(--color-surface-inset)]"
          />
          <MiniMap
            nodeColor={(node) => {
              const config = getNodeConfig(node.data?.nodeType as string);
              return config.border;
            }}
            maskColor="rgba(0,0,0,0.08)"
            className="!bg-[var(--color-surface)] !border-[var(--color-border-light)]"
            pannable
            zoomable
          />
          <Legend visibleTypes={visibleTypes} />
        </ReactFlow>
      </div>
    </div>
  );
}
