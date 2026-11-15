// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { WorkflowRunDetail } from '@/services/workflowService';
import { WorkflowRunPage } from './WorkflowRunPage';

const service = vi.hoisted(() => ({ run: vi.fn(), resume: vi.fn(), cancelRun: vi.fn(), decide: vi.fn(), createRequest: vi.fn() }));
vi.mock('@/services/workflowService', () => ({ workflowService: service }));
vi.mock('@/services/connectorService', () => ({ connectorService: service, connectorError: (reason: Error) => reason.message }));
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

async function renderRunPage() {
  await act(async () => root.render(<MemoryRouter initialEntries={['/ai/workflow-runs/run-1']}><Routes><Route path="/ai/workflow-runs/:runId" element={<WorkflowRunPage />} /></Routes></MemoryRouter>));
}

it('requests server-derived source access for a held invoice child without a Gmail tool node', async () => {
  await renderRunPage();
  expect(element.textContent).toContain('Source access is required');
  const request = [...element.querySelectorAll('button')].find(button => button.textContent === 'Request access for the pinned account');
  expect(request).toBeDefined();
  await act(async () => request!.click());
  expect(service.createRequest).toHaveBeenCalledWith(expect.objectContaining({ runId: 'run-1', actions: [] }));
  expect(element.querySelector('a[href="/settings/my-connections/requests/request-1"]')).not.toBeNull();
  expect(service.resume).not.toHaveBeenCalled(); expect(service.decide).not.toHaveBeenCalled();
});

it.each([true, false])('uses the server reviewer capability to gate the invoice approval card (%s)', async (canDecide) => {
  const approval = { id: 'approval-1', actionKind: 'Issue invoice', riskTier: 'Medium', status: 'Pending', canDecide,
    expiresAt: '2026-11-15T06:30:00Z', preview: { title: 'Issue invoice', before: { status: 'Draft' }, after: { status: 'Issued' } } };
  const run: WorkflowRunDetail = {
    id: 'run-1', workflowId: 'invoice-workflow', workflowVersionId: 'invoice-version', definitionHash: 'hash',
    status: 'WaitingForApproval', subjectType: 'Invoice', subjectId: 'invoice-1', actorType: 'User', actorId: 'author-1',
    initiatingUserId: 'author-1', expectedVersion: 'row-1', startedAt: '2026-11-15T06:00:00Z', completedAt: null, outcome: null, errorCode: null,
    steps: [{ id: 'step-1', nodeKey: 'issue', invocationPath: 'root', actionKey: 'finance.invoices.issue', actionVersion: 1, status: 'WaitingForApproval',
      selectedRoute: null, decisionTrace: null, result: null, approvalRequestId: approval.id, proposalId: null, aiRunId: null, errorCode: null, approval }],
  };
  service.run.mockResolvedValue(run);
  const step = run.steps[0];
  service.decide.mockImplementation(async () => {
    service.run.mockResolvedValue({ ...run, status: 'Completed', steps: [{ ...step, status: 'Completed',
      approval: { ...approval, status: 'Approved', canDecide: false } }] });
  });

  await renderRunPage();
  expect(element.textContent).toContain('Review exact changes');
  const approve = [...element.querySelectorAll('button')].find(button => button.textContent === 'Approve');
  expect(approve?.disabled).toBe(!canDecide);
  await act(async () => approve!.click());

  if (canDecide) {
    expect(service.decide).toHaveBeenCalledExactlyOnceWith('approval-1', 'Approve');
    expect(element.textContent).toContain('Issue invoice — Approved');
    expect([...element.querySelectorAll('button')].some(button => button.textContent === 'Approve')).toBe(false);
  } else {
    expect(service.decide).not.toHaveBeenCalled();
  }
});
