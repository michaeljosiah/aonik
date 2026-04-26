// Observability — Logs + Audit Log

// ─── Logs ────────────────────────────────────────────────────────
function ScreenObsLogs() {
  const [live, setLive] = React.useState(true);
  const [sevFilter, setSevFilter] = React.useState('All');

  const logs = [
    { t: '14:02:41.912', sev: 'info',  svc: 'agent-runner',  agent: 'Billing',    trace: 'trc_9f2c1a', msg: 'tool.call start', fields: { tool: 'match_invoice_to_txn', invoice: 'INV-2041' } },
    { t: '14:02:41.911', sev: 'debug', svc: 'tool-gateway',  agent: 'Billing',    trace: 'trc_9f2c1a', msg: 'rate-limit check passed', fields: { limit: '40/s', used: 18 } },
    { t: '14:02:41.894', sev: 'info',  svc: 'llm.proxy',     agent: 'Billing',    trace: 'trc_9f2c1a', msg: 'completion · 612ms · 184 tok', fields: { model: 'sonnet-4.5' } },
    { t: '14:02:41.602', sev: 'warn',  svc: 'tool-gateway',  agent: 'Ledger',     trace: 'trc_8b1a42', msg: 'downstream latency over p95 baseline', fields: { p95: '812ms', base: '420ms' } },
    { t: '14:02:41.210', sev: 'info',  svc: 'policy-engine', agent: 'Payout',     trace: 'trc_5g8d02', msg: 'policy.ceiling · hold', fields: { policy: 'pol_ceiling', amount: '£62,400' } },
    { t: '14:02:40.944', sev: 'error', svc: 'fx-feed',       agent: 'FX',         trace: 'trc_2a3f18', msg: 'upstream read_timeout · fallback engaged', fields: { provider: 'wise', elapsed: '3000ms' } },
    { t: '14:02:40.821', sev: 'info',  svc: 'agent-runner',  agent: 'Close',      trace: 'trc_f1c022', msg: 'run.started', fields: { op: 'ledger.close_month' } },
    { t: '14:02:40.614', sev: 'debug', svc: 'audit-log',     agent: '—',          trace: 'trc_9f2c1a', msg: 'audit.write · 88ms · immutable', fields: { records: 3 } },
    { t: '14:02:40.402', sev: 'info',  svc: 'agent-runner',  agent: 'Billing',    trace: 'trc_7c1082', msg: 'tool.call ok', fields: { tool: 'search_invoices', rows: 14 } },
    { t: '14:02:40.188', sev: 'warn',  svc: 'llm.proxy',     agent: 'Ledger',     trace: 'trc_8b1a42', msg: 'retry · rate-limit 429', fields: { attempt: 2, backoff: '220ms' } },
    { t: '14:02:39.944', sev: 'info',  svc: 'tool-gateway',  agent: 'Compliance', trace: 'trc_0f4e21', msg: 'tool.call start', fields: { tool: 'kyc.verify', party: 'primrose' } },
    { t: '14:02:39.721', sev: 'debug', svc: 'policy-engine', agent: 'Billing',    trace: 'trc_9f2c1a', msg: 'policy.evaluate · 18ms · 4 rules', fields: { blocked: 0, held: 1 } },
    { t: '14:02:39.503', sev: 'info',  svc: 'event-bus',     agent: '—',          trace: '—',          msg: 'dispatched · task.held', fields: { subscribers: 3 } },
    { t: '14:02:39.290', sev: 'error', svc: 'billing-svc',   agent: 'Billing',    trace: 'trc_4d7e09', msg: 'apply_invoice blocked · ceiling breach', fields: { amount: '£62,400', ceiling: '£50,000' } },
    { t: '14:02:39.082', sev: 'info',  svc: 'agent-runner',  agent: 'Billing',    trace: 'trc_4d7e09', msg: 'run.ended · held', fields: { duration: '840ms' } },
    { t: '14:02:38.844', sev: 'debug', svc: 'ledger-svc',    agent: 'Ledger',     trace: 'trc_186b10', msg: 'batch.flush · 412 entries', fields: { ms: 94 } },
    { t: '14:02:38.612', sev: 'info',  svc: 'agent-runner',  agent: 'Dunning',    trace: 'trc_a38c02', msg: 'scheduled · runs Mon 09:00', fields: {} },
    { t: '14:02:38.401', sev: 'info',  svc: 'llm.proxy',     agent: 'Close',      trace: 'trc_f1c022', msg: 'completion · 412ms · 84 tok', fields: { model: 'sonnet-4.5' } },
  ];

  const sevTone = {
    debug: { bg: 'var(--surface-inset)', fg: 'var(--text-tertiary)' },
    info:  { bg: '#055a6020',             fg: '#055a60' },
    warn:  { bg: '#b4741e22',             fg: '#b4741e' },
    error: { bg: '#c4453622',             fg: '#c44536' },
  };

  const counts = {
    All:   logs.length,
    Debug: logs.filter(l => l.sev === 'debug').length,
    Info:  logs.filter(l => l.sev === 'info').length,
    Warn:  logs.filter(l => l.sev === 'warn').length,
    Error: logs.filter(l => l.sev === 'error').length,
  };
  const sevMap = { All: null, Debug: 'debug', Info: 'info', Warn: 'warn', Error: 'error' };
  const filtered = sevFilter === 'All' ? logs : logs.filter(l => l.sev === sevMap[sevFilter]);

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 16, height: '100%', boxSizing: 'border-box' }}>
      <PageHeader eyebrow="Observability · Structured logs" title="Logs"
        subtitle="Live tail across every service · indexed · 7d retention"
        actions={<>
          <button onClick={() => setLive(!live)}
            className={'btn btn-sm ' + (live ? 'btn-primary' : 'btn-outline')}>
            <span style={{
              width: 6, height: 6, borderRadius: 999, background: live ? '#fff' : 'var(--success, #1f7a5e)',
              display: 'inline-block', marginRight: 6, verticalAlign: 'middle',
              animation: live ? 'pulse 1.6s infinite' : 'none',
            }}/>
            {live ? 'Live tail' : 'Paused'}
          </button>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
          <button className="btn btn-outline btn-sm"><Icon name="bell" size={12}/> Alert on…</button>
        </>}/>

      {/* Query + filters */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, padding: '10px 12px',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <Icon name="terminal" size={14} color="var(--text-secondary)"/>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)', flex: 1 }}>
          <span style={{ color: 'var(--text-tertiary)' }}>svc:</span>"agent-runner" <span style={{ color: 'var(--text-tertiary)' }}>OR</span> <span style={{ color: 'var(--text-tertiary)' }}>svc:</span>"tool-gateway" <span style={{ color: 'var(--text-tertiary)' }}>|</span> sev:&gt;=info
        </span>
        <div style={{ display: 'flex', gap: 4 }}>
          {['All', 'Debug', 'Info', 'Warn', 'Error'].map(s => {
            const active = sevFilter === s;
            return (
              <button key={s} onClick={() => setSevFilter(s)}
                style={{
                  border: '1px solid ' + (active ? 'var(--brand-primary)' : 'var(--border-light)'),
                  background: active ? 'var(--brand-primary-10)' : 'transparent',
                  color: active ? 'var(--brand-primary)' : 'var(--text-secondary)',
                  padding: '3px 10px', fontSize: 11, borderRadius: 999, cursor: 'pointer',
                  fontWeight: active ? 600 : 500, fontFamily: 'var(--font-sans)',
                }}>
                {s} <span style={{ opacity: 0.7, marginLeft: 3, fontFamily: 'var(--font-mono)' }}>{counts[s]}</span>
              </button>
            );
          })}
        </div>
      </div>

      {/* Volume sparkline */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, padding: '10px 14px',
        display: 'grid', gridTemplateColumns: '180px 1fr auto', gap: 16, alignItems: 'center',
      }}>
        <div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Volume · last 5 min</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, color: 'var(--text-primary)', fontWeight: 600 }}>4,218 <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontWeight: 400 }}>events</span></div>
        </div>
        <svg viewBox="0 0 600 40" preserveAspectRatio="none" style={{ width: '100%', height: 40, display: 'block' }}>
          {Array.from({ length: 60 }, (_, i) => {
            const x = (i / 59) * 600;
            const h = 8 + Math.random() * 28;
            const hasErr = i === 42 || i === 22;
            return <g key={i}>
              <rect x={x - 4} y={40 - h} width={4} height={h} fill="var(--brand-primary)" opacity="0.75"/>
              {hasErr && <rect x={x - 4} y={40 - h - 2} width={4} height={2} fill="#c44536"/>}
            </g>;
          })}
        </svg>
        <div style={{ display: 'flex', gap: 12, fontSize: 10.5, color: 'var(--text-secondary)' }}>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}><span style={{ width: 8, height: 8, background: 'var(--brand-primary)' }}/> events</span>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}><span style={{ width: 8, height: 8, background: '#c44536' }}/> errors</span>
        </div>
      </div>

      {/* Log stream */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', overflow: 'hidden',
      }}>
        <div style={{
          padding: '8px 14px', borderBottom: '1px solid var(--border-light)',
          background: 'var(--surface-inset)',
          display: 'grid', gridTemplateColumns: '110px 64px 120px 110px 110px 1fr',
          gap: 12, fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.04em',
        }}>
          <div>TIMESTAMP</div><div>SEV</div><div>SERVICE</div><div>AGENT</div><div>TRACE</div><div>MESSAGE</div>
        </div>
        <div style={{ flex: 1, overflowY: 'auto', fontFamily: 'var(--font-mono)', fontSize: 11.5 }}>
          {filtered.map((l, i) => {
            const s = sevTone[l.sev];
            return (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: '110px 64px 120px 110px 110px 1fr',
                gap: 12, padding: '6px 14px',
                borderBottom: '1px solid var(--border-light)',
                alignItems: 'start',
              }}>
                <span style={{ color: 'var(--text-tertiary)' }}>{l.t}</span>
                <span style={{
                  fontSize: 9.5, padding: '1px 6px', borderRadius: 3,
                  background: s.bg, color: s.fg, fontWeight: 600,
                  letterSpacing: '0.04em', textTransform: 'uppercase',
                  alignSelf: 'center', justifySelf: 'start',
                }}>{l.sev}</span>
                <span style={{ color: 'var(--text-secondary)' }}>{l.svc}</span>
                <span style={{ color: 'var(--text-secondary)' }}>{l.agent}</span>
                <span style={{ color: 'var(--brand-primary)' }}>{l.trace}</span>
                <span style={{ color: 'var(--text-primary)' }}>
                  {l.msg}
                  {Object.keys(l.fields).length > 0 && (
                    <span style={{ color: 'var(--text-tertiary)' }}>
                      {' '}· {Object.entries(l.fields).map(([k, v], j) => (
                        <span key={k}>
                          {j > 0 ? ' ' : ''}
                          <span>{k}=</span>
                          <span style={{ color: 'var(--text-secondary)' }}>{typeof v === 'string' ? `"${v}"` : v}</span>
                        </span>
                      ))}
                    </span>
                  )}
                </span>
              </div>
            );
          })}
          {live && (
            <div style={{ padding: '10px 14px', color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success, #1f7a5e)', animation: 'pulse 1.6s infinite' }}/>
              awaiting new events…
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Audit Log ────────────────────────────────────────────────────
function ScreenObsAudit() {
  const events = [
    { t: '14:02:41', actor: 'Maria Gomez',      kind: 'human', scope: 'billing.task', action: 'approved task',
      detail: 'Approved £62,400 ceiling breach · task tsk_0194 · reason "verified with Primrose treasurer"', tenant: 'primrose', risk: 'med' },
    { t: '14:01:18', actor: 'Billing Agent',    kind: 'agent', scope: 'billing.invoice', action: 'applied',
      detail: 'Applied INV-2041 to bank_txn_9f2c1a · confidence 0.94', tenant: 'primrose', risk: 'low' },
    { t: '13:58:02', actor: 'Rafa Q.',          kind: 'human', scope: 'ai.policy', action: 'modified',
      detail: 'Increased confidence threshold 0.92 → 0.95 · pol_conf v2→v3', tenant: '—', risk: 'high' },
    { t: '13:42:44', actor: 'System',           kind: 'system', scope: 'ai.policy', action: 'policy breach',
      detail: 'pol_ceiling · blocked auto-apply · amount £62,400 > £50,000', tenant: 'primrose', risk: 'med' },
    { t: '13:18:12', actor: 'Oliver Ikeda',     kind: 'human', scope: 'auth.session', action: 'signed in',
      detail: 'SSO · okta · ip 52.14.8.201 · London UK · device known', tenant: 'aonik', risk: 'low' },
    { t: '12:42:08', actor: 'Compliance Agent', kind: 'agent', scope: 'kyc.party', action: 'verified',
      detail: 'KYC re-check passed for Primrose Logistics Ltd · provider: Onfido', tenant: 'primrose', risk: 'low' },
    { t: '11:58:00', actor: 'Maria Gomez',      kind: 'human', scope: 'ai.policy', action: 'override',
      detail: 'Bypassed dual-control for payout_2318 · justification attached · requires review by Sec', tenant: 'brightwave', risk: 'high' },
    { t: '11:22:14', actor: 'System',           kind: 'system', scope: 'auth.mfa',  action: 'enforced',
      detail: 'Kill switch page · 2FA required · user: Rafa Q.', tenant: 'aonik', risk: 'med' },
    { t: '10:48:32', actor: 'Payout Router',    kind: 'agent', scope: 'payout.batch', action: 'settled',
      detail: 'Batch 38 payees · ₦41.2M · rails: Zenith · took 6.2s', tenant: 'primrose', risk: 'low' },
    { t: '09:14:02', actor: 'Alex Park',        kind: 'human', scope: 'users.role',  action: 'elevated',
      detail: 'Granted role=finance_admin to elena@aonik.co · expires 2026-05-01', tenant: 'aonik', risk: 'high' },
    { t: '08:42:14', actor: 'Ledger Agent',     kind: 'agent', scope: 'journal.entry', action: 'posted',
      detail: 'JE-1842 · Q3 depreciation · 412 lines · balanced', tenant: 'primrose', risk: 'low' },
    { t: '08:18:40', actor: 'System',           kind: 'system', scope: 'ai.model',    action: 'rollback',
      detail: 'Ledger Agent model rollback · v4.3 → v4.2 · reason: confidence drop', tenant: '—', risk: 'med' },
  ];

  const kindTone = {
    human:  { bg: '#3f41a018', fg: '#3f41a0', label: 'human' },
    agent:  { bg: '#055a6018', fg: '#055a60', label: 'agent' },
    system: { bg: 'var(--gray-200)', fg: 'var(--text-secondary)', label: 'system' },
  };
  const riskTone = {
    high: '#c44536', med: '#b4741e', low: 'var(--success, #1f7a5e)',
  };

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="Observability · Compliance & audit" title="Audit Log"
        subtitle="Immutable record of every sensitive action · 7-year retention · exportable for auditors"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="filter" size={12}/> Filters</button>
          <button className="btn btn-outline btn-sm"><Icon name="calendar" size={12}/> Last 24h</button>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export CSV</button>
          <button className="btn btn-primary btn-sm"><Icon name="verified" size={12}/> Verify chain</button>
        </>}/>

      {/* Chain-of-custody banner */}
      <div style={{
        background: 'linear-gradient(90deg, rgba(5,90,96,.06), rgba(5,90,96,.02))',
        border: '1px solid var(--border-light)',
        borderLeft: '3px solid var(--brand-primary)',
        borderRadius: 10, padding: '14px 18px',
        display: 'grid', gridTemplateColumns: 'auto 1fr auto auto auto', gap: 22, alignItems: 'center',
      }}>
        <div style={{
          width: 38, height: 38, borderRadius: 9,
          background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>
          <Icon name="verified" size={18}/>
        </div>
        <div>
          <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>Ledger chain verified</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>
            Every entry is cryptographically linked to the previous. Last verified 14 minutes ago by automated job.
          </div>
        </div>
        {[
          ['Entries',       '1,284,218'],
          ['Last sealed',   '08:00 UTC'],
          ['Root hash',     'a3f…49c2'],
        ].map(([k, v]) => (
          <div key={k} style={{ textAlign: 'right' }}>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{v}</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{k}</div>
          </div>
        ))}
      </div>

      {/* KPI row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        <KPI label="Events · 24h"        value="2,184" delta="+12%"  deltaTone="neutral" spark="0,12 20,14 40,13 60,15 80,16 100,14" sparkColor="#055a60"/>
        <KPI label="Human actions"       value="118"   delta="+8"    deltaTone="neutral" spark="0,10 20,12 40,11 60,13 80,14 100,12" sparkColor="#3f41a0"/>
        <KPI label="Policy overrides"    value="3"     delta="+2"    deltaTone="down"    spark="0,6 20,5 40,7 60,8 80,10 100,12"    sparkColor="#c44536"/>
        <KPI label="High-risk events"    value="8"     delta="+1"    deltaTone="down"    spark="0,8 20,7 40,9 60,9 80,10 100,11"    sparkColor="#b4741e"/>
      </div>

      {/* Event stream */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{
          display: 'grid', gridTemplateColumns: '88px 80px 200px 170px 1fr 110px 60px',
          padding: '10px 16px', gap: 12,
          background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)',
          fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.04em',
        }}>
          <div>TIME</div><div>KIND</div><div>ACTOR</div><div>SCOPE · ACTION</div><div>DETAIL</div><div>TENANT</div><div style={{ textAlign: 'center' }}>RISK</div>
        </div>
        {events.map((e, i) => {
          const kt = kindTone[e.kind];
          return (
            <div key={i} style={{
              display: 'grid', gridTemplateColumns: '88px 80px 200px 170px 1fr 110px 60px',
              padding: '13px 16px', gap: 12,
              borderTop: i === 0 ? 'none' : '1px solid var(--border-light)',
              alignItems: 'start',
            }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{e.t}</div>
              <div>
                <span style={{
                  fontSize: 10, padding: '2px 7px', borderRadius: 4,
                  background: kt.bg, color: kt.fg, fontWeight: 500, letterSpacing: '0.04em', textTransform: 'uppercase',
                }}>{kt.label}</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                {e.kind === 'human' && <Avatar name={e.actor} size={22} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>}
                {e.kind === 'agent' && <Avatar name={e.actor} size={22} color="#055a6018" textColor="#055a60"/>}
                {e.kind === 'system' && <div style={{
                  width: 22, height: 22, borderRadius: 5, background: 'var(--gray-200)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-secondary)',
                }}><Icon name="settings" size={11}/></div>}
                <span style={{ fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>{e.actor}</span>
              </div>
              <div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{e.scope}</div>
                <div style={{ fontSize: 12, color: 'var(--text-primary)', marginTop: 1 }}>{e.action}</div>
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{e.detail}</div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>{e.tenant}</div>
              <div style={{ textAlign: 'center' }}>
                <span style={{
                  display: 'inline-block', width: 8, height: 8, borderRadius: 999,
                  background: riskTone[e.risk],
                  boxShadow: `0 0 0 3px ${riskTone[e.risk]}22`,
                }}/>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

Object.assign(window, { ScreenObsLogs, ScreenObsAudit });
