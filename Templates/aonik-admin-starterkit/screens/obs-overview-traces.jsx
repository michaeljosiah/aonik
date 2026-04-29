// Observability — Overview + Traces

// ─── Overview ────────────────────────────────────────────────────
function ScreenObsOverview() {
  // Sparkline path generator
  const sparkline = (vals, w = 100, h = 28) => {
    const max = Math.max(...vals), min = Math.min(...vals);
    const range = max - min || 1;
    return vals.map((v, i) => `${(i / (vals.length - 1)) * w},${h - ((v - min) / range) * h}`).join(' ');
  };

  // Heatmap data — 14 days × 24 hours
  const heatmap = Array.from({ length: 14 }, (_, d) =>
    Array.from({ length: 24 }, (_, h) => {
      const base = Math.sin((h - 8) / 6) * 0.5 + 0.5;
      const noise = Math.sin(d * 13.7 + h * 7.3) * 0.3 + Math.cos(d * 5 + h * 11) * 0.2;
      return Math.max(0, Math.min(1, base * 0.7 + noise + (d === 11 && h > 14 && h < 19 ? 0.4 : 0)));
    })
  );

  const services = [
    { n: 'orchestrator',  st: 'ok',   p99: '142ms', err: '0.02%' },
    { n: 'billing-agent', st: 'ok',   p99: '894ms', err: '0.18%' },
    { n: 'ledger-agent',  st: 'warn', p99: '1.2s',  err: '0.84%' },
    { n: 'kyb-agent',     st: 'ok',   p99: '218ms', err: '0.04%' },
    { n: 'event-bus',     st: 'ok',   p99: ' 18ms', err: '0.00%' },
    { n: 'policy-engine', st: 'ok',   p99: ' 22ms', err: '0.01%' },
    { n: 'tool-gateway',  st: 'ok',   p99: '208ms', err: '0.12%' },
    { n: 'audit-store',   st: 'ok',   p99: ' 41ms', err: '0.00%' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="Observability · System health" title="Overview"
        subtitle="Live runtime telemetry across all agents · 14d window"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="calendar" size={12}/> Last 14d</button>
          <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Refresh</button>
        </>}/>

      {/* KPI strip with sparklines */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        {[
          { l: 'Trace volume',     v: '482K', d: '+8.2%', dt: 'up',   spark: [40,42,38,46,52,58,55,60,68,72,78,82], color: '#055a60' },
          { l: 'p99 latency',      v: '892ms', d: '-12ms', dt: 'down', spark: [120,118,116,112,110,108,106,104,102,98,95,92], color: '#3ab795' },
          { l: 'Error rate',       v: '0.18%', d: '+0.04%', dt: 'up',  spark: [10,12,11,14,12,15,18,16,14,18,19,18], color: '#c44536' },
          { l: 'Tool invocations', v: '1.42M', d: '+22%',  dt: 'up',   spark: [80,85,90,88,92,98,108,112,118,128,138,142], color: '#7b76b6' },
        ].map(k => (
          <Card key={k.l} padding={16}>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', letterSpacing: '0.04em', textTransform: 'uppercase' }}>{k.l}</div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 12, marginTop: 8 }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 26, fontWeight: 600, lineHeight: 1, color: 'var(--text-primary)' }}>{k.v}</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: k.dt === 'up' ? (k.l === 'Error rate' ? '#c44536' : 'var(--success, #1f7a5e)') : 'var(--success, #1f7a5e)', marginBottom: 2 }}>
                {k.d}
              </div>
              <div style={{ flex: 1 }}/>
              <svg width={100} height={28} style={{ display: 'block' }}>
                <polyline points={sparkline(k.spark)} fill="none" stroke={k.color} strokeWidth={1.5}/>
              </svg>
            </div>
          </Card>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr', gap: 14 }}>
        {/* Heatmap */}
        <Card padding={16}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14 }}>
            <Icon name="activity" size={14} color="var(--text-secondary)"/>
            <div style={{ fontSize: 13, fontWeight: 600 }}>Trace volume by hour</div>
            <div style={{ flex: 1 }}/>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>14d × 24h</span>
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between', fontSize: 9, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', paddingBlock: 2 }}>
              <span>00</span>
              <span>06</span>
              <span>12</span>
              <span>18</span>
              <span>23</span>
            </div>
            <div style={{ flex: 1, display: 'grid', gridTemplateColumns: 'repeat(14, 1fr)', gap: 2 }}>
              {heatmap.map((day, di) => (
                <div key={di} style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  {day.map((v, hi) => (
                    <div key={hi} style={{
                      flex: 1, height: 9, borderRadius: 2,
                      background: `color-mix(in oklab, #055a60 ${v * 100}%, var(--surface-inset))`,
                    }} title={`d${di} h${hi}: ${(v*1000).toFixed(0)}`}/>
                  ))}
                </div>
              ))}
            </div>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 9, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
            <span>14d ago</span><span>today</span>
          </div>
        </Card>

        {/* Incidents */}
        <Card padding={16}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
            <Icon name="alert" size={14} color="#c44536"/>
            <div style={{ fontSize: 13, fontWeight: 600 }}>Incidents</div>
            <div style={{ flex: 1 }}/>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>3 open</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {[
              { id: 'INC-2041', t: 'Ledger Agent · FX rate stale', sev: 'high',  status: 'investigating', age: '14m', svc: 'ledger-agent' },
              { id: 'INC-2040', t: 'Billing Agent · 4 holds queued', sev: 'med',  status: 'mitigating',    age: '38m', svc: 'billing-agent' },
              { id: 'INC-2039', t: 'Tool gateway · timeouts spiking', sev: 'low',  status: 'investigating', age: '1h',  svc: 'tool-gateway' },
              { id: 'INC-2038', t: 'KYB · doc parser regression',     sev: 'med',  status: 'resolved',      age: '3h',  svc: 'kyb-agent' },
            ].map(i => {
              const sevC = { high: '#c44536', med: '#b4741e', low: 'var(--text-tertiary)' }[i.sev];
              const stC = { investigating: '#b4741e', mitigating: '#055a60', resolved: 'var(--success, #1f7a5e)' }[i.status];
              return (
                <div key={i.id} style={{
                  padding: 14, borderRadius: 10,
                  border: '1px solid var(--border-light)',
                  background: 'var(--surface)',
                }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                    <span style={{ width: 6, height: 6, borderRadius: 999, background: sevC }}/>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{i.id}</span>
                    <div style={{ flex: 1 }}/>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{i.age}</span>
                  </div>
                  <div style={{ fontSize: 12.5, color: 'var(--text-primary)', fontWeight: 500, marginBottom: 6 }}>{i.t}</div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 10.5 }}>
                    <span style={{ padding: '1px 6px', borderRadius: 4, background: stC + '18', color: stC, fontWeight: 600, letterSpacing: '0.03em' }}>{i.status}</span>
                    <span style={{ color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{i.svc}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
      </div>

      {/* Services grid */}
      <Card padding={16}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14 }}>
          <Icon name="grid" size={14} color="var(--text-secondary)"/>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Services</div>
          <div style={{ flex: 1 }}/>
          <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>8 services</span>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
          {services.map(s => {
            const c = s.st === 'ok' ? 'var(--success, #1f7a5e)' : s.st === 'warn' ? '#b4741e' : '#c44536';
            return (
              <div key={s.n} style={{
                padding: '10px 12px', borderRadius: 8,
                border: '1px solid var(--border-light)',
                background: 'var(--surface)',
                display: 'flex', alignItems: 'center', gap: 10,
              }}>
                <span style={{ width: 8, height: 8, borderRadius: 999, background: c, flex: 'none' }}/>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{s.n}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', display: 'flex', gap: 8 }}>
                    <span>p99 {s.p99}</span>
                    <span>err {s.err}</span>
                  </div>
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
  const [openSpanId, setOpenSpanId] = React.useState('s9'); // tool.match_invoice_to_txn — opens by default to show the pattern

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

  const openSpan = spans.find(s => s.id === openSpanId);

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 16, height: '100%', boxSizing: 'border-box', position: 'relative', overflow: 'hidden' }}>
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
              const isOpen = s.id === openSpanId;
              return (
                <div key={s.id} onClick={() => setOpenSpanId(s.id)} style={{
                  display: 'grid', gridTemplateColumns: '260px 80px 1fr', gap: 8,
                  padding: '7px 18px', borderBottom: '1px solid var(--border-light)',
                  alignItems: 'center', cursor: 'pointer',
                  background: isOpen ? 'var(--brand-primary-10)' : 'transparent',
                  borderLeft: '3px solid ' + (isOpen ? 'var(--brand-primary)' : 'transparent'),
                  paddingLeft: isOpen ? 15 : 18,
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

      <SpanDetailSlideOut span={openSpan} totalMs={totalMs} onClose={() => setOpenSpanId(null)}/>
    </div>
  );
}

// ─── Span detail slide-out ─────────────────────────────────────────
function SpanDetailSlideOut({ span, totalMs, onClose }) {
  if (!span) return null;

  // Per-kind detail content
  const isLLM  = span.kind === 'llm';
  const isTool = span.kind === 'tool';
  const isHTTP = span.kind === 'http';
  const isDB   = span.kind === 'db';
  const isRPC  = span.kind === 'rpc';

  const startPct = (span.start / totalMs) * 100;
  const widthPct = Math.max(0.5, (span.dur / totalMs) * 100);

  return (
    <div style={{
      position: 'absolute', top: 0, right: 0, bottom: 0, width: 540,
      background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
      boxShadow: '-12px 0 32px -8px rgb(0 0 0 / 0.10)',
      display: 'flex', flexDirection: 'column', zIndex: 5,
    }}>
      {/* Header */}
      <div style={{
        padding: '14px 20px', borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 12,
      }}>
        <div style={{
          width: 34, height: 34, borderRadius: 8,
          background: span.color + '20', color: span.color,
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'var(--font-mono)', fontSize: 9, fontWeight: 700, letterSpacing: '0.04em',
        }}>{span.kind.toUpperCase()}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {span.name.trim()}
          </div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1 }}>
            span_{span.id} · trc_9f2c1a
          </div>
        </div>
        <span className="hover-halo" onClick={onClose} style={{ cursor: 'pointer' }}><Icon name="close" size={14}/></span>
      </div>

      {/* Tabs */}
      <div style={{
        padding: '0 20px', borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 4,
      }}>
        {[
          { l: 'Overview', active: true },
          { l: 'Attributes', count: 14 },
          { l: 'Events', count: 3 },
          { l: 'Logs', count: 8 },
          { l: 'Raw' },
        ].map((t, i) => (
          <div key={i} style={{
            padding: '11px 10px 10px', fontSize: 12,
            fontWeight: t.active ? 600 : 500,
            color: t.active ? 'var(--text-primary)' : 'var(--text-secondary)',
            borderBottom: t.active ? '2px solid var(--brand-primary)' : '2px solid transparent',
            display: 'inline-flex', alignItems: 'center', gap: 5, cursor: 'pointer',
          }}>
            {t.l}
            {t.count != null && (
              <span style={{
                fontSize: 9.5, fontFamily: 'var(--font-mono)', fontWeight: 600,
                padding: '1px 5px', borderRadius: 999,
                background: 'var(--surface-inset)', color: 'var(--text-tertiary)',
              }}>{t.count}</span>
            )}
          </div>
        ))}
      </div>

      {/* Body */}
      <div style={{ flex: 1, overflow: 'auto', padding: '18px 20px', display: 'flex', flexDirection: 'column', gap: 18 }}>

        {/* Timing strip */}
        <div>
          <SectionLabel>Timing</SectionLabel>
          <div style={{
            background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
            borderRadius: 8, padding: '12px 14px',
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 10 }}>
              <Stat label="Duration" value={span.dur + 'ms'} accent={span.color}/>
              <Stat label="Start offset" value={'+' + span.start + 'ms'}/>
              <Stat label="End offset" value={'+' + (span.start + span.dur) + 'ms'}/>
              <Stat label="% of trace" value={((span.dur / totalMs) * 100).toFixed(1) + '%'}/>
            </div>
            {/* Mini-timeline showing this span in context */}
            <div style={{ position: 'relative', height: 14, background: 'var(--surface)', borderRadius: 3, border: '1px solid var(--border-light)' }}>
              {[0.25, 0.5, 0.75].map(p => (
                <span key={p} style={{ position: 'absolute', left: (p*100) + '%', top: 0, bottom: 0, width: 1, background: 'var(--border-light)' }}/>
              ))}
              <div style={{
                position: 'absolute', left: startPct + '%', width: widthPct + '%',
                top: 2, bottom: 2, borderRadius: 2,
                background: span.color,
              }}/>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 4, fontSize: 9.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
              <span>0ms</span><span>{totalMs}ms</span>
            </div>
          </div>
        </div>

        {/* Attributes — vary by kind */}
        <div>
          <SectionLabel>Attributes</SectionLabel>
          <Attrs rows={[
            ['service.name', 'billing-agent'],
            ['service.version', '0.42.1'],
            ['span.kind', span.kind],
            ['agent.id', 'agt_billing_v3'],
            ['tenant.id', 'primrose'],
            ['user.id', 'usr_amara'],
            ['trace.flags', '01 (sampled)'],
            ['parent.span.id', span.depth === 0 ? '—' : 'span_s' + Math.max(1, span.depth)],
          ]}/>
        </div>

        {/* Kind-specific detail */}
        {isLLM && (
          <div>
            <SectionLabel>LLM call</SectionLabel>
            <Attrs rows={[
              ['model', 'claude-sonnet-4.5'],
              ['provider', 'anthropic'],
              ['temperature', '0.2'],
              ['max_tokens', '2048'],
              ['input.tokens', '1,284'],
              ['output.tokens', '612'],
              ['cache.hit', 'true · 942 tokens'],
              ['cost.usd', '$0.0184'],
              ['stop.reason', 'end_turn'],
            ]}/>
            <div style={{ marginTop: 10 }}>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.04em', marginBottom: 6 }}>SYSTEM PROMPT</div>
              <pre style={preStyle}>You are the Billing Agent. Match invoices to bank transactions using ledger context. Never apply payments without explicit policy approval.</pre>
            </div>
          </div>
        )}

        {isTool && (
          <div>
            <SectionLabel>Tool invocation</SectionLabel>
            <Attrs rows={[
              ['tool.name', span.name.trim().replace('tool.', '')],
              ['tool.version', '2.1.0'],
              ['idempotency.key', 'idm_8a3f12'],
              ['policy.required', 'finance:read'],
              ['policy.outcome', 'allow'],
              ['result.status', 'ok'],
              ['result.size', '4.2 KB'],
              ['retry.count', '0'],
            ]}/>
            <div style={{ marginTop: 10 }}>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.04em', marginBottom: 6 }}>INPUT</div>
              <pre style={preStyle}>{`{
  "invoice_id": "INV-2041",
  "txn_id": "bank_txn_9f2c1a",
  "tolerance_pct": 0.5
}`}</pre>
            </div>
            <div style={{ marginTop: 10 }}>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.04em', marginBottom: 6 }}>OUTPUT</div>
              <pre style={preStyle}>{`{
  "match": true,
  "confidence": 0.94,
  "delta_amount": 0.00,
  "ledger_account": "1200"
}`}</pre>
            </div>
          </div>
        )}

        {isHTTP && (
          <div>
            <SectionLabel>HTTP request</SectionLabel>
            <Attrs rows={[
              ['http.method', 'GET'],
              ['http.url', 'https://api.bank.example/v2/transactions'],
              ['http.status_code', '200'],
              ['http.response_size', '8.4 KB'],
              ['net.peer.name', 'api.bank.example'],
              ['net.protocol.version', 'HTTP/2'],
              ['tls.cipher', 'TLS_AES_128_GCM_SHA256'],
            ]}/>
          </div>
        )}

        {isDB && (
          <div>
            <SectionLabel>Database query</SectionLabel>
            <Attrs rows={[
              ['db.system', 'postgresql'],
              ['db.name', 'ledger_prod'],
              ['db.operation', 'SELECT'],
              ['db.rows_affected', '14'],
              ['db.connection_pool.size', '32'],
            ]}/>
            <div style={{ marginTop: 10 }}>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.04em', marginBottom: 6 }}>STATEMENT</div>
              <pre style={preStyle}>{`SELECT id, amount, party, date
  FROM invoices
 WHERE tenant = $1
   AND status IN ('open', 'pending')
   AND amount BETWEEN $2 AND $3
 ORDER BY date DESC
 LIMIT 50;`}</pre>
            </div>
          </div>
        )}

        {isRPC && (
          <div>
            <SectionLabel>Policy check</SectionLabel>
            <Attrs rows={[
              ['rpc.system', 'policy-engine'],
              ['policy.id', 'pol_finance_apply_v3'],
              ['policy.outcome', 'allow'],
              ['policy.matched_rules', '2'],
              ['rule.1', 'finance:read · tenant=primrose'],
              ['rule.2', 'amount_ceiling · max=$50,000'],
            ]}/>
          </div>
        )}

        {/* Events timeline */}
        <div>
          <SectionLabel>Events <span style={{ color: 'var(--text-tertiary)', fontWeight: 400 }}>· 3</span></SectionLabel>
          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 8, overflow: 'hidden',
          }}>
            {[
              { t: '+0ms',     name: 'span.started',         color: 'var(--text-tertiary)' },
              { t: '+82ms',    name: 'cache.lookup',         color: '#055a60' },
              { t: '+' + span.dur + 'ms', name: 'span.ended', color: 'var(--success, #1f7a5e)' },
            ].map((e, i, arr) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: '70px 12px 1fr',
                gap: 10, alignItems: 'center', padding: '8px 12px',
                borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
                fontSize: 11.5,
              }}>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{e.t}</span>
                <span style={{ width: 8, height: 8, borderRadius: 999, background: e.color, justifySelf: 'center' }}/>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{e.name}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Footer */}
      <div style={{
        padding: '12px 20px', borderTop: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10,
      }}>
        <div style={{ display: 'flex', gap: 6 }}>
          <button className="btn btn-ghost btn-sm"><Icon name="terminal" size={11}/> View logs</button>
          <button className="btn btn-ghost btn-sm"><Icon name="link" size={11}/> Copy span ID</button>
        </div>
        <div style={{ display: 'flex', gap: 6 }}>
          <button className="btn btn-outline btn-sm"><Icon name="arrowleft" size={11}/> Prev span</button>
          <button className="btn btn-outline btn-sm">Next span <Icon name="arrowright" size={11}/></button>
        </div>
      </div>
    </div>
  );
}

function SectionLabel({ children }) {
  return (
    <div style={{ fontSize: 10.5, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>
      {children}
    </div>
  );
}

function Stat({ label, value, accent }) {
  return (
    <div>
      <div style={{ fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.04em', textTransform: 'uppercase', fontWeight: 600, marginBottom: 2 }}>{label}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 600, color: accent || 'var(--text-primary)' }}>{value}</div>
    </div>
  );
}

function Attrs({ rows }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 8, overflow: 'hidden',
    }}>
      {rows.map(([k, v], i) => (
        <div key={i} style={{
          display: 'grid', gridTemplateColumns: '180px 1fr',
          gap: 12, padding: '7px 12px',
          borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none',
          fontFamily: 'var(--font-mono)', fontSize: 11,
        }}>
          <span style={{ color: 'var(--text-tertiary)' }}>{k}</span>
          <span style={{ color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{v}</span>
        </div>
      ))}
    </div>
  );
}

const preStyle = {
  background: 'var(--surface-inset)',
  border: '1px solid var(--border-light)',
  borderRadius: 6,
  padding: '10px 12px',
  fontFamily: 'var(--font-mono)',
  fontSize: 11,
  color: 'var(--text-primary)',
  margin: 0,
  whiteSpace: 'pre-wrap',
  overflow: 'auto',
  maxHeight: 200,
  lineHeight: 1.5,
};

Object.assign(window, { ScreenObsOverview, ScreenObsTraces });
