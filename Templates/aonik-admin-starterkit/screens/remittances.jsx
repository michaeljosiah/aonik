// Remittances — Corridors catalog + FX & Rates management
// "Corridor" = a configured GBP→NGN-style cross-border lane: which partners
// can carry it, what fees apply, what FX margin we hold, what compliance lives
// on it. Orders with money-transfer items consume corridors.

const CORRIDORS = [
  { id:'gbp-ngn', from:'GBP', to:'NGN', fromFlag:'🇬🇧', toFlag:'🇳🇬', fromName:'United Kingdom', toName:'Nigeria',
    rate: 2014.20, change:+0.42, weeklyVol:'£1.84M', orders: 312, success: 99.2, eta:'30s – 4 min',
    partners: [
      { name:'Flutterwave',  primary:true,  share: 64, fee:'0.4%', spread:'25 bps' },
      { name:'Wise',         primary:false, share: 24, fee:'0.5%', spread:'15 bps' },
      { name:'TransferGo',   primary:false, share: 12, fee:'0.3%', spread:'30 bps' },
    ],
    statusTone:'success', status:'Active',
  },
  { id:'usd-ngn', from:'USD', to:'NGN', fromFlag:'🇺🇸', toFlag:'🇳🇬', fromName:'United States', toName:'Nigeria',
    rate: 1602.80, change:-0.18, weeklyVol:'$2.10M', orders: 298, success: 98.8, eta:'45s – 6 min',
    partners: [
      { name:'Flutterwave',  primary:true,  share: 58, fee:'0.5%', spread:'30 bps' },
      { name:'Onafriq',      primary:false, share: 32, fee:'0.4%', spread:'25 bps' },
      { name:'Crown Agents', primary:false, share: 10, fee:'0.6%', spread:'20 bps' },
    ],
    statusTone:'success', status:'Active',
  },
  { id:'gbp-ghs', from:'GBP', to:'GHS', fromFlag:'🇬🇧', toFlag:'🇬🇭', fromName:'United Kingdom', toName:'Ghana',
    rate: 18.42, change:+0.08, weeklyVol:'£420K', orders: 84, success: 97.4, eta:'1 – 8 min',
    partners: [
      { name:'Flutterwave', primary:true,  share: 80, fee:'0.6%', spread:'45 bps' },
      { name:'Onafriq',     primary:false, share: 20, fee:'0.7%', spread:'40 bps' },
    ],
    statusTone:'warning', status:'Degraded · partner outage',
  },
  { id:'usd-kes', from:'USD', to:'KES', fromFlag:'🇺🇸', toFlag:'🇰🇪', fromName:'United States', toName:'Kenya',
    rate: 129.40, change:+0.22, weeklyVol:'$680K', orders: 142, success: 99.0, eta:'30s – 3 min',
    partners: [
      { name:'Onafriq',  primary:true,  share: 70, fee:'0.4%', spread:'20 bps' },
      { name:'Cellulant',primary:false, share: 30, fee:'0.5%', spread:'25 bps' },
    ],
    statusTone:'success', status:'Active',
  },
  { id:'eur-ngn', from:'EUR', to:'NGN', fromFlag:'🇪🇺', toFlag:'🇳🇬', fromName:'Eurozone', toName:'Nigeria',
    rate: 1740.10, change:+0.31, weeklyVol:'€520K', orders: 96, success: 98.4, eta:'1 – 5 min',
    partners: [
      { name:'Wise',        primary:true,  share: 55, fee:'0.4%', spread:'20 bps' },
      { name:'Flutterwave', primary:false, share: 45, fee:'0.5%', spread:'30 bps' },
    ],
    statusTone:'success', status:'Active',
  },
  { id:'gbp-zar', from:'GBP', to:'ZAR', fromFlag:'🇬🇧', toFlag:'🇿🇦', fromName:'United Kingdom', toName:'South Africa',
    rate: 23.18, change:-0.04, weeklyVol:'£212K', orders: 48, success: 99.4, eta:'45s – 4 min',
    partners: [
      { name:'Wise',     primary:true,  share: 65, fee:'0.5%', spread:'25 bps' },
      { name:'Onafriq',  primary:false, share: 35, fee:'0.6%', spread:'30 bps' },
    ],
    statusTone:'success', status:'Active',
  },
];

// ── Tiny CCY badge ────────────────────────────────────────────────────────
function CcyChip({ ccy, flag }) {
  return (
    <span style={{ display:'inline-flex', alignItems:'center', gap: 6, fontFamily:'var(--font-brand)', fontWeight: 700, fontSize: 13, color:'var(--text-primary)', padding:'3px 8px 3px 4px', background:'var(--surface-inset)', border:'1px solid var(--border-light)', borderRadius: 5 }}>
      <span style={{ fontSize: 13 }}>{flag}</span>
      {ccy}
    </span>
  );
}

// ── Corridor list ─────────────────────────────────────────────────────────
function ScreenCorridors() {
  return (
    <div style={{ padding:'22px 28px', height:'100%', overflow:'auto', display:'flex', flexDirection:'column', gap: 18 }}>
      {/* Header */}
      <div style={{ display:'flex', alignItems:'flex-end', justifyContent:'space-between' }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color:'var(--text-primary)', letterSpacing:'-0.01em' }}>Corridors</div>
          <div style={{ fontSize: 13, color:'var(--text-secondary)', marginTop: 2, maxWidth: 640 }}>
            Cross-border lanes with partner ranking, fees, FX margin and compliance scope. Orders containing a money transfer pick the best partner from the ranked list.
          </div>
        </div>
        <div style={{ display:'flex', gap: 8 }}>
          <button className="btn btn-sm"><Icon name="globe2" size={12}/> Coverage map</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New corridor</button>
        </div>
      </div>

      {/* Stats */}
      <div style={{ display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 12 }}>
        {[
          { l:'Active corridors',  v:'14',     s:'across 9 countries' },
          { l:'Volume this week',  v:'£4.92M', s:'+8.2% vs last week' },
          { l:'Avg success',       v:'98.7%',  s:'p95 ETA · 4m 18s' },
          { l:'FX margin captured',v:'£14.2K', s:'27 bps avg blended' },
        ].map(s => (
          <div key={s.l} style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding:'14px 16px' }}>
            <div style={{ fontSize: 11, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight: 600 }}>{s.l}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color:'var(--text-primary)', marginTop: 4, fontFamily:'var(--font-brand)' }}>{s.v}</div>
            <div style={{ fontSize: 11.5, color:'var(--text-secondary)', marginTop: 2 }}>{s.s}</div>
          </div>
        ))}
      </div>

      {/* Corridor cards */}
      <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 12 }}>
        {CORRIDORS.map(c => (
          <div key={c.id} style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 12, padding: 16, display:'flex', flexDirection:'column', gap: 12 }}>
            {/* Pair header */}
            <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between' }}>
              <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
                <CcyChip ccy={c.from} flag={c.fromFlag}/>
                <Icon name="arrowright" size={14} color="var(--text-tertiary)"/>
                <CcyChip ccy={c.to} flag={c.toFlag}/>
                <span style={{ fontSize: 11, color:'var(--text-tertiary)', marginLeft: 4 }}>{c.fromName} → {c.toName}</span>
              </div>
              <Pill tone={c.statusTone} dot size="sm">{c.status}</Pill>
            </div>

            {/* Rate strip */}
            <div style={{ display:'flex', alignItems:'baseline', gap: 10, padding:'10px 12px', background:'var(--surface-inset)', borderRadius: 8 }}>
              <span style={{ fontSize: 11, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight:600 }}>Live rate</span>
              <span style={{ fontFamily:'var(--font-mono)', fontSize: 18, fontWeight: 700, color:'var(--text-primary)' }}>
                1 {c.from} = {c.rate.toLocaleString('en-GB', { minimumFractionDigits: 2 })} {c.to}
              </span>
              <span style={{ fontSize: 12, fontWeight: 600, color: c.change >= 0 ? 'var(--success)' : 'var(--danger)', display:'inline-flex', alignItems:'center', gap: 2 }}>
                <Icon name={c.change >= 0 ? 'arrowup' : 'arrowdown'} size={11}/>
                {c.change >= 0 ? '+' : ''}{c.change}%
              </span>
            </div>

            {/* Metrics row */}
            <div style={{ display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 8 }}>
              {[
                { l:'Volume / week', v: c.weeklyVol },
                { l:'Orders',        v: c.orders.toLocaleString() },
                { l:'Success',       v: c.success + '%' },
                { l:'ETA',           v: c.eta },
              ].map(m => (
                <div key={m.l}>
                  <div style={{ fontSize: 10, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.05em', fontWeight:600 }}>{m.l}</div>
                  <div style={{ fontFamily:'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color:'var(--text-primary)', marginTop: 2 }}>{m.v}</div>
                </div>
              ))}
            </div>

            {/* Partner ranking */}
            <div>
              <div style={{ fontSize: 10.5, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.06em', fontWeight: 600, marginBottom: 6 }}>Partner ranking · routing</div>
              <div style={{ display:'flex', flexDirection:'column', gap: 5 }}>
                {c.partners.map((p, i) => (
                  <div key={p.name} style={{ display:'flex', alignItems:'center', gap: 8, padding:'6px 8px', background:'var(--surface-inset)', borderRadius: 6 }}>
                    <span style={{ width: 18, height: 18, borderRadius: 4, background: i === 0 ? 'var(--brand-primary)' : 'var(--border-medium)', color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontSize: 10, fontWeight: 700, flex:'none' }}>{i+1}</span>
                    <div style={{ flex: 1, fontSize: 12, fontWeight: 500, color:'var(--text-primary)' }}>
                      {p.name}
                      {p.primary && <span style={{ fontSize: 10, color:'var(--brand-primary)', marginLeft: 6, fontWeight: 600 }}>PRIMARY</span>}
                    </div>
                    {/* Volume bar */}
                    <div style={{ width: 120, height: 4, background:'var(--background)', borderRadius: 2, overflow:'hidden' }}>
                      <div style={{ width: p.share + '%', height:'100%', background: i === 0 ? 'var(--brand-primary)' : 'var(--border-medium)' }}/>
                    </div>
                    <span style={{ fontSize: 11, fontFamily:'var(--font-mono)', color:'var(--text-secondary)', width: 32, textAlign:'right' }}>{p.share}%</span>
                    <span style={{ fontSize: 11, fontFamily:'var(--font-mono)', color:'var(--text-tertiary)', width: 50, textAlign:'right' }}>{p.fee}</span>
                    <span style={{ fontSize: 11, fontFamily:'var(--font-mono)', color:'var(--text-tertiary)', width: 56, textAlign:'right' }}>{p.spread}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ── FX & Rates management ─────────────────────────────────────────────────
const FX_PAIRS = [
  { pair:'GBP/NGN', mid: 2014.20, buy: 2009.16, sell: 2019.24, mark:'25 bps', src:'Composite (3 sources)', refresh:'30s', drift:+0.42, alert:'> 2%', alerts: 2 },
  { pair:'USD/NGN', mid: 1602.80, buy: 1598.83, sell: 1606.77, mark:'25 bps', src:'Composite (3 sources)', refresh:'30s', drift:-0.18, alert:'> 2%', alerts: 0 },
  { pair:'EUR/NGN', mid: 1740.10, buy: 1736.62, sell: 1743.58, mark:'20 bps', src:'OANDA',                  refresh:'60s', drift:+0.31, alert:'> 1.5%', alerts: 0 },
  { pair:'GBP/GHS', mid: 18.42,   buy: 18.32,   buy2: 18.52,   sell: 18.51, mark:'45 bps', src:'OANDA + manual', refresh:'5m',  drift:+0.08, alert:'> 3%', alerts: 1 },
  { pair:'USD/KES', mid: 129.40,  buy: 129.14,  sell: 129.66,  mark:'20 bps', src:'Onafriq',                refresh:'60s', drift:+0.22, alert:'> 2%', alerts: 0 },
  { pair:'GBP/ZAR', mid: 23.18,   buy: 23.12,   sell: 23.24,   mark:'25 bps', src:'OANDA',                  refresh:'60s', drift:-0.04, alert:'> 2%', alerts: 0 },
];

function ScreenFxRates() {
  return (
    <div style={{ padding:'22px 28px', height:'100%', overflow:'auto', display:'flex', flexDirection:'column', gap: 18 }}>
      {/* Header */}
      <div style={{ display:'flex', alignItems:'flex-end', justifyContent:'space-between' }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color:'var(--text-primary)', letterSpacing:'-0.01em' }}>FX & Rates</div>
          <div style={{ fontSize: 13, color:'var(--text-secondary)', marginTop: 2, maxWidth: 640 }}>
            Live rate sources, our buy/sell, markup policy and drift alerts. Corridors quote from these pairs.
          </div>
        </div>
        <div style={{ display:'flex', gap: 8 }}>
          <button className="btn btn-sm"><Icon name="refresh" size={12}/> Refresh all</button>
          <button className="btn btn-sm"><Icon name="bell" size={12}/> Alert config</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add pair</button>
        </div>
      </div>

      {/* Live ticker (marquee-style chips) */}
      <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding:'10px 14px', display:'flex', alignItems:'center', gap: 16, overflow:'hidden' }}>
        <div style={{ display:'flex', alignItems:'center', gap: 5, fontSize: 11, color:'var(--text-tertiary)', textTransform:'uppercase', letterSpacing:'0.06em', fontWeight: 600, flex:'none' }}>
          <span style={{ width: 6, height: 6, borderRadius: '50%', background: 'var(--success)', boxShadow: '0 0 0 3px rgba(34, 139, 87, 0.2)' }}/>
          Live
        </div>
        <div style={{ display:'flex', gap: 18, alignItems:'center' }}>
          {FX_PAIRS.map(p => (
            <div key={p.pair} style={{ display:'flex', alignItems:'center', gap: 6, fontSize: 12 }}>
              <span style={{ fontFamily:'var(--font-brand)', fontWeight: 700, color:'var(--text-primary)' }}>{p.pair}</span>
              <span style={{ fontFamily:'var(--font-mono)', color:'var(--text-secondary)' }}>{p.mid.toLocaleString('en-GB', { minimumFractionDigits: 2 })}</span>
              <span style={{ fontSize: 11, fontWeight: 600, color: p.drift >= 0 ? 'var(--success)' : 'var(--danger)' }}>
                {p.drift >= 0 ? '▲' : '▼'} {Math.abs(p.drift)}%
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* Pair table */}
      <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, overflow:'hidden' }}>
        <div style={{ display:'grid', gridTemplateColumns:'120px 110px 110px 110px 90px 1fr 80px 110px 90px', gap: 10, padding:'10px 14px', background:'var(--surface-inset)', borderBottom:'1px solid var(--border-light)', fontSize: 10, fontWeight: 600, textTransform:'uppercase', letterSpacing:'0.06em', color:'var(--text-tertiary)' }}>
          <div>Pair</div><div>Mid (source)</div><div>Our Buy</div><div>Our Sell</div><div>Markup</div><div>Source</div><div>Refresh</div><div>Drift 24h</div><div>Alerts</div>
        </div>
        {FX_PAIRS.map((p, i) => (
          <div key={p.pair} style={{ display:'grid', gridTemplateColumns:'120px 110px 110px 110px 90px 1fr 80px 110px 90px', gap: 10, padding:'12px 14px', alignItems:'center', borderBottom: i < FX_PAIRS.length-1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
            <div style={{ fontFamily:'var(--font-brand)', fontWeight: 700, color:'var(--text-primary)' }}>{p.pair}</div>
            <div style={{ fontFamily:'var(--font-mono)', color:'var(--text-primary)' }}>{p.mid.toLocaleString('en-GB', { minimumFractionDigits: 2 })}</div>
            <div style={{ fontFamily:'var(--font-mono)', color:'var(--text-secondary)' }}>{p.buy.toLocaleString('en-GB', { minimumFractionDigits: 2 })}</div>
            <div style={{ fontFamily:'var(--font-mono)', color:'var(--text-secondary)' }}>{(p.sell || p.buy2).toLocaleString('en-GB', { minimumFractionDigits: 2 })}</div>
            <div><Pill tone="default" size="sm">{p.mark}</Pill></div>
            <div style={{ color:'var(--text-secondary)' }}>{p.src}</div>
            <div style={{ fontFamily:'var(--font-mono)', color:'var(--text-tertiary)' }}>{p.refresh}</div>
            <div>
              <span style={{ fontSize: 11.5, fontWeight: 600, color: p.drift >= 0 ? 'var(--success)' : 'var(--danger)', display:'inline-flex', alignItems:'center', gap: 3 }}>
                <Icon name={p.drift >= 0 ? 'arrowup' : 'arrowdown'} size={10}/>
                {Math.abs(p.drift)}%
              </span>
            </div>
            <div>
              {p.alerts > 0
                ? <Pill tone="warning" size="sm">{p.alerts} firing</Pill>
                : <span style={{ fontSize: 11.5, color:'var(--text-tertiary)' }}>{p.alert}</span>}
            </div>
          </div>
        ))}
      </div>

      {/* Two-column footer: rate spread inspector + recent jobs */}
      <div style={{ display:'grid', gridTemplateColumns:'1.1fr 1fr', gap: 12 }}>
        {/* Spread inspector */}
        <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding: 16 }}>
          <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom: 12 }}>
            <div>
              <div style={{ fontSize: 13, fontWeight: 600, color:'var(--text-primary)' }}>GBP/NGN · spread inspector</div>
              <div style={{ fontSize: 11.5, color:'var(--text-tertiary)' }}>Source rates vs our quote · last 30 min</div>
            </div>
            <button style={{ fontSize: 11, color:'var(--text-secondary)', background:'transparent', border:'1px solid var(--border-light)', borderRadius: 6, padding:'4px 8px', cursor:'pointer' }}>Switch pair</button>
          </div>
          {/* Source rows */}
          <div style={{ display:'flex', flexDirection:'column', gap: 6 }}>
            {[
              { src:'OANDA',      rate: 2014.18, weight: 40 },
              { src:'XE Money',   rate: 2014.42, weight: 30 },
              { src:'Bloomberg',  rate: 2014.02, weight: 30 },
              { src:'Composite',  rate: 2014.20, weight: 100, isUs: true },
            ].map(r => (
              <div key={r.src} style={{ display:'grid', gridTemplateColumns:'120px 1fr 80px 60px', alignItems:'center', gap: 10, padding: r.isUs ? '10px 10px' : '6px 10px', background: r.isUs ? 'var(--brand-primary-10)' : 'transparent', borderRadius: 6, border: r.isUs ? '1px solid var(--brand-primary-10)' : 'none' }}>
                <span style={{ fontSize: 12, fontWeight: r.isUs ? 700 : 500, color: r.isUs ? 'var(--brand-primary)' : 'var(--text-primary)' }}>{r.src}</span>
                <div style={{ height: 4, background:'var(--surface-inset)', borderRadius: 2, position:'relative' }}>
                  <div style={{ position:'absolute', left: ((r.rate - 2013.8) / 0.8) * 100 + '%', top: -3, width: 2, height: 10, background: r.isUs ? 'var(--brand-primary)' : 'var(--text-tertiary)' }}/>
                </div>
                <span style={{ fontFamily:'var(--font-mono)', fontSize: 12, color:'var(--text-secondary)' }}>{r.rate.toFixed(2)}</span>
                <span style={{ fontFamily:'var(--font-mono)', fontSize: 11, color:'var(--text-tertiary)', textAlign:'right' }}>{r.weight}%</span>
              </div>
            ))}
          </div>
          <div style={{ marginTop: 10, fontSize: 11.5, color:'var(--text-tertiary)', fontStyle:'italic' }}>
            Range: 2014.02 – 2014.42 · spread 0.02% · we add 25 bps markup before quoting customers
          </div>
        </div>

        {/* Recent FX jobs */}
        <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding: 16 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color:'var(--text-primary)', marginBottom: 8 }}>Rate refresh log</div>
          <div style={{ display:'flex', flexDirection:'column', gap: 4 }}>
            {[
              { t:'14:42:18', pair:'GBP/NGN', delta:'+0.04%', tone:'success' },
              { t:'14:42:01', pair:'USD/NGN', delta:'-0.02%', tone:'success' },
              { t:'14:41:48', pair:'EUR/NGN', delta:'+0.01%', tone:'success' },
              { t:'14:41:32', pair:'GBP/GHS', delta:'stale 4m', tone:'warning' },
              { t:'14:41:18', pair:'USD/KES', delta:'+0.05%', tone:'success' },
              { t:'14:40:48', pair:'GBP/ZAR', delta:'-0.01%', tone:'success' },
              { t:'14:40:32', pair:'GBP/NGN', delta:'+0.03%', tone:'success' },
              { t:'14:40:01', pair:'USD/NGN', delta:'+0.02%', tone:'success' },
            ].map((r, i) => (
              <div key={i} style={{ display:'grid', gridTemplateColumns:'70px 80px 1fr 14px', gap: 10, padding:'5px 8px', alignItems:'center', borderRadius: 4, fontSize: 11.5 }}>
                <span style={{ fontFamily:'var(--font-mono)', color:'var(--text-tertiary)' }}>{r.t}</span>
                <span style={{ fontFamily:'var(--font-brand)', fontWeight: 600, color:'var(--text-primary)' }}>{r.pair}</span>
                <span style={{ fontFamily:'var(--font-mono)', color: r.tone === 'warning' ? 'var(--warning)' : 'var(--text-secondary)' }}>{r.delta}</span>
                <span style={{ width: 6, height: 6, borderRadius: '50%', background: r.tone === 'warning' ? 'var(--warning)' : 'var(--success)' }}/>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCorridors, ScreenFxRates });
