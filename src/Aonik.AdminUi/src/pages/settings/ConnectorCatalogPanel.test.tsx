// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { ConnectorCatalogPanel } from './ConnectorCatalogPanel';

const service = vi.hoisted(() => ({
  releases: vi.fn(), installations: vi.fn(), inspect: vi.fn(), command: vi.fn(), impact: vi.fn(), review: vi.fn(), connect: vi.fn(),
}));
const access = vi.hoisted(() => ({ admin: true }));
vi.mock('@/services/connectorCatalogService', () => ({ connectorCatalogService: service }));
vi.mock('@/services/connectorService', () => ({ connectorError: (reason: Error) => reason.message }));
vi.mock('@/modules/useModules', () => ({ useModules: () => ({ hasPermission: () => true, allowsPolicy: () => access.admin }) }));

const release = { packageKey: 'synthetic-status', packageVersion: '1.0.0', connectorKey: 'synthetic',
  displayName: 'Synthetic status', publisherId: 'test-publisher', digest: 'a'.repeat(64), adapterKind: 'Mcp', actionCount: 2 };
const target = { ...release, packageVersion: '2.0.0', digest: 'b'.repeat(64) };
const installation = { id: 'installation-1', adapterKind: 'Mcp', packageKey: release.packageKey, packageVersion: release.packageVersion,
  digest: release.digest, action: null, approvalState: 'Approved', isActive: true, authConfigured: true, credentialVersion: 2, expectedVersion: 'exact-row-2' };
const read = { action: { key: 'synthetic.records.get', version: 1 }, tier: 'ReadOnly', scopes: [], contractHash: 'old' };
const write = { action: { key: 'synthetic.records.update', version: 2 }, tier: 'High', scopes: [], contractHash: 'new' };

let element: HTMLDivElement;
let root: Root;
beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  access.admin = true;
  service.releases.mockResolvedValue([target, release]);
  service.installations.mockResolvedValue([installation]);
  service.impact.mockResolvedValue({ installation, target, currentActions: [read], targetActions: [write],
    workflows: [{ slug: 'invoice-routing', workflowVersionId: 'version-1', version: 'v1', current: true }],
    runs: [{ runId: 'run-1', workflowVersionId: 'version-1', status: 'WaitingForApproval' }], truncated: false });
  service.command.mockResolvedValue({ ...installation, isActive: false });
  element = document.createElement('div');
  document.body.appendChild(element);
  root = createRoot(element);
});
afterEach(async () => {
  await act(async () => root.unmount());
  element.remove();
  vi.unstubAllGlobals();
});

const button = (label: string) => [...element.querySelectorAll('button')].find(candidate => candidate.textContent === label);
async function click(label: string) {
  expect(button(label)).toBeDefined();
  await act(async () => button(label)!.click());
}

async function render() {
  await act(async () => root.render(<MemoryRouter><ConnectorCatalogPanel availableActions={[]} onAccountConnected={vi.fn()} /></MemoryRouter>));
}

it('compares exact release versions and shows the affected workflow and run before any lifecycle command', async () => {
  await render();
  const select = [...element.querySelectorAll('select')].find(item => Array.from(item.options).some(option => option.value === target.packageVersion))!;
  await act(async () => { select.value = target.packageVersion; select.dispatchEvent(new Event('change', { bubbles: true })); });

  await click('Review workflow impact');

  expect(service.impact).toHaveBeenCalledWith(installation, target.packageVersion);
  expect(element.textContent).toContain('invoice-routing');
  expect(element.textContent).toContain('synthetic.records.update@2 · High');
  expect(element.querySelector('a[href="/ai/workflow-runs/run-1"]')).not.toBeNull();
  expect(service.command).not.toHaveBeenCalled();
});

it('requires a separate confirmation and uses the reviewed row version to disable an installation', async () => {
  await render();
  await click('Disable installation');
  expect(service.command).not.toHaveBeenCalled();
  await click('Confirm disable');
  expect(service.command).toHaveBeenCalledWith(installation, 'disable');
});

it('keeps platform review controls unavailable for a tenant manager without the review policy', async () => {
  access.admin = false;
  service.installations.mockResolvedValue([{ ...installation, approvalState: 'PendingPlatformReview', isActive: false }]);
  await render();
  expect(element.textContent).toContain('PendingPlatformReview');
  expect(button('Approve installation')).toBeUndefined();
  expect(button('Connect service account')).toBeUndefined();
  expect(service.review).not.toHaveBeenCalled();
});
