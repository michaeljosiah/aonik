// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AutomationIdentitiesPanel } from './AutomationIdentitiesPanel';

const service = vi.hoisted(() => ({ list: vi.fn(), usage: vi.fn(), capabilities: vi.fn(), save: vi.fn(), disable: vi.fn() }));
vi.mock('@/services/automationIdentityService', () => ({ automationIdentityService: service }));
vi.mock('@/services/workflowService', () => ({ workflowService: { list: async () => [], listVersions: async () => [] } }));
vi.mock('@/services/connectorService', () => ({ connectorError: (reason: Error) => reason.message }));
const identity = { id: 'identity-1', name: 'Invoice automation', accountableUserId: 'admin-1', status: 'Active', expiresAt: null, expectedVersion: 'row-2',
  grants: [{ workflowVersionId: 'version-1', subjectType: 'Invoice', allSubjects: false, subjectIds: ['invoice-1'], actions: [{ key: 'finance.invoices.issue', version: 1 }] }] };
let element: HTMLDivElement; let root: Root;
beforeEach(() => {
  vi.clearAllMocks(); vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  service.list.mockResolvedValue([identity]); service.usage.mockResolvedValue({ activeRuns: 4, workflowVersionIds: ['version-1'] });
  service.save.mockResolvedValue(identity); service.disable.mockResolvedValue({ ...identity, status: 'Disabled' });
  element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element);
});
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
async function click(label: string) { const button = [...element.querySelectorAll('button')].find(button => button.textContent === label); expect(button).toBeDefined(); await act(async () => button!.click()); }

it('shows affected runs and exact before/after grants before submitting an identity change', async () => {
  await act(async () => root.render(<AutomationIdentitiesPanel />));
  await click('Review changes');
  await click('Review exact grants');
  expect(element.textContent).toContain('4 active runs');
  expect(element.textContent).toContain('Current access');
  expect(element.textContent).toContain('Access after this change');
  expect(service.save).not.toHaveBeenCalled();
  await click('Confirm identity and grants');
  expect(service.save).toHaveBeenCalledWith('identity-1', { name: identity.name, accountableUserId: identity.accountableUserId,
    expiresAt: null, grants: identity.grants, expectedVersion: 'row-2' });
});

it('keeps disable as an explicit review of current usage and the selected revision', async () => {
  await act(async () => root.render(<AutomationIdentitiesPanel />));
  await click('Review disable');
  expect(element.textContent).toContain('4 active runs will be blocked');
  expect(service.disable).not.toHaveBeenCalled();
  await click('Confirm disable');
  expect(service.disable).toHaveBeenCalledWith(identity);
});

it('keeps identity creation unavailable when administrator access is denied', async () => {
  service.list.mockRejectedValue(new Error('Administrator access required'));
  await act(async () => root.render(<AutomationIdentitiesPanel />));
  expect(element.textContent).toContain('Administrator access required');
  expect([...element.querySelectorAll('button')].find(button => button.textContent === 'Create automation identity')!.disabled).toBe(true);
  expect(service.save).not.toHaveBeenCalled();
});
