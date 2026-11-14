// @vitest-environment jsdom
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, it, vi } from 'vitest';
import { WorkflowHumanPanel } from './WorkflowHumanPanel';
import { WorkflowWaitPanel } from './WorkflowWaitPanel';
import { humanTaskParameters, replaceNodeParameters } from './decisionParameters';
import type { WorkflowGraph } from './workflowTypes';

describe('typed workflow controls', () => {
  it('requires explicit conversion of a legacy human step and selects one reviewer authority', async () => {
    vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
    const onChange = vi.fn();
    const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
    try {
      await act(async () => root.render(<WorkflowHumanPanel parameters={{ group: 'Anyone' }} onChange={onChange} />));
      await act(async () => element.querySelector('button')!.click());
      expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ schemaVersion: 1, choices: expect.any(Array) }));
      await act(async () => root.render(<WorkflowHumanPanel parameters={humanTaskParameters()} onChange={onChange} />));
      await act(async () => { const select = element.querySelector('select')!; select.value = 'user'; select.dispatchEvent(new Event('change', { bubbles: true })); });
      expect(onChange.mock.lastCall![0].reviewer).toEqual({ userId: '', forbidSelfApproval: true });
    } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
  });

  it('promotes human drafts and retains outgoing route identity when choice order changes', () => {
    const parameters = humanTaskParameters();
    const graph = { schemaVersion: 1, nodes: [{ id: 'human', kind: 'human', params: parameters }], edges: [
      { id: 'yes', from: 'human', to: 'end', fromIdx: 0, routeKey: 'responded' },
      { id: 'no', from: 'human', to: 'end', fromIdx: 1, routeKey: 'rejected' },
    ] } as unknown as WorkflowGraph;
    const updated = replaceNodeParameters(graph, 'human', { ...parameters, choices: [{ key: 'hold', label: 'Hold', route: 'hold' }, ...parameters.choices!] });
    expect(updated.schemaVersion).toBe(2);
    expect(updated.edges.map(edge => [edge.routeKey, edge.fromIdx])).toEqual([['responded', 1], ['rejected', 2]]);
  });

  it('replaces duration with one fixed UTC deadline without retaining both modes', async () => {
    vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
    const onChange = vi.fn();
    const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
    try {
      await act(async () => root.render(<WorkflowWaitPanel parameters={{ schemaVersion: 1, delaySeconds: 120 }} onChange={onChange} />));
      await act(async () => { const select = element.querySelector('select')!; select.value = 'until'; select.dispatchEvent(new Event('change', { bubbles: true })); });
      expect(onChange.mock.lastCall![0]).toEqual({ schemaVersion: 1, delaySeconds: null, untilUtc: expect.stringMatching(/Z$/) });
    } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
  });
});
