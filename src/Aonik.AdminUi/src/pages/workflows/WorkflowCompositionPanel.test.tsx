// @vitest-environment jsdom
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { WorkflowCompositionPanel } from './WorkflowCompositionPanel';
import { compositionParameters, replaceNodeParameters } from './decisionParameters';
import { outputCount } from './editorGeometry';
import type { WorkflowGraph, WorkflowNodeParams } from './workflowTypes';

vi.mock('@/services/workflowService', () => ({ workflowService: {
  list: vi.fn(async () => [{ id: 'self', name: 'Parent', state: 'Active' }, { id: 'body', name: 'Invoice body', state: 'Active' }]),
  listVersions: vi.fn(async () => [{ id: 'draft', tag: 'Draft', isExecutable: false },
    { id: 'v1', tag: 'v1', publishedAt: '2026-11-14T12:00:00Z', definitionHash: 'a'.repeat(64), isExecutable: true },
    { id: 'v2', tag: 'v2', publishedAt: '2026-11-14T13:00:00Z', definitionHash: 'b'.repeat(64), isExecutable: true }]),
} }));

it('retains the reviewed child pin until another published version is explicitly selected', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element); const onChange = vi.fn();
  const parameters = { ...compositionParameters('child'), workflow: { versionId: 'v1', definitionHash: 'a'.repeat(64) } };
  try {
    await act(async () => root.render(<WorkflowCompositionPanel workflowId="self" kind="child" parameters={parameters} onChange={onChange} />));
    const workflow = element.querySelectorAll('select')[0];
    expect([...workflow.options].map(option => option.value)).toEqual(['', 'body']);
    await act(async () => { workflow.value = 'body'; workflow.dispatchEvent(new Event('change', { bubbles: true })); });
    expect(onChange).not.toHaveBeenCalled();
    const version = element.querySelectorAll('select')[1];
    expect(version.value).toBe('v1'); expect([...version.options].map(option => option.value)).not.toContain('draft');
    await act(async () => { version.value = 'v2'; version.dispatchEvent(new Event('change', { bubbles: true })); });
    expect(onChange.mock.lastCall![0].workflow).toEqual({ versionId: 'v2', definitionHash: 'b'.repeat(64) });
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});

it('requires an explicit typed conversion for a legacy loop and removes its expression vocabulary', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element); const onChange = vi.fn();
  try {
    await act(async () => root.render(<WorkflowCompositionPanel workflowId="self" kind="loop" parameters={{ over: 'invoices', maxIterations: 100 }} onChange={onChange} />));
    await act(async () => element.querySelector('button')!.click());
    expect(onChange.mock.lastCall![0]).toEqual(expect.objectContaining({ schemaVersion: 1, items: [], maxIterations: 100, timeoutSeconds: 3600 }));
    expect(onChange.mock.lastCall![0]).not.toHaveProperty('over');
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});

it('keeps control route identity when an exhaustion route changes', () => {
  const parameters = compositionParameters('loop');
  const graph = { schemaVersion: 1, nodes: [{ id: 'loop', kind: 'loop', params: parameters }], edges: [
    { id: 'done', from: 'loop', to: 'end', fromIdx: 0, routeKey: 'completed' },
    { id: 'limit', from: 'loop', to: 'end', fromIdx: 1, routeKey: 'exhausted' },
    { id: 'error', from: 'loop', to: 'end', fromIdx: 2, routeKey: 'error' },
  ] } as unknown as WorkflowGraph;
  const updated = replaceNodeParameters(graph, 'loop', { ...parameters, exhaustedRoute: 'review-limit' });
  expect(updated.schemaVersion).toBe(2); expect(updated.edges.map(edge => edge.id)).toEqual(['done', 'error']);
  expect(outputCount(updated.nodes[0])).toBe(3);
});

it('offers a safe typed replacement for a structurally invalid saved branch draft', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  try {
    await act(async () => root.render(<WorkflowCompositionPanel workflowId="self" kind="parallel" parameters={{ schemaVersion: 1, branches: 'invalid' } as unknown as WorkflowNodeParams} onChange={vi.fn()} />));
    expect(element.querySelector('button')!.textContent).toBe('Configure parallel');
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
