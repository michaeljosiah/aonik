// Commerce · Spec 042 — Carts & Orders group
//   • ScreenCommerceCarts  — open / abandoned / checked-out carts (guest vs party),
//     contents incl. build-your-own-box selections, recovery.
//   • ScreenCommerceOrders — ProductPurchase orders on the unified Order spine:
//     line items + box selections, lifecycle stepper, and the OrderChargeSummary
//     (subtotal → discount → tax → payable). Capture stays a Finance high-tier action.
// Reuses cmMoney from commerce-catalog.jsx. No decorative middots; dots = state only.

const CM_CART_STATUS = { open: { tone: 'success', label: 'Open' }, abandoned: { tone: 'warning', label: 'Abandoned' }, checkedout: { tone: 'muted', label: 'Checked out' } };
const CM_PAY  = { paid: { tone: 'success', label: 'Paid' }, pending: { tone: 'warning', label: 'Pending payment' }, refunded: { tone: 'muted', label: 'Refunded' }, cancelled: { tone: 'danger', label: 'Cancelled' } };
const CM_FULFIL = { fulfilled: { tone: 'success', label: 'Fulfilled' }, unfulfilled: { tone: 'warning', label: 'Unfulfilled' }, partial: { tone: 'pending', label: 'Partial' } };

const CM_CARTS = [
  { id: 'cart_9f2a', buyer: 'Guest', sub: 'anonymous, just browsing', kind: 'guest', status: 'open', ccy: 'NGN', age: '4m ago', lines: [
    { box: true, name: 'Build-Your-Own Wellness Box', sku: 'BOX-BYO', qty: 1, unit: 12000, sel: [
      { name: 'Almond & Honey Granola (500 g)', sku: 'GRN-ALM-500', qty: 2 },
      { name: 'Cacao Crunch Granola (500 g)', sku: 'GRN-CAC-500', qty: 2 },
      { name: 'Protein Energy Bar (Cacao)', sku: 'SNK-BAR-CAC', qty: 2 },
    ] },
    { name: 'Ginger Wellness Shot (6-pack)', sku: 'SHOT-GIN-6', qty: 1, unit: 5000 },
  ] },
  { id: 'cart_7b10', buyer: 'Adaeze Nwosu', sub: 'returning customer', kind: 'party', status: 'open', ccy: 'NGN', age: '18m ago', lines: [
    { name: 'Almond & Honey Granola (500 g)', sku: 'GRN-ALM-500', qty: 1, unit: 4500 },
    { name: 'Cold-Brew Coffee (1 L)', sku: 'DRK-CB-1L', qty: 1, unit: 5500 },
  ] },
  { id: 'cart_3e8c', buyer: 'Guest', sub: 'anonymous', kind: 'guest', status: 'abandoned', ccy: 'NGN', age: '2h ago', lines: [
    { name: 'Berry Bliss Granola (500 g)', sku: 'GRN-BER-500', qty: 3, unit: 5200 },
    { name: 'Hibiscus Cooler (500 ml)', sku: 'DRK-ZOBO-500', qty: 6, unit: 1500 },
  ] },
  { id: 'cart_5a01', buyer: 'Tunde Bello', sub: 'returning customer', kind: 'party', status: 'abandoned', ccy: 'NGN', age: '1d ago', lines: [
    { name: 'Ginger Wellness Shot (6-pack)', sku: 'SHOT-GIN-6', qty: 1, unit: 5000 },
  ] },
  { id: 'cart_2041', buyer: 'Maya Okonkwo', sub: 'checked out, order ord_2041', kind: 'party', status: 'checkedout', ccy: 'NGN', age: '1h ago', lines: [
    { name: 'Cold-Brew Coffee (250 ml)', sku: 'DRK-CB-250', qty: 12, unit: 1800 },
  ] },
  { id: 'cart_1c77', buyer: 'Guest', sub: 'anonymous', kind: 'guest', status: 'open', ccy: 'NGN', age: '32m ago', lines: [
    { name: 'Turmeric Shot', sku: 'SHOT-TUR-1', qty: 4, unit: 950 },
  ] },
];
const cmCartTotal = c => c.lines.reduce((a, l) => a + l.qty * l.unit, 0);
const cmCartItems = c => c.lines.reduce((a, l) => a + l.qty, 0);

const CM_ORDERS = [
  { id: 'ord_2041', buyer: 'Maya Okonkwo', sub: 'party, Lagos', kind: 'party', date: 'Today 09:14', ccy: 'NGN',
    pay: 'paid', fulfil: 'fulfilled', method: 'Card', intent: 'pi_8c12fa', ship: 'Lekki, Lagos',
    lines: [
      { box: true, name: 'Build-Your-Own Wellness Box', sku: 'BOX-BYO', qty: 1, unit: 12000, sel: [
        { name: 'Almond & Honey Granola (500 g)', sku: 'GRN-ALM-500', qty: 3 },
        { name: 'Ginger Wellness Shot (Single)', sku: 'SHOT-GIN-1', qty: 3 },
      ] },
      { name: 'Cold-Brew Coffee (250 ml)', sku: 'DRK-CB-250', qty: 2, unit: 1800 },
    ],
    charge: { subtotal: 15600, discountCode: 'WELLNESS10', discount: 1560, tax: 0, total: 14040 } },
  { id: 'ord_2037', buyer: 'Guest (J. Adeyemi)', sub: 'guest checkout', kind: 'guest', date: 'Today 08:02', ccy: 'NGN',
    pay: 'paid', fulfil: 'unfulfilled', method: 'Bank transfer', intent: 'pi_77a0b1', ship: 'Yaba, Lagos',
    lines: [{ name: 'Protein Energy Bar (Cacao)', sku: 'SNK-BAR-CAC', qty: 6, unit: 1200 }, { name: 'Cacao Crunch Granola (1 kg)', sku: 'GRN-CAC-1KG', qty: 1, unit: 8600 }],
    charge: { subtotal: 15800, discountCode: null, discount: 0, tax: 0, total: 15800 } },
  { id: 'ord_2034', buyer: 'Adaeze Nwosu', sub: 'party, Abuja', kind: 'party', date: 'Today 07:20', ccy: 'NGN',
    pay: 'pending', fulfil: 'unfulfilled', method: 'Card', intent: 'pi_5c91 (draft)', ship: 'Maitama, Abuja',
    lines: [{ name: 'Almond & Honey Granola (1 kg)', sku: 'GRN-ALM-1KG', qty: 1, unit: 8000 }],
    charge: { subtotal: 8000, discountCode: null, discount: 0, tax: 0, total: 8000 } },
  { id: 'ord_2028', buyer: 'Chidi Okafor', sub: 'party, Lagos', kind: 'party', date: 'Yesterday', ccy: 'NGN',
    pay: 'paid', fulfil: 'fulfilled', method: 'Card', intent: 'pi_41bd22', ship: 'Ikoyi, Lagos',
    lines: [{ name: 'Hibiscus Cooler (500 ml)', sku: 'DRK-ZOBO-500', qty: 10, unit: 1500 }],
    charge: { subtotal: 15000, discountCode: null, discount: 0, tax: 0, total: 15000 } },
  { id: 'ord_2019', buyer: 'Guest (F. Eze)', sub: 'guest checkout', kind: 'guest', date: '2 days ago', ccy: 'NGN',
    pay: 'refunded', fulfil: 'fulfilled', method: 'Card', intent: 'pi_2a7c08', ship: 'Surulere, Lagos',
    lines: [{ name: 'Ginger Wellness Shot (12-pack)', sku: 'SHOT-GIN-12', qty: 1, unit: 9400 }],
    charge: { subtotal: 9400, discountCode: null, discount: 0, tax: 0, total: 9400 } },
  { id: 'ord_2009', buyer: 'Maya Okonkwo', sub: 'party, Lagos', kind: 'party', date: '3 days ago', ccy: 'NGN',
    pay: 'cancelled', fulfil: 'unfulfilled', method: 'Card', intent: 'pi_19fe44 (voided)', ship: 'Lekki, Lagos',
    lines: [{ name: 'Cold-Brew Coffee (1 L)', sku: 'DRK-CB-1L', qty: 2, unit: 5500 }],
    charge: { subtotal: 11000, discountCode: null, discount: 0, tax: 0, total: 11000 } },
];

// ═══ Carts ═══════════════════════════════════════════════════════════════
function ScreenCommerceCarts() {
  const [status, setStatus] = React.useState('all');
  const [sel, setSel] = React.useState(null);
  const shown = status === 'all' ? CM_CARTS : CM_CARTS.filter(c => c.status === status);

  const open = CM_CARTS.filter(c => c.status === 'open');
  const abandoned = CM_CARTS.filter(c => c.status === 'abandoned');
  const kpis = [
    { l: 'Open carts', v: open.length, s: 'live sessions' },
    { l: 'Abandoned', v: abandoned.length, s: 'recoverable', warn: true },
    { l: 'Open cart value', v: cmMoney(open.reduce((a, c) => a + cmCartTotal(c), 0)), s: 'not yet checked out' },
    { l: 'Checked out', v: CM_CARTS.filter(c => c.status === 'checkedout').length, s: 'became orders' },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-crow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Carts</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Live and abandoned baskets. Guest carts are keyed by an anonymous token and merge into the customer on sign-in; abandoned carts can be recovered.</div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: k.warn && k.v > 0 ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, alignSelf: 'flex-start' }}>
          {[{ id: 'all', label: 'All' }, { id: 'open', label: 'Open' }, { id: 'abandoned', label: 'Abandoned' }, { id: 'checkedout', label: 'Checked out' }].map(s => {
            const on = status === s.id;
            return <button key={s.id} onClick={() => setStatus(s.id)} style={{ height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none', fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent', color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none' }}>{s.label}</button>;
          })}
        </div>

        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr 70px 110px 110px 90px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Cart</div><div>Buyer</div><div style={{ textAlign: 'right' }}>Items</div><div style={{ textAlign: 'right' }}>Value</div><div>Status</div><div style={{ textAlign: 'right' }}>Activity</div>
          </div>
          {shown.map((c, i) => (
            <div key={c.id} className="cm-crow" onClick={() => setSel(c)} style={{ display: 'grid', gridTemplateColumns: '120px 1fr 70px 110px 110px 90px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < shown.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: c.status === 'checkedout' ? 0.7 : 1 }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{c.id}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                <span style={{ width: 26, height: 26, borderRadius: 999, background: c.kind === 'guest' ? 'var(--surface-inset)' : 'var(--brand-primary-10)', color: c.kind === 'guest' ? 'var(--text-tertiary)' : 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name={c.kind === 'guest' ? 'user' : 'users'} size={13} /></span>
                <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{c.buyer}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{c.sub}</div></div>
              </div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{cmCartItems(c)}</div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(cmCartTotal(c))}</div>
              <div><Pill tone={CM_CART_STATUS[c.status].tone} dot size="sm">{CM_CART_STATUS[c.status].label}</Pill></div>
              <div style={{ textAlign: 'right', fontSize: 11.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{c.age}</div>
            </div>
          ))}
        </div>
      </div>
      {sel && <CmCartDrawer c={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmLineItems({ lines, ccy }) {
  return (
    <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
      {lines.map((l, i) => (
        <div key={i} style={{ borderTop: i ? '1px solid var(--border-light)' : 'none' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 44px 100px', gap: 10, padding: '10px 13px', alignItems: 'center' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
              {l.box && <span style={{ fontSize: 9, fontWeight: 700, letterSpacing: '0.04em', color: '#7b76b6', background: '#7b76b618', padding: '2px 6px', borderRadius: 4, fontFamily: 'var(--font-mono)', flex: 'none' }}>BOX</span>}
              <div style={{ minWidth: 0 }}>
                <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{l.name}</div>
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{l.sku}</div>
              </div>
            </div>
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)' }}>×{l.qty}</div>
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(l.qty * l.unit, ccy)}</div>
          </div>
          {l.box && l.sel && (
            <div style={{ padding: '0 13px 10px 13px' }}>
              <div style={{ borderLeft: '2px solid #7b76b633', paddingLeft: 12, display: 'flex', flexDirection: 'column', gap: 5 }}>
                {l.sel.map((s, j) => (
                  <div key={j} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 11.5, color: 'var(--text-secondary)' }}>
                    <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>×{s.qty}</span>
                    <span>{s.name}</span>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', marginLeft: 'auto' }}>{s.sku}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function CmCartDrawer({ c, onClose }) {
  const total = cmCartTotal(c);
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 460, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ width: 40, height: 40, borderRadius: 999, background: c.kind === 'guest' ? 'var(--surface-inset)' : 'var(--brand-primary-10)', color: c.kind === 'guest' ? 'var(--text-tertiary)' : 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name={c.kind === 'guest' ? 'user' : 'users'} size={18} /></span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}><span style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{c.buyer}</span><Pill tone={CM_CART_STATUS[c.status].tone} dot size="sm">{CM_CART_STATUS[c.status].label}</Pill></div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{c.id}<span style={{ marginLeft: 10, fontFamily: 'var(--font-sans)' }}>{c.age}</span></div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>
        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 14 }}>
          <CmLineItems lines={c.lines} ccy={c.ccy} />
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '4px 2px' }}>
            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Subtotal</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{cmMoney(total, c.ccy)}</span>
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>Final discount, tax and total are computed at checkout when the order is created.</div>
        </div>
        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          {c.status === 'abandoned'
            ? <><button className="btn btn-outline btn-sm" onClick={onClose}>Close</button><button className="btn btn-primary btn-sm"><Icon name="mail" size={12} /> Send recovery link</button></>
            : c.status === 'open'
            ? <><button className="btn btn-outline btn-sm" onClick={onClose}>Close</button><button className="btn btn-primary btn-sm">Resume checkout <Icon name="arrowright" size={12} /></button></>
            : <button className="btn btn-outline btn-sm" onClick={onClose}>View order</button>}
        </div>
      </div>
    </>
  );
}

// ═══ Orders ══════════════════════════════════════════════════════════════
function ScreenCommerceOrders() {
  const [pay, setPay] = React.useState('all');
  const [sel, setSel] = React.useState(null);
  const shown = pay === 'all' ? CM_ORDERS : CM_ORDERS.filter(o => o.pay === pay);

  const paid = CM_ORDERS.filter(o => o.pay === 'paid');
  const revenue = paid.reduce((a, o) => a + o.charge.total, 0);
  const kpis = [
    { l: 'Orders', v: CM_ORDERS.length, s: 'product purchases' },
    { l: 'Revenue (paid)', v: cmMoney(revenue), s: paid.length + ' paid orders' },
    { l: 'Avg order value', v: cmMoney(Math.round(revenue / Math.max(1, paid.length))), s: 'across paid' },
    { l: 'Awaiting fulfilment', v: CM_ORDERS.filter(o => o.pay === 'paid' && o.fulfil !== 'fulfilled').length, s: 'paid, not shipped', warn: true },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-orow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Orders</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Product purchases on the unified Order spine. Each is funded by a payment intent; capture is a Finance-governed step. Order, payment and ledger stay distinct.</div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: k.warn && k.v > 0 ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, alignSelf: 'flex-start' }}>
          {[{ id: 'all', label: 'All' }, { id: 'paid', label: 'Paid' }, { id: 'pending', label: 'Pending' }, { id: 'refunded', label: 'Refunded' }, { id: 'cancelled', label: 'Cancelled' }].map(s => {
            const on = pay === s.id;
            return <button key={s.id} onClick={() => setPay(s.id)} style={{ height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none', fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent', color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none' }}>{s.label}</button>;
          })}
        </div>

        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '110px 1fr 100px 130px 110px 90px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Order</div><div>Buyer</div><div style={{ textAlign: 'right' }}>Total</div><div>Payment</div><div>Fulfilment</div><div style={{ textAlign: 'right' }}>Date</div>
          </div>
          {shown.map((o, i) => (
            <div key={o.id} className="cm-orow" onClick={() => setSel(o)} style={{ display: 'grid', gridTemplateColumns: '110px 1fr 100px 130px 110px 90px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < shown.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: o.pay === 'cancelled' ? 0.65 : 1 }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', fontWeight: 500 }}>{o.id}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                <span style={{ width: 24, height: 24, borderRadius: 999, background: o.kind === 'guest' ? 'var(--surface-inset)' : 'var(--brand-primary-10)', color: o.kind === 'guest' ? 'var(--text-tertiary)' : 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name={o.kind === 'guest' ? 'user' : 'users'} size={12} /></span>
                <div><div style={{ color: 'var(--text-primary)' }}>{o.buyer}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{o.sub}</div></div>
              </div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(o.charge.total, o.ccy)}</div>
              <div><Pill tone={CM_PAY[o.pay].tone} dot size="sm">{CM_PAY[o.pay].label}</Pill></div>
              <div><Pill tone={CM_FULFIL[o.fulfil].tone} dot size="sm">{CM_FULFIL[o.fulfil].label}</Pill></div>
              <div style={{ textAlign: 'right', fontSize: 11.5, color: 'var(--text-tertiary)' }}>{o.date}</div>
            </div>
          ))}
        </div>
      </div>
      {sel && <CmOrderDrawer o={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmOrderStepper({ o }) {
  const steps = ['Created', 'Invoiced', 'Funded', 'Paid', 'Fulfilled'];
  // completed-step count; the next index is the in-progress (current) step, not a checkmark.
  // pending = invoiced, funding in progress (draft intent) — Funded is current, not done.
  const completed = o.pay === 'pending' ? 2 : o.fulfil === 'fulfilled' ? 5 : 4;
  const halted = o.pay === 'cancelled' || o.pay === 'refunded';
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 0 }}>
        {steps.map((s, i) => {
          const isDone = i < completed && !halted, isCur = i === completed && !halted;
          return (
            <React.Fragment key={s}>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 5, flex: 'none' }}>
                <span style={{ width: 20, height: 20, borderRadius: 999, display: 'grid', placeItems: 'center', fontSize: 10, fontWeight: 700, background: isDone || isCur ? 'var(--brand-primary)' : 'var(--surface-inset)', color: isDone || isCur ? '#fff' : 'var(--text-tertiary)', border: isDone || isCur ? 'none' : '1px solid var(--border-light)' }}>
                  {isDone ? <Icon name="check" size={11} color="#fff" /> : i + 1}
                </span>
                <span style={{ fontSize: 9.5, color: isDone || isCur ? 'var(--text-primary)' : 'var(--text-tertiary)', fontWeight: isCur ? 600 : 500 }}>{s}</span>
              </div>
              {i < steps.length - 1 && <div style={{ flex: 1, height: 2, background: i < completed && !halted ? 'var(--brand-primary)' : 'var(--border-light)', margin: '0 2px', marginBottom: 16 }} />}
            </React.Fragment>
          );
        })}
      </div>
      {halted && <div style={{ marginTop: 10, fontSize: 11.5, color: o.pay === 'refunded' ? 'var(--text-secondary)' : 'var(--danger)', display: 'flex', alignItems: 'center', gap: 6 }}><Icon name={o.pay === 'refunded' ? 'arrows' : 'ban'} size={12} />{o.pay === 'refunded' ? 'Refunded after fulfilment — a Finance-governed high-tier action.' : 'Cancelled before capture; payment intent voided, reserved stock released.'}</div>}
    </div>
  );
}

function CmChargeSummary({ ch, ccy }) {
  return (
    <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px', display: 'flex', flexDirection: 'column', gap: 8 }}>
      {[['Subtotal', ch.subtotal, false]].map(([l, v]) => (
        <div key={l} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, color: 'var(--text-secondary)' }}><span>{l}</span><span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmMoney(v, ccy)}</span></div>
      ))}
      {ch.discount > 0 && (
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, color: 'var(--text-secondary)' }}>
          <span>Discount {ch.discountCode && <span style={{ fontSize: 10, fontFamily: 'var(--font-mono)', color: 'var(--brand-primary)', background: 'var(--brand-primary-10)', padding: '1px 6px', borderRadius: 4, marginLeft: 4 }}>{ch.discountCode}</span>}</span>
          <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--success)' }}>−{cmMoney(ch.discount, ccy)}</span>
        </div>
      )}
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, color: 'var(--text-secondary)' }}><span>Tax</span><span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{ch.tax > 0 ? cmMoney(ch.tax, ccy) : '—'}</span></div>
      <div style={{ height: 1, background: 'var(--border-light)' }} />
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}><span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Total payable</span><span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{cmMoney(ch.total, ccy)}</span></div>
    </div>
  );
}

function CmOrderDrawer({ o, onClose }) {
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 560, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'flex-start', gap: 12 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{o.id}</span>
              <Pill tone={CM_PAY[o.pay].tone} dot size="sm">{CM_PAY[o.pay].label}</Pill>
              <Pill tone={CM_FULFIL[o.fulfil].tone} dot size="sm">{CM_FULFIL[o.fulfil].label}</Pill>
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 3 }}>{o.buyer}<span style={{ marginLeft: 10 }}>{o.date}</span><span style={{ marginLeft: 10 }}>ships to {o.ship}</span></div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ padding: '14px 16px 4px', border: '1px solid var(--border-light)', borderRadius: 10 }}><CmOrderStepper o={o} /></div>

          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Items</div>
            <CmLineItems lines={o.lines} ccy={o.ccy} />
          </div>

          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Charge summary</div>
            <CmChargeSummary ch={o.charge} ccy={o.ccy} />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <div style={{ background: 'var(--surface-inset)', borderRadius: 10, padding: '11px 13px' }}>
              <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>Payment</div>
              <div style={{ fontSize: 12.5, color: 'var(--text-primary)', marginTop: 4 }}>{o.method}</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', marginTop: 2 }}>{o.intent}</div>
            </div>
            <div style={{ background: 'var(--surface-inset)', borderRadius: 10, padding: '11px 13px' }}>
              <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>Fulfilment</div>
              <div style={{ fontSize: 12.5, color: 'var(--text-primary)', marginTop: 4 }}>{CM_FULFIL[o.fulfil].label}</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{o.ship}</div>
            </div>
          </div>
        </div>

        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <button className="btn btn-ghost btn-sm" disabled={o.pay !== 'paid'} title="Refund is a Finance high-tier action (durable proposal)" style={{ color: o.pay === 'paid' ? 'var(--danger)' : 'var(--text-tertiary)' }}><Icon name="arrows" size={12} /> Refund</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm">View invoice</button>
            <button className="btn btn-primary btn-sm" disabled={o.fulfil === 'fulfilled'}><Icon name="package" size={12} /> {o.fulfil === 'fulfilled' ? 'Fulfilled' : 'Fulfil order'}</button>
          </div>
        </div>
      </div>
    </>
  );
}

Object.assign(window, { ScreenCommerceCarts, ScreenCommerceOrders });
