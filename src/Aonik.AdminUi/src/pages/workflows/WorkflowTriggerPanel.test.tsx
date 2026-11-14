// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { WorkflowTriggerPanel } from './WorkflowTriggerPanel';

const triggers = vi.hoisted(() => ({ list: vi.fn(), request: vi.fn(), validate: vi.fn(), save: vi.fn(), delete: vi.fn() }));
const decide = vi.hoisted(() => vi.fn());
const requestConnection = vi.hoisted(() => vi.fn());
vi.mock('@/services/workflowTriggerService', () => ({ workflowTriggerService: triggers }));
vi.mock('@/services/workflowService', () => ({ workflowService: { listVersions: async () => [{ id: 'version-1', tag: 'v1', publishedAt: '2026-11-14' }], decide } }));
vi.mock('@/services/automationIdentityService', () => ({ automationIdentityService: { list: async () => [{ id: 'actor-1', name: 'Invoice worker', status: 'Active', grants: [{ workflowVersionId: 'version-1' }] }] } }));
vi.mock('@/services/connectorService', () => ({ connectorService: { connections: async () => [], createRequest: requestConnection }, connectorError: (reason: Error) => reason.message }));
vi.mock('@/components/ai/chatSupport', () => ({ ServerApprovalCard: ({ approval, onDecide }: { approval: { approvalRequestId: string; preview: { title: string } }; onDecide: (approval: unknown, decision: string) => void }) => <div>{approval.preview.title}<button onClick={() => onDecide(approval, 'Approve')}>Approve exact trigger</button></div> }));
const definition = { schemaVersion: 1, source: 'InvoiceCreated', workflowVersionId: 'version-1', automationIdentityId: 'actor-1',
  subject: { type: 'Invoice', mapping: 'EventSubject', fixedIds: [] }, limits: { maximumStartsPerHour: 10, maximumConcurrentRuns: 1, maximumCausationDepth: 4, ignoreSelfGenerated: true }, schedule: null, mailbox: null };
const current = { id: 'binding-1', workflowId: 'workflow-1', name: 'Invoice routing', status: 'Active', draft: definition, active: definition, activeRevision: 1, draftRevision: 2,
  activeHash: 'active-hash', draftHash: 'draft-hash', admittedRuns: 3, lastError: null, expectedVersion: 'row-2' };
let element: HTMLDivElement; let root: Root;
beforeEach(() => {
  vi.clearAllMocks(); vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  triggers.list.mockResolvedValue([current]); triggers.validate.mockResolvedValue({ diagnostics: [] }); triggers.save.mockResolvedValue(current);
  triggers.request.mockResolvedValue({ proposalId: 'proposal-1', approvalRequestId: 'approval-1', preview: { title: 'Review the exact pinned configuration' } });
  decide.mockResolvedValue({}); element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element);
  requestConnection.mockResolvedValue({ reviewPath: '/settings/my-connections/requests/request-1' });
});
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
async function render() { await act(async () => root.render(<MemoryRouter><WorkflowTriggerPanel slug="invoice-route" workflowId="workflow-1" publishedVersionId="version-1" /></MemoryRouter>)); }
async function click(text: string) { const button = [...element.querySelectorAll('button')].find(value => value.textContent === text); expect(button).toBeDefined(); await act(async () => button!.click()); }

it('requests the selected revision and shows its server preview before an explicit activation decision', async () => {
  await render(); await click('Request activation');
  expect(triggers.request).toHaveBeenCalledWith('invoice-route', current, false);
  expect(element.textContent).toContain('Review the exact pinned configuration'); expect(decide).not.toHaveBeenCalled();
  await click('Approve exact trigger'); expect(decide).toHaveBeenCalledWith('approval-1', 'Approve');
});

it('saves only the reviewed draft and leaves activation as a separate request', async () => {
  await render(); await click('Edit draft'); await click('Review exact draft'); expect(triggers.save).not.toHaveBeenCalled();
  await click('Confirm draft save'); expect(triggers.save).toHaveBeenCalledWith('invoice-route', current, 'Invoice routing', definition);
  expect(triggers.request).not.toHaveBeenCalled(); expect(decide).not.toHaveBeenCalled();
});

it('uses a separate high-risk request to disable the exact active revision', async () => {
  await render(); await click('Request disable'); expect(triggers.request).toHaveBeenCalledWith('invoice-route', current, true); expect(decide).not.toHaveBeenCalled();
});

it('requests all three explicit read actions for attachment intake and saves a document mapping with no invoice IDs', async () => {
  const mailbox = { connectionId: '', workflowGrantId: '', labelIds: ['INBOX'], mode: 'Polling', pollIntervalMinutes: 15, allowPollingFallback: false, backfillSinceUtc: null, subjectPrefix: 'Qualification:' };
  const documentDefinition = { ...definition, source: 'GmailMessageAdded', subject: { type: 'Document', mapping: 'MessageAttachments', fixedIds: [] }, mailbox };
  const documentTrigger = { ...current, draft: documentDefinition };
  triggers.list.mockResolvedValue([documentTrigger]);
  await render(); await click('Edit draft');
  expect(element.textContent).toContain('Optional exact subject prefix'); expect(element.textContent).not.toContain('One stored invoice ID for this mailbox route');
  await click('Request missing connection access');
  expect(requestConnection).toHaveBeenCalledWith(expect.objectContaining({ workflowId: 'workflow-1', actions: [
    { key: 'gmail.messages.search', version: 1 }, { key: 'gmail.messages.get', version: 1 }, { key: 'gmail.attachments.get', version: 1 },
  ] }));
  await click('Review exact draft'); await click('Confirm draft save');
  expect(triggers.save).toHaveBeenCalledWith('invoice-route', documentTrigger, 'Invoice routing', documentDefinition);
});
