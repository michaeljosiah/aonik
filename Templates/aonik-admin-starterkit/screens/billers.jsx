// Bill Payments — Billers catalog (the "noun" layer for Bill Pay)
// Catalog of provider integrations + per-biller routing/policy config.

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
  { id:'ekedc',   name:'Ikeja Electric',   sym:'IE', color:'#1e4d8c', cat:'utilities', country:'NG', txMonth: 4128, success: 99.4, partners:['Flutterwave','Paystack'], statusTone:'success', status:'Active',     fee:'1.20%', avg:'12s' },
  { id:'aedc',    name:'Abuja Electric',   sym:'AE', color:'#0d3b66', cat:'utilities', country:'NG', txMonth: 1840, success: 98.9, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'1.20%', avg:'14s' },
  { id:'ekedp',   name:'Eko Electric',     sym:'EK', color:'#16a085', cat:'utilities', country:'NG', txMonth: 2204, success: 97.8, partners:['Paystack','Squad'],       statusTone:'warning', status:'Degraded',   fee:'1.50%', avg:'28s' },
  { id:'lawma',   name:'LAWMA Waste',      sym:'LW', color:'#2c7a3f', cat:'utilities', country:'NG', txMonth:  612, success: 96.5, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'1.50%', avg:'18s' },
  // telco
  { id:'mtn',     name:'MTN Nigeria',      sym:'MT', color:'#e6b800', cat:'telco',     country:'NG', txMonth: 8302, success: 99.9, partners:['Flutterwave','Paystack','Direct'], statusTone:'success', status:'Active',     fee:'0.80%', avg:'4s' },
  { id:'glo',     name:'Glo Mobile',       sym:'GM', color:'#26a65b', cat:'telco',     country:'NG', txMonth: 3104, success: 99.8, partners:['Flutterwave','Paystack'],          statusTone:'success', status:'Active',     fee:'0.80%', avg:'5s' },
  { id:'airtel',  name:'Airtel',           sym:'AT', color:'#cc0000', cat:'telco',     country:'NG', txMonth: 4210, success: 99.7, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'0.80%', avg:'5s' },
  { id:'spectra', name:'Spectranet',       sym:'SN', color:'#5b3aaa', cat:'telco',     country:'NG', txMonth:  584, success: 98.2, partners:['Paystack'],               statusTone:'success', status:'Active',     fee:'1.00%', avg:'9s' },
  // tv
  { id:'dstv',    name:'DSTV',             sym:'DS', color:'#003087', cat:'tv',        country:'ZA', txMonth: 1820, success: 99.1, partners:['Flutterwave','Paystack'],          statusTone:'success', status:'Active',     fee:'1.00%', avg:'7s' },
  { id:'gotv',    name:'GOtv',             sym:'GO', color:'#0b6e3a', cat:'tv',        country:'ZA', txMonth:  912, success: 98.7, partners:['Flutterwave'],            statusTone:'success', status:'Active',     fee:'1.00%', avg:'8s' },
  { id:'startime',name:'StarTimes',        sym:'ST', color:'#d40e1e', cat:'tv',        country:'NG', txMonth:  402, success: 96.0, partners:['Paystack'],               statusTone:'pending', status:'Maintenance',fee:'1.20%', avg:'—' },
  // tax
  { id:'firs',    name:'FIRS Federal Tax', sym:'FT', color:'#1f3a5f', cat:'tax',       country:'NG', txMonth:  142, success: 99.5, partners:['Direct'],                  statusTone:'success', status:'Active',     fee:'flat ₦200', avg:'31s' },
  { id:'lirs',    name:'LIRS Lagos Tax',   sym:'LR', color:'#1b6e3f', cat:'tax',       country:'NG', txMonth:  308, success: 99.2, partners:['Direct','Flutterwave'],   statusTone:'success', status:'Active',     fee:'flat ₦200', avg:'24s' },
  { id:'cac',     name:'CAC Filings',      sym:'CC', color:'#3a3a3a', cat:'tax',       country:'NG', txMonth:   84, success: 97.3, partners:['Direct'],                  statusTone:'success', status:'Active',     fee:'flat ₦500', avg:'42s' },
  // subs
  { id:'netflix', name:'Netflix',          sym:'NF', color:'#e50914', cat:'subs',      country:'US', txMonth:  204, success: 99.6, partners:['Stripe'],                  statusTone:'success', status:'Active',     fee:'2.90%', avg:'3s' },
  { id:'spotify', name:'Spotify Premium',  sym:'SP', color:'#1ed760', cat:'subs',      country:'SE', txMonth:  158, success: 99.4, partners:['Stripe'],                  statusTone:'success', status:'Active',     fee:'2.90%', avg:'3s' },
];

function ScreenBillers() {
  const [cat, setCat] = React.useState('all');
  const [view, setView] = React.useState('grid');
  const filtered = cat === 'all' ? BILLERS : BILLERS.filter(b => b.cat === cat);

  const fmt = n => Number(n).toLocaleString('en-GB');

  return (
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
            fontSize: 12.5, fontWeight: cat === c.id ? 600 : 500,
            textAlign: 'left',
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
              <Icon name="search" size={13} color="var(--text-tertiary)"/>
              <input placeholder="Search billers" style={{
                background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius:6,
                padding:'7px 10px 7px 28px', fontSize:12.5, color:'var(--text-primary)', width: 220,
                fontFamily:'var(--font-sans)'
              }}/>
              <span style={{ position:'absolute', left: 9, top: 8 }}><Icon name="search" size={13} color="var(--text-tertiary)"/></span>
            </div>
            <button className="btn btn-sm"><Icon name="upload" size={12}/> Import</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add biller</button>
          </div>
        </div>

        {/* Stats strip */}
        <div style={{ display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 12 }}>
          {[
            { label:'Active billers',   v:'138',     sub:'4 in maintenance' },
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
              <div key={b.id} style={{
                background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10,
                padding: 14, display: 'flex', flexDirection: 'column', gap: 10,
              }}>
                {/* Header row */}
                <div style={{ display:'flex', alignItems:'flex-start', justifyContent:'space-between', gap: 10 }}>
                  <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                    <div style={{
                      width: 36, height: 36, borderRadius: 7, background: b.color, color: '#fff',
                      display: 'flex', alignItems: 'center', justifyContent: 'center',
                      fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: 12, flex: 'none',
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
                  <div>
                    <div style={{ fontSize: 10, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight:600 }}>Tx / mo</div>
                    <div style={{ fontFamily:'var(--font-mono)', fontSize: 13, fontWeight: 600, color:'var(--text-primary)', marginTop: 2 }}>{fmt(b.txMonth)}</div>
                  </div>
                  <div>
                    <div style={{ fontSize: 10, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight:600 }}>Success</div>
                    <div style={{ fontFamily:'var(--font-mono)', fontSize: 13, fontWeight: 600, color: b.success >= 99 ? 'var(--success)' : b.success >= 97 ? 'var(--text-primary)' : 'var(--warning)', marginTop: 2 }}>{b.success}%</div>
                  </div>
                  <div>
                    <div style={{ fontSize: 10, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight:600 }}>p50 ETA</div>
                    <div style={{ fontFamily:'var(--font-mono)', fontSize: 13, fontWeight: 600, color:'var(--text-primary)', marginTop: 2 }}>{b.avg}</div>
                  </div>
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
              </div>
            ))}
            {/* Add card */}
            <div style={{
              border: '1.5px dashed var(--border-medium)', borderRadius: 10,
              minHeight: 168, display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
              gap: 6, color: 'var(--text-tertiary)', cursor: 'pointer'
            }}>
              <Icon name="plus" size={18}/>
              <div style={{ fontSize: 12.5, fontWeight: 500 }}>New biller integration</div>
              <div style={{ fontSize: 11 }}>Connect a partner · Configure routing</div>
            </div>
          </div>
        )}

        {/* List view */}
        {view === 'list' && (
          <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
            <div style={{ display:'grid', gridTemplateColumns:'1fr 130px 100px 90px 70px 100px 110px 30px', gap: 12, padding:'10px 14px', background:'var(--surface-inset)', borderBottom:'1px solid var(--border-light)', fontSize: 10, fontWeight: 600, textTransform:'uppercase', letterSpacing:'0.06em', color:'var(--text-tertiary)' }}>
              <div>Biller</div><div>Category</div><div>Partners</div><div>Tx / mo</div><div>Success</div><div>Avg ETA</div><div>Status</div><div></div>
            </div>
            {filtered.map((b, i) => (
              <div key={b.id} style={{ display:'grid', gridTemplateColumns:'1fr 130px 100px 90px 70px 100px 110px 30px', gap: 12, padding:'10px 14px', alignItems:'center', borderBottom: i < filtered.length-1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
                  <div style={{ width: 28, height: 28, borderRadius: 5, background: b.color, color: '#fff', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--font-brand)', fontWeight: 800, fontSize: 10, flex: 'none' }}>{b.sym}</div>
                  <div>
                    <div style={{ color:'var(--text-primary)', fontWeight: 500 }}>{b.name}</div>
                    <div style={{ fontSize: 11, color:'var(--text-tertiary)' }}>{b.country}</div>
                  </div>
                </div>
                <div style={{ color:'var(--text-secondary)' }}>{BILLER_CATEGORIES.find(c => c.id === b.cat).label}</div>
                <div style={{ display:'flex', gap: 3 }}>
                  {b.partners.slice(0,3).map((p,j) => (
                    <span key={j} style={{ width: 18, height: 18, borderRadius: 4, background: 'var(--surface-inset)', border:'1px solid var(--border-light)', display:'flex', alignItems:'center', justifyContent:'center', fontSize: 9, fontWeight: 700, color:'var(--text-secondary)' }} title={p}>{p[0]}</span>
                  ))}
                </div>
                <div style={{ fontFamily:'var(--font-mono)', color:'var(--text-primary)' }}>{fmt(b.txMonth)}</div>
                <div style={{ fontFamily:'var(--font-mono)', color: b.success >= 99 ? 'var(--success)' : 'var(--text-primary)' }}>{b.success}%</div>
                <div style={{ fontFamily:'var(--font-mono)', color:'var(--text-secondary)' }}>{b.avg}</div>
                <div><Pill tone={b.statusTone} dot size="sm">{b.status}</Pill></div>
                <div style={{ color:'var(--text-tertiary)' }}><Icon name="more" size={14}/></div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

Object.assign(window, { ScreenBillers });
