// Commerce · Spec 042 — Discounts & Overview group (final)
//   • ScreenCommerceDiscounts — coupons (Percentage / FixedAmount, expiry,
//     redemption limit + usage), create/edit drawer, and the tax-calculator seam
//     (ZeroRate default; deployments register a jurisdiction-aware calculator).
//   • ScreenCommerceOverview — the commerce dashboard: KPIs, revenue trend,
//     recent orders, top products, and a "needs attention" panel (low stock,
//     abandoned carts, pending payment).
// Reuses cmMoney / CM_PRODUCTS / CM_ORDERS / CM_CARTS / cmLow / cmAvail / CM_PAY.
// No decorative middots; dots = state only.

const CM_DISC_TONE = { active: { tone: 'success', label: 'Active' }, scheduled: { tone: 'pending', label: 'Scheduled' }, expired: { tone: 'muted', label: 'Expired' }, disabled: { tone: 'warning', label: 'Disabled' } };
const cmDiscValue = d => d.type === 'percent' ? d.value + '%' : cmMoney(d.value);

const CM_DISCOUNTS = [
  { code: 'WELLNESS10', type: 'percent', value: 10, limit: 500,  used: 142, from: '01 Jun', to: '30 Jun', status: 'active',    min: 5000,  applies: 'All products' },
  { code: 'NEWBOX',     type: 'percent', value: 15, limit: 200,  used: 38,  from: '10 Jun', to: '31 Jul', status: 'active',    min: 12000, applies: 'Wellness Boxes' },
  { code: 'STAFF20',    type: 'percent', value: 20, limit: 50,   used: 12,  from: '01 Jan', to: '31 Dec', status: 'active',    min: 0,     applies: 'Staff only' },
  { code: 'LAGOS500',   type: 'fixed',   value: 500, limit: 300, used: 0,   from: '20 Jun', to: '20 Jul', status: 'scheduled', min: 3000,  applies: 'All products' },
  { code: 'FREEDEL2K',  type: 'fixed',   value: 2000, limit: 1000, used: 610, from: '01 May', to: '15 Jun', status: 'expired',  min: 0,     applies: 'All products' },
  { code: 'BLACKFRI',   type: 'percent', value: 25, limit: 1000, used: 0,   from: '25 Nov', to: '30 Nov', status: 'disabled',  min: 0,     applies: 'All products' },
];

function ScreenCommerceDiscounts() {
  const [sel, setSel] = React.useState(null);
  const active = CM_DISCOUNTS.filter(d => d.status === 'active');
  const redemptions = CM_DISCOUNTS.reduce((a, d) => a + d.used, 0);
  const kpis = [
    { l: 'Active coupons', v: active.length, s: CM_DISCOUNTS.length + ' total' },
    { l: 'Redemptions', v: redemptions.toLocaleString('en-GB'), s: 'all time' },
    { l: 'Discount given', v: cmMoney(186400), s: 'this month' },
    { l: 'Top coupon', v: 'WELLNESS10', s: '142 uses', mono: true },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-drow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Discounts</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Coupon codes applied at checkout. The discounted total is what the payment intent funds — order lines stay the goods; the breakdown is recorded on the order.</div>
          </div>
          <button className="btn btn-primary btn-sm" onClick={() => setSel({ _new: true, type: 'percent', value: 10, limit: 100, used: 0, status: 'scheduled', min: 0, applies: 'All products', from: '—', to: '—', code: '' })}><Icon name="plus" size={12} /> Create coupon</button>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: k.mono ? 16 : 22, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '150px 90px 1fr 120px 110px 100px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Code</div><div style={{ textAlign: 'right' }}>Value</div><div>Applies to</div><div>Redeemed</div><div>Validity</div><div>Status</div>
          </div>
          {CM_DISCOUNTS.map((d, i) => {
            const pct = d.limit ? Math.min(100, Math.round((d.used / d.limit) * 100)) : 0;
            return (
              <div key={d.code} className="cm-drow" onClick={() => setSel(d)} style={{ display: 'grid', gridTemplateColumns: '150px 90px 1fr 120px 110px 100px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < CM_DISCOUNTS.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: (d.status === 'expired' || d.status === 'disabled') ? 0.62 : 1 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name="tag" size={12} /></span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{d.code}</span>
                </div>
                <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmDiscValue(d)}</div>
                <div style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{d.applies}{d.min > 0 && <span style={{ color: 'var(--text-tertiary)' }}>, min {cmMoney(d.min)}</span>}</div>
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)', marginBottom: 3 }}><span>{d.used}</span><span style={{ color: 'var(--text-tertiary)' }}>/ {d.limit}</span></div>
                  <div style={{ height: 4, borderRadius: 9, background: 'var(--surface-inset)', overflow: 'hidden' }}><div style={{ width: pct + '%', height: '100%', background: pct >= 90 ? 'var(--warning)' : 'var(--brand-primary)' }} /></div>
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{d.from} — {d.to}</div>
                <div><Pill tone={CM_DISC_TONE[d.status].tone} dot size="sm">{CM_DISC_TONE[d.status].label}</Pill></div>
              </div>
            );
          })}
        </div>

        {/* Tax seam */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 16, display: 'flex', alignItems: 'center', gap: 14 }}>
          <span style={{ width: 38, height: 38, borderRadius: 9, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name="landmark" size={17} color="var(--text-secondary)" /></span>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Tax calculation</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>Currently <b style={{ color: 'var(--text-primary)' }}>Zero-rate</b> (default). Register a jurisdiction-aware calculator at deployment to apply VAT/sales tax — checkout computes subtotal then discount then tax then payable total.</div>
          </div>
          <button className="btn btn-outline btn-sm">Configure</button>
        </div>
      </div>
      {sel && <CmCouponDrawer d={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmCouponDrawer({ d, onClose }) {
  const pct = d.limit ? Math.min(100, Math.round((d.used / d.limit) * 100)) : 0;
  const fld = { width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px', fontSize: 13, color: 'var(--text-primary)' };
  const lbl = { fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 };
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 460, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ width: 38, height: 38, borderRadius: 9, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name="tag" size={17} /></span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>{d._new ? 'New coupon' : d.code}</div>
            {!d._new && <div style={{ marginTop: 3 }}><Pill tone={CM_DISC_TONE[d.status].tone} dot size="sm">{CM_DISC_TONE[d.status].label}</Pill></div>}
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {!d._new && (
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 8 }}>
                <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>Redeemed</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 700, color: 'var(--text-primary)' }}>{d.used} <span style={{ color: 'var(--text-tertiary)', fontWeight: 400 }}>of {d.limit}</span></span>
              </div>
              <div style={{ height: 6, borderRadius: 9, background: 'var(--surface-inset)', overflow: 'hidden' }}><div style={{ width: pct + '%', height: '100%', background: pct >= 90 ? 'var(--warning)' : 'var(--brand-primary)' }} /></div>
            </div>
          )}
          <div><div style={lbl}>Coupon code</div><input defaultValue={d._new ? '' : d.code} placeholder="e.g. WELLNESS10" style={{ ...fld, fontFamily: 'var(--font-mono)', fontWeight: 600 }} /></div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div><div style={lbl}>Type</div>
              <div style={{ display: 'flex', gap: 6 }}>
                {[['percent', 'Percentage'], ['fixed', 'Fixed']].map(([k, lab]) => (
                  <span key={k} style={{ flex: 1, textAlign: 'center', padding: '7px 0', borderRadius: 8, fontSize: 12, fontWeight: 600, cursor: 'pointer', border: '1px solid ' + (d.type === k ? 'var(--brand-primary)' : 'var(--border-light)'), color: d.type === k ? 'var(--brand-primary)' : 'var(--text-secondary)', background: d.type === k ? 'var(--brand-primary-10)' : 'var(--surface)' }}>{lab}</span>
                ))}
              </div>
            </div>
            <div><div style={lbl}>Value</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-tertiary)' }}>{d.type === 'percent' ? '%' : '₦'}</span>
                <input defaultValue={d.value} style={{ ...fld, fontFamily: 'var(--font-mono)', fontWeight: 600 }} />
              </div>
            </div>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div><div style={lbl}>Redemption limit</div><input defaultValue={d.limit} style={{ ...fld, fontFamily: 'var(--font-mono)' }} /></div>
            <div><div style={lbl}>Min order</div><input defaultValue={d.min || 0} style={{ ...fld, fontFamily: 'var(--font-mono)' }} /></div>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div><div style={lbl}>Valid from</div><input defaultValue={d._new ? '' : d.from} placeholder="dd Mon" style={fld} /></div>
            <div><div style={lbl}>Valid to</div><input defaultValue={d._new ? '' : d.to} placeholder="dd Mon" style={fld} /></div>
          </div>
          <div><div style={lbl}>Applies to</div><select defaultValue={d.applies} style={fld}>{['All products', 'Wellness Boxes', 'Granola & Cereals', 'Staff only'].map(o => <option key={o}>{o}</option>)}</select></div>
        </div>

        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          {!d._new ? <button className="btn btn-ghost btn-sm" style={{ color: 'var(--text-tertiary)' }}>{d.status === 'disabled' ? 'Enable' : 'Disable'}</button> : <span />}
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> {d._new ? 'Create coupon' : 'Save'}</button>
          </div>
        </div>
      </div>
    </>
  );
}

// ═══ Overview dashboard ══════════════════════════════════════════════════
const CM_REVENUE = [82, 96, 74, 110, 128, 142, 96, 118, 134, 156, 168, 144, 182, 210]; // last 14 days (₦000s)
const CM_TOP = [
  { name: 'Ginger Wellness Shot', emoji: '🫚', color: '#c79100', units: 412, rev: 372000 },
  { name: 'Almond & Honey Granola', emoji: '🥣', color: '#b4741e', units: 308, rev: 286000 },
  { name: 'Cold-Brew Coffee', emoji: '☕', color: '#3a2a1a', units: 286, rev: 198000 },
  { name: 'Build-Your-Own Wellness Box', emoji: '📦', color: '#055a60', units: 142, rev: 1704000 },
];

function ScreenCommerceOverview() {
  const paid = CM_ORDERS.filter(o => o.pay === 'paid');
  const revenue = paid.reduce((a, o) => a + o.charge.total, 0);
  const lowStock = CM_PRODUCTS.filter(cmLow).length;
  const abandoned = CM_CARTS.filter(c => c.status === 'abandoned').length;
  const pendingPay = CM_ORDERS.filter(o => o.pay === 'pending').length;
  const max = Math.max(...CM_REVENUE);

  const kpis = [
    { l: 'Revenue (14d)', v: cmMoney(2480000), d: '+18%' },
    { l: 'Orders (14d)', v: '186', d: '+12%' },
    { l: 'Avg order value', v: cmMoney(13330), d: '+4%' },
    { l: 'Active products', v: CM_PRODUCTS.filter(p => p.status === 'active').length, d: lowStock + ' low' },
  ];
  const attention = [
    { icon: 'alertc', tone: 'var(--warning)', label: lowStock + ' products low on stock', sub: 'restock before they sell out', to: 'Inventory' },
    { icon: 'store',  tone: 'var(--text-secondary)', label: abandoned + ' abandoned carts', sub: 'recoverable with a reminder', to: 'Carts' },
    { icon: 'invoice', tone: 'var(--warning)', label: pendingPay + ' order awaiting payment', sub: 'draft intent not yet captured', to: 'Orders' },
  ];
  // Maker operations (Spec 058 Phase 4) — the make-side pulse on the overview.
  // Active alerts = Open + Acknowledged (the one active set, per landed 052).
  const activeAlerts = CM_ALERTS.filter(a => a.status === 'open' || a.status === 'acknowledged');
  const worstAlert = activeAlerts.slice().sort((a, b) =>
    ((a.refreshedAvailable != null ? a.refreshedAvailable : a.availableAtRaise) / a.reorderPoint) -
    ((b.refreshedAvailable != null ? b.refreshedAvailable : b.availableAtRaise) / b.reorderPoint))[0];
  const pendingPos = CM_POS.filter(p => p.status === 'pending');
  const makerTiles = [
    { l: 'Low stock (ingredients)', v: activeAlerts.length, s: worstAlert ? worstAlert.name + ': ' + cmUnit(worstAlert.availableAtRaise, worstAlert.unit) + ' available' : 'no active alerts', danger: true },
    { l: 'POs awaiting receipt', v: pendingPos.length, s: cmMoney(pendingPos.reduce((a, p) => a + p.total, 0)) + ' committed' },
    { l: 'This-week margin', v: CM_MARGIN.totals.marginPct.toFixed(1) + '%', s: cmMoney(CM_MARGIN.totals.unknownCogsRevenue) + ' unknown-COGS excluded' },
  ];

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Commerce overview</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>The wellness-food storefront at a glance — sales, stock and what needs attention.</div>
        </div>
        <button className="btn btn-sm"><Icon name="globe" size={12} /> View store</button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
        {kpis.map(k => (
          <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.d}</div>
          </div>
        ))}
      </div>

      {/* Maker operations (Spec 058 Phase 4) */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
        {makerTiles.map(k => (
          <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color: k.danger && k.v > 0 ? 'var(--danger)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 18, alignItems: 'start' }}>
        {/* Left: revenue + recent orders */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '16px 18px' }}>
            <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 14 }}>
              <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Revenue, last 14 days</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{cmMoney(2480000)}</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 6, height: 110 }}>
              {CM_REVENUE.map((v, i) => (
                <div key={i} style={{ flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'flex-end', height: '100%' }}>
                  <div title={'₦' + v + 'k'} style={{ height: (v / max * 100) + '%', borderRadius: '4px 4px 0 0', background: i === CM_REVENUE.length - 1 ? 'var(--brand-primary)' : 'var(--brand-primary-10)' }} />
                </div>
              ))}
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
            <div style={{ padding: '13px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Recent orders</span>
              <span style={{ fontSize: 11.5, color: 'var(--brand-primary)', cursor: 'pointer' }}>View all</span>
            </div>
            {CM_ORDERS.slice(0, 5).map((o, i) => (
              <div key={o.id} style={{ display: 'grid', gridTemplateColumns: '100px 1fr 100px 110px', gap: 10, padding: '10px 16px', alignItems: 'center', borderTop: i ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{o.id}</span>
                <span style={{ color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{o.buyer}</span>
                <span style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(o.charge.total, o.ccy)}</span>
                <span style={{ textAlign: 'right' }}><Pill tone={CM_PAY[o.pay].tone} dot size="sm">{CM_PAY[o.pay].label}</Pill></span>
              </div>
            ))}
          </div>
        </div>

        {/* Right: top products + needs attention */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
            <div style={{ padding: '13px 16px', borderBottom: '1px solid var(--border-light)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Top products, 14 days</div>
            {CM_TOP.map((p, i) => (
              <div key={p.name} style={{ display: 'flex', alignItems: 'center', gap: 11, padding: '11px 16px', borderTop: i ? '1px solid var(--border-light)' : 'none' }}>
                <span style={{ width: 30, height: 30, borderRadius: 7, background: p.color + '22', display: 'grid', placeItems: 'center', fontSize: 15, flex: 'none' }}>{p.emoji}</span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{p.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{p.units} units</div>
                </div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(p.rev)}</span>
              </div>
            ))}
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
            <div style={{ padding: '13px 16px', borderBottom: '1px solid var(--border-light)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Needs attention</div>
            {attention.map((a, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 11, padding: '12px 16px', borderTop: i ? '1px solid var(--border-light)' : 'none', cursor: 'pointer' }}>
                <Icon name={a.icon} size={15} color={a.tone} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{a.label}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{a.sub}</div>
                </div>
                <Icon name="chevron" size={13} color="var(--text-tertiary)" />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCommerceDiscounts, ScreenCommerceOverview });
