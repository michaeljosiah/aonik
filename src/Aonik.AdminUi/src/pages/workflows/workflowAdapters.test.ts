import { describe, expect, it } from 'vitest';
import type { WorkflowGraphDto } from '@/services/workflowService';
import type { WorkflowGraph } from './workflowTypes';
import { adaptGraph, buildSaveRequest } from './workflowAdapters';

describe('workflow draft round trip', () => {
  it('uses schema 2 when an author explicitly changes a draft to a Document subject', () => {
    const graph: WorkflowGraph = { id: 'workflow', slug: 'intake', name: 'Intake', desc: '', version: 'v2', state: 'Active', ownerColor: '',
      schemaVersion: 1, publishedVersionId: 'old-invoice-version', nodes: [{ id: 'start', kind: 'trigger', label: 'Document', x: 0, y: 0, params: { subjectType: 'Document' } }], edges: [] };
    expect(buildSaveRequest(graph).schemaVersion).toBe(2);
    expect(graph.publishedVersionId).toBe('old-invoice-version');
  });
  it('preserves the concurrency token, logical keys, pinned version and decision routes', () => {
    const dto: WorkflowGraphDto = {
      id: 'workflow-id', slug: 'invoice-routing', name: 'Invoice routing', description: '',
      state: 'Active', version: 'v0.2', autoRetry: false, ownerColor: '', ownerName: '', contributors: [],
      expectedVersion: 'AQID', definitionKind: 'Workflow', publishedVersionId: 'published-version',
      nodes: [{ id: 'node-id', key: 'approval-policy', kind: 'Decision', label: 'Invoice policy',
        summary: '', notes: '', x: 0, y: 0, paramsJson: '{"schemaVersion":1,"defaultRoute":"manual"}' }],
      edges: [{ id: 'edge-id', fromNodeId: 'node-id', toNodeId: 'end-id', fromIndex: 0,
        label: 'Manual review', routeKey: 'manual', key: 'logical-edge' }], comments: [],
    };

    const graph = adaptGraph(dto);
    const saved = buildSaveRequest(graph);

    expect(saved.expectedVersion).toBe('AQID');
    expect(saved.nodes[0].key).toBe('approval-policy');
    expect(saved.nodes[0].clientId).toBe('node-id');
    expect(saved.edges[0].routeKey).toBe('manual');
    expect(JSON.parse(saved.nodes[0].paramsJson)).toEqual({ schemaVersion: 1, defaultRoute: 'manual' });
    expect(graph.publishedVersionId).toBe('published-version');
  });
});
