// Observability — Overview + Traces

// ─── Overview ────────────────────────────────────────────────────
function ScreenObsOverview() {
  // Latency heatmap cells — 24h × 12 agents
  const heatmap = Array.from({ length: 24 * 7 }, (_, i) => {
    const n = Math.random();
    // Make recent hours slightly hotter, introduce a spike at hour 17 / row 2
    const hour = i % 24, row = Math.floor(i / 24);
    let v = n;
    if (hour >= 17 && hour <= 19 && row === 2) v = 0.92 + Math.random() * 0.08;
    else if (hour >= 14 && hour <= 16)         v = 0.25 + Math.random() * 0.4;
    else                                        v = Math.random() * 0.5;
    return v;
  });
  const agents = ['Orchestrator', 'Ledger', 'Billing', 'Payout', 'Compliance', 'Close', 'Dunning'];
  const heat = v => {
    if (v > 0.85) return '#c44536';
    if (v > 0.65) return '#eb5c37';
    if (v > 0.4)  return '#b4741e';
    if (v > 0.2)  return '#3ab795';
    return 'var(--surface-inset)';
  };

  const incidents = [
    { id: 'inc_0042', sev: 'high', title: 'Billing Agent · elevated p99 latency', started: '18m ago', status: 'investigating',
      scope: 'tool: list_bank_transactions · +380ms over baseline', assignee: 'Rafa Q.' },
    { id: 'inc_0041', sev: 'med',  title: 'FX reference feed stale', started: '2h ago', status: 'mitigating',
      scope: 'provider: Wise · fallback engaged · last update 14m ago', assignee: 'Maria Gomez' },
    { id: 'inc_0040', sev: 'low',  title: 'Ledger Agent confidence drop', started: '5h ago', status: 'resolved',
      scope: '0.89 → 0.93 · model rollback to v4.2', assignee: 'Aonik' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="Observability · System health" title="Overview"
        subtitle="Live pulse across every agent, tool, and tenant · last 24 hours"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="calendar" size={12}/> 24h</button>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Snapshot</button>
          <button className="btn btn-primary btn-sm"><Icon name="bell" size={12}/> Alert rules</button>
        </>}/>

      {/* Top status strip */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 12, padding: '14px 18px',
        display: 'grid', gridTemplateColumns: 'auto 1fr auto auto auto auto auto', gap: 20, alignItems: 'center',
      }}>
        <div style={{
          width: 12, height: 12, borderRadius: 999, background: 'var(--success, #1f7a5e)',
          boxShadow: '0 0 0 4px rgba(31,122,94,.18)',
          animation: 'pulse 2s infinite',
        }}/>
        <div>
          <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>All systems operational</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>
            7 agents · 42 tools · 38 tenants · 0 critical incidents · 1 investigation open
          </div>
        </div>
        {[
          ['Uptime · 30d',  '99.982%'],
          ['p50',           '412ms'],
          ['p99',           '1.84s'],
          ['Success',       '99.61%'],
          ['Ops · 24h',     '4,218'],
        ].map(([k, v]) => (
          <div key={k} style={{ textAlign: 'right' }}>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{v}</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{k}</div>
          </div>
        ))}
      </div>

      {/* KPI row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        <KPI label="Error rate · 1h" value="0.39%" delta="-0.1%" deltaTone="up"   spark="0,18 15,16 30,14 45,15 60,13 75,11 90,10 100,9"  sparkColor="#3ab795"/>
        <KPI label="p95 latency"     value="1.21s" delta="+82ms" deltaTone="down" spark="0,12 15,14 30,13 45,15 60,17 75,16 90,18 100,21" sparkColor="#b4741e"/>
        <KPI label="Tool calls · 1h" value="612"   delta="+18%"  deltaTone="up"   spark="0,22 15,20 30,18 45,16 60,14 75,12 90,10 100,8"  sparkColor="#055a60"/>
        <KPI label="Saturation"      value="34%"   delta="+4%"   deltaTone="neutral" spark="0,14 15,14 30,15 45,15 60,16 75,16 90,17 100,17" sparkColor="#7b76b6"/>
      </div>

      {/* Heatmap + incidents */}
      <div style={{ display: 'grid', gridTemplateColumns: '1.45fr 1fr', gap: 20 }}>
        <Card title="Latency heatmap" subtitle="p95 · agent × hour · last 24h">
          <div style={{ display: 'flex', gap: 0, marginTop: 8 }}>
            {/* row labels */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, paddingRight: 10, paddingTop: 16 }}>
              {agents.map(a => (
                <div key={a} style={{ height: 18, fontSize: 11, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center' }}>{a}</div>
              ))}
            </div>
            <div style={{ flex: 1 }}>
              {/* hour ticks */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(24, 1fr)', gap: 2, marginBottom: 4, height: 12 }}>
                {Array.from({ length: 24 }, (_, h) => (
                  <div key={h} style={{ fontSize: 9, color: 'var(--text-tertiary)', textAlign: 'center', fontFamily: 'var(--font-mono)' }}>
                    {h % 4 === 0 ? h.toString().padStart(2,'0') : ''}
                  </div>
                ))}
              </div>
              <div style={{ display: 'grid', gridTemplateRows: 'repeat(7, 18px)', gap: 4 }}>
                {agents.map((_, row) => (
                  <div key={row} style={{ display: 'grid', gridTemplateColumns: 'repeat(24, 1fr)', gap: 2 }}>
                    {Array.from({ length: 24 }, (_, h) => {
                      const v = heatmap[row * 24 + h];
                      return <div key={h} style={{ background: heat(v), borderRadius: 2, height: 18 }} title={`${Math.round(v*3000)}ms`}/>;
                    })}
                  </div>
                ))}
              </div>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 14, marginTop: 12, fontSize: 10.5, color: 'var(--text-secondary)', alignItems: 'center', justifyContent: 'flex-end' }}>
            <span>cold</span>
            {['var(--surface-inset)', '#3ab795', '#b4741e', '#eb5c37', '#c44536'].map(c => (
              <span key={c} style={{ width: 20, height: 10, background: c, borderRadius: 2 }}/>
            ))}
            <span>hot</span>
          </div>
        </Card>

        <Card title="Incidents" subtitle="Open and recent · last 24h"
          action={<span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>3 total</span>}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
            {incidents.map(i => {
              const sevC = { high: '#c44536', med: '#b4741e', low: 'var(--text-tertiary)' }[i.sev];
              const stC = { investigating: '#b4741e', mitigating: '#055a60', resolved: 'var(--success, #1f7a5e)' }[i.status];
              return (
                <div key={i.id} style={{
                  padding: 14, borderRadius: 10,
                  background: 'var(--surface-inset)',
                  border: '1px solid var(--border-light)',
                  borderLeft: `3px solid ${sevC}`,
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
                    <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{i.title}</div>
                    <span style={{ fontSize: 10, fontFamily: 'var(--font-mono)', color: stC, padding: '1px 7px', borderRadius: 4, background: stC + '18' }}>
                      {i.status}
                    </span>
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginBottom: 6, fontFamily: 'var(--font-mono)' }}>{i.scope}</div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10.5, color: 'var(--text-tertiary)' }}>
                    <span>{i.id} · started {i.started}</span>
                    <span>@ {i.assignee}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
      </div>

      {/* Service grid */}
      <Card title="Services" subtitle="Every backing service · current state">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10, marginTop: 4 }}>
          {[
            { n: 'orchestrator',    st: 'ok', p: '38ms',  r: '2.1k rps' },
            { n: 'agent-runner',    st: 'ok', p: '412ms', r: '488 rps' },
            { n: 'tool-gateway',    st: 'warn', p: '1.2s', r: '612 rps' },
            { n: 'policy-engine',   st: 'ok', p: '8ms',   r: '4.8k rps' },
            { n: 'ledger-svc',      st: 'ok', p: '94ms',  r: '218 rps' },
            { n: 'billing-svc',     st: 'ok', p: '112ms', r: '184 rps' },
            { n: 'fx-feed',         st: 'warn', p: 'stale', r: '—' },
            { n: 'payout-rails',    st: 'ok', p: '1.8s',  r: '34 rps' },
            { n: 'kyc-provider',    st: 'ok', p: '682ms', r: '12 rps' },
            { n: 'audit-log',       st: 'ok', p: '14ms',  r: '8.2k rps' },
            { n: 'event-bus',       st: 'ok', p: '4ms',   r: '22k rps' },
            { n: 'notifier',        st: 'ok', p: '62ms',  r: '96 rps' },
          ].map(s => {
            const c = s.st === 'ok' ? 'var(--success, #1f7a5e)' : s.st === 'warn' ? '#b4741e' : '#c44536';
            return (
              <div key={s.n} style={{
                padding: '10px 12px', borderRadius: 8,
                background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
                display: 'flex', alignItems: 'center', gap: 10,
              }}>
                <span style={{ width: 8, height: 8, borderRadius: 999, background: c, flex: 'none' }}/>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{s.n}</div>
                  <div style={{ fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{s.p} · {s.r}</div>
                </div>
              </div>
            );
          })}
        </div>
      </Card>
    </div>
  );
}

// ─── Traces ──────────────────────────────────────────────────────
function ScreenObsTraces() {
  const [selectedId, setSelectedId] = React.useState('trc_9f2c1a');

  const traces = [
    { id: 'trc_9f2c1a', op: 'billing.match_and_apply',    agent: 'Billing',    dur: 3142, spans: 12, st: 'ok',    t: 'just now' },
    { id: 'trc_8b1a42', op: 'ledger.post_depreciation',   agent: 'Ledger',     dur: 2420, spans:  8, st: 'ok',    t: '3m ago' },
    { id: 'trc_4d7e09', op: 'billing.apply_invoice',      agent: 'Billing',    dur:  840, spans:  5, st: 'held',  t: '11m ago' },
    { id: 'trc_2a3f18', op: 'ledger.reconcile_fx',        agent: 'Ledger',     dur: 4218, spans: 18, st: 'err',   t: '18m ago' },
    { id: 'trc_7c1082', op: 'billing.summarize_ar',       agent: 'Billing',    dur: 1940, spans:  6, st: 'ok',    t: '1h ago' },
    { id: 'trc_0f4e21', op: 'compliance.kyc_recheck',     agent: 'Compliance', dur: 2840, spans: 11, st: 'ok',    t: '2h ago' },
    { id: 'trc_5g8d02', op: 'payout.settle_batch',        agent: 'Payout',     dur: 6218, spans: 22, st: 'ok',    t: '3h ago' },
  ];

  // Build sample span tree for the selected trace
  const spans = [
    { id: 's1', name: 'billing.match_and_apply',      kind: 'root',   start:   0, dur: 3142, depth: 0, color: '#eb5c37' },
    { id: 's2', name: 'auth.verify_tenant',           kind: 'http',   start:   0, dur:   42, depth: 1, color: '#7b76b6' },
    { id: 's3', name: 'policy.evaluate',              kind: 'rpc',    start:  44, dur:   18, depth: 1, color: '#055a60' },
    { id: 's4', name: 'llm.plan',                     kind: 'llm',    start:  62, dur:  884, depth: 1, color: '#3f41a0' },
    { id: 's5', name: 'tool.search_invoices',         kind: 'tool',   start: 950, dur:  142, depth: 1, color: '#0097a9' },
    { id: 's6', name: '  db.query',                   kind: 'db',     start: 956, dur:  128, depth: 2, color: '#5facbd' },
    { id: 's7', name: 'tool.list_bank_transactions',  kind: 'tool',   start:1094, dur:  318, depth: 1, color: '#0097a9' },
    { id: 's8', name: '  http.bank_api',              kind: 'http',   start:1110, dur:  298, depth: 2, color: '#7b76b6' },
    { id: 's9', name: 'tool.match_invoice_to_txn',    kind: 'tool',   start:1418, dur:  211, depth: 1, color: '#0097a9' },
    { id: 's10', name: 'llm.decide',                  kind: 'llm',    start:1632, dur:  612, depth: 1, color: '#3f41a0' },
    { id: 's11', name: 'tool.draft_journal_entry',    kind: 'tool',   start:2248, dur:  182, depth: 1, color: '#0097a9' },
    { id: 's12', name: 'policy.ceiling_check',        kind: 'rpc',    start:2430, dur:   12, depth: 1, color: '#055a60' },
    { id: 's13', name: 'tool.propose_apply',          kind: 'tool',   start:2444, dur:  698, depth: 1, color: '#0097a9' },
    { id: 's14', name: '  audit.write',               kind: 'db',     start:3050, dur:   88, depth: 2, color: '#5facbd' },
  ];
  const totalMs = 3200;

  const stPill = {
    ok:   { bg: '#1f7a5e18', fg: '#1f7a5e', label: 'ok' },
    held: { bg: '#b4741e18', fg: '#b4741e', label: 'held' },
    err:  { bg: '#c4453618', fg: '#c44536', label: 'error' },
  };

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 16, height: '100%', boxSizing: 'border-box' }}>
      <PageHeader eyebrow="Observability · Distributed tracing" title="Traces"
        subtitle="Every agent run captured as a span tree · tail 100"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="filter" size={12}/> Filters</button>
          <button className="btn btn-outline btn-sm"><Icon name="calendar" size={12}/> Last 1h</button>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
        </>}/>

      <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr', gap: 20, flex: 1, minHeight: 0 }}>
        {/* Left: trace list */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 12, display: 'flex', flexDirection: 'column', overflow: 'hidden',
        }}>
          <div style={{
            padding: '10px 14px', borderBottom: '1px solid var(--border-light)',
            display: 'flex', alignItems: 'center', gap: 8,
            background: 'var(--surface-inset)',
          }}>
            <Icon name="search" size={13} color="var(--text-tertiary)"/>
            <span style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>
              op:billing.* status:held
            </span>
            <span style={{ flex: 1 }}/>
            <span style={{ fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
              {traces.length} traces
            </span>
          </div>
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {traces.map(t => {
              const active = t.id === selectedId;
              const pill = stPill[t.st];
              return (
                <div key={t.id} onClick={() => setSelectedId(t.id)}
                  style={{
                    padding: '12px 14px',
                    borderBottom: '1px solid var(--border-light)',
                    cursor: 'pointer',
                    background: active ? 'var(--brand-primary-10)' : 'transparent',
                    borderLeft: '3px solid ' + (active ? 'var(--brand-primary)' : 'transparent'),
                  }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 3 }}>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)', fontWeight: active ? 600 : 500 }}>{t.op}</span>
                    <span style={{ fontSize: 10, fontFamily: 'var(--font-mono)', padding: '1px 6px', borderRadius: 4, background: pill.bg, color: pill.fg }}>{pill.label}</span>
                  </div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginBottom: 4 }}>{t.id}</div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10.5, color: 'var(--text-secondary)' }}>
                    <span>{t.agent} · {t.spans} spans</span>
                    <span style={{ fontFamily: 'var(--font-mono)' }}>{(t.dur/1000).toFixed(2)}s · {t.t}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Right: span waterfall */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 12, display: 'flex', flexDirection: 'column', overflow: 'hidden',
        }}>
          {/* header */}
          <div style={{ padding: '14px 18px', borderBottom: '1px solid var(--border-light)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>billing.match_and_apply</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>trc_9f2c1a</span>
              <span style={{ fontSize: 10, fontFamily: 'var(--font-mono)', padding: '2px 7px', borderRadius: 4, background: '#1f7a5e18', color: '#1f7a5e' }}>ok</span>
            </div>
            <div style={{ display: 'flex', gap: 18, fontSize: 11, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>
              <span>duration <b style={{ color: 'var(--text-primary)' }}>3.142s</b></span>
              <span>spans <b style={{ color: 'var(--text-primary)' }}>12</b></span>
              <span>tokens <b style={{ color: 'var(--text-primary)' }}>2,314</b></span>
              <span>tools <b style={{ color: 'var(--text-primary)' }}>6</b></span>
              <span>agent <b style={{ color: 'var(--text-primary)' }}>Billing</b></span>
              <span>tenant <b style={{ color: 'var(--text-primary)' }}>primrose</b></span>
            </div>
          </div>

          {/* timeline header */}
          <div style={{ display: 'grid', gridTemplateColumns: '260px 80px 1fr', gap: 8, padding: '10px 18px',
            borderBottom: '1px solid var(--border-light)', background: 'var(--surface-inset)',
            fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.04em' }}>
            <div>SPAN</div>
            <div style={{ textAlign: 'right' }}>DURATION</div>
            <div style={{ position: 'relative' }}>
              {[0, 0.25, 0.5, 0.75, 1].map(p => (
                <span key={p} style={{ position: 'absolute', left: (p*100) + '%', transform: 'translateX(-50%)', fontFamily: 'var(--font-mono)' }}>
                  {Math.round(p * totalMs)}ms
                </span>
              ))}
            </div>
          </div>

          {/* span rows */}
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {spans.map(s => {
              const leftPct = (s.start / totalMs) * 100;
              const widthPct = Math.max(0.5, (s.dur / totalMs) * 100);
              return (
                <div key={s.id} style={{
                  display: 'grid', gridTemplateColumns: '260px 80px 1fr', gap: 8,
                  padding: '7px 18px', borderBottom: '1px solid var(--border-light)',
                  alignItems: 'center',
                }}>
                  <div style={{
                    fontFamily: 'var(--font-mono)', fontSize: 11.5,
                    color: 'var(--text-primary)', paddingLeft: s.depth * 14,
                    display: 'flex', alignItems: 'center', gap: 6,
                    overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                  }}>
                    <span style={{ fontSize: 9, padding: '1px 5px', borderRadius: 3, background: s.color + '20', color: s.color, fontWeight: 600, letterSpacing: '0.04em' }}>{s.kind}</span>
                    {s.name.trim()}
                  </div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)', textAlign: 'right' }}>{s.dur}ms</div>
                  <div style={{ position: 'relative', height: 18, background: 'var(--surface-inset)', borderRadius: 4 }}>
                    {/* gridlines */}
                    {[0.25, 0.5, 0.75].map(p => (
                      <span key={p} style={{ position: 'absolute', left: (p*100) + '%', top: 0, bottom: 0, width: 1, background: 'var(--border-light)' }}/>
                    ))}
                    <div style={{
                      position: 'absolute', left: leftPct + '%', width: widthPct + '%',
                      top: 3, bottom: 3, borderRadius: 3,
                      background: s.color,
                      opacity: s.depth === 0 ? 0.85 : s.depth === 1 ? 0.92 : 1,
                    }}/>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenObsOverview, ScreenObsTraces });
