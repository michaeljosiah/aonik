// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { ConnectorMutationStatusPanel } from './ConnectorMutationStatusPanel';

const service = vi.hoisted(() => ({ mutationStatus: vi.fn(), reconcileMutation: vi.fn() }));
vi.mock('@/services/connectorService', () => ({ connectorService: service, connectorError: (error: Error) => error.message }));
const requestId = '11111111-1111-1111-1111-111111111111';
const held = { approvalRequestId: requestId, action: { key: 'gmail.messages.send', version: 1 }, status: 'NeedsAttention', errorCode: 'ProviderUnavailable', result: null };
let element: HTMLDivElement; let root: Root;
beforeEach(() => {
  vi.clearAllMocks(); vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  service.mutationStatus.mockResolvedValue(held); service.reconcileMutation.mockResolvedValue({ ...held, status: 'Completed', errorCode: null, result: { messageIds: ['sent-1'], reconciled: true } });
  element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element);
});
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
async function click(label: string) {
  const button = [...element.querySelectorAll('button')].find(item => item.textContent === label); expect(button).toBeDefined();
  await act(async () => button!.click());
}
async function lookup() {
  await act(async () => root.render(<ConnectorMutationStatusPanel />));
  const input = element.querySelector('input')!;
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, requestId); input.dispatchEvent(new Event('input', { bubbles: true })); });
  await click('Check recorded status');
}

it('reconciles only the selected existing approval and displays its verified receipt', async () => {
  await lookup();
  expect(element.textContent).toContain('NeedsAttention'); expect(service.reconcileMutation).not.toHaveBeenCalled();
  await click('Reconcile with Gmail');
  expect(service.mutationStatus).toHaveBeenCalledWith(requestId); expect(service.reconcileMutation).toHaveBeenCalledWith(requestId);
  expect(element.textContent).toContain('Completed'); expect(element.textContent).toContain('sent-1');
  expect(element.textContent).not.toContain('Reconcile with Gmail');
});

it('keeps ambiguous provider evidence held and never presents it as delivery', async () => {
  service.reconcileMutation.mockResolvedValue(held);
  await lookup(); await click('Reconcile with Gmail');
  expect(element.textContent).toContain('NeedsAttention'); expect(element.textContent).not.toContain('Completed');
  expect(service.reconcileMutation).toHaveBeenCalledTimes(1);
});

it('clears the receipt when access is revoked', async () => {
  await lookup(); service.reconcileMutation.mockRejectedValue(new Error('Mailbox access was revoked'));
  await click('Reconcile with Gmail');
  expect(element.querySelector('[role="alert"]')?.textContent).toBe('Mailbox access was revoked');
  expect(element.textContent).not.toContain('Recorded provider receipt');
});
