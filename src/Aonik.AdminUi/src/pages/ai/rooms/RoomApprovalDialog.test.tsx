// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { RoomApprovalDialog } from './RoomApprovalDialog';
import type { RoomApproval } from '@/services/chatRoomService';

const service = vi.hoisted(() => ({ mutationReview: vi.fn() }));
vi.mock('@/services/connectorService', () => ({ connectorService: service, connectorError: (error: Error) => error.message }));
const approval: RoomApproval = { id: 'approval-1', proposalId: 'proposal-1', tool: 'gmail_send_message', tier: 'High', actionKind: 'Send reviewed mail',
  status: 'Pending', argumentsJson: '{"messageReference":"private-reference"}', canDecide: true, canContinue: false };
let element: HTMLDivElement; let root: Root;
beforeEach(() => { vi.clearAllMocks(); vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true); element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element); });
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
function confirm() { return [...document.querySelectorAll('button')].find(button => button.textContent === 'Confirm approve')!; }

it('loads the private reviewed effect before enabling a room Gmail confirmation', async () => {
  let resolve: (value: unknown) => void = () => {};
  service.mutationReview.mockReturnValue(new Promise(value => { resolve = value; }));
  const decide = vi.fn();
  await act(async () => root.render(<RoomApprovalDialog approval={approval} choice="Approve" disabled={false} busy={false} error="" onClose={() => {}} onConfirm={decide} />));
  expect(confirm().disabled).toBe(true);
  await act(async () => resolve({ title: 'Send exact message', before: null, after: { bcc: ['private@example.test'], body: '<script>mail()</script>' } }));
  expect(service.mutationReview).toHaveBeenCalledWith('approval-1'); expect(confirm().disabled).toBe(false);
  expect(document.body.textContent).toContain('private@example.test'); expect(document.querySelector('script')).toBeNull();
  await act(async () => confirm().click()); expect(decide).toHaveBeenCalledTimes(1);
});

it('keeps confirmation unavailable when mailbox access is refused', async () => {
  service.mutationReview.mockRejectedValue(new Error('Mailbox access required'));
  await act(async () => root.render(<RoomApprovalDialog approval={approval} choice="Approve" disabled={false} busy={false} error="" onClose={() => {}} onConfirm={() => {}} />));
  expect(confirm().disabled).toBe(true); expect(document.querySelector('[role="alert"]')?.textContent).toBe('Mailbox access required');
});
