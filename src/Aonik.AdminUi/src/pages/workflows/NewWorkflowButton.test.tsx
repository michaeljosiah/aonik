// @vitest-environment jsdom
import { act, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import { NewWorkflowButton } from './NewWorkflowButton';
const create = vi.hoisted(() => vi.fn<(input: unknown) => Promise<{ slug: string }>>().mockResolvedValue({ slug: 'financial-document-intake' }));
vi.mock('@/services/workflowService', () => ({ workflowService: { create } }));
vi.mock('@/services/connectorService', () => ({ connectorError: (reason: Error) => reason.message }));
vi.mock('@/components/ui/dialog', () => { const content = ({ children }: { children: ReactNode }) => <div>{children}</div>; return { Dialog: content, DialogContent: content, DialogHeader: content, DialogTitle: content, DialogTrigger: content }; });
it('creates a document draft using existing extraction and confirmed-creation actions with a manual end', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  try {
    await act(async () => root.render(<MemoryRouter><NewWorkflowButton /></MemoryRouter>));
    const type = element.querySelector('select')!;
    await act(async () => { type.value = 'FinancialIntake'; type.dispatchEvent(new Event('change', { bubbles: true })); });
    const button = [...element.querySelectorAll('button')].find(button => button.textContent === 'Create draft');
    await act(async () => button!.click());
    const input = create.mock.calls[0][0] as { definitionKind: string; schemaVersion: number; nodes: { key: string; paramsJson: string }[] };
    expect(input.definitionKind).toBe('Workflow'); expect(input.schemaVersion).toBe(2);
    expect(JSON.parse(input.nodes.find(node => node.key === 'start')!.paramsJson)).toEqual({ subjectType: 'Document' });
    expect(JSON.parse(input.nodes.find(node => node.key === 'create')!.paramsJson)).toMatchObject({ actionKey: 'finance.invoices.create_from_document', inputs: { customerId: { literal: '' } } });
    expect(JSON.parse(input.nodes.find(node => node.key === 'manual')!.paramsJson)).toEqual({ outcome: 'NeedsManualReview' });
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
