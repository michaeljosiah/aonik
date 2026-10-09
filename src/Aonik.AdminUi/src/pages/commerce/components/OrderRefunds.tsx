import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { getSelectedTenant } from '@/lib/tenantContext';
import { commerceRefundService } from '@/services/commerceRefundService';
import type { RefundContextDto, RefundDraft, RefundDto, RefundPreviewDto, RefundRequest } from '@/types/commerceRefunds';
import { readRefundRequest, refundDraft, refundError, refundRequest, refundStatusLabel, refundStorageKey, refundWasRejected, type RefundInputs } from '../lib/refundForm';
import { OrderRefundDialog } from './OrderRefundDialog';
import { OrderRefundHistory } from './OrderRefundHistory';

export function OrderRefunds({ orderId, tenantId, identityScope, canWrite }: {
  orderId: string; tenantId: string; identityScope: string; canWrite: boolean;
}) {
  const [context, setContext] = useState<RefundContextDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [storageError, setStorageError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [inputs, setInputs] = useState<RefundInputs>({});
  const [preview, setPreview] = useState<RefundPreviewDto | null>(null);
  const [previewDraft, setPreviewDraft] = useState<RefundDraft | null>(null);
  const [pending, setPending] = useState<RefundRequest | null>(null);
  const alive = useRef(false);
  const requestAbort = useRef<AbortController | null>(null);
  const storageKey = refundStorageKey(`${identityScope}:${tenantId}`, orderId);
  const config = (signal: AbortSignal) => ({ headers: { 'X-Tenant-Id': tenantId }, signal });
  const current = (signal: AbortSignal) => alive.current && !signal.aborted && getSelectedTenant()?.tenantId === tenantId;

  useEffect(() => {
    alive.current = true;
    const controller = new AbortController();
    requestAbort.current = controller;
    try { setPending(readRefundRequest(sessionStorage.getItem(storageKey))); }
    catch { setStorageError('The saved refund request could not be read. Check refund history before continuing; new requests are blocked in this session.'); }
    commerceRefundService.getContext(orderId, { headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal })
      .then((result) => { if (!controller.signal.aborted && getSelectedTenant()?.tenantId === tenantId) setContext(result); })
      .catch((err: unknown) => { if (!controller.signal.aborted) setError(refundError(err, 'Refunds could not be loaded.')); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => { alive.current = false; requestAbort.current?.abort(); };
  }, [orderId, tenantId, storageKey]);

  function begin(): AbortController | null {
    if (busy || loading || getSelectedTenant()?.tenantId !== tenantId) return null;
    const controller = new AbortController();
    requestAbort.current = controller;
    setBusy(true); setError(null); setNotice(null);
    return controller;
  }

  async function refresh(signal: AbortSignal) {
    try {
      const result = await commerceRefundService.getContext(orderId, config(signal));
      if (current(signal)) setContext(result);
    } catch (err) {
      if (current(signal)) setContext(null);
      throw err;
    }
  }

  function clearPending() {
    try { sessionStorage.removeItem(storageKey); }
    catch { setStorageError('The saved request could not be cleared. Check its recorded status before another request.'); }
    setPending(null);
  }

  function recorded(result: RefundDto) {
    if (pending?.refundId === result.refundId) clearPending();
    setNotice(`Refund request recorded: ${refundStatusLabel(result.status)}. Reference: ${result.refundId}`);
  }

  function changed() { setPreview(null); setPreviewDraft(null); setError(null); }

  async function previewRefund() {
    if (!canWrite || !context?.canRequest || pending || storageError) return;
    const controller = begin();
    if (!controller) return;
    setPreview(null); setPreviewDraft(null);
    try {
      const draft = refundDraft(reason, inputs, context.components);
      const result = await commerceRefundService.preview(orderId, draft, config(controller.signal));
      if (current(controller.signal)) { setPreview(result); setPreviewDraft(draft); }
    } catch (err) { if (current(controller.signal)) setError(refundError(err, 'The refund could not be previewed.')); }
    finally { if (current(controller.signal)) setBusy(false); }
  }

  async function submit(existing?: RefundRequest) {
    if (!canWrite || storageError || (!existing && (!context?.canRequest || !preview || !previewDraft || pending))) return;
    const controller = begin();
    if (!controller) return;
    let request = existing;
    try {
      request ??= refundRequest(previewDraft!, preview!, crypto.randomUUID());
      // Persist before sending: closing this drawer or losing the response must not create
      // a new operation on retry. If browser storage fails, do not send money movement.
      sessionStorage.setItem(storageKey, JSON.stringify(request));
      setPending(request);
    } catch {
      setStorageError('This browser could not retain the refund request for safe retry. This action was not sent.');
      setBusy(false); return;
    }
    let accepted = false;
    try {
      const result = await commerceRefundService.request(orderId, request, config(controller.signal));
      if (!current(controller.signal)) return;
      accepted = true;
      clearPending(); recorded(result); setOpen(false); setReason(''); setInputs({}); changed();
      await refresh(controller.signal);
    } catch (err) {
      if (!current(controller.signal)) return;
      if (accepted) {
        setError('The refund request was recorded, but the latest history could not be loaded. Reload refunds to check it.');
      } else if (refundWasRejected(err)) {
        clearPending(); changed(); setOpen(false);
        setError(refundError(err, 'The request was rejected. Reload and preview the refund again.'));
        try { await refresh(controller.signal); } catch { /* The original rejection remains visible. */ }
      } else {
        setOpen(false);
        setError('The request outcome could not be confirmed. Check its status or retry the same request below.');
      }
    } finally { if (current(controller.signal)) setBusy(false); }
  }

  async function check(refundId?: string, reconcile = false) {
    if (reconcile && !canWrite) return;
    const controller = begin();
    if (!controller) return;
    try {
      if (refundId) {
        const result = reconcile
          ? await commerceRefundService.reconcile(orderId, refundId, config(controller.signal))
          : await commerceRefundService.getRefund(orderId, refundId, config(controller.signal));
        if (!current(controller.signal)) return;
        recorded(result);
      }
      await refresh(controller.signal);
    } catch (err) {
      if (current(controller.signal)) setError(refundError(err, 'The recorded refund status could not be checked. Retry the same request if its outcome is unknown.'));
    } finally { if (current(controller.signal)) setBusy(false); }
  }

  return <section aria-label="Order refunds" className="space-y-3">
    {loading && <p role="status" className="text-sm text-muted-foreground">Loading refunds…</p>}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {storageError && <p role="alert" className="text-sm text-destructive">{storageError}</p>}
    {notice && <p role="status" className="break-words text-sm">{notice}</p>}
    {context && <OrderRefundHistory context={context} canWrite={canWrite} busy={busy}
      onCheck={(id) => void check(id)} onReconcile={(id) => void check(id, true)} />}
    {pending ? <div className="space-y-2 rounded-md border border-border p-3">
      <p className="text-sm">A submitted request needs confirmation. Its original reason and selections are retained.</p>
      <p className="break-all text-xs text-muted-foreground">Reference: {pending.refundId}</p>
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" disabled={busy || loading} onClick={() => void check(pending.refundId)}>Check submitted request</Button>
        {canWrite && <Button disabled={busy || loading || !!storageError} onClick={() => void submit(pending)}>Retry same request</Button>}
      </div>
    </div> : canWrite && context?.canRequest && <Button variant="outline" disabled={busy || loading || !!storageError} onClick={() => { setOpen(true); setError(null); }}>Request refund</Button>}
    {context && !context.canRequest && <p className="text-sm text-muted-foreground">{context.disabledReason ?? 'A new refund cannot be requested for this order.'}</p>}
    {!loading && <Button variant="outline" size="sm" disabled={busy} onClick={() => void check()}>Reload refunds</Button>}
    {open && canWrite && context && !pending && !storageError && <OrderRefundDialog context={context} reason={reason} inputs={inputs} preview={preview} busy={busy} error={error}
      onReason={(value) => { setReason(value); changed(); }}
      onLine={(id, input) => { setInputs((value) => ({ ...value, [id]: input })); changed(); }}
      onSelectAll={() => { setInputs(Object.fromEntries(context.components.filter((line) => line.canRefund).map((line) => [line.componentId, { mode: 'full', amount: '' }]))); changed(); }}
      onPreview={() => void previewRefund()} onConfirm={() => void submit()} onClose={() => setOpen(false)} />}
  </section>;
}
