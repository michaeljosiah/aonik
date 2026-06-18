// Commerce · Spec 042 — Inventory group
// ScreenCommerceInventory — stock by variant + location (on-hand / reserved /
// available), set-on-hand adjust drawer, reservations & holds (Held / Committed /
// Released + TTL expiry sweep), and bundle buildability (bundles hold no stock —
// availability is the feasibility of filling their slots from component stock, §10/§12).
// Reuses CM_PRODUCTS / CM_KIND / CM_TOP_CATS / cmCatName from commerce-catalog.jsx.
// No decorative middots — spacing/commas/em-dash only; colored dots = state.

const CM_INV_TONE = {
  ok:   { tone: 'success', label: 'In stock' },
  low:  { tone: 'warning', label: 'Low' },
  out:  { tone: 'danger',  label: 'Out' },
  off:  { tone: 'muted',   label: 'Inactive' },
};
const cmInvStatus = (avail, active) => !active ? 'off' : avail <= 0 ? 'out' : avail <= 10 ? 'low' : 'ok';

// Flatten catalog variants into stock rows (bundles hold no stock of their own).
function cmInvRows() {
  const rows = [];
  CM_PRODUCTS.forEach(p => {
    if (p.kind === 'bundle') return;
    p.variants.forEach((v, i) => rows.push({
      pid: p.id, product: p.name, emoji: p.emoji, color: p.color, cat: p.cat,
      sku: v.sku, opt: v.opt, active: v.active,
      location: i % 3 === 2 ? 'Abuja DC' : 'Lagos DC',
      onHand: v.onHand, reserved: v.reserved,
    }));
  });
  return rows;
}

// Live reservation holds (InventoryReservation). held = TTL countdown.
const CM_RESERVATIONS = [
  { sku: 'SHOT-GIN-1', product: 'Ginger Wellness Shot', opt: 'Single',  qty: 12, ref: 'cart_9f2a', kind: 'cart',  status: 'held',      age: '4m ago',  expires: 26 },
  { sku: 'GRN-ALM-500',product: 'Almond & Honey Granola', opt: '500 g', qty: 4,  ref: 'cart_7b10', kind: 'cart',  status: 'held',      age: '18m ago', expires: 12 },
  { sku: 'GRN-BER-500',product: 'Berry Bliss Granola', opt: '500 g',    qty: 5,  ref: 'cart_3e8c', kind: 'cart',  status: 'held',      age: '22m ago', expires: 8 },
  { sku: 'DRK-CB-250', product: 'Cold-Brew Coffee', opt: '250 ml',      qty: 12, ref: 'ord_2041',  kind: 'order', status: 'committed', age: '1h ago',  expires: null },
  { sku: 'SNK-BAR-CAC',product: 'Protein Energy Bar', opt: 'Cacao',     qty: 7,  ref: 'ord_2037',  kind: 'order', status: 'committed', age: '2h ago',  expires: null },
  { sku: 'SHOT-GIN-6', product: 'Ginger Wellness Shot', opt: '6-pack',  qty: 6,  ref: 'cart_5a01', kind: 'cart',  status: 'released',  age: '46m ago', expires: null },
];
const CM_RES_TONE = { held: 'warning', committed: 'success', released: 'muted' };

// Bundle buildability — limited by the scarcest source category (§12 fan-out).
function cmBuildable(rows) {
  return CM_PRODUCTS.filter(p => p.kind === 'bundle').map(p => {
    const need = p.bundle.slots.reduce((a, s) => a + s.min, 0);
    const sources = [...new Set(p.bundle.slots.map(s => s.from))];
    const pool = rows.filter(r => sources.includes(r.cat) && r.active).reduce((a, r) => a + Math.max(0, r.onHand - r.reserved), 0);
    return { p, need, sources, buildable: need ? Math.floor(pool / need) : 0 };
  });
}

function ScreenCommerceInventory() {
  const rows = React.useMemo(() => cmInvRows(), []);
  const [tab, setTab] = React.useState('levels');
  const [lowOnly, setLowOnly] = React.useState(false);
  const [loc, setLoc] = React.useState('all');
  const [sel, setSel] = React.useState(null);   // row for adjust drawer

  const onHand = rows.reduce((a, r) => a + r.onHand, 0);
  const reserved = rows.reduce((a, r) => a + r.reserved, 0);
  const available = onHand - reserved;
  const lowOut = rows.filter(r => r.active && (r.onHand - r.reserved) <= 10).length;
  const heldCount = CM_RESERVATIONS.filter(r => r.status === 'held').length;

  let shown = rows;
  if (loc !== 'all') shown = shown.filter(r => r.location === loc);
  if (lowOnly) shown = shown.filter(r => r.active && (r.onHand - r.reserved) <= 10);

  const builds = cmBuildable(rows);

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-irow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Inventory</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Stock per variant and location. Checkout reserves before the order, commits on payment, and releases on expiry — so it never oversells.</div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <div style={{ position: 'relative' }}>
              <input placeholder="Search SKU or product" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6, padding: '7px 10px 7px 28px', fontSize: 12.5, color: 'var(--text-primary)', width: 220, fontFamily: 'var(--font-sans)' }} />
              <span style={{ position: 'absolute', left: 9, top: 8 }}><Icon name="search" size={13} color="var(--text-tertiary)" /></span>
            </div>
            <button className="btn btn-sm"><Icon name="download" size={12} /> Export</button>
          </div>
        </div>

        {/* KPIs */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {[
            { l: 'Units on hand', v: onHand.toLocaleString('en-GB'), s: rows.length + ' stocked SKUs' },
            { l: 'Reserved', v: reserved.toLocaleString('en-GB'), s: heldCount + ' active holds' },
            { l: 'Available', v: available.toLocaleString('en-GB'), s: 'on hand minus reserved' },
            { l: 'Low / out of stock', v: lowOut, s: '≤ 10 available', warn: true },
          ].map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: k.warn && k.v > 0 ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        {/* Tabs */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10 }}>
            {[{ id: 'levels', label: 'Stock levels' }, { id: 'holds', label: 'Reservations & holds' }].map(tt => {
              const on = tab === tt.id;
              return (
                <button key={tt.id} onClick={() => setTab(tt.id)} style={{
                  display: 'inline-flex', alignItems: 'center', gap: 7, height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none',
                  fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent',
                  color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none',
                }}>
                  {tt.label}
                  {tt.id === 'holds' && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: on ? 'var(--warning)' : 'var(--text-tertiary)' }}>{heldCount}</span>}
                </button>
              );
            })}
          </div>
          {tab === 'levels' && (
            <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              {['all', ...CM_LOC].map(l => (
                <button key={l} onClick={() => setLoc(l)} style={{
                  fontSize: 11, padding: '4px 10px', borderRadius: 999, cursor: 'pointer',
                  border: '1px solid ' + (loc === l ? 'var(--brand-primary)' : 'var(--border-light)'),
                  background: loc === l ? 'var(--brand-primary-10)' : 'var(--surface)', color: loc === l ? 'var(--brand-primary)' : 'var(--text-secondary)', fontWeight: loc === l ? 600 : 500,
                }}>{l === 'all' ? 'All locations' : l}</button>
              ))}
              <button onClick={() => setLowOnly(v => !v)} style={{
                display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 11.5, padding: '5px 11px', borderRadius: 999, cursor: 'pointer', marginLeft: 4,
                border: '1px solid ' + (lowOnly ? 'var(--warning)' : 'var(--border-light)'), background: lowOnly ? '#b4741e14' : 'var(--surface)', color: lowOnly ? '#b4741e' : 'var(--text-secondary)', fontWeight: lowOnly ? 600 : 500,
              }}><Icon name="alertc" size={12} /> Low stock only</button>
            </div>
          )}
        </div>

        {/* Stock levels */}
        {tab === 'levels' && (
          <>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 120px 90px 70px 70px 80px 96px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Variant</div><div>SKU</div><div>Location</div><div style={{ textAlign: 'right' }}>On hand</div><div style={{ textAlign: 'right' }}>Reserved</div><div style={{ textAlign: 'right' }}>Available</div><div style={{ textAlign: 'right' }}>Status</div>
              </div>
              {shown.map((r, i) => {
                const avail = r.onHand - r.reserved; const st = cmInvStatus(avail, r.active);
                return (
                  <div key={r.sku} className="cm-irow" onClick={() => setSel(r)} style={{ display: 'grid', gridTemplateColumns: '1fr 120px 90px 70px 70px 80px 96px', gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: i < shown.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: r.active ? 1 : 0.6 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <span style={{ width: 26, height: 26, borderRadius: 6, background: r.color + '22', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{r.emoji}</span>
                      <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{r.product}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{r.opt}</div></div>
                    </div>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{r.sku}</div>
                    <div style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{r.location}</div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{r.onHand}</div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: r.reserved > 0 ? 'var(--text-secondary)' : 'var(--text-tertiary)' }}>{r.reserved}</div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: st === 'out' ? 'var(--danger)' : st === 'low' ? 'var(--warning)' : 'var(--text-primary)' }}>{avail}</div>
                    <div style={{ textAlign: 'right' }}><Pill tone={CM_INV_TONE[st].tone} dot size="sm">{CM_INV_TONE[st].label}</Pill></div>
                  </div>
                );
              })}
            </div>

            {/* Bundle buildability */}
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 16 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
                <Icon name="package" size={14} color="#7b76b6" />
                <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Build-your-own boxes</span>
                <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>hold no stock — buildability is limited by component availability</span>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
                {builds.map(b => (
                  <div key={b.p.id} style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px', display: 'flex', alignItems: 'center', gap: 12 }}>
                    <span style={{ width: 34, height: 34, borderRadius: 8, background: b.p.color + '22', display: 'grid', placeItems: 'center', fontSize: 17, flex: 'none' }}>{b.p.emoji}</span>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{b.p.name}</div>
                      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>from {b.sources.map(cmCatName).join(', ')}</div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: b.buildable <= 5 ? 'var(--warning)' : 'var(--text-primary)' }}>{b.buildable}</div>
                      <div style={{ fontSize: 9.5, color: 'var(--text-tertiary)' }}>buildable</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </>
        )}

        {/* Reservations & holds */}
        {tab === 'holds' && (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '11px 14px', borderRadius: 10, background: 'var(--surface-inset)', border: '1px solid var(--border-light)' }}>
              <Icon name="refresh" size={14} color="var(--brand-primary)" />
              <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>
                Reservation sweep runs every 5 min, holds expire after 30 min — <b style={{ color: 'var(--text-primary)' }}>next sweep in ~2 min</b>. Expired holds release their stock automatically.
              </div>
            </div>

            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 130px 70px 110px 90px 110px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Variant</div><div>Held for</div><div style={{ textAlign: 'right' }}>Qty</div><div>Status</div><div style={{ textAlign: 'right' }}>Age</div><div style={{ textAlign: 'right' }}>Expires</div>
              </div>
              {CM_RESERVATIONS.map((r, i) => (
                <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 130px 70px 110px 90px 110px', gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: i < CM_RESERVATIONS.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: r.status === 'released' ? 0.6 : 1 }}>
                  <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{r.product}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.sku}</div></div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <Icon name={r.kind === 'order' ? 'invoice' : 'store'} size={12} color="var(--text-tertiary)" />
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{r.ref}</span>
                  </div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{r.qty}</div>
                  <div><Pill tone={CM_RES_TONE[r.status]} dot size="sm">{r.status[0].toUpperCase() + r.status.slice(1)}</Pill></div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-tertiary)' }}>{r.age}</div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11.5, color: r.expires != null ? (r.expires <= 10 ? 'var(--warning)' : 'var(--text-secondary)') : 'var(--text-tertiary)' }}>
                    {r.expires != null ? 'in ' + r.expires + 'm' : '—'}
                  </div>
                </div>
              ))}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
              <Icon name="check2" size={12} color="var(--success)" /> Committed holds belong to paid orders — their stock is already drawn down. Released holds returned stock to available.
            </div>
          </>
        )}
      </div>

      {sel && <CmInvAdjustDrawer row={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmInvAdjustDrawer({ row: r, onClose }) {
  const avail = r.onHand - r.reserved;
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 440, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ width: 40, height: 40, borderRadius: 9, background: r.color + '22', display: 'grid', placeItems: 'center', fontSize: 19, flex: 'none' }}>{r.emoji}</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{r.product}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>{r.opt}<span style={{ marginLeft: 10, fontFamily: 'var(--font-mono)' }}>{r.sku}</span></div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
            {[['On hand', r.onHand, 'var(--text-primary)'], ['Reserved', r.reserved, 'var(--text-secondary)'], ['Available', avail, avail <= 10 ? 'var(--warning)' : 'var(--success)']].map(([l, v, c]) => (
              <div key={l} style={{ background: 'var(--surface-inset)', borderRadius: 9, padding: '10px 12px' }}>
                <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>{l}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: c, marginTop: 3 }}>{v}</div>
              </div>
            ))}
          </div>

          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>Location</div>
            <select defaultValue={r.location} style={{ width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px', fontSize: 13, color: 'var(--text-primary)' }}>{CM_LOC.map(l => <option key={l}>{l}</option>)}</select>
          </div>
          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>Set on-hand to</div>
            <input defaultValue={r.onHand} style={{ width: 140, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px', fontSize: 14, fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }} />
          </div>
          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>Reason</div>
            <select defaultValue="Recount" style={{ width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px', fontSize: 13, color: 'var(--text-primary)' }}>
              {['Recount', 'Restock', 'Damage / write-off', 'Correction'].map(o => <option key={o}>{o}</option>)}
            </select>
          </div>
          <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--brand-primary-10)', borderLeft: '3px solid var(--brand-primary)' }}>
            <Icon name="alertc" size={14} color="var(--brand-primary)" style={{ flex: 'none', marginTop: 1 }} />
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>Setting on-hand adjusts available stock immediately. Reserved units stay held — it never releases an active reservation.</div>
          </div>
        </div>

        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button className="btn btn-outline btn-sm" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Save adjustment</button>
        </div>
      </div>
    </>
  );
}

const CM_LOC = ['Lagos DC', 'Abuja DC'];

Object.assign(window, { ScreenCommerceInventory });
