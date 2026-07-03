/* API mapping (Spec 058 §8.5 — landed endpoints):
   UI action                       Landed API
   List / create / from-shortfall  GET/POST /commerce/admin/purchase-orders  ·  POST /commerce/admin/purchase-orders/from-shortfall   (053)
   Submit / cancel                 POST /commerce/admin/purchase-orders/{id}/submit  ·  POST /commerce/admin/purchase-orders/{id}/cancel   (053)
*/
// Commerce · Make · Spec 058 §8.5 — Purchase orders
// ScreenCommercePurchaseOrders — raw-material sourcing on the Order spine.
// Landed codes ONLY: Draft | Pending | Complete | Cancelled — submit lands on
// Pending ("submitted to supplier"); there is NO Submitted/Received status.
// Partial receipt is the DERIVED received-vs-ordered progress bar
// (ProgressCells), never a status. The Cancelled PO-2026-0109 drawer shows
// the compare-and-set guard: a stale submit is rejected with a conflict.
// Creating a PO is either explicit lines or FROM SHORTFALL — seeded from the
// active low-stock alerts, pack-rounded to the supplier pack, min one pack.
// Data: CM_POS / CM_PO_STATUS / CM_RECEIPTS / CM_ALERTS / CM_SUPPLIERS. Mock-only.

const cmPoReceiptById = id => CM_RECEIPTS.find(r => r.id === id);
const cmPoSums = po => po.lines.reduce((a, l) => ({ recv: a.recv + (l.received || 0), qty: a.qty + l.qty }), { recv: 0, qty: 0 });
const cmPoUnit = po => { const u = [...new Set(po.lines.map(l => l.unit))]; return u.length === 1 ? u[0] : 'units'; };

function ScreenCommercePurchaseOrders() {
  const [status, setStatus] = React.useState('all');
  const [sel, setSel] = React.useState(CM_POS.find(p => p.id === 'po_0112'));   // partially-received Pending — the must-show state
  const [createOpen, setCreateOpen] = React.useState(false);

  const shown = status === 'all' ? CM_POS : CM_POS.filter(p => p.status === status);
  const pending = CM_POS.filter(p => p.status === 'pending');
  const committed = pending.reduce((a, p) => a + p.total, 0);
  const kpis = [
    { l: 'Open POs', v: CM_POS.filter(p => p.status === 'draft' || p.status === 'pending').length, s: 'draft + pending' },
    { l: 'Awaiting receipt', v: pending.length, s: 'submitted to supplier', warn: true },
    { l: 'Committed value', v: cmMoney(committed), s: 'pending POs, NGN' },
    { l: 'Cancelled (30d)', v: CM_POS.filter(p => p.status === 'cancelled').length, s: 'compare-and-set guarded' },
  ];

  const chips = ['all', 'draft', 'pending', 'complete', 'cancelled'].map(id => ({
    id,
    label: id === 'all' ? 'All' : CM_PO_STATUS[id].label,
    hint: id === 'all' ? null : CM_PO_STATUS[id].hint,
    n: id === 'all' ? CM_POS.length : CM_POS.filter(p => p.status === id).length,
  }));

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-porow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Purchase orders</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Raw-material sourcing on the Order spine. Draft, Pending (submitted to supplier), Complete, Cancelled — partial receipt is a derived progress bar, never a status.</div>
          </div>
          <button className="btn btn-primary btn-sm" onClick={() => setCreateOpen(v => !v)}><Icon name="plus" size={12} /> New purchase order</button>
        </div>

        {/* KPIs */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: k.warn && k.v > 0 ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        {/* Status filter — exactly the landed codes */}
        <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, alignSelf: 'flex-start', alignItems: 'stretch' }}>
          {chips.map(c => {
            const on = status === c.id;
            return (
              <button key={c.id} onClick={() => setStatus(c.id)} style={{
                display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1,
                minHeight: 30, padding: c.hint ? '4px 14px' : '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none',
                fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent',
                color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? 'var(--shadow-sm)' : 'none',
              }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}>
                  {c.label}
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: on ? 'var(--brand-primary)' : 'var(--text-tertiary)' }}>{c.n}</span>
                </span>
                {c.hint && <span style={{ fontSize: 9, color: 'var(--text-tertiary)', fontWeight: 500 }}>{c.hint}</span>}
              </button>
            );
          })}
        </div>

        {/* PO table */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '118px 1fr 52px 100px 108px 190px 96px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Ref</div><div>Supplier</div><div style={{ textAlign: 'right' }}>Lines</div><div style={{ textAlign: 'right' }}>Total</div><div>Status</div><div>Received</div><div style={{ textAlign: 'right' }}>Created</div>
          </div>
          {shown.map((p, i) => {
            const { recv, qty } = cmPoSums(p);
            const unit = cmPoUnit(p);
            const receivable = p.status === 'pending' || p.status === 'complete';
            return (
              <div key={p.id} className="cm-porow" onClick={() => setSel(p)} style={{ display: 'grid', gridTemplateColumns: '118px 1fr 52px 100px 108px 190px 96px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < shown.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: p.status === 'cancelled' ? 0.65 : 1 }}>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', fontWeight: 500 }}>{p.ref}</div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                  <Avatar name={p.supplierName} size={24} />
                  <div>
                    <div style={{ color: 'var(--text-primary)' }}>{p.supplierName}</div>
                    <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{p.provenance === 'from-shortfall' ? 'from low-stock shortfall' : 'manual'}</div>
                  </div>
                </div>
                <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{p.lines.length}</div>
                <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(p.total, p.ccy)}</div>
                <div><Pill tone={CM_PO_STATUS[p.status].tone} dot size="sm">{CM_PO_STATUS[p.status].label}</Pill></div>
                <div>
                  {receivable
                    ? <ProgressCells value={recv} max={qty} tone={recv >= qty ? 'success' : 'warning'} caption={recv + ' / ' + qty + ' ' + unit} />
                    : <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{p.status === 'draft' ? '— not submitted' : '—'}</span>}
                </div>
                <div style={{ textAlign: 'right', fontSize: 11.5, color: 'var(--text-tertiary)' }}>{p.createdAt}</div>
              </div>
            );
          })}
        </div>

        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="truck" size={12} color="var(--brand-primary)" /> Received progress is derived from posted receipts — a partially-received PO stays Pending until everything ordered has arrived.
        </div>
      </div>

      {createOpen && <CmPoCreatePanel onClose={() => setCreateOpen(false)} />}
      {sel && <CmPoDrawer po={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

// Lifecycle stepper — EVENTS on the landed statuses, not invented codes.
function CmPoStepper({ po }) {
  const steps = ['Created', 'Submitted', 'Received', 'Complete'];
  const halted = po.status === 'cancelled';
  const completed = po.status === 'draft' ? 1 : po.status === 'pending' ? 2 : po.status === 'complete' ? 4 : 1;
  const { recv, qty } = cmPoSums(po);
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 0 }}>
        {steps.map((s, i) => {
          const isDone = i < completed && !halted, isCur = i === completed && !halted;
          return (
            <React.Fragment key={s}>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 5, flex: 'none' }}>
                <span style={{ width: 20, height: 20, borderRadius: 999, display: 'grid', placeItems: 'center', fontSize: 10, fontWeight: 700, background: isDone || isCur ? 'var(--brand-primary)' : 'var(--surface-inset)', color: isDone || isCur ? 'var(--text-inverse)' : 'var(--text-tertiary)', border: isDone || isCur ? 'none' : '1px solid var(--border-light)' }}>
                  {isDone ? <Icon name="check" size={11} color="var(--text-inverse)" /> : i + 1}
                </span>
                <span style={{ fontSize: 9.5, color: isDone || isCur ? 'var(--text-primary)' : 'var(--text-tertiary)', fontWeight: isCur ? 600 : 500 }}>{s}</span>
              </div>
              {i < steps.length - 1 && <div style={{ flex: 1, height: 2, background: i < completed && !halted ? 'var(--brand-primary)' : 'var(--border-light)', margin: '0 2px', marginBottom: 16 }} />}
            </React.Fragment>
          );
        })}
      </div>
      {po.status === 'pending' && recv > 0 && (
        <div style={{ marginTop: 10, fontSize: 11.5, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="hourglass" size={12} /> Receiving in progress — {recv} of {qty} {cmPoUnit(po)} received. The PO stays Pending; the bar carries the partial.
        </div>
      )}
      {halted && (
        <div style={{ marginTop: 10, fontSize: 11.5, color: 'var(--danger)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="ban" size={12} /> Cancelled {po.cancelledAt} — nothing was received against this PO.
        </div>
      )}
    </div>
  );
}

function CmPoDrawer({ po, onClose }) {
  const { recv, qty } = cmPoSums(po);
  return (
    <React.Fragment>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 560, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: 'var(--shadow-lg)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        {/* Drawer header */}
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'flex-start', gap: 12 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{po.ref}</span>
              <Pill tone={CM_PO_STATUS[po.status].tone} dot size="sm">{CM_PO_STATUS[po.status].label}</Pill>
              {CM_PO_STATUS[po.status].hint && <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{CM_PO_STATUS[po.status].hint}</span>}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 3 }}>
              {po.supplierName} — created {po.createdAt} by {po.createdBy}
              {po.submittedAt && <span>, submitted {po.submittedAt}</span>}
              {po.expectedBy && <span>, expected {po.expectedBy}</span>}
              {po.completedAt && <span>, completed {po.completedAt}</span>}
            </div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Compare-and-set conflict — stale submit rejected (PO-2026-0109) */}
          {po.staleSubmit && (
            <div style={{ display: 'flex', gap: 10, padding: '12px 14px', borderRadius: 10, background: 'var(--danger-light)', borderLeft: '3px solid var(--danger)', alignItems: 'flex-start' }}>
              <Icon name="ban" size={15} color="var(--danger)" style={{ flex: 'none', marginTop: 1 }} />
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 12.5, fontWeight: 700, color: 'var(--danger)' }}>Conflict — stale submit rejected</div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5, marginTop: 2 }}>{po.staleSubmit.message}</div>
              </div>
              <button className="btn btn-outline btn-sm" style={{ flex: 'none' }}><Icon name="refresh" size={12} /> Reload</button>
            </div>
          )}

          {/* Cancellation reason */}
          {po.cancelReason && (
            <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--surface-inset)', borderLeft: '3px solid var(--border)' }}>
              <Icon name="ban" size={14} color="var(--text-tertiary)" style={{ flex: 'none', marginTop: 1 }} />
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>Cancelled {po.cancelledAt} by {po.cancelledBy} — {po.cancelReason}</div>
            </div>
          )}

          {/* Lifecycle */}
          <div style={{ padding: '14px 16px 12px', border: '1px solid var(--border-light)', borderRadius: 10 }}><CmPoStepper po={po} /></div>

          {/* Lines */}
          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Lines</div>
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1.3fr 84px 104px 104px', gap: 10, padding: '8px 13px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Ingredient</div><div style={{ textAlign: 'right' }}>Qty</div><div style={{ textAlign: 'right' }}>Unit price</div><div style={{ textAlign: 'right' }}>Line total</div>
              </div>
              {po.lines.map((l, i) => (
                <div key={l.ing} style={{ display: 'grid', gridTemplateColumns: '1.3fr 84px 104px 104px', gap: 10, padding: '9px 13px', alignItems: 'center', borderBottom: i < po.lines.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 12, flex: 'none' }}>{l.emoji}</span>
                    <span style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{l.name}</span>
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmUnit(l.qty, l.unit)}</div>
                    {po.status === 'pending' && <div style={{ fontSize: 10, color: l.received > 0 ? 'var(--warning)' : 'var(--text-tertiary)' }}>{l.received > 0 ? cmUnit(l.received, l.unit) + ' received' : 'none received'}</div>}
                  </div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{cmMoney(l.unitPrice, po.ccy)}</div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(l.lineTotal, po.ccy)}</div>
                </div>
              ))}
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, padding: '9px 13px', background: 'var(--surface-inset)', borderTop: '1px solid var(--border-light)', alignItems: 'baseline' }}>
                <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>Total</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 700, color: 'var(--text-primary)' }}>{cmMoney(po.total, po.ccy)}</span>
              </div>
            </div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 6 }}>Quantities in the ingredient's base unit; unit prices are stored exact (4 dp honest) — line totals never re-round.</div>
          </div>

          {/* Provenance */}
          <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: po.provenance === 'from-shortfall' ? 'var(--brand-primary-10)' : 'var(--surface-inset)', borderLeft: '3px solid ' + (po.provenance === 'from-shortfall' ? 'var(--brand-primary)' : 'var(--border)') }}>
            <Icon name="zap" size={14} color={po.provenance === 'from-shortfall' ? 'var(--brand-primary)' : 'var(--text-tertiary)'} style={{ flex: 'none', marginTop: 1 }} />
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
              {po.provenanceNote || ('Created manually by ' + po.createdBy + '.')}
              {po.alerts.length > 0 && (
                <span style={{ marginLeft: 8, display: 'inline-flex', gap: 5 }}>
                  {po.alerts.map(aid => {
                    const al = CM_ALERTS.find(a => a.id === aid);
                    return al ? <span key={aid} style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--brand-primary)', background: 'var(--brand-primary-10)', padding: '1px 6px', borderRadius: 4 }}>{al.ref}</span> : null;
                  })}
                </span>
              )}
            </div>
          </div>

          {/* Receipts */}
          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Receipts</div>
            {po.receipts.length === 0 ? (
              <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', padding: '10px 13px', border: '1px dashed var(--border-light)', borderRadius: 10 }}>
                {po.status === 'pending' ? 'No receipts yet — Receive posts one against this PO.' : 'No receipts.'}
              </div>
            ) : (
              <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
                {po.receipts.map((rid, i) => {
                  const r = cmPoReceiptById(rid);
                  if (!r) return null;
                  const rQty = r.lines.reduce((a, l) => a + l.qty, 0);
                  const rUnit = r.lines.length === 1 ? r.lines[0].unit : 'units';
                  const remaining = (r.outcomes.remaining || []).map(x => cmUnit(x.qty, x.unit)).join(', ');
                  return (
                    <div key={rid} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 13px', borderBottom: i < po.receipts.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12 }}>
                      <Icon name="receipt" size={14} color="var(--brand-primary)" style={{ flex: 'none' }} />
                      <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', fontWeight: 500 }}>{r.ref}</span>
                      <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{r.receivedAt}</span>
                      <span style={{ marginLeft: 'auto', fontSize: 11.5, color: r.outcomes.poStatus === 'complete' ? 'var(--success)' : 'var(--text-secondary)' }}>
                        +{cmUnit(rQty, rUnit)} — {r.outcomes.poStatus === 'complete' ? 'completed the PO' : 'PO stayed Pending, ' + remaining + ' remaining'}
                      </span>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Actions by state */}
        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10 }}>
          {po.status === 'draft' && (
            <React.Fragment>
              <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Submit is compare-and-set guarded — a stale revision is rejected.</span>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="btn btn-outline btn-sm" style={{ color: 'var(--danger)' }}><Icon name="ban" size={12} /> Cancel</button>
                <button className="btn btn-primary btn-sm"><Icon name="send" size={12} /> Submit to supplier</button>
              </div>
            </React.Fragment>
          )}
          {po.status === 'pending' && (
            <React.Fragment>
              <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Receive routes to Goods receipt.</span>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="btn btn-outline btn-sm" style={{ color: 'var(--danger)' }}><Icon name="ban" size={12} /> Cancel</button>
                <button className="btn btn-primary btn-sm"><Icon name="truck" size={12} /> Receive <Icon name="arrowright" size={11} /></button>
              </div>
            </React.Fragment>
          )}
          {(po.status === 'complete' || po.status === 'cancelled') && (
            <React.Fragment>
              <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{po.status === 'complete' ? 'Fully received — no further actions.' : 'Cancelled — no further actions.'}</span>
              <button className="btn btn-outline btn-sm" onClick={onClose}>Close</button>
            </React.Fragment>
          )}
        </div>
      </div>
    </React.Fragment>
  );
}

// Create popover — explicit lines vs FROM SHORTFALL (053 from-shortfall flow).
function CmPoCreatePanel({ onClose }) {
  const [mode, setMode] = React.useState('shortfall');
  const [supId, setSupId] = React.useState('sup-lagosgrains');
  const sup = CM_SUPPLIERS.find(s => s.id === supId) || CM_SUPPLIERS[0];
  const activeAlerts = CM_ALERTS.filter(a => a.status === 'open' || a.status === 'acknowledged');
  const seeded = activeAlerts.map(a => {
    const row = sup.catalog.find(r => r.ing === a.ing);
    if (!row) return { a, row: null };
    const shortfall = a.reorderPoint - (a.refreshedAvailable != null ? a.refreshedAvailable : a.availableAtRaise);
    const packs = Math.max(1, Math.ceil(shortfall / row.packSize));
    return { a, row, shortfall, packs, est: packs * row.packPrice };
  });
  const est = seeded.reduce((t, s) => t + (s.est || 0), 0);

  return (
    <div style={{ position: 'absolute', top: 64, right: 28, width: 440, zIndex: 34, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, boxShadow: 'var(--shadow-lg)', padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 13.5, fontWeight: 700, color: 'var(--text-primary)' }}>New purchase order</span>
        <button onClick={onClose} style={{ width: 24, height: 24, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={12} color="var(--text-secondary)" /></button>
      </div>
      <div style={{ display: 'inline-flex', padding: 3, gap: 2, background: 'var(--surface-inset)', borderRadius: 9, alignSelf: 'flex-start' }}>
        {[{ id: 'shortfall', label: 'From shortfall' }, { id: 'explicit', label: 'Explicit lines' }].map(mm => {
          const on = mode === mm.id;
          return <button key={mm.id} onClick={() => setMode(mm.id)} style={{ height: 26, padding: '0 12px', borderRadius: 7, cursor: 'pointer', border: 'none', fontSize: 11.5, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent', color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? 'var(--shadow-sm)' : 'none' }}>{mm.label}</button>;
        })}
      </div>

      {mode === 'shortfall' ? (
        <React.Fragment>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>Seeds draft lines from the active low-stock alerts — quantities pack-rounded to the supplier's pack, minimum one pack.</div>
          <div>
            <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Supplier</div>
            <select value={supId} onChange={e => setSupId(e.target.value)} style={{ width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 10px', fontSize: 12.5, color: 'var(--text-primary)' }}>
              {CM_SUPPLIERS.map(s => (
                <option key={s.id} value={s.id} disabled={s.ccy !== 'NGN'}>
                  {s.name}{s.ccy !== 'NGN' ? " (GBP — excluded: can't price an NGN PO)" : ''}
                </option>
              ))}
            </select>
          </div>
          <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
            {seeded.map(({ a, row, shortfall, packs, est: lineEst }, i) => (
              <div key={a.id} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '9px 12px', borderBottom: i < seeded.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12, opacity: row ? 1 : 0.55 }}>
                <span style={{ width: 22, height: 22, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 11, flex: 'none' }}>{a.emoji}</span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{a.name}</div>
                  <div style={{ fontSize: 10, color: 'var(--text-tertiary)' }}>{row ? 'short ' + cmUnit(shortfall, a.unit) + ' (' + a.ref + ')' : 'no catalog row for this supplier'}</div>
                </div>
                {row && (
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', fontWeight: 600 }}>{cmUnit(packs, row.packLabel)}</div>
                    <div style={{ fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{cmMoney(lineEst, row.ccy)}</div>
                  </div>
                )}
              </div>
            ))}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Est. total <b style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmMoney(est)}</b> — lands as a Draft</span>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Create draft PO</button>
          </div>
        </React.Fragment>
      ) : (
        <React.Fragment>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>Add lines by ingredient, quantity in its base unit, and an exact unit price.</div>
          <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 72px 92px', gap: 6 }}>
            <select defaultValue="ing-ginger" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 8px', fontSize: 12, color: 'var(--text-primary)' }}>
              {CM_INGREDIENTS.filter(i => i.active).map(i => <option key={i.id} value={i.id}>{i.name}</option>)}
            </select>
            <input defaultValue="5 kg" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 8px', fontSize: 12, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }} />
            <input defaultValue="₦2,300" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 8px', fontSize: 12, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }} />
          </div>
          <button className="btn btn-ghost btn-sm" style={{ alignSelf: 'flex-start' }}><Icon name="plus" size={12} /> Add line</button>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Create draft PO</button>
          </div>
        </React.Fragment>
      )}
    </div>
  );
}

Object.assign(window, { ScreenCommercePurchaseOrders });
