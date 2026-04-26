// ─── Create Order — Bill Payment & Money Transfer ─────────────────────────
// Unified single-screen order builder. Left = item configurator (tab-switched).
// Right = live cart. Supports mixed bill payment + money transfer items.

// ── Data ──────────────────────────────────────────────────────────────────

const PARTIES = [
  { id: 'p1', name: 'Primrose Logistics',  type: 'Business', role: 'Corporate', color: '#055a60', tier: 'Gold',    email: 'ops@primrose.co',     phone: '+44 7700 900123' },
  { id: 'p2', name: 'Oliver Chen',          type: 'Person',   role: 'Admin',    color: '#7b76b6', tier: 'Standard', email: 'oliver@primrose.co',  phone: '+44 7700 900124' },
  { id: 'p3', name: 'Maria Gomez',          type: 'Person',   role: 'Finance',  color: '#eb5c37', tier: 'Standard', email: 'maria@primrose.co',   phone: '+44 7700 900125' },
  { id: 'p4', name: 'Northstar Freight',    type: 'Business', role: 'Supplier', color: '#0097a9', tier: 'Silver',   email: 'pay@northstar.co',    phone: '+234 701 234 5678' },
  { id: 'p5', name: 'James Okafor',         type: 'Person',   role: 'Driver',   color: '#1f7a5e', tier: 'Basic',    email: 'james.o@primrose.co', phone: '+234 801 234 5678' },
  { id: 'p6', name: 'Adaeze Nwosu',         type: 'Person',   role: 'Finance',  color: '#d97706', tier: 'Basic',    email: 'adaeze@primrose.co',  phone: '+234 802 111 2222' },
];

const BILLERS = [
  { id: 'b-dstv',   name: 'DSTV',             cat: 'TV & Cable',    color: '#003087', bg: '#002475', text: '#fff', symbol: 'DS', currencies: ['NGN','USD'] },
  { id: 'b-gotv',   name: 'GOtv',             cat: 'TV & Cable',    color: '#007c3e', bg: '#005c2d', text: '#fff', symbol: 'GO', currencies: ['NGN'] },
  { id: 'b-mtn',    name: 'MTN',              cat: 'Airtime & Data', color: '#ffcb00', bg: '#e6b800', text: '#1a1200', symbol: 'MT', currencies: ['NGN','GHS'] },
  { id: 'b-airtel', name: 'Airtel',           cat: 'Airtime & Data', color: '#e40000', bg: '#c20000', text: '#fff', symbol: 'AI', currencies: ['NGN'] },
  { id: 'b-glo',    name: 'Glo',              cat: 'Airtime & Data', color: '#008000', bg: '#006600', text: '#fff', symbol: 'GL', currencies: ['NGN'] },
  { id: 'b-9mob',   name: '9mobile',          cat: 'Airtime & Data', color: '#00b38f', bg: '#009475', text: '#fff', symbol: '9M', currencies: ['NGN'] },
  { id: 'b-ikeja',  name: 'Ikeja Electric',   cat: 'Electricity',    color: '#1e4d8c', bg: '#163870', text: '#fff', symbol: 'IE', currencies: ['NGN'] },
  { id: 'b-eko',    name: 'Eko Electricity',  cat: 'Electricity',    color: '#009900', bg: '#007700', text: '#fff', symbol: 'EE', currencies: ['NGN'] },
  { id: 'b-aedc',   name: 'AEDC',             cat: 'Electricity',    color: '#b22222', bg: '#8c1a1a', text: '#fff', symbol: 'AE', currencies: ['NGN'] },
  { id: 'b-phcn',   name: 'PHCN / Ibadan',   cat: 'Electricity',    color: '#4a4a8a', bg: '#383878', text: '#fff', symbol: 'PH', currencies: ['NGN'] },
  { id: 'b-smile',  name: 'Smile',            cat: 'Internet',       color: '#00bcd4', bg: '#0097a9', text: '#fff', symbol: 'SM', currencies: ['NGN'] },
  { id: 'b-specta', name: 'Spectranet',       cat: 'Internet',       color: '#5c35a0', bg: '#47298a', text: '#fff', symbol: 'SP', currencies: ['NGN'] },
];

const BANKS = [
  { id: 'bk-gt',  name: 'GTBank',        country: 'NG', color: '#f26522', symbol: 'GT', swift: 'GTBINGLA' },
  { id: 'bk-ac',  name: 'Access Bank',   country: 'NG', color: '#e8461e', symbol: 'AB', swift: 'ABNGNGLA' },
  { id: 'bk-zn',  name: 'Zenith Bank',   country: 'NG', color: '#cc0000', symbol: 'ZB', swift: 'ZEIBNGLA' },
  { id: 'bk-fb',  name: 'First Bank',    country: 'NG', color: '#003366', symbol: 'FB', swift: 'FBNINGLA' },
  { id: 'bk-ub',  name: 'UBA',           country: 'NG', color: '#c00000', symbol: 'UB', swift: 'UNAFNGLA' },
  { id: 'bk-sb',  name: 'Stanbic IBTC', country: 'NG', color: '#009ee2', symbol: 'SI', swift: 'SBICNGLA' },
  { id: 'bk-wm',  name: 'Wema Bank',    country: 'NG', color: '#7c3aed', symbol: 'WB', swift: 'WEMANGLA' },
  { id: 'bk-hs',  name: 'HSBC',         country: 'GB', color: '#db0011', symbol: 'HS', swift: 'MIDLGB22' },
  { id: 'bk-br',  name: 'Barclays',     country: 'GB', color: '#00aeef', symbol: 'BA', swift: 'BARCGB22' },
  { id: 'bk-ll',  name: 'Lloyds Bank',  country: 'GB', color: '#024731', symbol: 'LL', swift: 'LOYDGB21' },
  { id: 'bk-bm',  name: 'BofA',         country: 'US', color: '#e31837', symbol: 'BA', swift: 'BOFAUS3N' },
  { id: 'bk-jp',  name: 'JPMorgan',     country: 'US', color: '#003DA5', symbol: 'JP', swift: 'CHASUS33' },
];

const BILLER_CATS = ['All', 'TV & Cable', 'Airtime & Data', 'Electricity', 'Internet'];
const BANK_COUNTRIES = ['All', 'NG', 'GB', 'US'];

const FX_RATES = { 'GBP→NGN': 1985, 'GBP→USD': 1.27, 'USD→NGN': 1563, 'NGN→GBP': 0.000504 };

// ── Helpers ───────────────────────────────────────────────────────────────
function fmt(n, cur = 'GBP') {
  const symbols = { GBP: '£', NGN: '₦', USD: '$' };
  const s = symbols[cur] || cur + ' ';
  return s + Number(n).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function partyInitials(name) {
  return (name || '?').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
}

function LogoMark({ color, bg, text, symbol, size = 40, radius = 8 }) {
  return (
    <div style={{
      width: size, height: size, borderRadius: radius,
      background: bg || color, color: text || '#fff',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: size * 0.32,
      letterSpacing: '-0.01em', flex: 'none',
    }}>{symbol}</div>
  );
}

function PartyAvatar({ party, size = 36 }) {
  return (
    <div style={{
      width: size, height: size, borderRadius: size * 0.28,
      background: party.color, color: '#fff', flex: 'none',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: size * 0.38,
    }}>{partyInitials(party.name)}</div>
  );
}

// Searchable party dropdown pill
function PartyPicker({ label, value, onChange, exclude = [], placeholder = 'Select party' }) {
  const [open, setOpen] = React.useState(false);
  const [q, setQ] = React.useState('');
  const ref = React.useRef(null);
  const sel = PARTIES.find(p => p.id === value);

  React.useEffect(() => {
    if (!open) return;
    const fn = e => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    document.addEventListener('mousedown', fn);
    return () => document.removeEventListener('mousedown', fn);
  }, [open]);

  const filtered = PARTIES.filter(p => !exclude.includes(p.id) && p.name.toLowerCase().includes(q.toLowerCase()));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)' }}>{label}</div>
      <div ref={ref} style={{ position: 'relative' }}>
        <button onClick={() => setOpen(o => !o)} style={{
          width: '100%', display: 'flex', alignItems: 'center', gap: 10,
          padding: '8px 12px', borderRadius: 10, cursor: 'pointer', background: 'var(--surface-inset)',
          border: open ? '1.5px solid var(--brand-primary)' : '1.5px solid var(--border-light)',
          boxShadow: open ? '0 0 0 3px var(--brand-primary-10)' : 'none',
          transition: 'border 150ms, box-shadow 150ms',
        }}>
          {sel
            ? <><PartyAvatar party={sel} size={32}/><div style={{ flex: 1, textAlign: 'left' }}>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{sel.name}</div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{sel.type} · {sel.tier}</div>
              </div></>
            : <><div style={{ width: 32, height: 32, borderRadius: 8, background: 'var(--surface)', border: '1px dashed var(--border-medium)', display: 'grid', placeItems: 'center' }}>
                <Icon name="users" size={14} color="var(--text-tertiary)"/>
              </div><span style={{ fontSize: 13, color: 'var(--text-tertiary)', flex: 1, textAlign: 'left' }}>{placeholder}</span></>}
          <Icon name={open ? 'chevup' : 'chevdown'} size={13} color="var(--text-secondary)"/>
        </button>

        {open && (
          <div style={{
            position: 'absolute', top: 'calc(100% + 6px)', left: 0, right: 0, zIndex: 60,
            background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12,
            boxShadow: '0 12px 32px -8px rgb(0 0 0 / 0.18)', overflow: 'hidden',
          }}>
            <div style={{ padding: '10px 12px', borderBottom: '1px solid var(--border-light)' }}>
              <div style={{ position: 'relative' }}>
                <span style={{ position: 'absolute', left: 10, top: 10 }}><Icon name="search" size={12} color="var(--text-tertiary)"/></span>
                <input autoFocus className="input" placeholder="Search parties…"
                  value={q} onChange={e => setQ(e.target.value)}
                  style={{ paddingLeft: 28, height: 34, fontSize: 12.5, width: '100%' }}/>
              </div>
            </div>
            <div style={{ maxHeight: 280, overflow: 'auto' }}>
              {filtered.map(p => (
                <div key={p.id} onClick={() => { onChange(p.id); setOpen(false); setQ(''); }}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 12, padding: '10px 14px', cursor: 'pointer',
                    background: p.id === value ? 'var(--brand-primary-10)' : 'transparent',
                    borderBottom: '1px solid var(--border-light)',
                  }}>
                  <PartyAvatar party={p} size={36}/>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 6 }}>
                      {p.name}
                      <Pill tone={p.tier === 'Gold' ? 'tint' : p.tier === 'Silver' ? 'default' : 'default'} size="sm">{p.tier}</Pill>
                    </div>
                    <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 1 }}>{p.email} · {p.phone}</div>
                  </div>
                  {p.id === value && <Icon name="check" size={13} color="var(--brand-primary)"/>}
                </div>
              ))}
              {filtered.length === 0 && <div style={{ padding: 20, textAlign: 'center', color: 'var(--text-tertiary)', fontSize: 12.5 }}>No parties found</div>}
            </div>
            <div style={{ padding: '8px 12px', borderTop: '1px solid var(--border-light)' }}>
              <button className="btn btn-ghost btn-sm" style={{ width: '100%', justifyContent: 'center' }}>
                <Icon name="plus" size={12}/> Add new party
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// Grid of biller logo cards
function BillerGrid({ value, onChange }) {
  const [cat, setCat] = React.useState('All');
  const [q, setQ] = React.useState('');
  const filtered = BILLERS.filter(b =>
    (cat === 'All' || b.cat === cat) &&
    b.name.toLowerCase().includes(q.toLowerCase())
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
        {BILLER_CATS.map(c => (
          <button key={c} onClick={() => setCat(c)} style={{
            padding: '4px 10px', borderRadius: 6, fontSize: 11.5, fontWeight: 500, cursor: 'pointer',
            background: cat === c ? 'var(--brand-primary)' : 'var(--surface-inset)',
            color: cat === c ? '#fff' : 'var(--text-secondary)',
            border: 'none',
          }}>{c}</button>
        ))}
      </div>
      <div style={{ position: 'relative' }}>
        <span style={{ position: 'absolute', left: 10, top: 10 }}><Icon name="search" size={12} color="var(--text-tertiary)"/></span>
        <input className="input" placeholder="Search billers…"
          value={q} onChange={e => setQ(e.target.value)}
          style={{ paddingLeft: 28, height: 34, fontSize: 12.5, width: '100%' }}/>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8, maxHeight: 220, overflow: 'auto', paddingRight: 2 }}>
        {filtered.map(b => {
          const sel = b.id === value;
          return (
            <div key={b.id} onClick={() => onChange(b.id === value ? '' : b.id)} style={{
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
              padding: '12px 8px', borderRadius: 10, cursor: 'pointer',
              border: sel ? '2px solid var(--brand-primary)' : '1.5px solid var(--border-light)',
              background: sel ? 'var(--brand-primary-10)' : 'var(--surface)',
              transition: 'border 120ms',
              position: 'relative',
            }}>
              {sel && <span style={{ position: 'absolute', top: 6, right: 6 }}><Icon name="check" size={11} color="var(--brand-primary)"/></span>}
              <LogoMark color={b.color} bg={b.bg} text={b.text} symbol={b.symbol} size={38} radius={8}/>
              <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-primary)', textAlign: 'center', lineHeight: 1.2 }}>{b.name}</div>
              <div style={{ fontSize: 10, color: 'var(--text-tertiary)', textAlign: 'center' }}>{b.cat}</div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// Grid of bank logo cards
function BankGrid({ value, onChange }) {
  const [cc, setCc] = React.useState('NG');
  const filtered = BANKS.filter(b => cc === 'All' || b.country === cc);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div style={{ display: 'flex', gap: 4 }}>
        {BANK_COUNTRIES.map(c => (
          <button key={c} onClick={() => setCc(c)} style={{
            padding: '4px 10px', borderRadius: 6, fontSize: 11.5, fontWeight: 500, cursor: 'pointer',
            background: cc === c ? 'var(--brand-primary)' : 'var(--surface-inset)',
            color: cc === c ? '#fff' : 'var(--text-secondary)',
            border: 'none',
          }}>{c === 'All' ? 'All' : c}</button>
        ))}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8, maxHeight: 220, overflow: 'auto', paddingRight: 2 }}>
        {filtered.map(b => {
          const sel = b.id === value;
          return (
            <div key={b.id} onClick={() => onChange(b.id === value ? '' : b.id)} style={{
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
              padding: '12px 8px', borderRadius: 10, cursor: 'pointer',
              border: sel ? '2px solid var(--brand-primary)' : '1.5px solid var(--border-light)',
              background: sel ? 'var(--brand-primary-10)' : 'var(--surface)',
              transition: 'border 120ms', position: 'relative',
            }}>
              {sel && <span style={{ position: 'absolute', top: 6, right: 6 }}><Icon name="check" size={11} color="var(--brand-primary)"/></span>}
              <LogoMark color={b.color} symbol={b.symbol} size={38} radius={8}/>
              <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-primary)', textAlign: 'center', lineHeight: 1.2 }}>{b.name}</div>
              <div style={{ fontSize: 10, color: 'var(--text-tertiary)' }}>{b.country}</div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// FX rate mini-quote display
function FxQuote({ origin, dest, amount }) {
  const key = `${origin}→${dest}`;
  const rate = FX_RATES[key];
  if (!rate || !amount || !origin || !dest || origin === dest) return null;
  const out = (parseFloat(amount) || 0) * rate;
  return (
    <div style={{
      background: 'var(--brand-primary-10)', border: '1px solid var(--brand-primary)', borderRadius: 8,
      padding: '10px 14px', display: 'flex', alignItems: 'center', justifyContent: 'space-between',
    }}>
      <div style={{ fontSize: 12, color: 'var(--brand-primary)', fontWeight: 500 }}>Live FX quote</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, color: 'var(--brand-primary)', fontWeight: 700 }}>
        {fmt(parseFloat(amount) || 0, origin)} → {fmt(out, dest)}
      </div>
      <div style={{ fontSize: 11, color: 'var(--brand-primary)', opacity: 0.75 }}>
        1 {origin} = {rate} {dest}
      </div>
    </div>
  );
}

// Cart item row
function CartItem({ item, idx, onRemove }) {
  const isBill = item.type === 'bill';
  const biller = BILLERS.find(b => b.id === item.billerId);
  const bank = BANKS.find(b => b.id === item.bankId);
  const payer = PARTIES.find(p => p.id === item.payerId);
  const beneficiary = PARTIES.find(p => p.id === item.benefId);

  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px',
      display: 'flex', flexDirection: 'column', gap: 8,
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
        {isBill
          ? (biller ? <LogoMark color={biller.color} bg={biller.bg} text={biller.text} symbol={biller.symbol} size={34} radius={6}/> : <div style={{ width: 34, height: 34, borderRadius: 6, background: 'var(--surface-inset)', border: '1px solid var(--border-light)' }}/>)
          : (bank ? <LogoMark color={bank.color} symbol={bank.symbol} size={34} radius={6}/> : <div style={{ width: 34, height: 34, borderRadius: 6, background: 'var(--surface-inset)', border: '1px solid var(--border-light)' }}/>)}
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>
              {isBill ? (biller?.name || 'Bill payment') : (bank?.name || 'Money transfer')}
            </span>
            <Pill tone={isBill ? 'tint' : 'success'} size="sm">{isBill ? 'Bill payment' : 'Transfer'}</Pill>
          </div>
          {isBill && item.service && <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{item.service}{item.accountRef && ` · ${item.accountRef}`}</div>}
          {!isBill && item.accountName && <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{item.accountName}{item.acctNo && ` · ${item.acctNo}`}</div>}
        </div>
        <button onClick={() => onRemove(idx)} style={{
          width: 24, height: 24, display: 'grid', placeItems: 'center', borderRadius: 6, cursor: 'pointer',
          background: 'transparent', border: 'none', color: 'var(--text-tertiary)',
        }} className="hover-halo">
          <Icon name="x" size={12}/>
        </button>
      </div>

      {/* Party row */}
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        {payer && <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <PartyAvatar party={payer} size={20}/>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{payer.name}</span>
        </div>}
        <Icon name="arrowright" size={11} color="var(--text-tertiary)"/>
        {beneficiary && <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <PartyAvatar party={beneficiary} size={20}/>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{beneficiary.name}</span>
        </div>}
      </div>

      {/* Amount row */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingTop: 6, borderTop: '1px solid var(--border-light)' }}>
        <div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Amount</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 700, color: 'var(--text-primary)' }}>{fmt(item.amount, item.currency)}</div>
        </div>
        {item.fxOut && <div style={{ textAlign: 'right' }}>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Equivalent</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, color: 'var(--brand-primary)', fontWeight: 600 }}>{fmt(item.fxOut, item.destCurrency)}</div>
        </div>}
        <div style={{ textAlign: 'right' }}>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Fee est.</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)' }}>{fmt(item.fee, item.currency)}</div>
        </div>
      </div>
    </div>
  );
}

// ── Bill Payment item builder ─────────────────────────────────────────────
function BillPaymentForm({ onAdd }) {
  const [payer, setPayer] = React.useState('p1');
  const [benef, setBenef] = React.useState('p5');
  const [billerId, setBillerId] = React.useState('b-ikeja');
  const [service, setService] = React.useState('Prepaid token');
  const [accountRef, setAccountRef] = React.useState('7012345678');
  const [amount, setAmount] = React.useState('15000');
  const [currency, setCurrency] = React.useState('NGN');
  const biller = BILLERS.find(b => b.id === billerId);

  const handleAdd = () => {
    if (!payer || !billerId || !amount) return;
    const fxKey = `${currency}→GBP`;
    const fxRate = FX_RATES[fxKey];
    onAdd({
      type: 'bill', payerId: payer, benefId: benef, billerId, service, accountRef,
      amount: parseFloat(amount) || 0, currency, fee: Math.round(parseFloat(amount) * 0.015),
      fxOut: fxRate ? parseFloat(amount) * fxRate : null,
      destCurrency: fxRate ? 'GBP' : null,
    });
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Parties */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <PartyPicker label="Payer" value={payer} onChange={setPayer} exclude={[benef]}/>
        <PartyPicker label="Beneficiary" value={benef} onChange={setBenef} exclude={[payer]} placeholder="Select beneficiary"/>
      </div>

      {/* Biller selection */}
      <div>
        <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Biller</div>
        <BillerGrid value={billerId} onChange={setBillerId}/>
      </div>

      {/* Service + Account */}
      {biller && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <div>
            <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Service type</label>
            <select className="select" value={service} onChange={e => setService(e.target.value)}>
              <option>Prepaid token</option><option>Postpaid bill</option><option>Recharge</option><option>Subscription</option>
            </select>
          </div>
          <div>
            <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>
              {biller.cat === 'Electricity' ? 'Meter number' : biller.cat === 'TV & Cable' ? 'Smart card no.' : 'Account / phone'}
            </label>
            <input className="input" value={accountRef} onChange={e => setAccountRef(e.target.value)} placeholder="Account reference"/>
          </div>
        </div>
      )}

      {/* Amount */}
      <div style={{ display: 'grid', gridTemplateColumns: '80px 1fr', gap: 10 }}>
        <div>
          <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Currency</label>
          <select className="select" value={currency} onChange={e => setCurrency(e.target.value)}>
            <option>NGN</option><option>GBP</option><option>USD</option>
          </select>
        </div>
        <div>
          <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Amount</label>
          <input className="input" value={amount} onChange={e => setAmount(e.target.value)} placeholder="0.00" style={{ fontFamily: 'var(--font-mono)' }}/>
        </div>
      </div>

      <FxQuote origin={currency} dest="GBP" amount={amount}/>

      <button onClick={handleAdd} className="btn btn-primary" style={{ width: '100%', justifyContent: 'center', marginTop: 4 }}
        disabled={!payer || !billerId || !amount}>
        <Icon name="plus" size={14}/> Add to order
      </button>
    </div>
  );
}

// ── Money Transfer item builder ───────────────────────────────────────────
function MoneyTransferForm({ onAdd }) {
  const [sender, setSender] = React.useState('p1');
  const [recipient, setRecipient] = React.useState('p4');
  const [bankId, setBankId] = React.useState('bk-gt');
  const [accountName, setAccountName] = React.useState('Northstar Freight Ltd');
  const [acctNo, setAcctNo] = React.useState('0123456789');
  const [amount, setAmount] = React.useState('5000');
  const [sendCur, setSendCur] = React.useState('GBP');
  const [destCur, setDestCur] = React.useState('NGN');
  const [purpose, setPurpose] = React.useState('TRADE');
  const bank = BANKS.find(b => b.id === bankId);

  const handleAdd = () => {
    if (!sender || !bankId || !amount) return;
    const fxKey = `${sendCur}→${destCur}`;
    const fxRate = FX_RATES[fxKey];
    onAdd({
      type: 'transfer', payerId: sender, benefId: recipient, bankId, accountName, acctNo,
      amount: parseFloat(amount) || 0, currency: sendCur,
      fee: Math.round(parseFloat(amount) * 0.005 * (sendCur === 'GBP' ? 1 : 0.01)),
      fxOut: fxRate ? parseFloat(amount) * fxRate : null, destCurrency: destCur,
    });
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Parties */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <PartyPicker label="Sender" value={sender} onChange={setSender} exclude={[recipient]}/>
        <PartyPicker label="Recipient" value={recipient} onChange={setRecipient} exclude={[sender]} placeholder="Select recipient"/>
      </div>

      {/* Bank */}
      <div>
        <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Destination bank</div>
        <BankGrid value={bankId} onChange={setBankId}/>
      </div>

      {/* Account details */}
      {bank && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <div>
            <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Account name</label>
            <input className="input" value={accountName} onChange={e => setAccountName(e.target.value)} placeholder="Account holder name"/>
          </div>
          <div>
            <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Account number / IBAN</label>
            <input className="input" value={acctNo} onChange={e => setAcctNo(e.target.value)} placeholder="Account number" style={{ fontFamily: 'var(--font-mono)' }}/>
          </div>
        </div>
      )}

      {/* Currencies + amount */}
      <div style={{ display: 'grid', gridTemplateColumns: '80px 1fr 80px', gap: 10 }}>
        <div>
          <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Send</label>
          <select className="select" value={sendCur} onChange={e => setSendCur(e.target.value)}>
            <option>GBP</option><option>USD</option><option>NGN</option>
          </select>
        </div>
        <div>
          <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Amount</label>
          <input className="input" value={amount} onChange={e => setAmount(e.target.value)} placeholder="0.00" style={{ fontFamily: 'var(--font-mono)' }}/>
        </div>
        <div>
          <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Receive</label>
          <select className="select" value={destCur} onChange={e => setDestCur(e.target.value)}>
            <option>NGN</option><option>GBP</option><option>USD</option>
          </select>
        </div>
      </div>

      <FxQuote origin={sendCur} dest={destCur} amount={amount}/>

      {/* Purpose code */}
      <div>
        <label style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, display: 'block' }}>Purpose code</label>
        <select className="select" value={purpose} onChange={e => setPurpose(e.target.value)}>
          <option value="TRADE">TRADE — Goods & services</option>
          <option value="SALA">SALA — Salary / payroll</option>
          <option value="SUPP">SUPP — Supplier payment</option>
          <option value="FAMI">FAMI — Family support</option>
          <option value="LOAN">LOAN — Loan repayment</option>
        </select>
      </div>

      <button onClick={handleAdd} className="btn btn-primary" style={{ width: '100%', justifyContent: 'center', marginTop: 4 }}
        disabled={!sender || !bankId || !amount}>
        <Icon name="plus" size={14}/> Add to order
      </button>
    </div>
  );
}

// ── Main create-order screen ──────────────────────────────────────────────
function ScreenCreateOrder() {
  const [mode, setMode] = React.useState('bill'); // 'bill' | 'transfer'
  const [items, setItems] = React.useState([
    // Pre-seeded item for visual interest
    {
      type: 'bill', payerId: 'p1', benefId: 'p5', billerId: 'b-ikeja',
      service: 'Prepaid token', accountRef: '7012345678',
      amount: 15000, currency: 'NGN', fee: 225,
      fxOut: 15000 * FX_RATES['NGN→GBP'], destCurrency: 'GBP',
    },
  ]);

  const removeItem = idx => setItems(prev => prev.filter((_, i) => i !== idx));
  const addItem = item => setItems(prev => [...prev, item]);

  // Totals per currency
  const totals = React.useMemo(() => {
    const map = {};
    items.forEach(it => {
      if (!map[it.currency]) map[it.currency] = { amount: 0, fee: 0 };
      map[it.currency].amount += it.amount;
      map[it.currency].fee += it.fee;
    });
    return Object.entries(map).map(([cur, v]) => ({ cur, ...v }));
  }, [items]);

  const gbpTotal = items.reduce((sum, it) => {
    if (it.currency === 'GBP') return sum + it.amount;
    const r = FX_RATES[`${it.currency}→GBP`];
    return sum + (r ? it.amount * r : 0);
  }, 0);

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 380px', height: '100%', minHeight: 0 }}>

      {/* ── Left: item builder ── */}
      <div style={{ borderRight: '1px solid var(--border-light)', overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
        {/* Top bar */}
        <div style={{ padding: '18px 24px 0', flex: 'none' }}>
          <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 4 }}>Orders · New order</div>
          <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', marginBottom: 16 }}>
            <div>
              <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Create order</div>
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Build a multi-item order — mix bill payments and money transfers in one submission.</div>
            </div>
            <Pill tone="pending" dot>Draft</Pill>
          </div>

          {/* Mode tabs */}
          <div style={{ display: 'flex', gap: 0, background: 'var(--surface-inset)', borderRadius: 10, padding: 4, width: 'fit-content', marginBottom: 20 }}>
            {[['bill', 'Bill payment', 'invoice'], ['transfer', 'Money transfer', 'payout']].map(([m, label, icon]) => {
              const active = mode === m;
              return (
                <button key={m} onClick={() => setMode(m)} style={{
                  display: 'flex', alignItems: 'center', gap: 6,
                  padding: '7px 16px', borderRadius: 7, fontSize: 13, fontWeight: 500, cursor: 'pointer', border: 'none',
                  background: active ? 'var(--surface)' : 'transparent',
                  color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                  boxShadow: active ? '0 1px 3px rgba(0,0,0,0.08)' : 'none',
                  transition: 'all 150ms',
                }}>
                  <Icon name={icon} size={13} color={active ? 'var(--brand-primary)' : 'currentColor'}/>
                  {label}
                </button>
              );
            })}
          </div>
        </div>

        {/* Form area */}
        <div style={{ padding: '0 24px 24px', overflow: 'auto', flex: 1 }}>
          {mode === 'bill'
            ? <BillPaymentForm onAdd={addItem}/>
            : <MoneyTransferForm onAdd={addItem}/>}
        </div>
      </div>

      {/* ── Right: order cart ── */}
      <div style={{ display: 'flex', flexDirection: 'column', background: 'var(--surface-inset)', overflow: 'hidden' }}>
        {/* Cart header */}
        <div style={{ padding: '18px 20px 14px', borderBottom: '1px solid var(--border-light)', flex: 'none' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>Order</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <div style={{
                width: 22, height: 22, borderRadius: '50%', background: items.length > 0 ? 'var(--brand-primary)' : 'var(--text-tertiary)',
                color: '#fff', display: 'grid', placeItems: 'center', fontSize: 11, fontWeight: 700,
              }}>{items.length}</div>
              <span style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>items</span>
            </div>
          </div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 3 }}>ORD-{new Date().toISOString().slice(0,10).replace(/-/g,'')}-DRAFT</div>
        </div>

        {/* Cart items */}
        <div style={{ flex: 1, overflow: 'auto', padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 10 }}>
          {items.length === 0 && (
            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 12, padding: 32, textAlign: 'center' }}>
              <div style={{ width: 48, height: 48, borderRadius: 12, background: 'var(--surface)', border: '1px dashed var(--border-medium)', display: 'grid', placeItems: 'center' }}>
                <Icon name="invoice" size={20} color="var(--text-tertiary)"/>
              </div>
              <div>
                <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-secondary)' }}>No items yet</div>
                <div style={{ fontSize: 12, color: 'var(--text-tertiary)', marginTop: 4 }}>Configure an item on the left, then click Add to order.</div>
              </div>
            </div>
          )}
          {items.map((item, idx) => (
            <CartItem key={idx} item={item} idx={idx} onRemove={removeItem}/>
          ))}
        </div>

        {/* Totals + submit */}
        {items.length > 0 && (
          <div style={{ borderTop: '1px solid var(--border-light)', padding: '14px 16px', flex: 'none', background: 'var(--surface)', display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {totals.map(t => (
                <div key={t.cur} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5 }}>
                  <span style={{ color: 'var(--text-secondary)' }}>{t.cur} total</span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 600 }}>{fmt(t.amount, t.cur)}</span>
                </div>
              ))}
              {totals.map(t => (
                <div key={t.cur + '-fee'} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12 }}>
                  <span style={{ color: 'var(--text-tertiary)' }}>Est. fees ({t.cur})</span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{fmt(t.fee, t.cur)}</span>
                </div>
              ))}
              <div style={{ height: 1, background: 'var(--border-light)', margin: '4px 0' }}/>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13.5 }}>
                <span style={{ color: 'var(--text-secondary)', fontWeight: 500 }}>GBP equivalent</span>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 700 }}>{fmt(gbpTotal, 'GBP')}</span>
              </div>
            </div>

            {/* Policy check */}
            <div style={{ background: 'var(--brand-primary-10)', border: '1px solid var(--brand-primary)', borderRadius: 8, padding: '8px 12px', display: 'flex', gap: 8, alignItems: 'center' }}>
              <Icon name="shield" size={13} color="var(--brand-primary)"/>
              <div style={{ fontSize: 11.5, color: 'var(--brand-primary)', lineHeight: 1.4 }}>
                {gbpTotal < 50000 ? 'All items within auto-apply policy ceiling (£50K).' : 'Exceeds £50K threshold — manual approval required.'}
              </div>
            </div>

            <div style={{ display: 'flex', gap: 8 }}>
              <button className="btn btn-ghost btn-sm" style={{ flex: '0 0 auto' }}>Save draft</button>
              <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }}>
                <Icon name="check" size={13}/> Submit order
              </button>
            </div>

            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'center' }}>
              {items.length} item{items.length !== 1 ? 's' : ''} · compliance checks run on submit
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCreateOrder });
