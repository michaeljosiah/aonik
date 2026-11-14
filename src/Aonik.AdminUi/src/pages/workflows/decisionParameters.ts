import type { DecisionParameters, WorkflowGraph, WorkflowNode, WorkflowNodeParams } from './workflowTypes';

export const invoiceDecision = (): DecisionParameters => {
  const facts = (amount: string) => ({ 'invoice.id': '00000000-0000-0000-0000-000000000001', 'invoice.total': amount, 'invoice.currency': 'GBP', 'invoice.status': 'Draft' });
  return { schemaVersion: 1, factSchema: 'invoice-approval.v1', hitPolicy: 'FirstMatch',
    rows: [{ id: 'standard', priority: 10, route: 'standard', all: [{ field: 'invoice.currency', operator: 'eq', value: 'GBP' }, { field: 'invoice.total', operator: 'lte', value: '5000.00' }] }], defaultRoute: 'manual',
    fixtures: [{ name: 'Normal invoice', kind: 'Normal', facts: facts('100.00'), expectedStatus: 'Completed', expectedRoutes: ['standard'] },
      { name: 'Threshold boundary', kind: 'Boundary', facts: facts('5000.00'), expectedStatus: 'Completed', expectedRoutes: ['standard'] },
      { name: 'Missing data', kind: 'MissingData', facts: {}, expectedStatus: 'NeedsManualReview', expectedRoutes: [] }] };
};

export function decisionRoutes(params: WorkflowNodeParams): string[] {
  return [...new Set([...(params.rows ?? []).map(row => row.route), params.defaultRoute ?? 'manual'])];
}

export const humanTaskParameters = (): WorkflowNodeParams => ({ schemaVersion: 1, purpose: 'Review this invoice', reviewer: { roleId: '', forbidSelfApproval: true }, timeoutSeconds: 3600,
  choices: [{ key: 'continue', label: 'Continue review', route: 'responded' }], rejectRoute: 'rejected', timeoutRoute: 'timeout' });

export function humanRoutes(params: WorkflowNodeParams): string[] {
  return [...new Set([...(params.choices ?? []).map(choice => choice.route), params.rejectRoute ?? 'rejected', params.timeoutRoute ?? 'timeout'])];
}

export function nodeRoutes(kind: string | undefined, params: WorkflowNodeParams): string[] {
  return kind === 'human' ? humanRoutes(params) : kind === 'decision' ? decisionRoutes(params) : [];
}

export function replaceNodeParameters(graph: WorkflowGraph, nodeId: string, parameters: WorkflowNodeParams, patch: Partial<WorkflowNode> = {}): WorkflowGraph {
  const node = graph.nodes.find(item => item.id === nodeId);
  const routed = node?.kind === 'decision' || node?.kind === 'human';
  const beforeRoutes = nodeRoutes(node?.kind, node?.params ?? {});
  const routes = nodeRoutes(node?.kind, parameters);
  return { ...graph, ...(node?.kind === 'wait' || node?.kind === 'human' ? { schemaVersion: 2 } : {}), nodes: graph.nodes.map(item => item.id === nodeId ? { ...item, ...patch, params: parameters } : item),
    edges: !routed ? graph.edges : graph.edges.flatMap(edge => {
      if (edge.from !== nodeId) return [edge];
      const route = edge.routeKey ?? beforeRoutes[edge.fromIdx ?? 0];
      const position = routes.indexOf(route);
      return position < 0 ? [] : [{ ...edge, fromIdx: position, routeKey: route, label: route }];
    }) };
}
