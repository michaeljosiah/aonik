/* API mapping (Spec 058 §8.3 — landed endpoints):
   UI action              Landed API
   List / acknowledge     GET /commerce/admin/low-stock-alerts?status=  ·  POST /commerce/admin/low-stock-alerts/{id}/acknowledge   (052)
   Order from shortfall   POST /commerce/admin/purchase-orders/from-shortfall   (053)
*/
// Commerce · Make · Spec 058 §8.3 — Low stock
// ScreenCommerceLowStock — the 052 alert lifecycle on one surface. Landed
// vocabulary ONLY: Open | Acknowledged | Ordered | Resolved. Open +
// Acknowledged form the single ACTIVE set — a re-scan refreshes an active
// alert's snapshot in place (AL-0476), it never re-opens one and never opens
// a duplicate. Ordered links its outstanding PO; Resolved links the receipt
// that restocked it. "Order from shortfall" hands off to the §8.5
// from-shortfall flow (pack-rounded to the supplier pack, minimum one pack).
// Data: CM_ALERTS / CM_ALERT_STATUS / cmUnit (mock-data.js). Mock-only.

function ScreenCommerceLowStock() {
  const [tab, setTab] = React.useState('active');

  const lsActive = CM_ALERTS.filter(a => a.status === 'open' || a.status === 'acknowledged');
  const lsOpen = CM_ALERTS.filter(a => a.status === 'open');
  const lsOrdered = CM_ALERTS.filter(a => a.status === 'ordered');
  const lsResolved = CM_ALERTS.filter(a => a.status === 'resolved');
  const shown = tab === 'active' ? lsActive : tab === 'ordered' ? lsOrdered : lsResolved;

  const tabs = [
    { id: 'active', label: 'Active', n: lsActive.length, cap: 'one active set: a re-scan refreshes, never re-opens' },
    { id: 'ordered', label: 'Ordered', n: lsOrdered.length, cap: 'ordered alerts leave the active set — a fresh scan may open a new alert for the same ingredient' },
    { id: 'resolved', label: 'Resolved', n: lsResolved.length, cap: 'resolved by the goods receipt that brought stock back above the reorder point' },
  ];
  const cap = (tabs.find(t => t.id === tab) || {}).cap;

  const kpis = [
    { l: 'Active alerts', v: lsActive.length, s: 'Open + Acknowledged — one active set', tone: 'warn' },
    { l: 'Open', v: lsOpen.length, s: 'awaiting acknowledgement', tone: 'danger' },
    { l: 'Ordered — awaiting receipt', v: lsOrdered.length, s: lsOrdered.map(a => a.poRef).join(', ') + ' outstanding' },
    { l: 'Resolved (30d)', v: lsResolved.length, s: 'restocked above the reorder point' },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-lsrow:hover{background:var(--surface-inset);}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Low stock</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Raw-material reorder alerts from the stock scan. Open and Acknowledged alerts are the one active set — ordering hands off to Purchase orders, and receiving resolves an alert only when stock is truly back above the reorder point.</div>
          </div>
          <button className="btn btn-sm"><Icon name="refresh" size={12} /> Re-scan now</button>
        </div>

        {/* KPIs */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, marginTop: 4, fontFamily: 'var(--font-mono)', color: k.tone === 'danger' && k.v > 0 ? 'var(--danger)' : k.tone === 'warn' && k.v > 0 ? 'var(--warning)' : 'var(--text-primary)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        {/* Tabs + lifecycle caption */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, flex: 'none' }}>
            {tabs.map(t => {
              const on = tab === t.id;
              return (
                <button key={t.id} onClick={() => setTab(t.id)} style={{
                  display: 'inline-flex', alignItems: 'center', gap: 7, height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none',
                  fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent',
                  color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? 'var(--shadow-sm)' : 'none',
                }}>
                  {t.label}
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: on ? 'var(--brand-primary)' : 'var(--text-tertiary)' }}>{t.n}</span>
                </button>
              );
            })}
          </div>
          {cap && <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>{cap}</div>}
        </div>

        {/* Alerts table */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1.05fr 1.5fr 96px 118px 236px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Ingredient</div><div>Snapshot at raise</div><div style={{ textAlign: 'right' }}>Raised</div><div>Status</div><div style={{ textAlign: 'right' }}>Actions</div>
          </div>
          {shown.map((a, i) => (
            <div key={a.id} className="cm-lsrow" style={{ display: 'grid', gridTemplateColumns: '1.05fr 1.5fr 96px 118px 236px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < shown.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span style={{ width: 26, height: 26, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{a.emoji}</span>
                <div>
                  <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{a.name}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{a.ref}</div>
                </div>
              </div>
              <div>
                <div style={{ color: 'var(--text-primary)' }}>{a.message}</div>
                {a.refreshedAvailable != null && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 3 }}>
                    <Icon name="refresh" size={11} color="var(--warning)" />
                    Re-scanned {a.refreshedAt} — {cmUnit(a.refreshedAvailable, a.unit)} available now
                  </div>
                )}
                {a.refreshNote && <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2, lineHeight: 1.45 }}>{a.refreshNote}</div>}
                {!a.refreshNote && a.note && <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2, lineHeight: 1.45 }}>{a.note}</div>}
              </div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-tertiary)' }}>{a.raisedAt}</div>
              <div><Pill tone={CM_ALERT_STATUS[a.status].tone} dot size="sm">{CM_ALERT_STATUS[a.status].label}</Pill></div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: 6 }}>
                {a.status === 'open' && (
                  <React.Fragment>
                    <button className="btn btn-outline btn-sm"><Icon name="check" size={12} /> Acknowledge</button>
                    <button className="btn btn-primary btn-sm"><Icon name="cart" size={12} /> Order from shortfall</button>
                  </React.Fragment>
                )}
                {a.status === 'acknowledged' && (
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 2 }}>
                    <button className="btn btn-primary btn-sm"><Icon name="cart" size={12} /> Order from shortfall</button>
                    <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>acknowledged {a.acknowledgedAt} by {a.acknowledgedBy}</span>
                  </div>
                )}
                {a.status === 'ordered' && (
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 2 }}>
                    <button className="btn btn-ghost btn-sm" style={{ color: 'var(--brand-primary)' }}><Icon name="clipboard" size={12} /> {a.poRef} <Icon name="arrowright" size={11} /></button>
                    <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>ordered {a.orderedAt}</span>
                  </div>
                )}
                {a.status === 'resolved' && (
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 2 }}>
                    <button className="btn btn-ghost btn-sm" style={{ color: 'var(--brand-primary)' }}><Icon name="receipt" size={12} /> {a.receiptRef} <Icon name="arrowright" size={11} /></button>
                    <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>resolved {a.resolvedAt}</span>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>

        {/* Hand-off note */}
        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="zap" size={12} color="var(--brand-primary)" /> "Order from shortfall" seeds a draft purchase order from the active alerts — quantities pack-rounded to the supplier pack, minimum one pack (053).
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCommerceLowStock });
