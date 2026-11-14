// @vitest-environment jsdom
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import { WorkflowQualificationPanel } from './WorkflowQualificationPanel';
import type { WorkflowGraph } from './workflowTypes';
const start = vi.hoisted(() => vi.fn(async () => ({ id: 'run-1' })));
vi.mock('@/services/workflowService', () => ({ workflowService: { start } }));
vi.mock('@/services/connectorService', () => ({ connectorError: (reason: Error) => reason.message }));
vi.mock('@/components/ai/chatSupport', () => ({ ServerApprovalCard: () => null }));
vi.mock('./WorkflowTriggerPanel', () => ({ WorkflowTriggerPanel: () => null }));

it('starts the published Document subject even while the saved draft advertises an Invoice trigger', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  const workflow: WorkflowGraph = { id: 'workflow-1', slug: 'intake', name: 'Intake', desc: '', version: 'v2', state: 'Active', ownerColor: '',
    publishedVersionId: 'published-document', nodes: [{ id: 'start', kind: 'trigger', label: 'Draft invoice', x: 0, y: 0, params: { subjectType: 'Invoice' } }], edges: [] };
  try {
    await act(async () => root.render(<MemoryRouter><WorkflowQualificationPanel workflow={workflow} publishedSubjectType="Document" hasChanges={false} onChanged={() => {}} onClose={() => {}} onClearRollback={() => {}} /></MemoryRouter>));
    const input = [...element.querySelectorAll('input')][0];
    const id = '11111111-1111-1111-1111-111111111111';
    await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, id); input.dispatchEvent(new Event('input', { bubbles: true })); });
    const launch = [...element.querySelectorAll('button')].find(button => button.textContent === 'Start live document run');
    await act(async () => launch!.click());
    expect(start).toHaveBeenCalledWith('intake', 'published-document', id, expect.any(String), 'Document');
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
