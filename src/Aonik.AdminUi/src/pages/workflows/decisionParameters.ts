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

export function replaceNodeParameters(graph: WorkflowGraph, nodeId: string, parameters: WorkflowNodeParams, patch: Partial<WorkflowNode> = {}): WorkflowGraph {
  const node = graph.nodes.find(item => item.id === nodeId);
  const beforeRoutes = node?.kind === 'decision' ? decisionRoutes(node.params) : [];
  const routes = node?.kind === 'decision' ? decisionRoutes(parameters) : [];
  return { ...graph, nodes: graph.nodes.map(item => item.id === nodeId ? { ...item, ...patch, params: parameters } : item),
    edges: node?.kind !== 'decision' ? graph.edges : graph.edges.flatMap(edge => {
      if (edge.from !== nodeId) return [edge];
      const route = edge.routeKey ?? beforeRoutes[edge.fromIdx ?? 0];
      const position = routes.indexOf(route);
      return position < 0 ? [] : [{ ...edge, fromIdx: position, routeKey: route, label: route }];
    }) };
}
