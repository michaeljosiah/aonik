import { describe, expect, it } from 'vitest';
import { decisionRoutes, invoiceDecision, replaceNodeParameters } from './decisionParameters';
import { adaptGraph, buildSaveRequest } from './workflowAdapters';
import type { WorkflowGraphDto } from '@/services/workflowService';

describe('decision authoring', () => {
  const dto = (): WorkflowGraphDto => ({ id: 'workflow', slug: 'invoice', name: 'Invoice', description: '', state: 'Draft', version: 'v1', autoRetry: false,
    ownerName: '', ownerColor: '', contributors: [], expectedVersion: 'token', comments: [], nodes: [
      { id: 'decision', key: 'route', kind: 'Decision', label: 'Route', summary: '', notes: '', x: 0, y: 0, paramsJson: JSON.stringify(invoiceDecision()) },
      { id: 'issue', key: 'issue', kind: 'Tool', label: 'Issue', summary: '', notes: '', x: 1, y: 0,
        paramsJson: JSON.stringify({ actionKey: 'gmail.messages.search', contractVersion: 1, inputs: { query: { literal: 'invoice' } }, connection: { connectionId: 'account', workflowGrantId: 'grant', allowedActions: [{ key: 'gmail.messages.search', version: 1 }] } }) }],
    edges: [{ id: 'standard-edge', key: 'standard-route', fromNodeId: 'decision', toNodeId: 'issue', fromIndex: 0, routeKey: 'standard', label: 'Standard' }] });

  it('keeps decimal strings, fixture expectations and exact connector bindings through save', () => {
    const source = dto();
    const saved = buildSaveRequest(adaptGraph(source));
    expect(JSON.parse(saved.nodes[0].paramsJson)).toEqual(invoiceDecision());
    expect(JSON.parse(saved.nodes[0].paramsJson).rows[0].all[1].value).toBe('5000.00');
    expect(JSON.parse(saved.nodes[1].paramsJson)).toEqual(JSON.parse(source.nodes[1].paramsJson));
    expect(saved.edges[0].routeKey).toBe('standard');
  });

  it('preserves a route target when another route changes its port position', () => {
    const graph = adaptGraph(dto());
    const table = invoiceDecision();
    table.rows.unshift({ id: 'new-row', priority: 5, route: 'elevated', all: [{ field: 'invoice.status', operator: 'eq', value: 'Draft' }] });

    const changed = replaceNodeParameters(graph, 'decision', table);

    expect(decisionRoutes(table)).toEqual(['elevated', 'standard', 'manual']);
    expect(changed.edges[0]).toMatchObject({ id: 'standard-edge', routeKey: 'standard', fromIdx: 1, to: 'issue' });
    expect(graph.edges[0].fromIdx).toBe(0);
  });

  it('removes obsolete route connections instead of silently assigning them to another route', () => {
    const table = invoiceDecision(); table.rows = [];
    const changed = replaceNodeParameters(adaptGraph(dto()), 'decision', table);
    expect(changed.edges).toEqual([]);
    expect(decisionRoutes(table)).toEqual(['manual']);
  });
});
