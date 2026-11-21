// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { AccessRoleSummary } from '@/types';
import { MyConnectionsPage, TenantIntegrationsPage } from './ConnectionsPage';

const service = vi.hoisted(() => ({ catalog: vi.fn(), managed: vi.fn(), connections: vi.fn(), requests: vi.fn(), createRequest: vi.fn() }));
const settings = vi.hoisted(() => ({ get: vi.fn(), put: vi.fn() }));
const roles = vi.hoisted(() => ({ list: vi.fn() }));
vi.mock('@/services/connectorService', () => ({ connectorService: service, connectorError: (reason: Error) => reason.message, consentUrl: (value: string) => value }));
vi.mock('@/lib/api', () => ({ api: settings }));
vi.mock('@/services/roleService', () => ({ roleService: roles }));
vi.mock('./AutomationIdentitiesPanel', () => ({ AutomationIdentitiesPanel: () => null }));
vi.mock('./WorkflowExecutionPolicyPanel', () => ({ WorkflowExecutionPolicyPanel: () => null }));
vi.mock('./ConnectorCatalogPanel', () => ({ ConnectorCatalogPanel: () => null }));
vi.mock('./ConnectorMutationStatusPanel', () => ({ ConnectorMutationStatusPanel: () => null }));

const reviewerRoleId = 'f56a47c4-adf6-4ee9-b304-533e947fddf2';
const reviewerRole: AccessRoleSummary = { roleId: reviewerRoleId, name: 'TenantAdmin', permissionCount: 31, userCount: 1 };
let element: HTMLDivElement; let root: Root;
beforeEach(() => {
  vi.clearAllMocks(); vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  service.catalog.mockResolvedValue([{ key: 'gmail', displayName: 'Gmail', actions: [{ key: 'gmail.messages.search', version: 1 }] }]);
  service.managed.mockResolvedValue([]); service.connections.mockResolvedValue([]); service.requests.mockResolvedValue([]);
  settings.get.mockResolvedValue({ value: null }); settings.put.mockResolvedValue({});
  roles.list.mockResolvedValue({ items: [reviewerRole] });
  element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element);
});
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
async function render(tenant = false) {
  await act(async () => root.render(<MemoryRouter>{tenant ? <TenantIntegrationsPage /> : <MyConnectionsPage />}</MemoryRouter>));
}
function button(label: string) {
  const result = [...element.querySelectorAll('button')].find(item => item.textContent === label);
  expect(result).toBeDefined(); return result!;
}
async function check(label: string) {
  const input = [...element.querySelectorAll<HTMLButtonElement>('button[role="checkbox"]')].find(item => item.closest('label')?.textContent?.includes(label));
  expect(input).toBeDefined(); await act(async () => input!.click());
}

it('focuses a failed request explanation and retains the selected actions', async () => {
  service.createRequest.mockRejectedValue(new Error('Current platform or tenant policy denies this connector use.'));
  await render(); await check('gmail.messages.search');
  await act(async () => button('Submit access request').click());
  const alert = element.querySelector('[role="alert"]');
  expect(alert?.textContent).toContain('platform or tenant policy');
  expect(document.activeElement).toBe(alert);
  expect(element.querySelector('button[role="checkbox"]')?.getAttribute('data-state')).toBe('checked');
  expect(service.createRequest).toHaveBeenCalledTimes(1);
  expect(settings.put).not.toHaveBeenCalled();
});

it('requires an explicit named reviewer for reviewed connection requests and saves its ID', async () => {
  await render(true); await check('Enable Gmail');
  expect(button('Save tenant policy').disabled).toBe(true);
  expect(element.textContent).toContain('Choose a reviewer role for connection requests');
  const reviewer = [...element.querySelectorAll('select')].find(item => [...item.options].some(option => option.text === 'TenantAdmin'));
  expect(reviewer).toBeDefined();
  expect(reviewer!.value).toBe('');
  expect(settings.put).not.toHaveBeenCalled();
  await act(async () => { reviewer!.value = reviewerRoleId; reviewer!.dispatchEvent(new Event('change', { bubbles: true })); });
  await act(async () => button('Save tenant policy').click());
  expect(settings.put).toHaveBeenCalledWith('/tenant/settings/values/Integrations.RequestReviewerRoleId', { value: reviewerRoleId });
  expect(settings.put).toHaveBeenCalledWith('/tenant/settings/values/Integrations.Gmail.Enabled', { value: 'true' });
});

it('preserves the current reviewer when role-name access is unavailable', async () => {
  roles.list.mockRejectedValue(new Error('Role read access denied'));
  settings.get.mockImplementation(async (path: string) => ({ value: path.endsWith('Integrations.RequestReviewerRoleId') ? reviewerRoleId : null }));
  await render(true);
  const reviewer = element.querySelector<HTMLInputElement>('input[aria-label="Integration reviewer role ID"]');
  expect(reviewer?.value).toBe(reviewerRoleId);
  expect(element.textContent).toContain('Role names could not be loaded');
  expect(settings.put).not.toHaveBeenCalled();
});
