// @vitest-environment jsdom
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import type { AccessRoleSummary } from '@/types';
import { WorkflowToolPanel } from './WorkflowToolPanel';

const roles = vi.hoisted(() => ({ list: vi.fn() }));
vi.mock('@/services/roleService', () => ({ roleService: roles }));
vi.mock('@/services/workflowService', () => ({ workflowService: {
  capabilities: async () => ({ actions: [{ key: 'finance.invoices.issue', contractVersion: 1, riskTier: 'Medium', inputSchema: {} }] }),
} }));
vi.mock('@/services/connectorService', () => ({ connectorService: {}, connectorError: (reason: Error) => reason.message }));

it('requires a named action reviewer to be selected explicitly and preserves separate approval', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const role: AccessRoleSummary = { roleId: 'f56a47c4-adf6-4ee9-b304-533e947fddf2', name: 'Invoice reviewers', permissionCount: 3, userCount: 2 };
  roles.list.mockResolvedValue({ items: [role] });
  const onChange = vi.fn();
  const inputs = { invoiceId: { fact: 'invoice.id' } };
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  try {
    await act(async () => root.render(<MemoryRouter><WorkflowToolPanel workflowId="workflow" parameters={{ actionKey: 'finance.invoices.issue', contractVersion: 1, inputs }} onChange={onChange} /></MemoryRouter>));
    const reviewer = element.querySelector<HTMLSelectElement>('select[aria-label="Action reviewer role"]')!;
    expect([...reviewer.options].map(option => option.text)).toContain('Invoice reviewers');
    expect(reviewer.value).toBe('');
    expect(onChange).not.toHaveBeenCalled();

    await act(async () => { reviewer.value = role.roleId; reviewer.dispatchEvent(new Event('change', { bubbles: true })); });

    expect(onChange).toHaveBeenCalledWith({ actionKey: 'finance.invoices.issue', contractVersion: 1, inputs, reviewer: { roleId: role.roleId, forbidSelfApproval: true } });
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
