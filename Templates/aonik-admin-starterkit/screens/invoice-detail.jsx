// ─── Invoice detail + dialogs + enhanced order list ───────────────────────

// ── Shared order/invoice data ─────────────────────────────────────────────
const INV_ITEMS = [
  { id: 'itm-01', label: 'Ikeja Electric',  sub: 'Prepaid token · 7012345678', type: 'bill',     amount: 15000, currency: 'NGN', color: '#1e4d8c', symbol: 'IE', fxRate: 0.000504, fxCur: 'GBP' },
  { id: 'itm-02', label: 'GTBank',           sub: 'Northstar Freight · 0123456789', type: 'transfer', amount: 5000, currency: 'GBP', color: '#f26522', symbol: 'GT', fxRate: null },
  { id: 'itm-03', label: 'DSTV',             sub: 'Subscription · SC-00884712', type: 'bill',     amount: 24500, currency: 'NGN', color: '#003087', symbol: 'DS', fxRate: 0.000504, fxCur: 'GBP' },
];

const INV = {
  id: 'INV-0047',
  orderId: 'ORD-20250422-0047',
  status: 'Awaiting payment',
  tone: 'pending',
  issued: '22 Apr 2025',
  due: '22 May 2025',
  terms: 'Net 30',
  payer: { id: 'p1', name: 'Primrose Logistics', email: 'finance@primrose.co', phone: '+44 7700 900123', tier: 'Gold', color: '#055a60' },
  gbpTotal: 5000 + 15000 * 0.000504 + 24500 * 0.000504,
  fee: 5000 * 0.014,
  notes: [
    { author: 'Oliver Chen', color: '#7b76b6', ts: '22 Apr 14:32', internal: true,  body: 'Fuel budget overshoot in April — using Wise corridor to NGN. Please process same-day.' },
    { author: 'Billing Agent', color: '#055a60', ts: '22 Apr 14:22', internal: false, body: 'Invoice generated automatically on order submit. Payment link available via Collect payment.' },
  ],
  payments: [],
  activity: [
    { ts: '14:22:41', msg: 'Compliance check started for ITM-0047-02 (GTBank transfer).' },
    { ts: '14:22:05', msg: 'Invoice INV-0047 generated from order ORD-20250422-0047.' },
    { ts: '14:22:03', msg: 'Order ORD-20250422-0047 submitted by Oliver Chen.' },
  ],
};

function invFmt(n, cur) {
  const s = { GBP: '£', NGN: '₦', USD: '$' }[cur] || cur + ' ';
  return s + Number(n).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function invInitials(name) { return (name||'?').split(' ').map(w=>w[0]).slice(0,2).join('').toUpperCase(); }

function InvAvatar({ name, color, size = 30 }) {
  return (
    <div style={{
      width: size, height: size, borderRadius: size * 0.28, background: color, color: '#fff',
      display: 'flex', alignItems: 'center', justifyContent: 'center', flex: 'none',
      fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: size * 0.38,
    }}>{invInitials(name)}</div>
  );
}

function InvLogoMark({ color, symbol, size = 28 }) {
  return (
    <div style={{
      width: size, height: size, borderRadius: 6, background: color, color: '#fff',
      display: 'flex', alignItems: 'center', justifyContent: 'center', flex: 'none',
      fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: size * 0.34,
    }}>{symbol}</div>
  );
}

// ── Dialog shell ──────────────────────────────────────────────────────────
function InvDialog({ open, onClose, title, width = 480, children }) {
  if (!open) return null;
  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 100,
      background: 'rgba(0,0,0,0.45)', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 32,
    }} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{
        width, maxWidth: '90%', maxHeight: '85vh', overflow: 'auto',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 14, boxShadow: '0 20px 60px -12px rgba(0,0,0,0.35)',
        display: 'flex', flexDirection: 'column',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '16px 20px', borderBottom: '1px solid var(--border-light)', flex: 'none' }}>
          <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</div>
          <button onClick={onClose} style={{ width: 28, height: 28, display: 'grid', placeItems: 'center', borderRadius: 6, border: 'none', background: 'transparent', cursor: 'pointer', color: 'var(--text-tertiary)' }}>
            <Icon name="x" size={14}/>
          </button>
        </div>
        <div style={{ padding: '20px', flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 16 }}>{children}</div>
      </div>
    </div>
  );
}

function InvField({ label, children }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
      <label style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 500 }}>{label}</label>
      {children}
    </div>
  );
}

// ── Cancel dialog ─────────────────────────────────────────────────────────
function CancelDialog({ open, onClose }) {
  const [reason, setReason] = React.useState('');
  const [creditNote, setCreditNote] = React.useState(true);
  return (
    <InvDialog open={open} onClose={onClose} title="Cancel invoice" width={460}>
      <div style={{ background: 'rgba(204,46,46,0.08)', border: '1px solid var(--danger)', borderRadius: 8, padding: '10px 14px', display: 'flex', gap: 10 }}>
        <Icon name="warn" size={14} color="var(--danger)"/>
        <div style={{ fontSize: 12.5, color: 'var(--danger)', lineHeight: 1.5 }}>
          Cancelling INV-0047 will void all pending line items. This action cannot be undone. If payment has been partially received, a credit note will be issued automatically.
        </div>
      </div>
      <InvField label="Reason for cancellation">
        <select className="select" value={reason} onChange={e => setReason(e.target.value)}>
          <option value="">Select reason…</option>
          <option>Order placed in error</option>
          <option>Duplicate invoice</option>
          <option>Customer request</option>
          <option>Goods / services not delivered</option>
          <option>Pricing error</option>
          <option>Other</option>
        </select>
      </InvField>
      {reason === 'Other' && (
        <InvField label="Details">
          <textarea className="input" rows={3} placeholder="Describe the reason…" style={{ height: 'auto', resize: 'vertical', padding: '8px 12px' }}/>
        </InvField>
      )}
      <InvField label="Credit note">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <input type="checkbox" checked={creditNote} onChange={e => setCreditNote(e.target.checked)} id="cn-chk"/>
          <label htmlFor="cn-chk" style={{ fontSize: 13, color: 'var(--text-primary)' }}>Issue credit note CN-{INV.id.slice(-4)} automatically</label>
        </div>
      </InvField>
      <div style={{ display: 'flex', gap: 8, paddingTop: 4 }}>
        <button onClick={onClose} className="btn btn-ghost" style={{ flex: 1 }}>Keep invoice</button>
        <button className="btn" style={{ flex: 1, background: 'var(--danger)', color: '#fff' }} disabled={!reason}>Confirm cancellation</button>
      </div>
    </InvDialog>
  );
}

// ── Refund dialog ─────────────────────────────────────────────────────────
function RefundDialog({ open, onClose }) {
  const [amount, setAmount] = React.useState('5007.55');
  const [method, setMethod] = React.useState('original');
  const [reason, setReason] = React.useState('');
  return (
    <InvDialog open={open} onClose={onClose} title="Issue refund" width={460}>
      <div style={{ background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '10px 14px', fontSize: 12.5, color: 'var(--text-secondary)', display: 'flex', justifyContent: 'space-between' }}>
        <span>Available to refund</span>
        <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-primary)' }}>£0.00</span>
      </div>
      <div style={{ background: 'rgba(235,195,52,0.12)', border: '1px solid var(--warning)', borderRadius: 8, padding: '10px 14px', fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
        No payment has been received yet. A refund can only be processed once the invoice is partially or fully paid.
      </div>
      <InvField label="Refund amount (GBP)">
        <input className="input" value={amount} onChange={e => setAmount(e.target.value)} style={{ fontFamily: 'var(--font-mono)' }} disabled/>
      </InvField>
      <InvField label="Refund method">
        <select className="select" value={method} onChange={e => setMethod(e.target.value)} disabled>
          <option value="original">Original payment method</option>
          <option value="bank">Bank transfer</option>
          <option value="credit">Credit to account</option>
        </select>
      </InvField>
      <InvField label="Reason">
        <textarea className="input" rows={3} value={reason} onChange={e => setReason(e.target.value)} placeholder="Explain the reason for refund…" style={{ height: 'auto', resize: 'vertical', padding: '8px 12px' }}/>
      </InvField>
      <div style={{ display: 'flex', gap: 8, paddingTop: 4 }}>
        <button onClick={onClose} className="btn btn-ghost" style={{ flex: 1 }}>Cancel</button>
        <button className="btn btn-primary" style={{ flex: 1 }} disabled>Process refund</button>
      </div>
    </InvDialog>
  );
}

// ── Print dialog ──────────────────────────────────────────────────────────
function PrintDialog({ open, onClose }) {
  return (
    <InvDialog open={open} onClose={onClose} title="Print / export invoice" width={560}>
      {/* Mini invoice preview */}
      <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden', boxShadow: '0 4px 18px -4px rgba(0,0,0,0.1)' }}>
        <div style={{ background: 'var(--brand-primary)', padding: '16px 20px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 15, color: '#fff' }}>AONIK</span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 700, color: '#fff' }}>{INV.id}</span>
        </div>
        <div style={{ padding: '14px 20px', display: 'flex', flexDirection: 'column', gap: 10, fontSize: 12 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div>
              <div style={{ fontSize: 10, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: 3 }}>Bill to</div>
              <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{INV.payer.name}</div>
              <div style={{ color: 'var(--text-secondary)' }}>{INV.payer.email}</div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <div style={{ color: 'var(--text-secondary)' }}>Issued: {INV.issued}</div>
              <div style={{ color: 'var(--text-secondary)' }}>Due: {INV.due}</div>
            </div>
          </div>
          {INV_ITEMS.map(item => (
            <div key={item.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderTop: '1px solid var(--border-light)' }}>
              <div>
                <div style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{item.label}</div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{item.sub}</div>
              </div>
              <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{invFmt(item.amount, item.currency)}</span>
            </div>
          ))}
          <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '2px solid var(--border-light)', paddingTop: 8 }}>
            <span style={{ fontWeight: 700, color: 'var(--text-primary)' }}>GBP Total</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-primary)' }}>{invFmt(INV.gbpTotal + INV.fee, 'GBP')}</span>
          </div>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        {[['Print PDF', 'download', 'btn-primary'], ['Send by email', 'mail', 'btn-outline'], ['Copy link', 'link', 'btn-outline'], ['Export CSV', 'download', 'btn-ghost']].map(([l, ic, cls]) => (
          <button key={l} onClick={l === 'Print PDF' ? onClose : undefined} className={`btn ${cls}`} style={{ justifyContent: 'center' }}>
            <Icon name={ic} size={12}/> {l}
          </button>
        ))}
      </div>
      <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', textAlign: 'center' }}>PDF includes all line items, FX rates, compliance status, and payment instructions.</div>
    </InvDialog>
  );
}

// ── Add note dialog ───────────────────────────────────────────────────────
function AddNoteDialog({ open, onClose }) {
  const [note, setNote] = React.useState('');
  const [internal, setInternal] = React.useState(true);
  return (
    <InvDialog open={open} onClose={onClose} title="Add note" width={440}>
      <InvField label="Note">
        <textarea className="input" rows={4} value={note} onChange={e => setNote(e.target.value)} placeholder="Add context, chase notes, or instructions…" style={{ height: 'auto', resize: 'vertical', padding: '10px 12px', fontSize: 13, lineHeight: 1.5 }}/>
      </InvField>
      <div style={{ display: 'flex', gap: 12 }}>
        {[true, false].map(v => (
          <div key={String(v)} onClick={() => setInternal(v)} style={{
            flex: 1, padding: '10px 14px', borderRadius: 8, cursor: 'pointer',
            border: internal === v ? '2px solid var(--brand-primary)' : '1.5px solid var(--border-light)',
            background: internal === v ? 'var(--brand-primary-10)' : 'var(--surface)',
          }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 2 }}>{v ? 'Internal' : 'External'}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{v ? 'Visible to operators only' : 'Sent to payer via email'}</div>
          </div>
        ))}
      </div>
      <div style={{ display: 'flex', gap: 8, paddingTop: 4 }}>
        <button onClick={onClose} className="btn btn-ghost" style={{ flex: 1 }}>Cancel</button>
        <button className="btn btn-primary" style={{ flex: 1 }} disabled={!note.trim()}>Save note</button>
      </div>
    </InvDialog>
  );
}

// ── Send reminder dialog ──────────────────────────────────────────────────
function ReminderDialog({ open, onClose }) {
  const [channel, setChannel] = React.useState('email');
  const [schedule, setSchedule] = React.useState('now');
  return (
    <InvDialog open={open} onClose={onClose} title="Send payment reminder" width={440}>
      <InvField label="Send via">
        <div style={{ display: 'flex', gap: 8 }}>
          {['email', 'sms', 'both'].map(c => (
            <div key={c} onClick={() => setChannel(c)} style={{
              flex: 1, padding: '10px 14px', borderRadius: 8, cursor: 'pointer', textAlign: 'center',
              border: channel === c ? '2px solid var(--brand-primary)' : '1.5px solid var(--border-light)',
              background: channel === c ? 'var(--brand-primary-10)' : 'var(--surface)',
              fontSize: 13, fontWeight: 500, color: 'var(--text-primary)',
              textTransform: 'capitalize',
            }}>{c === 'both' ? 'Both' : c.toUpperCase()}</div>
          ))}
        </div>
      </InvField>
      <InvField label="Recipient">
        <input className="input" defaultValue={INV.payer.email} readOnly/>
      </InvField>
      <InvField label="Schedule">
        <select className="select" value={schedule} onChange={e => setSchedule(e.target.value)}>
          <option value="now">Send now</option>
          <option value="tomorrow">Tomorrow 9am</option>
          <option value="3days">In 3 days</option>
          <option value="7days">In 7 days (due date)</option>
        </select>
      </InvField>
      <InvField label="Message preview">
        <div style={{ background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '10px 12px', fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
          Hi Primrose Logistics, this is a reminder that invoice <strong>{INV.id}</strong> for <strong>{invFmt(INV.gbpTotal + INV.fee, 'GBP')}</strong> is due on <strong>{INV.due}</strong>. Please pay using the link below or contact us if you have questions.
        </div>
      </InvField>
      <div style={{ display: 'flex', gap: 8, paddingTop: 4 }}>
        <button onClick={onClose} className="btn btn-ghost" style={{ flex: 1 }}>Cancel</button>
        <button className="btn btn-primary" style={{ flex: 1 }}>Send reminder</button>
      </div>
    </InvDialog>
  );
}

// ── Write-off dialog ──────────────────────────────────────────────────────
function WriteOffDialog({ open, onClose }) {
  const [reason, setReason] = React.useState('');
  return (
    <InvDialog open={open} onClose={onClose} title="Write off invoice" width={440}>
      <div style={{ background: 'rgba(204,46,46,0.08)', border: '1px solid var(--danger)', borderRadius: 8, padding: '10px 14px', fontSize: 12.5, color: 'var(--danger)', lineHeight: 1.5 }}>
        Writing off this invoice will record it as bad debt in the ledger. A journal entry will be posted to the Bad Debt Expense account. This is irreversible.
      </div>
      <InvField label="Write-off reason">
        <select className="select" value={reason} onChange={e => setReason(e.target.value)}>
          <option value="">Select reason…</option>
          <option>Uncollectable — customer insolvent</option>
          <option>Statute of limitations</option>
          <option>Small balance — not worth pursuing</option>
          <option>Settlement agreement</option>
          <option>Other</option>
        </select>
      </InvField>
      <InvField label="Authorising reference">
        <input className="input" placeholder="e.g. CFO approval email ref, case number…"/>
      </InvField>
      <div style={{ display: 'flex', gap: 8, paddingTop: 4 }}>
        <button onClick={onClose} className="btn btn-ghost" style={{ flex: 1 }}>Cancel</button>
        <button className="btn" style={{ flex: 1, background: 'var(--danger)', color: '#fff' }} disabled={!reason}>Write off</button>
      </div>
    </InvDialog>
  );
}

// ── Invoice detail screen ─────────────────────────────────────────────────
function ScreenInvoiceDetail() {
  const [dialog, setDialog] = React.useState(null);
  const [moreOpen, setMoreOpen] = React.useState(false);
  const moreRef = React.useRef(null);

  React.useEffect(() => {
    if (!moreOpen) return;
    const fn = e => { if (moreRef.current && !moreRef.current.contains(e.target)) setMoreOpen(false); };
    document.addEventListener('mousedown', fn);
    return () => document.removeEventListener('mousedown', fn);
  }, [moreOpen]);

  const ngnTotal = INV_ITEMS.filter(i => i.currency === 'NGN').reduce((s, i) => s + i.amount, 0);

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>

      {/* Dialogs */}
      <CancelDialog   open={dialog === 'cancel'}   onClose={() => setDialog(null)}/>
      <RefundDialog   open={dialog === 'refund'}   onClose={() => setDialog(null)}/>
      <PrintDialog    open={dialog === 'print'}    onClose={() => setDialog(null)}/>
      <AddNoteDialog  open={dialog === 'note'}     onClose={() => setDialog(null)}/>
      <ReminderDialog open={dialog === 'reminder'} onClose={() => setDialog(null)}/>
      <WriteOffDialog open={dialog === 'writeoff'} onClose={() => setDialog(null)}/>

      {/* Header bar */}
      <div style={{ borderBottom: '1px solid var(--border-light)', padding: '16px 28px', flex: 'none', background: 'var(--surface)' }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 20 }}>
          <div>
            <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 4 }}>
              {INV.orderId} · Invoice
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em', fontFamily: 'var(--font-mono)' }}>{INV.id}</div>
              <Pill tone={INV.tone} dot>{INV.status}</Pill>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 6 }}>
              <InvAvatar name={INV.payer.name} color={INV.payer.color} size={24}/>
              <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>{INV.payer.name}</span>
              <span style={{ color: 'var(--text-tertiary)' }}>·</span>
              <span style={{ fontSize: 13, color: 'var(--text-tertiary)' }}>Issued {INV.issued}</span>
              <span style={{ color: 'var(--text-tertiary)' }}>·</span>
              <span style={{ fontSize: 13, color: 'var(--text-tertiary)' }}>Due {INV.due}</span>
            </div>
          </div>

          {/* Actions */}
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flex: 'none' }}>
            <button className="btn btn-primary" onClick={() => setDialog('collect')}>
              <Icon name="bank" size={13}/> Collect payment
            </button>
            <button className="btn btn-outline" onClick={() => setDialog('reminder')}>
              <Icon name="mail" size={13}/> Send reminder
            </button>
            <button className="btn btn-outline" onClick={() => setDialog('print')}>
              <Icon name="download" size={13}/> Print
            </button>
            {/* More dropdown */}
            <div ref={moreRef} style={{ position: 'relative' }}>
              <button className="btn btn-outline" onClick={() => setMoreOpen(o => !o)}>
                <Icon name="more" size={14}/> More
              </button>
              {moreOpen && (
                <div style={{
                  position: 'absolute', right: 0, top: 'calc(100% + 6px)', zIndex: 50, minWidth: 200,
                  background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10,
                  boxShadow: '0 8px 24px -6px rgba(0,0,0,0.16)', padding: 6,
                }}>
                  {[
                    ['note',     'Add note',      'invoice',  false],
                    ['refund',   'Refund',        'arrowright', false],
                    ['writeoff', 'Write off',     'warn',     true ],
                    ['cancel',   'Cancel invoice','x',        true ],
                  ].map(([d, l, ic, danger]) => (
                    <div key={d} onClick={() => { setDialog(d); setMoreOpen(false); }} style={{
                      display: 'flex', alignItems: 'center', gap: 10, padding: '9px 12px', borderRadius: 6, cursor: 'pointer',
                      color: danger ? 'var(--danger)' : 'var(--text-primary)', fontSize: 13.5,
                    }} className="hover-bg">
                      <Icon name={ic} size={13} color={danger ? 'var(--danger)' : 'var(--text-secondary)'}/>
                      {l}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Body */}
      <div style={{ flex: 1, overflow: 'auto', display: 'grid', gridTemplateColumns: '1fr 320px' }}>

        {/* Left — main content */}
        <div style={{ overflow: 'auto', padding: '24px 28px', display: 'flex', flexDirection: 'column', gap: 18, borderRight: '1px solid var(--border-light)' }}>

          {/* Line items */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
            <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--border-light)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Line items</div>
              <Pill tone="default" size="sm">{INV_ITEMS.length} items</Pill>
            </div>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ background: 'var(--surface-inset)' }}>
                  {['Provider', 'Type', 'Description', 'Status', 'Amount', 'GBP equiv.'].map(h => (
                    <th key={h} style={{ padding: '10px 16px', fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.07em', color: 'var(--text-tertiary)', textAlign: h === 'Amount' || h === 'GBP equiv.' ? 'right' : 'left', whiteSpace: 'nowrap' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {INV_ITEMS.map((item, i) => {
                  const gbpEq = item.fxRate ? item.amount * item.fxRate : item.currency === 'GBP' ? item.amount : null;
                  return (
                    <tr key={item.id} style={{ borderTop: '1px solid var(--border-light)' }}>
                      <td style={{ padding: '12px 16px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                          <InvLogoMark color={item.color} symbol={item.symbol} size={30}/>
                          <div>
                            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{item.label}</div>
                            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{item.id}</div>
                          </div>
                        </div>
                      </td>
                      <td style={{ padding: '12px 16px' }}>
                        <Pill tone={item.type === 'transfer' ? 'tint' : 'default'} size="sm">
                          {item.type === 'transfer' ? 'Transfer' : 'Bill pay'}
                        </Pill>
                      </td>
                      <td style={{ padding: '12px 16px', fontSize: 12.5, color: 'var(--text-secondary)' }}>{item.sub}</td>
                      <td style={{ padding: '12px 16px' }}>
                        <Pill tone={i === 1 ? 'warning' : 'pending'} size="sm" dot>{i === 1 ? 'Compliance' : 'Pending'}</Pill>
                      </td>
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>{invFmt(item.amount, item.currency)}</td>
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>{gbpEq ? invFmt(gbpEq, 'GBP') : '—'}</td>
                    </tr>
                  );
                })}
              </tbody>
              <tfoot>
                <tr style={{ borderTop: '2px solid var(--border-light)', background: 'var(--surface-inset)' }}>
                  <td colSpan={4} style={{ padding: '12px 16px', fontSize: 12, color: 'var(--text-tertiary)' }}>Est. processing fee (1.4%)</td>
                  <td/>
                  <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)' }}>{invFmt(INV.fee, 'GBP')}</td>
                </tr>
                <tr style={{ background: 'var(--surface-inset)' }}>
                  <td colSpan={4} style={{ padding: '12px 16px', fontSize: 13, fontWeight: 700, color: 'var(--text-primary)' }}>GBP total</td>
                  <td/>
                  <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 700, color: 'var(--text-primary)' }}>{invFmt(INV.gbpTotal + INV.fee, 'GBP')}</td>
                </tr>
              </tfoot>
            </table>
          </div>

          {/* Payment history */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '16px 20px' }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 14, display: 'flex', justifyContent: 'space-between' }}>
              Payment history
              <button className="btn btn-ghost btn-sm" onClick={() => setDialog('reminder')}><Icon name="mail" size={12}/> Chase</button>
            </div>
            <div style={{ textAlign: 'center', padding: '28px 0', color: 'var(--text-tertiary)', fontSize: 13 }}>
              <Icon name="bank" size={32} color="var(--border-medium)"/>
              <div style={{ marginTop: 8 }}>No payments received yet.</div>
              <div style={{ fontSize: 12, marginTop: 4 }}>Due {INV.due} · {INV.terms}</div>
            </div>
          </div>

          {/* Notes */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '16px 20px' }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 14, display: 'flex', justifyContent: 'space-between' }}>
              Notes
              <button className="btn btn-ghost btn-sm" onClick={() => setDialog('note')}><Icon name="plus" size={12}/> Add note</button>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {INV.notes.map((n, i) => (
                <div key={i} style={{ display: 'flex', gap: 12, padding: '12px 14px', background: 'var(--surface-inset)', borderRadius: 8, border: '1px solid var(--border-light)' }}>
                  <InvAvatar name={n.author} color={n.color} size={28}/>
                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                      <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{n.author}</span>
                      <Pill tone={n.internal ? 'default' : 'tint'} size="sm">{n.internal ? 'Internal' : 'External'}</Pill>
                      <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 'auto' }}>{n.ts}</span>
                    </div>
                    <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{n.body}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Activity */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '16px 20px' }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 14 }}>Activity log</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
              {INV.activity.map((a, i) => (
                <div key={i} style={{ display: 'flex', gap: 14, padding: '9px 0', borderBottom: i < INV.activity.length - 1 ? '1px solid var(--border-light)' : 'none' }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', flex: 'none', marginTop: 1 }}>{a.ts}</div>
                  <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{a.msg}</div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Right — summary rail */}
        <div style={{ padding: '20px 18px', background: 'var(--surface-inset)', display: 'flex', flexDirection: 'column', gap: 14, overflow: 'auto' }}>
          {/* Amount summary */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>Summary</div>
            {[
              ['NGN subtotal', invFmt(ngnTotal, 'NGN')],
              ['GBP subtotal', invFmt(INV.gbpTotal, 'GBP')],
              ['Processing fee', invFmt(INV.fee, 'GBP')],
            ].map(([l, v]) => (
              <div key={l} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, marginBottom: 6 }}>
                <span style={{ color: 'var(--text-secondary)' }}>{l}</span>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{v}</span>
              </div>
            ))}
            <div style={{ height: 1, background: 'var(--border-light)', margin: '8px 0' }}/>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14 }}>
              <span style={{ fontWeight: 700, color: 'var(--text-primary)' }}>Total (GBP)</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-primary)' }}>{invFmt(INV.gbpTotal + INV.fee, 'GBP')}</span>
            </div>
            <div style={{ height: 1, background: 'var(--border-light)', margin: '8px 0' }}/>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, color: 'var(--danger)' }}>
              <span>Outstanding</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700 }}>{invFmt(INV.gbpTotal + INV.fee, 'GBP')}</span>
            </div>
          </div>

          {/* Payer details */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>Payer</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
              <InvAvatar name={INV.payer.name} color={INV.payer.color} size={36}/>
              <div>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{INV.payer.name}</div>
                <Pill tone="tint" size="sm">{INV.payer.tier} tier</Pill>
              </div>
            </div>
            {[['Email', INV.payer.email], ['Phone', INV.payer.phone]].map(([l, v]) => (
              <div key={l} style={{ fontSize: 12, marginBottom: 4 }}>
                <span style={{ color: 'var(--text-tertiary)' }}>{l}: </span>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{v}</span>
              </div>
            ))}
          </div>

          {/* Dates & terms */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>Dates</div>
            {[['Issued', INV.issued], ['Due', INV.due], ['Terms', INV.terms], ['Days until due', '30']].map(([l, v]) => (
              <div key={l} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, marginBottom: 6 }}>
                <span style={{ color: 'var(--text-secondary)' }}>{l}</span>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 500 }}>{v}</span>
              </div>
            ))}
          </div>

          {/* Linked order */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>Linked order</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <div style={{ width: 32, height: 32, borderRadius: 8, background: 'var(--brand-primary-10)', display: 'grid', placeItems: 'center' }}>
                <Icon name="invoice" size={14} color="var(--brand-primary)"/>
              </div>
              <div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{INV.orderId}</div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{INV_ITEMS.length} items · 22 Apr 2025</div>
              </div>
              <Icon name="arrowright" size={12} color="var(--text-tertiary)" style={{ marginLeft: 'auto' }}/>
            </div>
          </div>

          {/* Quick actions */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', marginBottom: 10 }}>Actions</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
              {[
                ['Collect payment', 'bank',     () => setDialog('collect'), false],
                ['Send reminder',   'mail',     () => setDialog('reminder'), false],
                ['Add note',        'invoice',  () => setDialog('note'),    false],
                ['Print / export',  'download', () => setDialog('print'),   false],
                ['Refund',          'arrowright', () => setDialog('refund'), false],
                ['Write off',       'warn',     () => setDialog('writeoff'), true ],
                ['Cancel invoice',  'x',        () => setDialog('cancel'),   true ],
              ].map(([l, ic, fn, danger]) => (
                <button key={l} onClick={fn} className="btn btn-ghost btn-sm" style={{ justifyContent: 'flex-start', color: danger ? 'var(--danger)' : 'inherit' }}>
                  <Icon name={ic} size={12} color={danger ? 'var(--danger)' : 'var(--text-secondary)'}/> {l}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Enhanced order listing ─────────────────────────────────────────────────
const ORDER_LIST_DATA = [
  {
    id: 'ORD-20250422-0047', invoice: 'INV-0047', status: 'Awaiting payment', tone: 'pending',
    payer: 'Primrose Logistics', payerColor: '#055a60', created: '22 Apr 14:22',
    gbpTotal: 5017.55, items: [
      { symbol: 'IE', color: '#1e4d8c' }, { symbol: 'GT', color: '#f26522' }, { symbol: 'DS', color: '#003087' },
    ],
  },
  {
    id: 'ORD-20250421-0046', invoice: 'INV-0046', status: 'Processing', tone: 'tint',
    payer: 'Northstar Freight', payerColor: '#0097a9', created: '21 Apr 09:14',
    gbpTotal: 12840.00, items: [
      { symbol: 'GT', color: '#f26522' }, { symbol: 'ZB', color: '#cc0000' },
    ],
  },
  {
    id: 'ORD-20250419-0045', invoice: 'INV-0045', status: 'Settled', tone: 'success',
    payer: 'Primrose Logistics', payerColor: '#055a60', created: '19 Apr 11:30',
    gbpTotal: 3200.00, items: [
      { symbol: 'MT', color: '#e6b800' },
    ],
  },
  {
    id: 'ORD-20250417-0044', invoice: 'INV-0044', status: 'Cancelled', tone: 'default',
    payer: 'James Okafor', payerColor: '#1f7a5e', created: '17 Apr 16:45',
    gbpTotal: 450.72, items: [
      { symbol: 'AI', color: '#e40000' }, { symbol: 'SM', color: '#00bcd4' },
    ],
  },
  {
    id: 'ORD-20250415-0043', invoice: 'INV-0043', status: 'Overdue', tone: 'danger',
    payer: 'Primrose Logistics', payerColor: '#055a60', created: '15 Apr 08:12',
    gbpTotal: 28400.00, items: [
      { symbol: 'GT', color: '#f26522' }, { symbol: 'FB', color: '#003366' }, { symbol: 'UB', color: '#c00000' }, { symbol: 'ZB', color: '#cc0000' },
    ],
  },
];

function ScreenOrderList() {
  const [sel, setSel] = React.useState('ORD-20250422-0047');

  return (
    <div style={{ padding: '22px 28px', height: '100%', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 18 }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between' }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Orders</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Each order contains one or more payment items and generates a single invoice.</div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-ghost btn-sm"><Icon name="filter" size={12}/> Filter</button>
          <button className="btn btn-ghost btn-sm"><Icon name="download" size={12}/> Export</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New order</button>
        </div>
      </div>

      {/* Stats bar */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
        {[
          { l: 'Outstanding', v: '£33,418', sub: '2 invoices', tone: 'danger' },
          { l: 'Processing',  v: '£12,840', sub: '1 order',    tone: 'tint' },
          { l: 'Settled MTD', v: '£3,200',  sub: '1 order',    tone: 'success' },
          { l: 'Total orders',v: '5',        sub: 'Apr 2025',   tone: 'default' },
        ].map(s => (
          <div key={s.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginBottom: 4 }}>{s.l}</div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 20, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>{s.v}</div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 2 }}>{s.sub}</div>
          </div>
        ))}
      </div>

      {/* Table */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: 'var(--surface-inset)' }}>
              {['Order', 'Payer', 'Items', 'Invoice', 'Status', 'Created', 'Total'].map(h => (
                <th key={h} style={{ padding: '10px 16px', fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.07em', color: 'var(--text-tertiary)', textAlign: h === 'Total' ? 'right' : 'left', whiteSpace: 'nowrap' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {ORDER_LIST_DATA.map(o => {
              const active = o.id === sel;
              return (
                <tr key={o.id} onClick={() => setSel(o.id)} style={{
                  borderTop: '1px solid var(--border-light)', cursor: 'pointer',
                  background: active ? 'var(--brand-primary-10)' : 'transparent',
                }}>
                  {/* Order ID */}
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{o.id}</div>
                  </td>
                  {/* Payer */}
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <InvAvatar name={o.payer} color={o.payerColor} size={26}/>
                      <span style={{ fontSize: 13, color: 'var(--text-primary)' }}>{o.payer}</span>
                    </div>
                  </td>
                  {/* Items — stacked logo strip */}
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <div style={{ display: 'flex' }}>
                        {o.items.slice(0, 3).map((it, i) => (
                          <div key={i} style={{ width: 24, height: 24, borderRadius: 5, background: it.color, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: 8, marginLeft: i > 0 ? -6 : 0, border: '1.5px solid var(--surface)', position: 'relative', zIndex: 3 - i }}>{it.symbol}</div>
                        ))}
                      </div>
                      <span style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 500 }}>{o.items.length} item{o.items.length !== 1 ? 's' : ''}</span>
                    </div>
                  </td>
                  {/* Invoice */}
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <Icon name="invoice" size={12} color="var(--text-tertiary)"/>
                      <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)' }}>{o.invoice}</span>
                    </div>
                  </td>
                  {/* Status */}
                  <td style={{ padding: '14px 16px' }}><Pill tone={o.tone} dot size="sm">{o.status}</Pill></td>
                  {/* Created */}
                  <td style={{ padding: '14px 16px', fontSize: 12.5, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)', whiteSpace: 'nowrap' }}>{o.created}</td>
                  {/* Total */}
                  <td style={{ padding: '14px 16px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 700, color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>{invFmt(o.gbpTotal, 'GBP')}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Data model callout */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 18px', display: 'flex', gap: 12, alignItems: 'flex-start' }}>
        <Icon name="invoice" size={16} color="var(--brand-primary)"/>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.55 }}>
          <strong style={{ color: 'var(--text-primary)' }}>One order → one invoice.</strong> Each order can contain a mix of bill payments and money transfers. All items share a single invoice and are collected in one payment event. Individual item status is visible on the order item detail page. Expand an order row to see item-level progress inline.
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenInvoiceDetail, ScreenOrderList });
