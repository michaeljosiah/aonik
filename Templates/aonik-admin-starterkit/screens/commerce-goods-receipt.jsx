/* API mapping (Spec 058 §8.6 — landed endpoint):
   UI action             Landed API
   Receive against a PO  POST /commerce/admin/purchase-orders/{id}/receipts   (054 — idempotency key + payload hash)
*/
// Commerce · Make · Spec 058 §8.6 — Goods receipt
// ScreenCommerceGoodsReceipt — a FLOW, not a list. LEFT: the receive form
// against Pending PO-2026-0112 (per line: ordered, previously received,
// receive-now, remaining preview, optional actual unit cost that updates the
// ingredient's cost from this delivery) plus a summary rail of what posting
// will apply: stock up, cost refresh, alert re-check, PO completion check.
// RIGHT: posted outcomes — RCPT-2026-0141 (short: alert KEPT, still below the
// reorder point — the honesty rule; PO stayed Pending, 10 kg remaining) and
// RCPT-2026-0138 (full: alert resolved, PO completed) — plus the over-receipt
// REJECTION (cumulative received may never exceed ordered, 054 v1 no
// tolerance) and the claim-first idempotent-retry captions.
// Data: CM_POS / CM_RECEIPTS / CM_ALERTS / CM_INGREDIENTS / cmIngAvail. Mock-only.

const CMGR_IDEM_KEYS = { po_0112: 'rcv-9d41f2', po_0119: 'rcv-3c77a0' };
const cmGrIngById = id => CM_INGREDIENTS.find(i => i.id === id) || { name: id, emoji: '❔', unit: '', onHand: 0, reserved: 0 };
// Landed 054 ResolveIfRecoveredAsync flips Open, Acknowledged AND Ordered alerts — an Ordered
// alert is exactly what a receipt against its seeded PO recovers (the rice chain's payoff).
const cmGrUnresolvedAlertsFor = ing => CM_ALERTS.filter(a => a.ing === ing && (a.status === 'open' || a.status === 'acknowledged' || a.status === 'ordered'));

function ScreenCommerceGoodsReceipt() {
  const grPending = CM_POS.filter(p => p.status === 'pending');
  const [poId, setPoId] = React.useState('po_0112');
  const po = grPending.find(p => p.id === poId) || grPending[0];
  const [recvNow, setRecvNow] = React.useState({ 'po_0112:ing-rice': 10, 'po_0119:ing-oats': 20 });
  const [costNow, setCostNow] = React.useState({ 'po_0112:ing-rice': '1120', 'po_0119:ing-oats': '' });

  const rej = CM_RECEIPTS.find(r => r.kind === 'rejected');
  const rShort = CM_RECEIPTS.find(r => r.id === 'rcpt_0141');
  const rFull = CM_RECEIPTS.find(r => r.id === 'rcpt_0138');

  const calc = po.lines.map(l => {
    const key = po.id + ':' + l.ing;
    const now = Number(recvNow[key]) || 0;
    const cost = costNow[key];
    const remainingBefore = l.qty - (l.received || 0);
    const remainingAfter = remainingBefore - now;
    const over = now > remainingBefore;
    const ingr = cmGrIngById(l.ing);
    const availAfter = cmIngAvail(ingr) + now;
    const alerts = cmGrUnresolvedAlertsFor(l.ing);
    return { l, key, now, cost, remainingBefore, remainingAfter, over, ingr, availAfter, alerts };
  });
  const anyOver = calc.some(x => x.over);
  const allDone = !anyOver && calc.every(x => x.remainingAfter <= 0);
  const totalRemainingAfter = calc.reduce((a, x) => a + Math.max(0, x.remainingAfter), 0);

  const railRows = [];
  calc.forEach(x => {
    railRows.push({ icon: 'arrowup', color: 'var(--success)', text: <span>Stock up — <b>{x.ingr.name}</b> +{cmUnit(x.now, x.l.unit)}, available becomes {cmUnit(x.availAfter, x.l.unit)}</span> });
    railRows.push(x.cost
      ? { icon: 'refresh', color: 'var(--brand-primary)', text: <span>Cost refresh — {x.ingr.name} {cmMoney(Number(x.cost))}/{x.l.unit} written effective from received-at (new cost window)</span> }
      : { icon: 'refresh', color: 'var(--text-tertiary)', text: <span>No actual cost given — {x.ingr.name} keeps its current standard cost{x.ingr.cost ? ' (' + cmMoney(x.ingr.cost.current) + '/' + x.ingr.unit + ')' : ''}</span> });
    // Boundary per landed 052/054: resolve ONLY strictly above the reorder point — landing
    // exactly on it keeps the alert (the short-receipt honesty rule).
    if (x.alerts.length === 0) {
      railRows.push({ icon: 'check2', color: 'var(--text-tertiary)', text: <span>No unresolved low-stock alert for {x.ingr.name}</span> });
    } else x.alerts.forEach(al => {
      railRows.push(x.availAfter > al.reorderPoint
        ? { icon: 'check2', color: 'var(--success)', text: <span>Alert {al.ref} resolves — {cmUnit(x.availAfter, x.l.unit)} is strictly above the {cmUnit(al.reorderPoint, x.l.unit)} reorder point</span> }
        : { icon: 'alertc', color: 'var(--warning)', text: <span>Alert {al.ref} kept — still at or below the reorder point ({cmUnit(x.availAfter, x.l.unit)} of {cmUnit(al.reorderPoint, x.l.unit)})</span> });
    });
  });
  railRows.push(allDone
    ? { icon: 'clipcheck', color: 'var(--success)', text: <span>PO completion — everything ordered received, <b>{po.ref} completes</b></span> }
    : { icon: 'hourglass', color: 'var(--warning)', text: <span>PO completion — {po.ref} stays Pending, {cmUnit(totalRemainingAfter, calc[0].l.unit)} still outstanding</span> });

  const inputStyle = { background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '6px 9px', fontSize: 12.5, fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' };

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Goods receipt</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Receive stock against a Pending purchase order. Posting is claim-first and idempotent — it applies stock, optionally refreshes the ingredient's cost, re-checks low-stock alerts honestly, and completes the PO only when everything ordered has arrived.</div>
          </div>
          <button className="btn btn-sm"><Icon name="clipboard" size={12} /> View purchase orders</button>
        </div>

        {/* Two-panel flow: receive form + posted outcomes */}
        <div style={{ display: 'grid', gridTemplateColumns: '1.05fr 0.95fr', gap: 18, alignItems: 'start' }}>
          {/* LEFT — receive form */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {/* Over-receipt rejection — the landed 054 v1 rule */}
            {rej && rej.po === po.id && (
              <div style={{ display: 'flex', gap: 10, padding: '12px 14px', borderRadius: 10, background: 'var(--danger-light)', borderLeft: '3px solid var(--danger)', alignItems: 'flex-start' }}>
                <Icon name="ban" size={15} color="var(--danger)" style={{ flex: 'none', marginTop: 1 }} />
                <div>
                  <div style={{ fontSize: 12.5, fontWeight: 700, color: 'var(--danger)' }}>Over-receipt rejected — {rej.error.name}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-primary)', marginTop: 3 }}>
                    ordered {cmUnit(rej.error.ordered, rej.error.unit)} — already received {cmUnit(rej.error.alreadyReceived, rej.error.unit)} — attempted {cmUnit(rej.error.attempted, rej.error.unit)}
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5, marginTop: 3 }}>{rej.error.message}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 3 }}>attempted {rej.attemptedAt} — nothing was applied</div>
                </div>
              </div>
            )}

            {/* Receive form */}
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
              <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 10 }}>
                <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-secondary)', flex: 'none' }}>Receiving against</span>
                <select value={po.id} onChange={e => setPoId(e.target.value)} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '6px 9px', fontSize: 12.5, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>
                  {grPending.map(p => <option key={p.id} value={p.id}>{p.ref} — {p.supplierName}</option>)}
                </select>
                <Pill tone={CM_PO_STATUS[po.status].tone} dot size="sm">{CM_PO_STATUS[po.status].label}</Pill>
                {po.expectedBy && <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 'auto' }}>expected {po.expectedBy}</span>}
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1.1fr 76px 88px 104px 108px', gap: 10, padding: '8px 16px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Ingredient</div><div style={{ textAlign: 'right' }}>Ordered</div><div style={{ textAlign: 'right' }}>Received</div><div style={{ textAlign: 'right' }}>Receive now</div><div style={{ textAlign: 'right' }}>Remaining</div>
              </div>
              {calc.map(x => (
                <div key={x.key} style={{ borderBottom: '1px solid var(--border-light)' }}>
                  <div style={{ display: 'grid', gridTemplateColumns: '1.1fr 76px 88px 104px 108px', gap: 10, padding: '11px 16px', alignItems: 'center', fontSize: 12.5 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ width: 26, height: 26, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{x.l.emoji}</span>
                      <span style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{x.l.name}</span>
                    </div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmUnit(x.l.qty, x.l.unit)}</div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: x.l.received > 0 ? 'var(--text-secondary)' : 'var(--text-tertiary)' }}>{cmUnit(x.l.received || 0, x.l.unit)}</div>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: 5 }}>
                      <input value={recvNow[x.key] != null ? recvNow[x.key] : ''} onChange={e => setRecvNow({ ...recvNow, [x.key]: e.target.value })} style={{ ...inputStyle, width: 58, textAlign: 'right' }} />
                      <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{x.l.unit}</span>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <div style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: x.over ? 'var(--danger)' : x.remainingAfter <= 0 ? 'var(--success)' : 'var(--warning)' }}>
                        {x.over ? 'over by ' + cmUnit(x.now - x.remainingBefore, x.l.unit) : cmUnit(Math.max(0, x.remainingAfter), x.l.unit)}
                      </div>
                      <div style={{ fontSize: 10, color: 'var(--text-tertiary)' }}>{x.over ? 'exceeds ordered' : 'after posting'}</div>
                    </div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '0 16px 11px 50px' }}>
                    <span style={{ fontSize: 11, color: 'var(--text-secondary)', flex: 'none' }}>Actual unit cost (optional)</span>
                    <input value={costNow[x.key] != null ? costNow[x.key] : ''} onChange={e => setCostNow({ ...costNow, [x.key]: e.target.value })} placeholder="—" style={{ ...inputStyle, width: 76, textAlign: 'right' }} />
                    <span style={{ fontSize: 11, color: 'var(--text-tertiary)', flex: 'none' }}>NGN/{x.l.unit}</span>
                    <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>updates the ingredient's cost from this delivery</span>
                  </div>
                </div>
              ))}

              <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px', borderBottom: '1px solid var(--border-light)' }}>
                <span style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', flex: 'none' }}>Received at</span>
                <input defaultValue="2026-07-03 09:15" style={{ ...inputStyle, width: 150, fontWeight: 500 }} />
                <span style={{ marginLeft: 'auto', fontSize: 10.5, color: 'var(--text-tertiary)', display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                  <Icon name="key" size={11} color="var(--text-tertiary)" />
                  <span style={{ fontFamily: 'var(--font-mono)' }}>{CMGR_IDEM_KEYS[po.id] || 'rcv-new'}</span>
                  — claim-first: a keyed retry returns the same receipt, applied once
                </span>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', background: 'var(--surface-inset)' }}>
                <span style={{ fontSize: 11, color: anyOver ? 'var(--danger)' : 'var(--text-tertiary)' }}>
                  {anyOver ? 'Over-receipt — the API rejects this post outright (v1 tolerance: none).' : 'Short receipts are fine — the PO stays Pending and the bar carries the partial.'}
                </span>
                <button className="btn btn-primary btn-sm" disabled={anyOver}><Icon name="check" size={12} /> Post receipt</button>
              </div>
            </div>

            {/* Summary rail — what posting will do */}
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 14 }}>
              <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Posting will apply</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
                {railRows.map((r, i) => (
                  <div key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.45 }}>
                    <Icon name={r.icon} size={13} color={r.color} style={{ flex: 'none', marginTop: 1 }} />
                    <span>{r.text}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* RIGHT — posted outcomes */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>Posted outcomes</div>

            {/* Short receipt — alert honestly KEPT */}
            {rShort && (
              <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 700, color: 'var(--text-primary)' }}>{rShort.ref}</span>
                  <Pill tone="success" dot size="sm">Posted</Pill>
                  <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 'auto' }}>{rShort.receivedAt} — {rShort.supplierName}</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', flex: 'none' }}>against <span style={{ fontFamily: 'var(--font-mono)' }}>{rShort.poRef}</span></span>
                  <ProgressCells value={rShort.lines[0].previouslyReceived + rShort.lines[0].qty} max={rShort.lines[0].ordered} tone="warning" caption={(rShort.lines[0].previouslyReceived + rShort.lines[0].qty) + ' / ' + rShort.lines[0].ordered + ' ' + rShort.lines[0].unit} />
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {rShort.outcomes.stockApplied.map(s => (
                    <div key={s.ing} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.45 }}>
                      <Icon name="arrowup" size={13} color="var(--success)" style={{ flex: 'none', marginTop: 1 }} />
                      <span>Stock applied — <b style={{ color: 'var(--text-primary)' }}>{s.name}</b> +{cmUnit(s.qty, s.unit)}, on hand {cmUnit(s.onHandAfter, s.unit)}, available {cmUnit(s.availableAfter, s.unit)}</span>
                    </div>
                  ))}
                  {rShort.outcomes.costRowsWritten.map(c => (
                    <div key={c.ing} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.45 }}>
                      <Icon name="refresh" size={13} color="var(--brand-primary)" style={{ flex: 'none', marginTop: 1 }} />
                      <span>Cost row written — {c.name} <b style={{ color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>{cmMoney(c.cost, c.ccy)}/{cmGrIngById(c.ing).unit}</b> effective from {c.effectiveFrom} (received-at)</span>
                    </div>
                  ))}
                </div>
                {rShort.outcomes.alertsKept.map(k => {
                  const al = CM_ALERTS.find(a => a.id === k.alert);
                  return (
                    <div key={k.alert} style={{ display: 'flex', gap: 9, padding: '10px 12px', borderRadius: 10, background: 'var(--warning-light)', borderLeft: '3px solid var(--warning)' }}>
                      <Icon name="alertc" size={14} color="var(--warning)" style={{ flex: 'none', marginTop: 1 }} />
                      <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                        <b style={{ color: 'var(--text-primary)' }}>Alert kept{al ? ' — ' + al.ref : ''}.</b> {k.reason} A short receipt never fakes a resolution.
                      </div>
                    </div>
                  );
                })}
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: 'var(--text-secondary)' }}>
                  <Icon name="hourglass" size={13} color="var(--warning)" style={{ flex: 'none' }} />
                  <span>PO stayed <b style={{ color: 'var(--text-primary)' }}>{CM_PO_STATUS[rShort.outcomes.poStatus].label}</b> — {rShort.outcomes.remaining.map(x => cmUnit(x.qty, x.unit) + ' ' + x.name.toLowerCase()).join(', ')} remaining</span>
                </div>
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', borderTop: '1px dashed var(--border-light)', paddingTop: 8 }}>
                  <span style={{ fontFamily: 'var(--font-mono)' }}>{rShort.idempotencyKey}</span> — {rShort.retryNote}
                </div>
              </div>
            )}

            {/* Full receipt — compact success card */}
            {rFull && (
              <div style={{ background: 'var(--success-light)', border: '1px solid var(--border-light)', borderLeft: '3px solid var(--success)', borderRadius: 12, padding: '14px 16px', display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                <Icon name="check2" size={15} color="var(--success)" style={{ flex: 'none', marginTop: 1 }} />
                <div style={{ flex: 1 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 700, color: 'var(--text-primary)' }}>{rFull.ref}</span>
                    <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>full receipt, {rFull.lines[0].qty} / {rFull.lines[0].ordered} {rFull.lines[0].unit}</span>
                    <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginLeft: 'auto' }}>{rFull.receivedAt}</span>
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.55, marginTop: 4 }}>
                    {rFull.outcomes.stockApplied.map(s => s.name + ' +' + cmUnit(s.qty, s.unit) + ' (available ' + cmUnit(s.availableAfter, s.unit) + ')').join(', ')}
                    {' — alert '}{rFull.outcomes.alertsResolved.map(aid => { const al = CM_ALERTS.find(a => a.id === aid); return al ? al.ref : aid; }).join(', ')} resolved
                    {' — '}<span style={{ fontFamily: 'var(--font-mono)' }}>{rFull.poRef}</span> completed
                    {' — cost row '}{rFull.outcomes.costRowsWritten.map(c => cmMoney(c.cost, c.ccy) + '/' + cmGrIngById(c.ing).unit + ' effective ' + c.effectiveFrom).join(', ')}
                  </div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 5 }}>
                    <span style={{ fontFamily: 'var(--font-mono)' }}>{rFull.idempotencyKey}</span> — {rFull.retryNote}
                  </div>
                </div>
              </div>
            )}

            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'flex-start', gap: 6, lineHeight: 1.5 }}>
              <Icon name="check2" size={12} color="var(--success)" style={{ flex: 'none', marginTop: 2 }} /> An alert resolves only when available stock is strictly back above the reorder point — landing exactly on it, or a short receipt, keeps the alert honestly. Cumulative received may never exceed ordered.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCommerceGoodsReceipt });
