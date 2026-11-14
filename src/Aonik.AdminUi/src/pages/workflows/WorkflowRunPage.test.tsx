// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { WorkflowRunPage } from './WorkflowRunPage';

const service = vi.hoisted(() => ({ run: vi.fn(), resume: vi.fn(), cancelRun: vi.fn(), decide: vi.fn(), createRequest: vi.fn() }));
vi.mock('@/services/workflowService', () => ({ workflowService: service }));
vi.mock('@/services/connectorService', () => ({ connectorService: service, connectorError: (reason: Error) => reason.message }));
vi.mock('@/components/ai/chatSupport', () => ({ ServerApprovalCard: () => <div>Approval</div> }));
let element: HTMLDivElement; let root: Root;
beforeEach(() => {
  vi.clearAllMocks(); vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element);
  service.run.mockResolvedValue({ id: 'run-1', workflowId: 'child-workflow', workflowVersionId: 'child-version',
    definitionHash: 'hash', status: 'WaitingForConnection', subjectType: 'Invoice', subjectId: 'invoice-1',
    actorType: 'AutomationIdentity', actorId: 'machine-1', expectedVersion: 'row-1', evidenceRestricted: true,
    steps: [{ id: 'step-1', nodeKey: 'issue', actionKey: 'finance.invoices.issue', actionVersion: 1, status: 'WaitingForApproval' }] });
  service.createRequest.mockResolvedValue({ reviewPath: '/settings/my-connections/requests/request-1' });
});
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });

it('requests server-derived source access for a held invoice child without a Gmail tool node', async () => {
  await act(async () => root.render(<MemoryRouter initialEntries={['/ai/workflow-runs/run-1']}><Routes><Route path="/ai/workflow-runs/:runId" element={<WorkflowRunPage />} /></Routes></MemoryRouter>));
  expect(element.textContent).toContain('Source access is required');
  const request = [...element.querySelectorAll('button')].find(button => button.textContent === 'Request access for the pinned account');
  expect(request).toBeDefined();
  await act(async () => request!.click());
  expect(service.createRequest).toHaveBeenCalledWith(expect.objectContaining({ runId: 'run-1', actions: [] }));
  expect(element.querySelector('a[href="/settings/my-connections/requests/request-1"]')).not.toBeNull();
  expect(service.resume).not.toHaveBeenCalled(); expect(service.decide).not.toHaveBeenCalled();
});
