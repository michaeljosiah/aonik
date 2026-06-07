// Bill Payments — Billers catalog (the "noun" layer for Bill Pay)
// Catalog of provider integrations + per-biller routing/policy config.
//
// Spec 040 (Partner biller catalogue import) adds:
//   • an "Import from partner" wizard (Source → Preview → Confirm) that pulls a
//     partner connector's live biller catalogue, tags each row New/Mapped/Changed,
//     lets the operator select a subset, and reports an idempotent upsert summary.
//   • imported-from provenance on each biller (the ConnectorBillerMapping owner).
//   • a soft-deactivated "Inactive" state (a biller the partner dropped — kept, not deleted).
//   • a biller → services detail drawer (packages: Fixed/Variable amount + customer fields).

const BILLER_CATEGORIES = [
  { id: 'all',        label: 'All billers',     count: 142 },
  { id: 'utilities',  label: 'Utilities',       count:  38 },
  { id: 'telco',      label: 'Telco & Internet',count:  24 },
  { id: 'tv',         label: 'TV & Media',      count:  12 },
  { id: 'tax',        label: 'Tax & Government',count:  18 },
  { id: 'subs',       label: 'Subscriptions',   count:  31 },
  { id: 'edu',        label: 'Education',       count:   9 },
  { id: 'health',     label: 'Healthcare',      count:  10 },
];

const BILLERS = [
  // utilities
  { id:'ekedc',   name:'Ikeja Electric',   sym:'IE', color:'#1e4d8c', cat:'utilities', country:'NG', txMonth: 4128, success: 99.4, partners:['Flutterwave','Paystack'], statusTone:'success', status:'Active',     fee:'1.20%', avg:'12s', from:'Flutterwave', code:'BIL112' },
  { id:'aedc',    name:'Abuja Electric',   sym:'AE', color:'#0d3b66', cat:'utilities', country:'NG', txMonth: 1840, success: 98.9, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'1.20%', avg:'14s', from:'Flutterwave', code:'BIL110' },
  { id:'ekedp',   name:'Eko Electric',     sym:'EK', color:'#16a085', cat:'utilities', country:'NG', txMonth: 2204, success: 97.8, partners:['Paystack','Squad'],       statusTone:'warning', status:'Degraded',   fee:'1.50%', avg:'28s', from:'Paystack',    code:'BIL099' },
  { id:'lawma',   name:'LAWMA Waste',      sym:'LW', color:'#2c7a3f', cat:'utilities', country:'NG', txMonth:  612, success: 96.5, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'1.50%', avg:'18s', from:'Flutterwave', code:'BIL118' },
  // telco
  { id:'mtn',     name:'MTN Nigeria',      sym:'MT', color:'#e6b800', cat:'telco',     country:'NG', txMonth: 8302, success: 99.9, partners:['Flutterwave','Paystack','Direct'], statusTone:'success', status:'Active',     fee:'0.80%', avg:'4s', from:'Flutterwave', code:'BIL099a' },
  { id:'glo',     name:'Glo Mobile',       sym:'GM', color:'#26a65b', cat:'telco',     country:'NG', txMonth: 3104, success: 99.8, partners:['Flutterwave','Paystack'],          statusTone:'success', status:'Active',     fee:'0.80%', avg:'5s', from:'Flutterwave', code:'BIL102' },
  { id:'airtel',  name:'Airtel',           sym:'AT', color:'#cc0000', cat:'telco',     country:'NG', txMonth: 4210, success: 99.7, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'0.80%', avg:'5s', from:'Flutterwave', code:'BIL100' },
  { id:'spectra', name:'Spectranet',       sym:'SN', color:'#5b3aaa', cat:'telco',     country:'NG', txMonth:  584, success: 98.2, partners:['Paystack'],               statusTone:'success', status:'Active',     fee:'1.00%', avg:'9s', from:'Paystack',    code:'BIL133' },
  // tv
  { id:'dstv',    name:'DSTV',             sym:'DS', color:'#003087', cat:'tv',        country:'NG', txMonth: 1820, success: 99.1, partners:['Flutterwave','Paystack'],          statusTone:'success', status:'Active',     fee:'1.00%', avg:'7s', from:'Flutterwave', code:'BIL104' },
  { id:'gotv',    name:'GOtv',             sym:'GO', color:'#0b6e3a', cat:'tv',        country:'NG', txMonth:  912, success: 98.7, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'1.00%', avg:'8s', from:'Flutterwave', code:'BIL105' },
  { id:'startime',name:'StarTimes',        sym:'ST', color:'#d40e1e', cat:'tv',        country:'NG', txMonth:  402, success: 96.0, partners:['Paystack'],               statusTone:'pending', status:'Maintenance',fee:'1.20%', avg:'—',  from:'Paystack',    code:'BIL106' },
  // tax
  { id:'firs',    name:'FIRS Federal Tax', sym:'FT', color:'#1f3a5f', cat:'tax',       country:'NG', txMonth:  142, success: 99.5, partners:['Direct'],                  statusTone:'success', status:'Active',     fee:'flat ₦200', avg:'31s', from:'Manual', code:'—' },
  { id:'lirs',    name:'LIRS Lagos Tax',   sym:'LR', color:'#1b6e3f', cat:'tax',       country:'NG', txMonth:  308, success: 99.2, partners:['Direct','Flutterwave'],   statusTone:'success', status:'Active',     fee:'flat ₦200', avg:'24s', from:'Flutterwave', code:'BIL150' },
  { id:'cac',     name:'CAC Filings',      sym:'CC', color:'#3a3a3a', cat:'tax',       country:'NG', txMonth:   84, success: 97.3, partners:['Direct'],                  statusTone:'success', status:'Active',     fee:'flat ₦500', avg:'42s', from:'Manual', code:'—' },
  // subs
  { id:'netflix', name:'Netflix',          sym:'NF', color:'#e50914', cat:'subs',      country:'US', txMonth:  204, success: 99.6, partners:['Stripe'],                  statusTone:'success', status:'Active',     fee:'2.90%', avg:'3s', from:'Manual', code:'—' },
  { id:'spotify', name:'Spotify Premium',  sym:'SP', color:'#1ed760', cat:'subs',      country:'SE', txMonth:  158, success: 99.4, partners:['Stripe'],                  statusTone:'success', status:'Active',     fee:'2.90%', avg:'3s', from:'Manual', code:'—' },
  // soft-deactivated — partner dropped these on a previous sync; kept, not deleted (Spec 040 §8)
  { id:'phed',    name:'PH Electric (legacy)', sym:'PH', color:'#8a94a3', cat:'utilities', country:'NG', txMonth: 0, success: 0, partners:['Flutterwave'], statusTone:'muted', status:'Inactive', fee:'—', avg:'—', from:'Flutterwave', code:'BIL121', inactive:true, dropped:'dropped on 02 Jun sync' },
  { id:'swift',   name:'Swift 4G (legacy)',    sym:'SW', color:'#8a94a3', cat:'telco',     country:'NG', txMonth: 0, success: 0, partners:['Flutterwave'], statusTone:'muted', status:'Inactive', fee:'—', avg:'—', from:'Flutterwave', code:'BIL134', inactive:true, dropped:'dropped on 02 Jun sync' },
];

// Service (package) generator — what the import writes per biller (Spec 040 §7).
function bzServices(b) {
  const code = (p, n) => p + (100 + ((b.name.charCodeAt(0) + n * 7) % 800));
  if (b.cat === 'telco') return [
    { name: 'Airtime top-up',        kind: 'Variable', amount: '—',      field: 'Mobile Number', code: code('AT', 1), active: true },
    { name: b.sym + ' Data · 1.5GB', kind: 'Fixed',    amount: '₦1,200', field: 'Mobile Number', code: code('DT', 2), active: true },
    { name: b.sym + ' Data · 6GB',   kind: 'Fixed',    amount: '₦2,500', field: 'Mobile Number', code: code('DT', 3), active: true },
    { name: b.sym + ' Data · 11GB',  kind: 'Fixed',    amount: '₦4,000', field: 'Mobile Number', code: code('DT', 4), active: !b.inactive },
  ];
  if (b.cat === 'utilities') return [
    { name: 'Prepaid (token)', kind: 'Variable', amount: '—', field: 'Meter Number',   code: code('PRE', 1), active: true },
    { name: 'Postpaid',        kind: 'Variable', amount: '—', field: 'Account Number', code: code('PST', 2), active: !b.inactive },
  ];
  if (b.cat === 'tv') return [
    { name: 'Compact',      kind: 'Fixed', amount: '₦10,500', field: 'Smartcard Number', code: code('TV', 1), active: true },
    { name: 'Compact Plus', kind: 'Fixed', amount: '₦16,600', field: 'Smartcard Number', code: code('TV', 2), active: true },
    { name: 'Premium',      kind: 'Fixed', amount: '₦29,500', field: 'Smartcard Number', code: code('TV', 3), active: true },
  ];
  if (b.cat === 'tax')  return [{ name: 'Tax payment',  kind: 'Variable', amount: '—', field: 'Tax ID (TIN)',  code: code('TAX', 1), active: true }];
  if (b.cat === 'subs') return [{ name: 'Monthly plan', kind: 'Fixed',    amount: '₦4,400', field: 'Account Email', code: code('SUB', 1), active: true }];
  return [{ name: 'Standard payment', kind: 'Variable', amount: '—', field: 'Account Number', code: code('GEN', 1), active: true }];
}

const BZ_FROM_TONE = { Flutterwave: '#0e7490', Paystack: '#0a7d4b', Stripe: '#635bff', Direct: '#7b76b6', Manual: '#8a94a3' };

function ScreenBillers() {
  const [cat, setCat] = React.useState('all');
  const [view, setView] = React.useState('grid');
  const [importing, setImporting] = React.useState(false);
  const [detail, setDetail] = React.useState(null);   // biller for the services drawer
  const [flash, setFlash] = React.useState(null);      // post-import success banner
  const filtered = cat === 'all' ? BILLERS : BILLERS.filter(b => b.cat === cat);

  const fmt = n => Number(n).toLocaleString('en-GB');

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.bz-card{transition:border-color 140ms ease, box-shadow 140ms ease, transform 140ms ease;}
.bz-card:hover{border-color:var(--border-medium)!important;box-shadow:0 4px 14px -8px rgba(20,25,30,0.18);transform:translateY(-1px);}
.bz-row:hover{background:var(--surface-inset);}`}</style>

      <div style={{ height: '100%', display: 'grid', gridTemplateColumns: '220px 1fr', overflow: 'hidden' }}>
        {/* Category rail */}
        <div style={{ borderRight: '1px solid var(--border-light)', padding: '20px 14px', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 2, background: 'var(--surface-inset)' }}>
          <div style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', padding: '4px 8px 8px' }}>Categories</div>
          {BILLER_CATEGORIES.map(c => (
            <button key={c.id} onClick={() => setCat(c.id)} style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              padding: '8px 10px', borderRadius: 6, border: 'none', cursor: 'pointer',
              background: cat === c.id ? 'var(--brand-primary-10)' : 'transparent',
              color: cat === c.id ? 'var(--brand-primary)' : 'var(--text-secondary)',
              fontSize: 12.5, fontWeight: cat === c.id ? 600 : 500, textAlign: 'left',
            }}>
              <span>{c.label}</span>
              <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', opacity: 0.7 }}>{c.count}</span>
            </button>
          ))}
          <div style={{ height: 1, background: 'var(--border-light)', margin: '12px 4px' }}/>
          <button style={{
            display:'flex', alignItems:'center', gap: 8, padding:'8px 10px', borderRadius:6,
            border:'1px dashed var(--border-medium)', cursor:'pointer', background:'transparent',
            color:'var(--text-secondary)', fontSize:12.5, justifyContent:'center'
          }}>
            <Icon name="plus" size={12}/> Add category
          </button>
        </div>

        {/* Main */}
        <div style={{ padding: '22px 28px', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 18 }}>
          {/* Header */}
          <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
            <div>
              <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Billers</div>
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>
                The catalog of providers your operators can pay through. Routing, fees and policy live here — orders consume this.
              </div>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <div style={{ position: 'relative' }}>
                <input placeholder="Search billers" style={{
                  background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius:6,
                  padding:'7px 10px 7px 28px', fontSize:12.5, color:'var(--text-primary)', width: 200, fontFamily:'var(--font-sans)'
                }}/>
                <span style={{ position:'absolute', left: 9, top: 8 }}><Icon name="search" size={13} color="var(--text-tertiary)"/></span>
              </div>
              <button className="btn btn-sm" onClick={() => setImporting(true)}><Icon name="download" size={12}/> Import from partner</button>
              <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add biller</button>
            </div>
          </div>

          {/* Post-import flash */}
          {flash && (
            <div style={{ display:'flex', alignItems:'center', gap: 10, padding:'11px 14px', borderRadius: '0 10px 10px 0', background:'var(--success-tint, #1f7a5e12)', borderLeft:'3px solid var(--success)' }}>
              <span style={{ width: 22, height: 22, borderRadius: 999, background:'var(--success)', color:'#fff', display:'grid', placeItems:'center', flex:'none' }}><Icon name="check" size={13} color="#fff"/></span>
              <div style={{ fontSize: 12.5, color:'var(--text-primary)' }}>
                Imported from <b>Flutterwave</b> — <b style={{ fontFamily:'var(--font-mono)' }}>{flash.created}</b> created · <b style={{ fontFamily:'var(--font-mono)' }}>{flash.updated}</b> updated · <b style={{ fontFamily:'var(--font-mono)' }}>{flash.deactivated}</b> deactivated.
              </div>
              <div style={{ flex: 1 }}/>
              <button onClick={() => setFlash(null)} style={{ border:'none', background:'transparent', cursor:'pointer', color:'var(--text-tertiary)' }}><Icon name="close" size={14}/></button>
            </div>
          )}

          {/* Stats strip */}
          <div style={{ display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 12 }}>
            {[
              { label:'Active billers',   v:'138',     sub:'2 inactive · 4 in maintenance' },
              { label:'Tx this month',    v:'24,810',  sub:'+12% vs last month' },
              { label:'Avg success rate', v:'99.1%',   sub:'across all billers' },
              { label:'Avg time-to-receipt', v:'9.4s', sub:'p95 · 38s' },
            ].map(s => (
              <div key={s.label} style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
                <div style={{ fontSize: 11, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight: 600 }}>{s.label}</div>
                <div style={{ fontSize: 22, fontWeight: 700, color:'var(--text-primary)', marginTop: 4, fontFamily:'var(--font-brand)' }}>{s.v}</div>
                <div style={{ fontSize: 11.5, color:'var(--text-secondary)', marginTop: 2 }}>{s.sub}</div>
              </div>
            ))}
          </div>

          {/* View toggle + filters row */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
              Showing <b style={{ color: 'var(--text-primary)' }}>{filtered.length}</b> billers
              {cat !== 'all' && <> in <b style={{ color: 'var(--text-primary)' }}>{BILLER_CATEGORIES.find(c => c.id === cat).label}</b></>}
            </div>
            <div style={{ display:'flex', gap: 6, alignItems:'center' }}>
              <span style={{ fontSize: 11, color:'var(--text-tertiary)', marginRight: 4 }}>View</span>
              {['grid','list'].map(v => (
                <button key={v} onClick={() => setView(v)} style={{
                  background: view === v ? 'var(--surface-inset)' : 'transparent',
                  color: view === v ? 'var(--text-primary)' : 'var(--text-tertiary)',
                  border: '1px solid ' + (view === v ? 'var(--border-medium)' : 'var(--border-light)'),
                  borderRadius: 6, padding: '5px 8px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 4,
                  fontSize: 11.5, fontWeight: 500
                }}>
                  <Icon name={v} size={12}/>
                  {v[0].toUpperCase() + v.slice(1)}
                </button>
              ))}
            </div>
          </div>

          {/* Grid view */}
          {view === 'grid' && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
              {filtered.map(b => (
                <div key={b.id} className="bz-card" onClick={() => setDetail(b)} style={{
                  background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10,
                  padding: 14, display: 'flex', flexDirection: 'column', gap: 10, cursor: 'pointer',
                  opacity: b.inactive ? 0.66 : 1,
                }}>
                  <div style={{ display:'flex', alignItems:'flex-start', justifyContent:'space-between', gap: 10 }}>
                    <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                      <div style={{
                        width: 36, height: 36, borderRadius: 7, background: b.color, color: '#fff',
                        display: 'flex', alignItems: 'center', justifyContent: 'center',
                        fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: 12, flex: 'none',
                        filter: b.inactive ? 'grayscale(1)' : 'none',
                      }}>{b.sym}</div>
                      <div style={{ minWidth: 0 }}>
                        <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis' }}>{b.name}</div>
                        <div style={{ fontSize: 11, color: 'var(--text-tertiary)', display:'flex', alignItems:'center', gap:6, marginTop: 1 }}>
                          <span>{BILLER_CATEGORIES.find(c => c.id === b.cat).label}</span>
                          <span>·</span>
                          <span>{b.country}</span>
                        </div>
                      </div>
                    </div>
                    <Pill tone={b.statusTone} dot size="sm">{b.status}</Pill>
                  </div>

                  {/* Metrics */}
                  <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap: 6, padding: '10px 0', borderTop:'1px dashed var(--border-light)', borderBottom:'1px dashed var(--border-light)' }}>
                    {[['Tx / mo', b.inactive ? '—' : fmt(b.txMonth), 'var(--text-primary)'],
                      ['Success', b.inactive ? '—' : b.success + '%', b.success >= 99 ? 'var(--success)' : b.success >= 97 ? 'var(--text-primary)' : 'var(--warning)'],
                      ['p50 ETA', b.avg, 'var(--text-primary)']].map(([l, v, c]) => (
                      <div key={l}>
                        <div style={{ fontSize: 10, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight:600 }}>{l}</div>
                        <div style={{ fontFamily:'var(--font-mono)', fontSize: 13, fontWeight: 600, color: c, marginTop: 2 }}>{v}</div>
                      </div>
                    ))}
                  </div>

                  {/* Partners + fee */}
                  <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between' }}>
                    <div style={{ display:'flex', gap: 4, flexWrap:'wrap' }}>
                      {b.partners.map(p => (
                        <span key={p} style={{ fontSize: 10.5, padding:'2px 7px', background:'var(--surface-inset)', border:'1px solid var(--border-light)', borderRadius: 4, color:'var(--text-secondary)' }}>{p}</span>
                      ))}
                    </div>
                    <span style={{ fontSize: 11, color:'var(--text-tertiary)', fontFamily:'var(--font-mono)' }}>{b.fee}</span>
                  </div>

                  {/* Provenance */}
                  <div style={{ display:'flex', alignItems:'center', gap: 6, fontSize: 10.5, color:'var(--text-tertiary)', borderTop:'1px solid var(--border-light)', paddingTop: 8 }}>
                    <Icon name={b.from === 'Manual' ? 'edit' : 'download'} size={11} color={BZ_FROM_TONE[b.from]}/>
                    {b.from === 'Manual'
                      ? <span>Manual entry</span>
                      : <span>Imported · <b style={{ color: BZ_FROM_TONE[b.from] }}>{b.from}</b> · <span style={{ fontFamily:'var(--font-mono)' }}>{b.code}</span></span>}
                    {b.inactive && <><span>·</span><span style={{ color:'var(--text-tertiary)' }}>{b.dropped}</span></>}
                  </div>
                </div>
              ))}
              {/* Add card */}
              <div style={{
                border: '1.5px dashed var(--border-medium)', borderRadius: 10,
                minHeight: 168, display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
                gap: 6, color: 'var(--text-tertiary)', cursor: 'pointer'
              }} onClick={() => setImporting(true)}>
                <Icon name="download" size={18}/>
                <div style={{ fontSize: 12.5, fontWeight: 500 }}>Import from a partner</div>
                <div style={{ fontSize: 11 }}>Pull a connector's live catalogue</div>
              </div>
            </div>
          )}

          {/* List view */}
          {view === 'list' && (
            <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display:'grid', gridTemplateColumns:'1fr 120px 130px 80px 70px 110px 30px', gap: 12, padding:'10px 14px', background:'var(--surface-inset)', borderBottom:'1px solid var(--border-light)', fontSize: 10, fontWeight: 600, textTransform:'uppercase', letterSpacing:'0.06em', color:'var(--text-tertiary)' }}>
                <div>Biller</div><div>Category</div><div>Source</div><div>Tx / mo</div><div>Success</div><div>Status</div><div></div>
              </div>
              {filtered.map((b, i) => (
                <div key={b.id} className="bz-row" onClick={() => setDetail(b)} style={{ display:'grid', gridTemplateColumns:'1fr 120px 130px 80px 70px 110px 30px', gap: 12, padding:'10px 14px', alignItems:'center', borderBottom: i < filtered.length-1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, cursor:'pointer', opacity: b.inactive ? 0.66 : 1 }}>
                  <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
                    <div style={{ width: 28, height: 28, borderRadius: 5, background: b.color, color: '#fff', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--font-brand)', fontWeight: 800, fontSize: 10, flex: 'none', filter: b.inactive ? 'grayscale(1)' : 'none' }}>{b.sym}</div>
                    <div>
                      <div style={{ color:'var(--text-primary)', fontWeight: 500 }}>{b.name}</div>
                      <div style={{ fontSize: 11, color:'var(--text-tertiary)' }}>{b.country} · <span style={{ fontFamily:'var(--font-mono)' }}>{b.code}</span></div>
                    </div>
                  </div>
                  <div style={{ color:'var(--text-secondary)' }}>{BILLER_CATEGORIES.find(c => c.id === b.cat).label}</div>
                  <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
                    <Icon name={b.from === 'Manual' ? 'edit' : 'download'} size={11} color={BZ_FROM_TONE[b.from]}/>
                    <span style={{ color: b.from === 'Manual' ? 'var(--text-tertiary)' : BZ_FROM_TONE[b.from], fontWeight: 500 }}>{b.from}</span>
                  </div>
                  <div style={{ fontFamily:'var(--font-mono)', color:'var(--text-primary)' }}>{b.inactive ? '—' : fmt(b.txMonth)}</div>
                  <div style={{ fontFamily:'var(--font-mono)', color: b.inactive ? 'var(--text-tertiary)' : b.success >= 99 ? 'var(--success)' : 'var(--text-primary)' }}>{b.inactive ? '—' : b.success + '%'}</div>
                  <div><Pill tone={b.statusTone} dot size="sm">{b.status}</Pill></div>
                  <div style={{ color:'var(--text-tertiary)' }}><Icon name="chevron" size={14}/></div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {importing && <BillerImportWizard onClose={() => setImporting(false)} onImported={(s) => { setImporting(false); setFlash(s); }}/>}
      {detail && <BillerDetailDrawer biller={detail} onClose={() => setDetail(null)}/>}
    </div>
  );
}

// ═══ Biller → services detail drawer ════════════════════════════════════
function BillerDetailDrawer({ biller: b, onClose }) {
  const services = bzServices(b);
  return (
    <>
      <div onClick={onClose} style={{ position:'absolute', inset:0, background:'rgba(20,25,30,0.28)', zIndex:35 }}/>
      <div style={{ position:'absolute', top:0, right:0, bottom:0, width:520, background:'var(--surface)', borderLeft:'1px solid var(--border-light)', boxShadow:'-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex:36, display:'flex', flexDirection:'column' }}>
        {/* header */}
        <div style={{ padding:'18px 22px 16px', borderBottom:'1px solid var(--border-light)', display:'flex', alignItems:'flex-start', gap: 13 }}>
          <div style={{ width: 44, height: 44, borderRadius: 9, background: b.color, color:'#fff', display:'grid', placeItems:'center', fontFamily:'var(--font-brand)', fontWeight: 800, fontSize: 15, flex:'none', filter: b.inactive ? 'grayscale(1)' : 'none' }}>{b.sym}</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
              <span style={{ fontSize: 16, fontWeight: 700, color:'var(--text-primary)' }}>{b.name}</span>
              <Pill tone={b.statusTone} dot size="sm">{b.status}</Pill>
            </div>
            <div style={{ fontSize: 12, color:'var(--text-secondary)', marginTop: 3 }}>
              {BILLER_CATEGORIES.find(c => c.id === b.cat).label} · {b.country}
            </div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border:'1px solid var(--border-light)', background:'var(--surface)', cursor:'pointer', display:'grid', placeItems:'center' }}><Icon name="close" size={13} color="var(--text-secondary)"/></button>
        </div>

        <div style={{ flex: 1, overflow:'auto', padding: 22, display:'flex', flexDirection:'column', gap: 18 }}>
          {/* Provenance / mapping */}
          <div style={{ padding:'12px 14px', borderRadius: 10, background:'var(--surface-inset)', border:'1px solid var(--border-light)' }}>
            <div style={{ display:'flex', alignItems:'center', gap: 8, marginBottom: 8 }}>
              <Icon name={b.from === 'Manual' ? 'edit' : 'download'} size={13} color={BZ_FROM_TONE[b.from]}/>
              <span style={{ fontSize: 12.5, fontWeight: 600, color:'var(--text-primary)' }}>{b.from === 'Manual' ? 'Manually authored' : `Imported from ${b.from}`}</span>
            </div>
            {b.from !== 'Manual' && (
              <div style={{ display:'flex', gap: 22, fontSize: 11.5, color:'var(--text-secondary)' }}>
                <span>provider biller code <b style={{ fontFamily:'var(--font-mono)', color:'var(--text-primary)' }}>{b.code}</b></span>
                <span>last sync <b style={{ color:'var(--text-primary)' }}>{b.inactive ? '02 Jun (dropped)' : 'today 09:14'}</b></span>
              </div>
            )}
            {b.inactive && <div style={{ fontSize: 11.5, color:'var(--warning)', marginTop: 8 }}>This biller was no longer offered by the partner on the last import, so it was soft-deactivated. Its history and any orders are preserved.</div>}
          </div>

          {/* KPIs */}
          <div style={{ display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 8 }}>
            {[['Tx / mo', b.inactive ? '—' : Number(b.txMonth).toLocaleString('en-GB')],
              ['Success', b.inactive ? '—' : b.success + '%'],
              ['p50 ETA', b.avg], ['Fee', b.fee]].map(([l, v]) => (
              <div key={l} style={{ background:'var(--surface-inset)', borderRadius: 9, padding:'9px 11px' }}>
                <div style={{ fontSize: 9.5, fontWeight: 600, color:'var(--text-tertiary)', letterSpacing:'0.05em', textTransform:'uppercase' }}>{l}</div>
                <div style={{ fontFamily:'var(--font-mono)', fontSize: 13.5, fontWeight: 600, color:'var(--text-primary)', marginTop: 3 }}>{v}</div>
              </div>
            ))}
          </div>

          {/* Services */}
          <div>
            <div style={{ display:'flex', alignItems:'center', gap: 8, marginBottom: 10 }}>
              <span style={{ fontSize: 13, fontWeight: 600, color:'var(--text-primary)' }}>Services</span>
              <span style={{ fontFamily:'var(--font-mono)', fontSize: 11, fontWeight: 600, color:'var(--text-tertiary)', padding:'1px 7px', borderRadius: 999, background:'var(--surface-inset)' }}>{services.length}</span>
              <div style={{ flex: 1 }}/>
              <span style={{ fontSize: 11, color:'var(--text-tertiary)' }}>packages this biller offers</span>
            </div>
            <div style={{ border:'1px solid var(--border-light)', borderRadius: 10, overflow:'hidden' }}>
              <div style={{ display:'grid', gridTemplateColumns:'1fr 80px 84px 30px', gap: 10, padding:'8px 12px', background:'var(--surface-inset)', borderBottom:'1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform:'uppercase', letterSpacing:'0.05em', color:'var(--text-tertiary)' }}>
                <div>Service · field</div><div>Type</div><div style={{ textAlign:'right' }}>Amount</div><div/>
              </div>
              {services.map((s, i) => (
                <div key={i} style={{ display:'grid', gridTemplateColumns:'1fr 80px 84px 30px', gap: 10, padding:'10px 12px', alignItems:'center', borderTop: i ? '1px solid var(--border-light)' : 'none', opacity: s.active ? 1 : 0.5 }}>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontSize: 12.5, fontWeight: 500, color:'var(--text-primary)' }}>{s.name}</div>
                    <div style={{ fontSize: 10.5, color:'var(--text-tertiary)', marginTop: 1 }}>{s.field} · <span style={{ fontFamily:'var(--font-mono)' }}>{s.code}</span></div>
                  </div>
                  <span style={{ justifySelf:'start', fontSize: 9.5, fontWeight: 700, letterSpacing:'0.04em', textTransform:'uppercase', padding:'2px 7px', borderRadius: 4, fontFamily:'var(--font-mono)',
                    color: s.kind === 'Fixed' ? '#0e7490' : '#b4741e', background: (s.kind === 'Fixed' ? '#0e7490' : '#b4741e') + '18' }}>{s.kind}</span>
                  <span style={{ textAlign:'right', fontFamily:'var(--font-mono)', fontSize: 12, fontWeight: 600, color: s.amount === '—' ? 'var(--text-tertiary)' : 'var(--text-primary)' }}>{s.amount}</span>
                  <span style={{ justifySelf:'center', width: 7, height: 7, borderRadius: 999, background: s.active ? 'var(--success)' : 'var(--gray-400, #9aa3ad)' }} title={s.active ? 'Active' : 'Inactive'}/>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div style={{ flex:'none', padding:'14px 22px', borderTop:'1px solid var(--border-light)', background:'var(--surface-inset)', display:'flex', justifyContent:'flex-end', gap: 8 }}>
          <button className="btn btn-outline btn-sm">View orders</button>
          <button className="btn btn-primary btn-sm"><Icon name="edit" size={12}/> Edit biller</button>
        </div>
      </div>
    </>
  );
}

// ═══ Import-from-partner wizard ═════════════════════════════════════════
const BZ_CONNECTORS = [
  { id:'flw',  name:'Flutterwave', sub:'NG · Bill payment · v3 Bills API', sym:'FW', color:'#f59e0b', status:'Connected', tone:'success', meta:'~340 billers available' },
  { id:'pst',  name:'Paystack',    sub:'NG · Bill payment',                sym:'PS', color:'#0ea5e9', status:'Connected', tone:'success', meta:'~180 billers available' },
  { id:'sim',  name:'Simulated',   sub:'Sandbox connector · fallback',     sym:'SIM',color:'#7b76b6', status:'Sandbox',   tone:'muted',   meta:'2 fake billers' },
];

// The partner's live catalogue (what POST /import/preview returns), grouped by
// category, each entry tagged New / Mapped / Changed against what's imported.
const FLW_CATALOGUE = [
  { cat: 'Electricity', items: [
    { id:'c-ikeja', name:'Ikeja Electric',          sym:'IE', color:'#1e4d8c', code:'BIL112', status:'mapped',  svc:2 },
    { id:'c-eko',   name:'Eko Electricity',          sym:'EK', color:'#16a085', code:'BIL099', status:'changed', svc:2, note:'name + 1.20%→1.50% fee' },
    { id:'c-abuja', name:'Abuja Electricity (AEDC)', sym:'AE', color:'#0d3b66', code:'BIL110', status:'mapped',  svc:2 },
    { id:'c-kano',  name:'Kano Electricity',         sym:'KE', color:'#7a3b16', code:'BIL120', status:'new',     svc:2 },
    { id:'c-ph',    name:'Port Harcourt Electric',   sym:'PH', color:'#2c5f2d', code:'BIL121', status:'new',     svc:2 },
    { id:'c-jos',   name:'Jos Electricity (JED)',    sym:'JE', color:'#8a5a16', code:'BIL122', status:'new',     svc:2 },
    { id:'c-benin', name:'Benin Electricity (BEDC)', sym:'BE', color:'#1f6f54', code:'BIL123', status:'new',     svc:2 },
  ]},
  { cat: 'Airtime & Data', items: [
    { id:'c-mtn',   name:'MTN Nigeria',     sym:'MT', color:'#e6b800', code:'BIL099a', status:'mapped',  svc:4 },
    { id:'c-glo',   name:'Glo Mobile',      sym:'GM', color:'#26a65b', code:'BIL102',  status:'mapped',  svc:4 },
    { id:'c-airtel',name:'Airtel',          sym:'AT', color:'#cc0000', code:'BIL100',  status:'changed', svc:4, note:'2 new data bundles' },
    { id:'c-9mob',  name:'9mobile',         sym:'9M', color:'#0a7d4b', code:'BIL103',  status:'new',     svc:4 },
    { id:'c-smile', name:'Smile Internet',  sym:'SM', color:'#7b2cbf', code:'BIL130',  status:'new',     svc:2 },
  ]},
  { cat: 'Cable TV', items: [
    { id:'c-dstv',  name:'DSTV',      sym:'DS', color:'#003087', code:'BIL104', status:'changed', svc:3, note:'package price update' },
    { id:'c-gotv',  name:'GOtv',      sym:'GO', color:'#0b6e3a', code:'BIL105', status:'mapped',  svc:3 },
    { id:'c-star',  name:'StarTimes', sym:'ST', color:'#d40e1e', code:'BIL106', status:'new',     svc:3 },
    { id:'c-show',  name:'Showmax',   sym:'SX', color:'#e50914', code:'BIL131', status:'new',     svc:2 },
  ]},
  { cat: 'Internet', items: [
    { id:'c-ipnx',  name:'ipNX Fibre', sym:'IP', color:'#1f6feb', code:'BIL132', status:'new', svc:2 },
    { id:'c-spectra',name:'Spectranet',sym:'SN', color:'#5b3aaa', code:'BIL133', status:'new', svc:2 },
  ]},
  { cat: 'Toll & Transport', items: [
    { id:'c-lcc',   name:'LCC Lekki Toll', sym:'LC', color:'#0e7490', code:'BIL140', status:'new', svc:1 },
  ]},
];

const BZ_ALL = FLW_CATALOGUE.flatMap(g => g.items.map(it => ({ ...it, cat: g.cat })));
const BZ_STATUS = {
  new:     { label: 'New',     fg: 'var(--brand-primary)', bg: 'var(--brand-primary-10)' },
  changed: { label: 'Changed', fg: '#b4741e',              bg: '#b4741e18' },
  mapped:  { label: 'Mapped',  fg: 'var(--text-tertiary)', bg: 'var(--surface-inset)' },
};

function BzChip({ status }) {
  const s = BZ_STATUS[status];
  return <span style={{ fontSize: 9.5, fontWeight: 700, letterSpacing:'0.05em', textTransform:'uppercase', padding:'2px 7px', borderRadius: 4, color: s.fg, background: s.bg, fontFamily:'var(--font-mono)' }}>{s.label}</span>;
}

function BzStepDots({ step }) {
  const steps = ['Source', 'Preview', 'Confirm'];
  return (
    <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
      {steps.map((label, i) => {
        const n = i + 1, active = n === step, done = n < step;
        return (
          <React.Fragment key={label}>
            <div style={{ display:'flex', alignItems:'center', gap: 7 }}>
              <span style={{ width: 20, height: 20, borderRadius: 999, display:'grid', placeItems:'center', fontSize: 10.5, fontWeight: 700,
                background: active ? 'var(--brand-primary)' : done ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
                color: active ? '#fff' : done ? 'var(--brand-primary)' : 'var(--text-tertiary)',
                border: active ? 'none' : '1px solid var(--border-light)' }}>
                {done ? <Icon name="check" size={11} color="var(--brand-primary)"/> : n}
              </span>
              <span style={{ fontSize: 12, fontWeight: active ? 600 : 500, color: active ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{label}</span>
            </div>
            {n < 3 && <div style={{ width: 22, height: 1, background:'var(--border-light)' }}/>}
          </React.Fragment>
        );
      })}
    </div>
  );
}

function BillerImportWizard({ onClose, onImported }) {
  const [step, setStep] = React.useState(1);
  const [connector, setConnector] = React.useState('flw');
  const [sel, setSel] = React.useState(() => new Set(BZ_ALL.filter(i => i.status !== 'mapped').map(i => i.id)));
  const [catFilter, setCatFilter] = React.useState('all');
  const [open, setOpen] = React.useState({});           // expanded category groups
  const [done, setDone] = React.useState(false);

  const conn = BZ_CONNECTORS.find(c => c.id === connector);
  const counts = {
    avail: BZ_ALL.length,
    new: BZ_ALL.filter(i => i.status === 'new').length,
    changed: BZ_ALL.filter(i => i.status === 'changed').length,
    mapped: BZ_ALL.filter(i => i.status === 'mapped').length,
  };
  const selItems = BZ_ALL.filter(i => sel.has(i.id));
  const selNew = selItems.filter(i => i.status === 'new');
  const selChanged = selItems.filter(i => i.status === 'changed');
  const selMapped = selItems.filter(i => i.status === 'mapped');
  const summary = {
    created: selNew.length,
    updated: selChanged.length + selMapped.length,
    servicesCreated: selNew.reduce((a, i) => a + i.svc, 0),
    servicesUpdated: [...selChanged, ...selMapped].reduce((a, i) => a + i.svc, 0),
    deactivated: 1,
  };

  const toggle = (id) => setSel(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n; });
  const selectAllNew = () => setSel(prev => { const n = new Set(prev); BZ_ALL.filter(i => i.status === 'new').forEach(i => n.add(i.id)); return n; });
  const groups = catFilter === 'all' ? FLW_CATALOGUE : FLW_CATALOGUE.filter(g => g.cat === catFilter);

  return (
    <div onClick={onClose} style={{ position:'absolute', inset:0, zIndex:40, background:'rgba(20,25,30,0.34)', display:'flex', alignItems:'center', justifyContent:'center', padding: 28 }}>
      <div onClick={e => e.stopPropagation()} style={{ width:'min(880px, 94%)', maxHeight:'90%', background:'var(--surface)', borderRadius: 16, boxShadow:'0 28px 64px -22px rgba(0,0,0,0.45)', display:'flex', flexDirection:'column', overflow:'hidden' }}>
        {/* Header */}
        <div style={{ padding:'18px 22px 14px', borderBottom:'1px solid var(--border-light)', display:'flex', alignItems:'center', gap: 14 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 16, fontWeight: 700, color:'var(--text-primary)' }}>Import billers from a partner</div>
            <div style={{ fontSize: 12, color:'var(--text-secondary)', marginTop: 2 }}>Pull a connector's live catalogue · idempotent upsert · no money moves</div>
          </div>
          {!done && <BzStepDots step={step}/>}
          <button onClick={onClose} style={{ width: 28, height: 28, borderRadius: 6, border:'1px solid var(--border-light)', background:'var(--surface)', cursor:'pointer', display:'grid', placeItems:'center', flex:'none' }}><Icon name="close" size={14} color="var(--text-secondary)"/></button>
        </div>

        {/* Preview toolbar */}
        {step === 2 && !done && (
          <div style={{ padding:'10px 22px', borderBottom:'1px solid var(--border-light)', background:'var(--surface-inset)', display:'flex', alignItems:'center', gap: 10, flexWrap:'wrap' }}>
            <span style={{ display:'inline-flex', alignItems:'center', gap: 6, fontSize: 11.5, color:'var(--text-secondary)' }}>
              <span style={{ width: 7, height: 7, borderRadius: 999, background:'var(--success)' }}/> Live · {conn.name}
            </span>
            <div style={{ display:'flex', gap: 4, flexWrap:'wrap' }}>
              {['all', ...FLW_CATALOGUE.map(g => g.cat)].map(c => (
                <button key={c} onClick={() => setCatFilter(c)} style={{
                  fontSize: 11, padding:'4px 9px', borderRadius: 999, cursor:'pointer',
                  border:'1px solid ' + (catFilter === c ? 'var(--brand-primary)' : 'var(--border-light)'),
                  background: catFilter === c ? 'var(--brand-primary-10)' : 'var(--surface)',
                  color: catFilter === c ? 'var(--brand-primary)' : 'var(--text-secondary)', fontWeight: catFilter === c ? 600 : 500,
                }}>{c === 'all' ? 'All' : c}</button>
              ))}
            </div>
            <div style={{ flex: 1 }}/>
            <button onClick={selectAllNew} className="btn btn-ghost btn-sm" style={{ fontSize: 11.5 }}><Icon name="check" size={11}/> Select all new</button>
          </div>
        )}

        {/* Body */}
        <div style={{ flex: 1, overflow:'auto', padding: 22 }}>
          {done ? (
            <BzResult summary={summary} conn={conn}/>
          ) : step === 1 ? (
            <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
              <div style={{ fontSize: 12.5, color:'var(--text-secondary)', marginBottom: 2 }}>Choose a configured partner connector to import from. Catalogues are NG-only for bill payment.</div>
              {BZ_CONNECTORS.map(c => {
                const on = connector === c.id;
                return (
                  <div key={c.id} onClick={() => setConnector(c.id)} style={{
                    display:'flex', alignItems:'center', gap: 13, padding: 14, borderRadius: 12, cursor:'pointer',
                    border:'1px solid ' + (on ? 'var(--brand-primary)' : 'var(--border-light)'),
                    background: on ? 'var(--brand-primary-10)' : 'var(--surface)',
                    boxShadow: on ? '0 0 0 1px var(--brand-primary)' : 'none',
                  }}>
                    <div style={{ width: 40, height: 40, borderRadius: 9, background: c.color, color:'#fff', display:'grid', placeItems:'center', fontFamily:'var(--font-brand)', fontWeight: 800, fontSize: 12, flex:'none' }}>{c.sym}</div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
                        <span style={{ fontSize: 14, fontWeight: 600, color:'var(--text-primary)' }}>{c.name}</span>
                        <Pill tone={c.tone} dot size="sm">{c.status}</Pill>
                      </div>
                      <div style={{ fontSize: 12, color:'var(--text-secondary)', marginTop: 2 }}>{c.sub}</div>
                    </div>
                    <div style={{ textAlign:'right' }}>
                      <div style={{ fontSize: 11.5, color:'var(--text-tertiary)' }}>{c.meta}</div>
                    </div>
                    <span style={{ width: 18, height: 18, borderRadius: 999, border:'2px solid ' + (on ? 'var(--brand-primary)' : 'var(--border-medium)'), display:'grid', placeItems:'center', flex:'none' }}>
                      {on && <span style={{ width: 8, height: 8, borderRadius: 999, background:'var(--brand-primary)' }}/>}
                    </span>
                  </div>
                );
              })}
            </div>
          ) : step === 2 ? (
            <div style={{ display:'flex', flexDirection:'column', gap: 16 }}>
              {groups.map(g => {
                const opened = open[g.cat] !== false;  // default open
                const groupNew = g.items.filter(i => i.status === 'new').length;
                return (
                  <div key={g.cat}>
                    <div onClick={() => setOpen(o => ({ ...o, [g.cat]: !opened }))} style={{ display:'flex', alignItems:'center', gap: 8, padding:'4px 2px', cursor:'pointer', marginBottom: 6 }}>
                      <Icon name={opened ? 'chevdown' : 'chevron'} size={13} color="var(--text-tertiary)"/>
                      <span style={{ fontSize: 12.5, fontWeight: 700, color:'var(--text-primary)' }}>{g.cat}</span>
                      <span style={{ fontFamily:'var(--font-mono)', fontSize: 11, color:'var(--text-tertiary)' }}>{g.items.length}</span>
                      {groupNew > 0 && <span style={{ fontSize: 10, color:'var(--brand-primary)', fontWeight: 600 }}>· {groupNew} new</span>}
                    </div>
                    {opened && (
                      <div style={{ border:'1px solid var(--border-light)', borderRadius: 10, overflow:'hidden' }}>
                        {g.items.map((it, i) => {
                          const checked = sel.has(it.id);
                          return (
                            <div key={it.id} onClick={() => toggle(it.id)} style={{
                              display:'grid', gridTemplateColumns:'22px 30px 1fr auto auto', gap: 11, alignItems:'center',
                              padding:'10px 13px', cursor:'pointer', borderTop: i ? '1px solid var(--border-light)' : 'none',
                              background: checked ? 'var(--brand-primary-10)' : 'transparent',
                            }}>
                              <span style={{ width: 17, height: 17, borderRadius: 5, display:'grid', placeItems:'center', flex:'none',
                                border:'1.5px solid ' + (checked ? 'var(--brand-primary)' : 'var(--border-medium)'),
                                background: checked ? 'var(--brand-primary)' : 'var(--surface)' }}>
                                {checked && <Icon name="check" size={11} color="#fff"/>}
                              </span>
                              <div style={{ width: 30, height: 30, borderRadius: 6, background: it.color, color:'#fff', display:'grid', placeItems:'center', fontFamily:'var(--font-brand)', fontWeight: 800, fontSize: 10.5, flex:'none' }}>{it.sym}</div>
                              <div style={{ minWidth: 0 }}>
                                <div style={{ fontSize: 13, fontWeight: 500, color:'var(--text-primary)' }}>{it.name}</div>
                                <div style={{ fontSize: 10.5, color:'var(--text-tertiary)' }}>
                                  <span style={{ fontFamily:'var(--font-mono)' }}>{it.code}</span> · {it.svc} service{it.svc > 1 ? 's' : ''}
                                  {it.note && <span style={{ color:'#b4741e' }}> · {it.note}</span>}
                                </div>
                              </div>
                              <BzChip status={it.status}/>
                              <Icon name="chevron" size={12} color="var(--text-tertiary)"/>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            /* step 3 — confirm */
            <div style={{ display:'flex', flexDirection:'column', gap: 16 }}>
              <div style={{ fontSize: 13.5, fontWeight: 600, color:'var(--text-primary)' }}>Review import</div>
              <div style={{ display:'flex', alignItems:'center', gap: 12, padding: 16, borderRadius: 12, background:'var(--surface-inset)', border:'1px solid var(--border-light)' }}>
                <div style={{ width: 40, height: 40, borderRadius: 9, background: conn.color, color:'#fff', display:'grid', placeItems:'center', fontFamily:'var(--font-brand)', fontWeight: 800, fontSize: 12, flex:'none' }}>{conn.sym}</div>
                <Icon name="arrowright" size={16} color="var(--text-tertiary)"/>
                <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
                  <AonikMark size={26}/>
                  <span style={{ fontSize: 13, fontWeight: 600, color:'var(--text-primary)' }}>Aonik catalog</span>
                </div>
                <div style={{ flex: 1 }}/>
                <span style={{ fontSize: 12, color:'var(--text-secondary)' }}><b style={{ fontFamily:'var(--font-mono)', color:'var(--text-primary)' }}>{selItems.length}</b> selected</span>
              </div>

              <div style={{ display:'grid', gridTemplateColumns:'repeat(3, 1fr)', gap: 10 }}>
                {[['Billers created', summary.created, 'var(--brand-primary)'],
                  ['Billers updated', summary.updated, 'var(--text-primary)'],
                  ['Deactivated',     summary.deactivated, 'var(--warning)'],
                  ['Services created', summary.servicesCreated, 'var(--brand-primary)'],
                  ['Services updated', summary.servicesUpdated, 'var(--text-primary)'],
                  ['Duplicates',       0, 'var(--text-tertiary)']].map(([l, v, c]) => (
                  <div key={l} style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding:'12px 14px' }}>
                    <div style={{ fontSize: 10.5, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight: 600 }}>{l}</div>
                    <div style={{ fontFamily:'var(--font-mono)', fontSize: 22, fontWeight: 700, color: c, marginTop: 3 }}>{v}</div>
                  </div>
                ))}
              </div>

              <div style={{ display:'flex', gap: 9, padding:'11px 14px', borderRadius: '0 10px 10px 0', background:'var(--brand-primary-10)', borderLeft:'3px solid var(--brand-primary)' }}>
                <Icon name="refresh" size={14} color="var(--brand-primary)" style={{ marginTop: 1, flex:'none' }}/>
                <div style={{ fontSize: 11.5, color:'var(--text-secondary)', lineHeight: 1.5 }}>
                  Identity is the provider mapping, so this is <b style={{ color:'var(--text-primary)' }}>idempotent</b> — running it again would report <span style={{ fontFamily:'var(--font-mono)' }}>0 created</span>. The {summary.deactivated} deactivation is a biller Flutterwave no longer offers; it's kept (soft-deactivated), never deleted.
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div style={{ flex:'none', padding:'14px 22px', borderTop:'1px solid var(--border-light)', background:'var(--surface-inset)', display:'flex', alignItems:'center', justifyContent:'space-between', gap: 12 }}>
          <div style={{ fontSize: 12, color:'var(--text-secondary)' }}>
            {done ? <span>Catalogue refreshed.</span>
              : step === 1 ? <span>{conn.name} · {conn.status}</span>
              : step === 2 ? <span><b style={{ fontFamily:'var(--font-mono)', color:'var(--text-primary)' }}>{selItems.length}</b> selected · {selNew.length} new · {selChanged.length} changed</span>
              : <span>Catalog.Write · medium-risk reference-data write</span>}
          </div>
          <div style={{ display:'flex', gap: 8 }}>
            {done ? (
              <button className="btn btn-primary btn-sm" onClick={() => onImported(summary)}>Done</button>
            ) : (
              <>
                {step > 1 && <button className="btn btn-outline btn-sm" onClick={() => setStep(step - 1)}>Back</button>}
                <button className="btn btn-ghost btn-sm" onClick={onClose}>Cancel</button>
                {step < 3
                  ? <button className="btn btn-primary btn-sm" onClick={() => setStep(step + 1)} disabled={step === 2 && selItems.length === 0}>
                      {step === 1 ? 'Preview catalogue' : `Review ${selItems.length}`} <Icon name="arrowright" size={12}/>
                    </button>
                  : <button className="btn btn-primary btn-sm" onClick={() => setDone(true)}><Icon name="download" size={12}/> Import {selItems.length} billers</button>}
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function BzResult({ summary, conn }) {
  return (
    <div style={{ display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center', padding:'18px 0 8px', gap: 14 }}>
      <span style={{ width: 52, height: 52, borderRadius: 999, background:'var(--success)', color:'#fff', display:'grid', placeItems:'center' }}><Icon name="check" size={26} color="#fff"/></span>
      <div>
        <div style={{ fontSize: 18, fontWeight: 700, color:'var(--text-primary)' }}>Import complete</div>
        <div style={{ fontSize: 13, color:'var(--text-secondary)', marginTop: 4 }}>Billers from {conn.name} are now in your catalogue, each routed through its connector mapping.</div>
      </div>
      <div style={{ display:'flex', gap: 22, padding:'14px 22px', borderRadius: 12, background:'var(--surface-inset)', border:'1px solid var(--border-light)', fontFamily:'var(--font-mono)' }}>
        {[['created', summary.created, 'var(--success)'], ['updated', summary.updated, 'var(--text-primary)'], ['duplicates', 0, 'var(--text-tertiary)'], ['deactivated', summary.deactivated, 'var(--warning)']].map(([l, v, c]) => (
          <div key={l} style={{ textAlign:'center' }}>
            <div style={{ fontSize: 24, fontWeight: 700, color: c }}>{v}</div>
            <div style={{ fontSize: 10.5, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontFamily:'var(--font-sans)', marginTop: 2 }}>{l}</div>
          </div>
        ))}
      </div>
      <div style={{ fontSize: 11.5, color:'var(--text-tertiary)', maxWidth: 440, lineHeight: 1.5 }}>
        Re-running this import is safe — it would refresh changed rows and create nothing new.
      </div>
    </div>
  );
}

Object.assign(window, { ScreenBillers });
