import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { documentService, type FinancialDocumentCandidate, type FinancialExtractionReceipt } from '@/services/documentService';
import { connectorError } from '@/services/connectorService';

export function FinancialDocumentPanel({ documentId }: { documentId: string }) {
  const [receipts, setReceipts] = useState<FinancialExtractionReceipt[]>([]);
  const [candidate, setCandidate] = useState<FinancialDocumentCandidate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const controller = new AbortController(); setReceipts([]); setCandidate(null); setError(null); setLoading(true);
    void documentService.financialExtractions(documentId, controller.signal).then(async values => {
      if (controller.signal.aborted) return;
      setReceipts(values);
      if (values[0]) {
        const value = await documentService.financialCandidate(values[0].extractionId, controller.signal);
        if (!controller.signal.aborted) setCandidate(value);
      }
    }).catch(reason => { if (!controller.signal.aborted) { setCandidate(null); setError(connectorError(reason)); } })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [documentId]);
  const act = async (operation: () => Promise<void>) => {
    setBusy(true); setError(null);
    try { await operation(); } catch (reason) { setCandidate(null); setError(connectorError(reason)); } finally { setBusy(false); }
  };
  return <Card><CardHeader><CardTitle>Financial source review</CardTitle></CardHeader><CardContent className="space-y-4 text-sm">
    <p>Extracted evidence needs human review. Sales-invoice creation and payment initiation each require their own workflow approval. Supplier bills stay in manual review.</p>
    {error && <p role="alert" className="text-destructive">{error}</p>}
    {loading && <p role="status">Loading the recorded extraction…</p>}
    {!loading && !error && receipts.length === 0 && <p className="text-muted-foreground">No extraction is recorded yet. Start the published document-intake workflow to prepare a candidate.</p>}
    {receipts.length > 1 && <div className="flex flex-wrap gap-2">{receipts.map(receipt => <Button key={receipt.extractionId} size="sm" variant="outline" disabled={busy} onClick={() => void act(async () => setCandidate(await documentService.financialCandidate(receipt.extractionId)))}>{receipt.status} · {receipt.extractionId.slice(0, 8)}</Button>)}</div>}
    {candidate && <>
      <p className="font-medium">{candidate.status} · {candidate.fields.kind} · {Math.round(candidate.fields.confidence * 100)}% extraction confidence</p>
      <dl className="grid gap-2 sm:grid-cols-2"><div><dt className="text-muted-foreground">Invoice reference</dt><dd>{candidate.fields.invoiceNumber}</dd></div><div><dt className="text-muted-foreground">Counterparty</dt><dd>{candidate.fields.counterpartyName}</dd></div><div><dt className="text-muted-foreground">Total</dt><dd>{candidate.fields.total} {candidate.fields.currency}</dd></div><div><dt className="text-muted-foreground">Due</dt><dd>{candidate.fields.dueUtc ? new Date(candidate.fields.dueUtc).toLocaleDateString() : 'Not established'}</dd></div></dl>
      {candidate.issues.length > 0 && <div role="status"><p className="font-medium">Review required</p><ul className="list-disc space-y-1 pl-5">{candidate.issues.map((issue, index) => <li key={index}>{issue}</li>)}</ul></div>}
      <Table><TableHeader><TableRow><TableHead>Description</TableHead><TableHead numeric>Quantity</TableHead><TableHead numeric>Unit price</TableHead><TableHead numeric>Total</TableHead></TableRow></TableHeader><TableBody>{candidate.fields.lines.map((line, index) => <TableRow key={index}><TableCell>{line.description}</TableCell><TableCell numeric>{line.quantity}</TableCell><TableCell numeric>{line.unitPrice}</TableCell><TableCell numeric>{line.total}</TableCell></TableRow>)}</TableBody></Table>
      <p>Subtotal {candidate.fields.subtotal} · Tax {candidate.fields.tax} · Discount {candidate.fields.discount}</p>
      <Button variant="outline" disabled={busy} onClick={() => void act(async () => {
        const bytes = await documentService.financialContent(documentId, candidate.fileId);
        const extension = ({ 'application/pdf': 'pdf', 'image/png': 'png', 'image/jpeg': 'jpg', 'text/plain': 'txt' } as Record<string, string>)[bytes.type] ?? 'bin';
        const url = URL.createObjectURL(bytes); const download = document.createElement('a'); download.href = url; download.download = `financial-source-${candidate.fileId}.${extension}`;
        try { download.click(); } finally { URL.revokeObjectURL(url); }
      })}>Download private source for review</Button>
      <details><summary className="cursor-pointer text-muted-foreground">Evidence and provenance</summary><pre className="mt-2 max-h-80 overflow-auto whitespace-pre-wrap break-all rounded-md border p-3 text-xs">{JSON.stringify({ evidence: candidate.fields.evidence, sourceHash: candidate.sourceHash, candidateHash: candidate.candidateHash, aiRunId: candidate.aiRunId, modelId: candidate.modelId, promptHash: candidate.promptHash, extractor: candidate.extractor, source: candidate.source }, null, 2)}</pre></details>
    </>}
    <Link to="/approvals" className="inline-block text-primary underline">Open pending approvals</Link>
  </CardContent></Card>;
}
