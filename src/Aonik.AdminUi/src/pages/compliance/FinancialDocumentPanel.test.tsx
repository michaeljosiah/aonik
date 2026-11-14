// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { FinancialDocumentPanel } from './FinancialDocumentPanel';

const service = vi.hoisted(() => ({ financialExtractions: vi.fn(), financialCandidate: vi.fn(), financialContent: vi.fn() }));
vi.mock('@/services/documentService', () => ({ documentService: service }));
vi.mock('@/services/connectorService', () => ({ connectorError: (reason: Error) => reason.message }));
let element: HTMLDivElement; let root: Root;
beforeEach(() => {
  vi.clearAllMocks(); vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  service.financialExtractions.mockResolvedValue([{ extractionId: 'candidate-1', status: 'NeedsManualReview' }, { extractionId: 'candidate-2', status: 'Ready' }]);
  service.financialCandidate.mockResolvedValue({ extractionId: 'candidate-1', documentId: 'source-1', fileId: 'file-1', status: 'NeedsManualReview',
    fields: { kind: 'SupplierBill', counterpartyName: '<img src=x onerror=alert(1)>', invoiceNumber: 'DEMO-1', confidence: .99, total: 250, subtotal: 250, tax: 0, discount: 0,
      currency: 'GBP', lines: [{ description: 'Consulting', quantity: 2, unitPrice: 125, total: 250 }], evidence: ['TOTAL: 250.00'] }, issues: ['UnsupportedTenantPayable'],
    sourceHash: 'source-hash', candidateHash: 'candidate-hash', aiRunId: 'ai-run-1', modelId: 'route-1' });
  element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element);
});
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
async function render() { await act(async () => root.render(<MemoryRouter><FinancialDocumentPanel documentId="source-1" /></MemoryRouter>)); }
it('shows authorized source evidence and manual payable holds as escaped text without proposing a domain write', async () => {
  await render(); expect(element.textContent).toContain('SupplierBill'); expect(element.textContent).toContain('UnsupportedTenantPayable');
  expect(element.textContent).toContain('<img src=x onerror=alert(1)>'); expect(element.querySelector('img')).toBeNull();
  expect(element.textContent).toContain('source-hash'); expect(element.textContent).toContain('ai-run-1');
  expect(element.textContent).toContain('each require their own workflow approval'); expect(service.financialContent).not.toHaveBeenCalled();
});
it('clears previously displayed private evidence when the next authorized read is refused', async () => {
  await render(); service.financialCandidate.mockRejectedValueOnce(new Error('Source access was revoked'));
  const next = [...element.querySelectorAll('button')].find(button => button.textContent?.includes('Ready'));
  await act(async () => next!.click());
  expect(element.querySelector('[role="alert"]')?.textContent).toBe('Source access was revoked');
  expect(element.textContent).not.toContain('TOTAL: 250.00'); expect(element.textContent).not.toContain('<img');
});
