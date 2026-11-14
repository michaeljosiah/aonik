// @vitest-environment jsdom
import { act, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { DecisionTablePanel } from './DecisionTablePanel';
import { DecisionConditionBuilder } from './DecisionConditionBuilder';
import { WorkflowCompositionPanel } from './WorkflowCompositionPanel';
import { WorkflowDecisionLibraryPanel } from './WorkflowDecisionLibraryPanel';
import { compositionParameters, decisionRoutes, invoiceDecision, replaceNodeParameters } from './decisionParameters';
import type { WorkflowGraph, WorkflowNodeParams } from './workflowTypes';

const fields = [{ name: 'invoice.id', type: 'Id', required: true }, { name: 'invoice.status', type: 'String', required: true }, { name: 'invoice.currency', type: 'Currency', required: true }, { name: 'invoice.hasRiskConcern', type: 'Boolean', required: false }];
vi.mock('@/services/workflowService', () => ({ workflowService: {
  capabilities: vi.fn(async () => ({ invoiceFacts: fields, comparisonOperators: ['eq', 'ne', 'isMissing'] })),
  list: vi.fn(async () => [{ id: 'body', name: 'Effectful workflow', state: 'Active', definitionKind: 'Workflow' }, { id: 'library', name: 'Pure approval library', state: 'Active', definitionKind: 'DecisionLibrary' }]),
  listVersions: vi.fn(async () => ['v1', 'v2'].map((id, index) => ({ id, tag: id, publishedAt: '2026-11-14T12:00:00Z', definitionHash: (index ? 'b' : 'a').repeat(64), isExecutable: true,
    libraryContract: { schemaVersion: 1, inputs: [fields[1], fields[2]], routes: ['review', 'manual'], outputType: 'Route' } }))),
} }));

let element: HTMLDivElement; let root: ReturnType<typeof createRoot>;
beforeEach(() => { vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true); element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element); });
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
const click = async (text: string) => { await act(async () => [...element.querySelectorAll('button')].find(button => button.textContent === text)!.click()); };

it('requires explicit schema adoption and preserves Unknown semantics when enabling richer hit policies', async () => {
  const changed = vi.fn();
  function Editor() { const [parameters, update] = useState<WorkflowNodeParams>(invoiceDecision()); return <DecisionTablePanel parameters={parameters} onChange={value => { update(value); changed(value); }} />; }
  await act(async () => root.render(<Editor />));
  await click('Enable advanced rules');
  expect(changed.mock.lastCall![0]).toEqual(expect.objectContaining({ schemaVersion: 2, optionalFactBehavior: 'Unknown', hitPolicy: 'FirstMatch' }));
  const policy = [...element.querySelectorAll('select')].find(select => select.options[0]?.value === 'FirstMatch')!;
  await act(async () => { policy.value = 'Score'; policy.dispatchEvent(new Event('change', { bubbles: true })); });
  expect(changed.mock.lastCall![0].scoreBands).toEqual([{ minimumInclusive: '-500000', maximumExclusive: '500001', route: 'score' }]);
  expect(element.textContent).toContain('scoring cannot reduce action approval tiers');
  expect(decisionRoutes(changed.mock.lastCall![0])).toEqual(['score', 'manual']);
});

it('keeps the old library pin until an exact new version and declared contract are selected', async () => {
  const changed = vi.fn(); const parameters = { schemaVersion: 1, library: { versionId: 'v1', definitionHash: 'a'.repeat(64) }, inputs: { 'invoice.status': { fact: 'invoice.status' } }, routes: ['review', 'manual'], outputType: 'Route' } as WorkflowNodeParams;
  await act(async () => root.render(<WorkflowDecisionLibraryPanel workflowId="parent" parameters={parameters} onChange={changed} onInline={vi.fn()} />));
  const picker = element.querySelector('select')!;
  expect([...picker.options].map(option => option.value)).toEqual(['', 'library']);
  await act(async () => { picker.value = 'library'; picker.dispatchEvent(new Event('change', { bubbles: true })); });
  expect(changed).not.toHaveBeenCalled();
  const version = element.querySelectorAll('select')[1];
  await act(async () => { version.value = 'v2'; version.dispatchEvent(new Event('change', { bubbles: true })); });
  expect(changed.mock.lastCall![0]).toEqual({ schemaVersion: 1, library: { versionId: 'v2', definitionHash: 'b'.repeat(64) },
    inputs: { 'invoice.status': { fact: 'invoice.status' }, 'invoice.currency': { fact: 'invoice.currency' } }, routes: ['review', 'manual'], outputType: 'Route' });
});

it('bounds condition groups at depth four and keeps Not explicit', async () => {
  const changed = vi.fn();
  await act(async () => root.render(<DecisionConditionBuilder value={{ kind: 'Comparison', comparison: { field: 'invoice.status', operator: 'eq', value: 'Draft' } }} depth={4} canAdd fields={fields.map(field => field.name)} operators={['eq']} onChange={changed} />));
  const type = element.querySelector('select')!;
  expect([...type.options].filter(option => option.value !== 'Comparison').every(option => option.disabled)).toBe(true);
  expect(changed).not.toHaveBeenCalled();
});

it('uses a typed Collect source only after clearing the prior literal collection', async () => {
  const changed = vi.fn(); const parameters = { ...compositionParameters('loop'), items: [{ key: 'old', invoiceId: 'invoice-1' }] };
  await act(async () => root.render(<WorkflowCompositionPanel workflowId="parent" kind="loop" parameters={parameters} onChange={changed} />));
  const source = [...element.querySelectorAll('select')].find(select => [...select.options].some(option => option.value === 'decision'))!;
  await act(async () => { source.value = 'decision'; source.dispatchEvent(new Event('change', { bubbles: true })); });
  expect(changed.mock.lastCall![0]).toEqual(expect.objectContaining({ schemaVersion: 2, items: [], itemsFromDecision: '' }));
});

it('updates library output declarations with the draft table and retains unrelated route identities', () => {
  const graph = { schemaVersion: 2, definitionKind: 'DecisionLibrary', nodes: [{ id: 'start', kind: 'trigger', params: { libraryContract: { schemaVersion: 1, inputs: [], routes: ['standard', 'manual'], outputType: 'Route' } } },
    { id: 'decision', kind: 'decision', params: invoiceDecision() }], edges: [{ id: 'standard', from: 'decision', to: 'end', routeKey: 'standard' }, { id: 'manual', from: 'decision', to: 'end', routeKey: 'manual' }] } as unknown as WorkflowGraph;
  const updated = replaceNodeParameters(graph, 'decision', { ...invoiceDecision(), schemaVersion: 2, optionalFactBehavior: 'Unknown', hitPolicy: 'Collect', collectRoute: 'collected' });
  expect(updated.nodes[0].params.libraryContract).toEqual({ schemaVersion: 1, inputs: [], routes: ['collected', 'manual'], outputType: 'InvoiceList' });
  expect(updated.edges.map(edge => edge.id)).toEqual(['manual']);
});
