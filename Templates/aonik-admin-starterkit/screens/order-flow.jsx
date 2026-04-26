// ─── Order flow screens ────────────────────────────────────────────────────
// 1. ScreenOrderSuccess   — animated confirmation after submit
// 2. ScreenPaymentCollection — choose how to collect payment / send invoice
// 3. ScreenOrderItemDetail — animated pipeline + item state detail

// ─── Shared data (mirrors orders.jsx, must be re-declared for scope) ────────
const FLOW_PARTIES = [
  { id: 'p1', name: 'Primrose Logistics', color: '#055a60' },
  { id: 'p2', name: 'Oliver Chen',        color: '#7b76b6' },
  { id: 'p4', name: 'Northstar Freight',  color: '#0097a9' },
  { id: 'p5', name: 'James Okafor',       color: '#1f7a5e' },
];

function flowParty(id) { return FLOW_PARTIES.find(p => p.id === id) || { name: id, color: '#055a60' }; }
function flowInitials(n) { return (n||'?').split(' ').map(w=>w[0]).slice(0,2).join('').toUpperCase(); }

function FlowAvatar({ id, size = 28 }) {
  const p = flowParty(id);
  return (
    <div style={{
      width: size, height: size, borderRadius: size * 0.28,
      background: p.color, color: '#fff', flex: 'none',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: size * 0.38,
    }}>{flowInitials(p.name)}</div>
  );
}

// ── Demo order data ────────────────────────────────────────────────────────
const DEMO_ORDER = {
  id: 'ORD-20250422-0047',
  items: [
    { id: 'itm-01', type: 'bill',     label: 'Ikeja Electric',   sub: 'Prepaid token · 7012345678', amount: 15000, currency: 'NGN', payerId: 'p1', benefId: 'p5', color: '#1e4d8c', symbol: 'IE' },
    { id: 'itm-02', type: 'transfer', label: 'GTBank',           sub: 'Northstar Freight Ltd · 0123456789', amount: 5000, currency: 'GBP', payerId: 'p1', benefId: 'p4', color: '#f26522', symbol: 'GT' },
    { id: 'itm-03', type: 'bill',     label: 'DSTV',             sub: 'Subscription · SC-00884712', amount: 24500, currency: 'NGN', payerId: 'p1', benefId: 'p1', color: '#003087', symbol: 'DS' },
  ],
  createdAt: new Date().toISOString(),
};

function fmtAmt(n, cur) {
  const s = { GBP: '£', NGN: '₦', USD: '$' }[cur] || cur + ' ';
  return s + Number(n).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

// ── Animated SVG check ────────────────────────────────────────────────────
function AnimatedCheck({ size = 80, color = '#fff', delay = 0 }) {
  const uid = React.useId().replace(/:/g, '');
  return (
    <svg width={size} height={size} viewBox="0 0 80 80" fill="none" xmlns="http://www.w3.org/2000/svg">
      <style>{`
        @keyframes ring-${uid} {
          from { stroke-dashoffset: 251; }
          to   { stroke-dashoffset: 0; }
        }
        @keyframes tick-${uid} {
          0%   { stroke-dashoffset: 60; opacity: 0; }
          30%  { opacity: 1; }
          100% { stroke-dashoffset: 0; }
        }
        .ring-${uid} {
          stroke-dasharray: 251;
          stroke-dashoffset: 251;
          animation: ring-${uid} 0.7s cubic-bezier(.4,0,.2,1) ${delay}s forwards;
        }
        .tick-${uid} {
          stroke-dasharray: 60;
          stroke-dashoffset: 60;
          animation: tick-${uid} 0.4s cubic-bezier(.4,0,.2,1) ${delay + 0.5}s forwards;
        }
      `}</style>
      <circle cx="40" cy="40" r="36" stroke={color} strokeWidth="4" opacity="0.2"/>
      <circle className={`ring-${uid}`} cx="40" cy="40" r="36" stroke={color} strokeWidth="4"
        strokeLinecap="round" transform="rotate(-90 40 40)"/>
      <path className={`tick-${uid}`} d="M24 40l11 11 21-22" stroke={color} strokeWidth="5"
        strokeLinecap="round" strokeLinejoin="round"/>
    </svg>
  );
}

// ── Animated pulse dot ─────────────────────────────────────────────────────
function PulseDot({ color = 'var(--brand-primary)', size = 10 }) {
  return (
    <span style={{ position: 'relative', display: 'inline-flex', width: size, height: size, flex: 'none' }}>
      <span style={{
        position: 'absolute', inset: 0, borderRadius: '50%', background: color, opacity: 0.4,
        animation: 'pulse-ring 1.4s ease-out infinite',
      }}/>
      <span style={{ width: '100%', height: '100%', borderRadius: '50%', background: color, display: 'block' }}/>
      <style>{`
        @keyframes pulse-ring {
          0%   { transform: scale(1);   opacity: 0.4; }
          70%  { transform: scale(2.4); opacity: 0; }
          100% { transform: scale(2.4); opacity: 0; }
        }
      `}</style>
    </span>
  );
}

// ── 1. ORDER SUCCESS ───────────────────────────────────────────────────────
function ScreenOrderSuccess() {
  const [visible, setVisible] = React.useState(false);
  React.useEffect(() => { const t = setTimeout(() => setVisible(true), 60); return () => clearTimeout(t); }, []);

  return (
    <div style={{ height: '100%', display: 'grid', gridTemplateRows: '1fr auto', background: 'var(--surface)' }}>
      <style>{`
        @keyframes fadeUp {
          from { opacity: 0; transform: translateY(18px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes confetti-fall {
          0%   { transform: translateY(-20px) rotate(0deg);   opacity: 1; }
          100% { transform: translateY(200px) rotate(720deg); opacity: 0; }
        }
        .success-item { animation: fadeUp 0.45s cubic-bezier(.4,0,.2,1) both; }
      `}</style>

      {/* Confetti particles */}
      <div style={{ position: 'absolute', inset: 0, pointerEvents: 'none', overflow: 'hidden' }}>
        {visible && Array.from({ length: 22 }).map((_, i) => {
          const colors = ['var(--brand-primary)', '#eb5c37', '#e8a838', '#4caf50', '#7b76b6', '#0097a9'];
          const col = colors[i % colors.length];
          const left = 20 + (i * 3.4) % 80;
          const delay = (i * 0.12) % 1.8;
          const dur = 1.6 + (i % 5) * 0.22;
          const size = 6 + (i % 4) * 3;
          return (
            <div key={i} style={{
              position: 'absolute', top: '35%', left: `${left}%`,
              width: size, height: size * (i % 3 === 0 ? 0.4 : 1),
              background: col, borderRadius: i % 2 === 0 ? '50%' : 2,
              animation: `confetti-fall ${dur}s ${delay}s ease-in both`,
            }}/>
          );
        })}
      </div>

      {/* Main success block */}
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '48px 40px', textAlign: 'center', gap: 0 }}>
        {/* Check circle */}
        <div style={{
          width: 100, height: 100, borderRadius: '50%',
          background: 'var(--brand-primary)', display: 'grid', placeItems: 'center', marginBottom: 24,
          boxShadow: '0 8px 32px -8px rgba(5,90,96,0.45)',
          animation: visible ? 'fadeUp 0.5s cubic-bezier(.4,0,.2,1) both' : 'none',
        }}>
          <AnimatedCheck size={64} color="#fff" delay={0.1}/>
        </div>

        <div className="success-item" style={{ animationDelay: '0.3s', marginBottom: 6 }}>
          <div style={{ fontSize: 28, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>Order saved</div>
        </div>
        <div className="success-item" style={{ animationDelay: '0.42s', marginBottom: 28 }}>
          <div style={{ fontSize: 14, color: 'var(--text-secondary)', maxWidth: 480 }}>
            Your order has been created and is processing. An invoice has been generated and is ready for payment collection.
          </div>
        </div>

        {/* Order ID chip */}
        <div className="success-item" style={{ animationDelay: '0.52s', marginBottom: 32 }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 10,
            padding: '8px 16px', borderRadius: 999, border: '1px solid var(--border-light)',
            background: 'var(--surface-inset)', fontSize: 13,
          }}>
            <Icon name="invoice" size={14} color="var(--brand-primary)"/>
            <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 600 }}>{DEMO_ORDER.id}</span>
            <span style={{ color: 'var(--text-tertiary)' }}>·</span>
            <span style={{ color: 'var(--text-secondary)' }}>{DEMO_ORDER.items.length} items</span>
          </div>
        </div>

        {/* Item summary pills */}
        <div className="success-item" style={{ animationDelay: '0.62s', display: 'flex', gap: 10, flexWrap: 'wrap', justifyContent: 'center', marginBottom: 36 }}>
          {DEMO_ORDER.items.map((item, i) => (
            <div key={item.id} style={{
              display: 'flex', alignItems: 'center', gap: 8,
              padding: '8px 14px', borderRadius: 10, border: '1px solid var(--border-light)',
              background: 'var(--surface)',
              animation: `fadeUp 0.35s cubic-bezier(.4,0,.2,1) ${0.65 + i * 0.08}s both`,
            }}>
              <div style={{ width: 28, height: 28, borderRadius: 6, background: item.color, color: '#fff', display: 'grid', placeItems: 'center', fontWeight: 800, fontSize: 10, flex: 'none' }}>{item.symbol}</div>
              <div>
                <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{item.label}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{fmtAmt(item.amount, item.currency)}</div>
              </div>
            </div>
          ))}
        </div>

        {/* CTAs */}
        <div className="success-item" style={{ animationDelay: '0.8s', display: 'flex', gap: 12 }}>
          <button className="btn btn-primary btn-lg">
            <Icon name="bank" size={14}/> Collect payment
          </button>
          <button className="btn btn-outline btn-lg">
            <Icon name="invoice" size={14}/> View order
          </button>
          <button className="btn btn-ghost btn-lg">
            <Icon name="plus" size={14}/> New order
          </button>
        </div>
      </div>

      {/* Bottom bar */}
      <div style={{ borderTop: '1px solid var(--border-light)', padding: '14px 32px', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12.5, color: 'var(--text-secondary)' }}>
          <PulseDot color="var(--success)"/>
          Processing started · Compliance checks running
        </div>
        <div style={{ fontSize: 12, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
          INV-{DEMO_ORDER.id.slice(-4)} generated
        </div>
      </div>
    </div>
  );
}

// ── 2. PAYMENT COLLECTION ─────────────────────────────────────────────────
const COLLECTION_METHODS = [
  {
    id: 'bank-transfer', icon: 'bank', label: 'Bank transfer',
    desc: 'Send account details. Funds credited on confirmation.',
    badge: 'Recommended', badgeTone: 'success',
    eta: '1–2 business days', fee: 'Free',
    detail: { account: '40-27-18 · 12345678', bank: 'Barclays Business', ref: DEMO_ORDER.id },
  },
  {
    id: 'payment-link', icon: 'link', label: 'Payment link',
    desc: 'Secure card or open banking link sent by email or SMS.',
    badge: 'Fast', badgeTone: 'tint',
    eta: 'Instant on card', fee: '1.4% + 20p',
    detail: { url: 'pay.aonik.com/inv-0047', expires: '48h' },
  },
  {
    id: 'invoice-email', icon: 'mail', label: 'Send invoice',
    desc: 'Email a PDF invoice with payment instructions.',
    badge: null,
    eta: 'Net-30 terms', fee: 'Free',
    detail: { to: 'finance@primrose.co', template: 'Standard invoice' },
  },
  {
    id: 'bnpl', icon: 'calendar', label: 'Installment plan',
    desc: 'Split into 3–12 monthly payments. Requires approval.',
    badge: 'Pending approval', badgeTone: 'warning',
    eta: 'Subject to credit check', fee: '2.5%/yr',
    detail: { months: 3, downPayment: '33%' },
  },
];

function ScreenPaymentCollection() {
  const [method, setMethod] = React.useState('bank-transfer');
  const sel = COLLECTION_METHODS.find(m => m.id === method);

  const gbpTotal = 5000 + 15000 * 0.000504 + 24500 * 0.000504;
  const ngnTotal = 15000 + 24500;

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 380px', height: '100%', minHeight: 0 }}>

      {/* Left — collection method picker */}
      <div style={{ overflow: 'auto', borderRight: '1px solid var(--border-light)', display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '22px 28px 0', flex: 'none' }}>
          <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 4 }}>{DEMO_ORDER.id}</div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em', marginBottom: 4 }}>Collect payment</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 22 }}>
            Choose how to collect for this order. The invoice has been generated and is awaiting payment.
          </div>

          {/* Agent suggestion */}
          <div style={{
            display: 'flex', gap: 12, padding: '12px 16px', borderRadius: 10, marginBottom: 20,
            background: 'var(--brand-primary-10)', border: '1px solid var(--brand-primary)',
          }}>
            <div style={{ width: 28, height: 28, borderRadius: '50%', background: 'var(--brand-primary)', color: '#fff', display: 'grid', placeItems: 'center', fontSize: 11, fontWeight: 700, flex: 'none' }}>B</div>
            <div>
              <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--brand-primary)', marginBottom: 2 }}>Billing Agent · recommendation</div>
              <div style={{ fontSize: 12, color: 'var(--text-primary)', lineHeight: 1.5 }}>
                Primrose Logistics (Gold tier) consistently pays via bank transfer within 2 days. Recommend sending account details now. Payment link is a good fallback if no confirmation within 24h.
              </div>
            </div>
          </div>
        </div>

        <div style={{ padding: '0 28px 28px', overflow: 'auto', flex: 1, display: 'flex', flexDirection: 'column', gap: 10 }}>
          {COLLECTION_METHODS.map(m => {
            const active = m.id === method;
            return (
              <div key={m.id} onClick={() => setMethod(m.id)} style={{
                padding: '16px 18px', borderRadius: 12, cursor: 'pointer',
                border: active ? '2px solid var(--brand-primary)' : '1.5px solid var(--border-light)',
                background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
                display: 'flex', gap: 16, alignItems: 'flex-start',
                transition: 'border 120ms, background 120ms',
              }}>
                <div style={{
                  width: 40, height: 40, borderRadius: 10, flex: 'none',
                  background: active ? 'var(--brand-primary)' : 'var(--surface-inset)',
                  display: 'grid', placeItems: 'center',
                  transition: 'background 120ms',
                }}>
                  <Icon name={m.icon} size={18} color={active ? '#fff' : 'var(--text-secondary)'}/>
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                    <span style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{m.label}</span>
                    {m.badge && <Pill tone={m.badgeTone} size="sm">{m.badge}</Pill>}
                  </div>
                  <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: 8 }}>{m.desc}</div>
                  <div style={{ display: 'flex', gap: 16, fontSize: 11.5 }}>
                    <span style={{ color: 'var(--text-tertiary)' }}>ETA: <span style={{ color: 'var(--text-secondary)', fontWeight: 500 }}>{m.eta}</span></span>
                    <span style={{ color: 'var(--text-tertiary)' }}>Fee: <span style={{ color: 'var(--text-secondary)', fontWeight: 500 }}>{m.fee}</span></span>
                  </div>
                </div>
                {active && <Icon name="check" size={16} color="var(--brand-primary)"/>}
              </div>
            );
          })}

          {/* Detail panel for selected method */}
          {sel?.detail && (
            <div style={{ background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 4 }}>
                {sel.label} details
              </div>
              {Object.entries(sel.detail).map(([k, v]) => (
                <div key={k} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5 }}>
                  <span style={{ color: 'var(--text-secondary)', textTransform: 'capitalize' }}>{k.replace(/([A-Z])/g,' $1').toLowerCase()}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 500 }}>{v}</span>
                </div>
              ))}
            </div>
          )}

          <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
            <button className="btn btn-ghost btn-sm">Back</button>
            <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }}>
              <Icon name="send" size={13}/> {sel?.id === 'invoice-email' ? 'Send invoice' : sel?.id === 'payment-link' ? 'Generate link' : sel?.id === 'bnpl' ? 'Request plan' : 'Confirm & notify'}
            </button>
          </div>
        </div>
      </div>

      {/* Right — invoice preview */}
      <div style={{ overflow: 'auto', padding: 20, background: 'var(--surface-inset)', display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div style={{ fontSize: 11, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)' }}>Invoice preview</div>

        {/* Invoice paper */}
        <div style={{
          background: 'var(--surface)', borderRadius: 12, border: '1px solid var(--border-light)',
          boxShadow: '0 4px 20px -4px rgba(0,0,0,0.1)', overflow: 'hidden',
        }}>
          {/* Invoice header */}
          <div style={{ background: 'var(--brand-primary)', padding: '20px 20px 18px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <div style={{ fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 16, color: '#fff', letterSpacing: '-0.01em' }}>AONIK</div>
                <div style={{ fontSize: 10.5, color: 'rgba(255,255,255,0.7)', marginTop: 2 }}>Finance Operations Platform</div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontSize: 10, color: 'rgba(255,255,255,0.6)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Invoice</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 700, color: '#fff' }}>INV-{DEMO_ORDER.id.slice(-4)}</div>
              </div>
            </div>
          </div>

          <div style={{ padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 14 }}>
            {/* Bill to */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <div>
                <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 4 }}>Bill to</div>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Primrose Logistics</div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>ops@primrose.co</div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 4 }}>Dates</div>
                <div style={{ fontSize: 12, color: 'var(--text-primary)' }}>Issued: {new Date().toLocaleDateString('en-GB')}</div>
                <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>Due: {new Date(Date.now() + 30*864e5).toLocaleDateString('en-GB')}</div>
              </div>
            </div>

            <div style={{ height: 1, background: 'var(--border-light)' }}/>

            {/* Line items */}
            <div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr auto auto', gap: '6px 12px', marginBottom: 8 }}>
                <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)' }}>Description</div>
                <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', textAlign: 'right' }}>Qty</div>
                <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', textAlign: 'right' }}>Amount</div>
              </div>
              {DEMO_ORDER.items.map((item, i) => (
                <div key={item.id} style={{
                  display: 'grid', gridTemplateColumns: '1fr auto auto', gap: '6px 12px',
                  padding: '8px 0', borderBottom: i < DEMO_ORDER.items.length - 1 ? '1px solid var(--border-light)' : 'none',
                }}>
                  <div>
                    <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{item.label}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{item.sub}</div>
                  </div>
                  <div style={{ fontSize: 12, color: 'var(--text-secondary)', textAlign: 'right' }}>1</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600, color: 'var(--text-primary)', textAlign: 'right' }}>{fmtAmt(item.amount, item.currency)}</div>
                </div>
              ))}
            </div>

            <div style={{ height: 1, background: 'var(--border-light)' }}/>

            {/* Totals */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {[['GBP subtotal', fmtAmt(gbpTotal, 'GBP')], ['NGN subtotal', fmtAmt(ngnTotal, 'NGN')], ['Est. fees', fmtAmt(gbpTotal * 0.014, 'GBP')]].map(([l, v]) => (
                <div key={l} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12 }}>
                  <span style={{ color: 'var(--text-secondary)' }}>{l}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{v}</span>
                </div>
              ))}
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, paddingTop: 8, borderTop: '2px solid var(--border-light)' }}>
                <span style={{ fontWeight: 700, color: 'var(--text-primary)' }}>GBP total</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-primary)' }}>{fmtAmt(gbpTotal + gbpTotal * 0.014, 'GBP')}</span>
              </div>
            </div>

            <Pill tone="pending" dot>Awaiting payment</Pill>
          </div>
        </div>
      </div>
    </div>
  );
}

// ── 3. ORDER ITEM DETAIL ───────────────────────────────────────────────────
const ITEM_STAGES = [
  { id: 'created',    label: 'Order created',      icon: 'check',    done: true,   ts: '14:22:03' },
  { id: 'invoice',    label: 'Invoice generated',  icon: 'invoice',  done: true,   ts: '14:22:05' },
  { id: 'compliance', label: 'Compliance check',   icon: 'shield',   active: true, ts: '14:22:41', waiting: 'Sanctions & AML screening in progress' },
  { id: 'payment',    label: 'Payment received',   icon: 'bank',     pending: true, ts: null },
  { id: 'processing', label: 'Processing',         icon: 'refresh',  pending: true, ts: null },
  { id: 'settled',    label: 'Settled',            icon: 'verified', pending: true, ts: null },
];

function ScreenOrderItemDetail() {
  const item = DEMO_ORDER.items[1]; // GTBank money transfer
  const [expandStage, setExpandStage] = React.useState('compliance');

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 340px', height: '100%', minHeight: 0 }}>

      {/* Left — main detail */}
      <div style={{ overflow: 'auto', padding: '24px 28px', display: 'flex', flexDirection: 'column', gap: 20 }}>
        {/* Header */}
        <div>
          <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 6 }}>
            {DEMO_ORDER.id} · Item 2 of 3
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 4 }}>
            <div style={{ width: 44, height: 44, borderRadius: 10, background: item.color, color: '#fff', display: 'grid', placeItems: 'center', fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: 15, flex: 'none' }}>{item.symbol}</div>
            <div>
              <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>{item.label}</div>
              <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>{item.sub}</div>
            </div>
            <div style={{ marginLeft: 'auto', display: 'flex', gap: 8, alignItems: 'center' }}>
              <Pill tone="warning" dot>Compliance check</Pill>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, fontWeight: 700, color: 'var(--text-primary)' }}>{fmtAmt(item.amount, item.currency)}</div>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <FlowAvatar id={item.payerId} size={22}/>
            <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{flowParty(item.payerId).name}</span>
            <Icon name="arrowright" size={11} color="var(--text-tertiary)"/>
            <FlowAvatar id={item.benefId} size={22}/>
            <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{flowParty(item.benefId).name}</span>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 6 }}>GBP → NGN · Wise</span>
          </div>
        </div>

        {/* ── Progress pipeline ── */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '20px 24px' }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: 20 }}>Order progress</div>

          <style>{`
            @keyframes fill-line { from { width: 0%; } to { width: 100%; } }
            @keyframes stage-in  { from { opacity:0; transform:scale(0.7); } to { opacity:1; transform:scale(1); } }
          `}</style>

          {/* Horizontal pipeline */}
          <div style={{ position: 'relative', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
            {/* Connecting track */}
            <div style={{ position: 'absolute', top: 17, left: '8%', right: '8%', height: 3, background: 'var(--border-light)', borderRadius: 2, zIndex: 0 }}>
              {/* Filled portion (2/6 done) */}
              <div style={{ position: 'absolute', left: 0, top: 0, height: '100%', width: '28%', background: 'var(--brand-primary)', borderRadius: 2, animation: 'fill-line 0.8s 0.2s cubic-bezier(.4,0,.2,1) both' }}/>
              {/* Active portion */}
              <div style={{ position: 'absolute', left: '28%', top: 0, height: '100%', width: '12%', background: 'var(--warning)', borderRadius: 2, animation: 'fill-line 0.5s 1s cubic-bezier(.4,0,.2,1) both' }}/>
            </div>

            {ITEM_STAGES.map((stage, i) => {
              const active = stage.active;
              const done = stage.done;
              return (
                <div key={stage.id} onClick={() => setExpandStage(stage.id === expandStage ? null : stage.id)}
                  style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8, flex: 1, position: 'relative', zIndex: 1, cursor: 'pointer' }}>
                  {/* Node */}
                  <div style={{
                    width: 36, height: 36, borderRadius: '50%', display: 'grid', placeItems: 'center',
                    background: done ? 'var(--brand-primary)' : active ? 'var(--surface)' : 'var(--surface-inset)',
                    border: done ? 'none' : active ? '2.5px solid var(--warning)' : '2px solid var(--border-medium)',
                    boxShadow: active ? '0 0 0 5px rgba(235,195,52,0.2)' : done ? '0 2px 8px -2px rgba(5,90,96,0.35)' : 'none',
                    animation: `stage-in 0.35s cubic-bezier(.4,0,.2,1) ${i * 0.08}s both`,
                    position: 'relative',
                  }}>
                    {active && <PulseDot color="var(--warning)" size={8}/>}
                    {!active && <Icon name={stage.icon} size={14} color={done ? '#fff' : 'var(--text-tertiary)'}/>}
                  </div>
                  <div style={{ textAlign: 'center' }}>
                    <div style={{ fontSize: 11, fontWeight: 600, color: done ? 'var(--brand-primary)' : active ? 'var(--text-primary)' : 'var(--text-tertiary)', lineHeight: 1.3 }}>{stage.label}</div>
                    {stage.ts && <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', marginTop: 2 }}>{stage.ts}</div>}
                    {active && <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--warning)' }}>In progress</div>}
                    {stage.pending && <div style={{ fontSize: 10, color: 'var(--text-tertiary)' }}>Pending</div>}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Expanded stage detail */}
          {expandStage && (() => {
            const s = ITEM_STAGES.find(s => s.id === expandStage);
            if (!s) return null;
            return (
              <div style={{ marginTop: 20, padding: '12px 14px', background: 'var(--surface-inset)', borderRadius: 8, border: '1px solid var(--border-light)' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
                  {s.active && <PulseDot color="var(--warning)" size={8}/>}
                  {s.done && <Icon name="check" size={12} color="var(--brand-primary)"/>}
                  <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{s.label}</span>
                  {s.ts && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 'auto' }}>{s.ts}</span>}
                </div>
                <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                  {s.done && 'Completed successfully.'}
                  {s.active && (s.waiting || 'This stage is currently in progress.')}
                  {s.pending && 'This stage has not started yet. It will begin once all prior stages are complete.'}
                </div>
              </div>
            );
          })()}
        </div>

        {/* Key fields */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '18px 20px' }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: 14 }}>Item details</div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
            {[
              { l: 'Item ID',        v: 'ITM-0047-02',   mono: true },
              { l: 'Type',           v: 'Money transfer'            },
              { l: 'Gateway',        v: 'Wise Business'             },
              { l: 'Send amount',    v: fmtAmt(item.amount, item.currency), mono: true },
              { l: 'FX rate',        v: '1 GBP = 1,985 NGN',       mono: true },
              { l: 'Receive amount', v: fmtAmt(item.amount * 1985, 'NGN'), mono: true },
              { l: 'Est. fee',       v: fmtAmt(item.amount * 0.005, 'GBP'), mono: true },
              { l: 'Purpose code',   v: 'SUPP · Supplier payment'  },
              { l: 'Reference',      v: DEMO_ORDER.id,              mono: true },
            ].map(f => (
              <div key={f.l}>
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginBottom: 3 }}>{f.l}</div>
                <div style={{ fontSize: 12.5, fontFamily: f.mono ? 'var(--font-mono)' : 'inherit', fontWeight: 500, color: 'var(--text-primary)' }}>{f.v}</div>
              </div>
            ))}
          </div>
        </div>

        {/* Activity log */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '18px 20px' }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: 14 }}>Activity</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
            {[
              { ts: '14:22:41', msg: 'Compliance check started — AML and sanctions screening initiated via ComplyAdvantage.' },
              { ts: '14:22:05', msg: 'Invoice INV-0047 generated automatically on order creation.' },
              { ts: '14:22:04', msg: 'FX rate locked: 1 GBP = 1,985 NGN via Wise Business live quote.' },
              { ts: '14:22:03', msg: 'Order item created. Payer: Primrose Logistics · Beneficiary: Northstar Freight.' },
            ].map((e, i) => (
              <div key={i} style={{ display: 'flex', gap: 14, padding: '10px 0', borderBottom: i < 3 ? '1px solid var(--border-light)' : 'none' }}>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', flex: 'none', marginTop: 1 }}>{e.ts}</div>
                <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{e.msg}</div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Right — status rail */}
      <div style={{ borderLeft: '1px solid var(--border-light)', background: 'var(--surface-inset)', overflow: 'auto', padding: '20px 18px', display: 'flex', flexDirection: 'column', gap: 14 }}>
        {/* Current state */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--warning)', borderRadius: 12, padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <PulseDot color="var(--warning)" size={9}/>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Compliance check</div>
          </div>
          <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            Sanctions screening and AML checks in progress via ComplyAdvantage. Average wait: 45–90 seconds.
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            <Pill tone="warning" size="sm" dot>Running</Pill>
            <Pill tone="default" size="sm">Auto-pass eligible</Pill>
          </div>
        </div>

        {/* Waiting on */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
          <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>Waiting for</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {[
              { label: 'AML screening', status: 'running' },
              { label: 'Sanctions list match', status: 'running' },
              { label: 'Payment receipt',      status: 'pending' },
              { label: 'Governance sign-off',  status: 'pending' },
            ].map(w => (
              <div key={w.label} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                {w.status === 'running'
                  ? <PulseDot color="var(--warning)" size={8}/>
                  : <div style={{ width: 8, height: 8, borderRadius: '50%', background: 'var(--border-medium)', flex: 'none' }}/>}
                <span style={{ fontSize: 12.5, color: w.status === 'running' ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{w.label}</span>
              </div>
            ))}
          </div>
        </div>

        {/* SLA */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
          <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>SLA</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {[
              { l: 'Compliance',  v: '< 2 min', ok: true },
              { l: 'Processing',  v: '< 4h',    ok: true },
              { l: 'Settlement',  v: 'T+1',      ok: true },
              { l: 'Breach risk', v: 'Low',      ok: true },
            ].map(s => (
              <div key={s.l} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5 }}>
                <span style={{ color: 'var(--text-secondary)' }}>{s.l}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: s.ok ? 'var(--success)' : 'var(--danger)' }}>{s.v}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Quick actions */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
          <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>Actions</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <button className="btn btn-ghost btn-sm" style={{ justifyContent: 'flex-start' }}><Icon name="refresh" size={12}/> Refresh status</button>
            <button className="btn btn-ghost btn-sm" style={{ justifyContent: 'flex-start' }}><Icon name="invoice" size={12}/> View invoice</button>
            <button className="btn btn-ghost btn-sm" style={{ justifyContent: 'flex-start' }}><Icon name="gitbranch" size={12}/> View trace</button>
            <button className="btn btn-ghost btn-sm" style={{ justifyContent: 'flex-start', color: 'var(--danger)' }}><Icon name="x" size={12}/> Cancel item</button>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenOrderSuccess, ScreenPaymentCollection, ScreenOrderItemDetail });
