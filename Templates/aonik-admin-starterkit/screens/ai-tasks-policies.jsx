// AI Tasks + Policies — AI section sub-pages

// ─── AI Tasks ───────────────────────────────────────────────────
function ScreenAiTasks() {
  const [filter, setFilter] = React.useState('All');

  const tasks = [
    { id: 'tsk_0194', title: 'Match bank txn 9f2c1a to open invoices', agent: 'Billing', agentColor: '#eb5c37',
      status: 'held',    conf: 0.82, ceiling: '£62,400 · above ceiling', wait: '4m',  tools: 4, owner: 'Maria Gomez' },
    { id: 'tsk_0193', title: 'Post Q3 depreciation on fleet assets',    agent: 'Ledger',  agentColor: '#055a60',
      status: 'running', conf: 0.96, ceiling: '£18,200',                 wait: 'now', tools: 3, owner: 'Aonik' },
    { id: 'tsk_0192', title: 'Apply INV-2041 to bank txn 9f2c1a',       agent: 'Billing', agentColor: '#eb5c37',
      status: 'running', conf: 0.94, ceiling: '£12,480',                 wait: 'now', tools: 5, owner: 'Aonik' },
    { id: 'tsk_0191', title: 'Settle NGN payouts batch · 38 payees',    agent: 'Payout',  agentColor: '#3ab795',
      status: 'held',    conf: 0.88, ceiling: '₦41.2M · dual-control',   wait: '11m', tools: 6, owner: 'Rafa Q.' },
    { id: 'tsk_0190', title: 'Reconcile FX buffer account · October',   agent: 'Ledger',  agentColor: '#055a60',
      status: 'error',   conf: null, ceiling: 'tool: read_timeout',      wait: '—',   tools: 4, owner: 'Aonik' },
    { id: 'tsk_0189', title: 'Draft dunning sequence · 14 overdue',     agent: 'Dunning', agentColor: '#5facbd',
      status: 'scheduled', conf: null, ceiling: 'runs Mon 09:00',         wait: '3d',  tools: 2, owner: 'Aonik' },
    { id: 'tsk_0188', title: 'KYC re-check · Primrose Logistics',        agent: 'Compliance', agentColor: '#7b76b6',
      status: 'done',    conf: 0.98, ceiling: 'verified',                 wait: '2h',  tools: 3, owner: 'Aonik' },
    { id: 'tsk_0187', title: 'Month-end close · book reversals',        agent: 'Close',    agentColor: '#0097a9',
      status: 'running', conf: 0.91, ceiling: '—',                        wait: 'now', tools: 8, owner: 'Aonik' },
    { id: 'tsk_0186', title: 'Classify 412 uncategorized expenses',      agent: 'Ledger',   agentColor: '#055a60',
      status: 'done',    conf: 0.93, ceiling: '—',                        wait: '1h',  tools: 2, owner: 'Aonik' },
  ];

  const tones = {
    running:   { bg: 'var(--brand-primary-10)',    fg: 'var(--brand-primary)', dot: 'var(--brand-primary)', label: 'running' },
    held:      { bg: '#b4741e18',                  fg: '#b4741e',              dot: '#b4741e',              label: 'held for review' },
    error:     { bg: '#c4453618',                  fg: '#c44536',              dot: '#c44536',              label: 'error' },
    scheduled: { bg: '#7b76b618',                  fg: '#7b76b6',              dot: '#7b76b6',              label: 'scheduled' },
    done:      { bg: '#1f7a5e18',                  fg: '#1f7a5e',              dot: '#1f7a5e',              label: 'completed' },
  };

  const counts = {
    All:       tasks.length,
    Running:   tasks.filter(t => t.status === 'running').length,
    Held:      tasks.filter(t => t.status === 'held').length,
    Error:     tasks.filter(t => t.status === 'error').length,
    Scheduled: tasks.filter(t => t.status === 'scheduled').length,
    Done:      tasks.filter(t => t.status === 'done').length,
  };
  const statusMap = { All: null, Running: 'running', Held: 'held', Error: 'error', Scheduled: 'scheduled', Done: 'done' };
  const filtered = filter === 'All' ? tasks : tasks.filter(t => t.status === statusMap[filter]);

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="AI · Tasks" title="Task Queue"
        subtitle="Every agent run in the last 24 hours · 3 awaiting review · 2 ceiling breaches"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="filter" size={12}/> Filters</button>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
          <button className="btn btn-primary btn-sm"><Icon name="play" size={12}/> Run task</button>
        </>}/>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 14 }}>
        <KPI label="In flight"         value="3"   delta="live"   deltaTone="neutral" spark="0,10 25,12 50,11 75,13 100,10" sparkColor="#055a60"/>
        <KPI label="Awaiting review"   value="2"   delta="+1"     deltaTone="down"    spark="0,8 25,6 50,10 75,12 100,14" sparkColor="#b4741e"/>
        <KPI label="Completed · 24h"   value="184" delta="+22"    deltaTone="up"      spark="0,16 25,14 50,12 75,10 100,7" sparkColor="#1f7a5e"/>
        <KPI label="Avg duration"      value="2.4s" delta="-0.3s" deltaTone="up"      spark="0,18 25,16 50,15 75,13 100,12" sparkColor="#3ab795"/>
        <KPI label="Error rate"        value="0.4%" delta="-0.1%" deltaTone="up"      spark="0,10 25,8 50,7 75,6 100,4" sparkColor="#c44536"/>
      </div>

      {/* Filter pills */}
      <div style={{ display: 'flex', gap: 6, alignItems: 'center', borderBottom: '1px solid var(--border-light)', paddingBottom: 12 }}>
        {['All', 'Running', 'Held', 'Error', 'Scheduled', 'Done'].map(f => {
          const active = filter === f;
          return (
            <button key={f} onClick={() => setFilter(f)}
              style={{
                border: '1px solid ' + (active ? 'var(--brand-primary)' : 'var(--border-light)'),
                background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
                color: active ? 'var(--brand-primary)' : 'var(--text-primary)',
                padding: '5px 12px', fontSize: 12, borderRadius: 999, cursor: 'pointer',
                fontWeight: active ? 600 : 500,
              }}>
              {f} <span style={{ opacity: 0.7, marginLeft: 4, fontFamily: 'var(--font-mono)', fontSize: 11 }}>{counts[f]}</span>
            </button>
          );
        })}
        <div style={{ flex: 1 }}/>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: 'var(--text-secondary)' }}>
          <Icon name="search" size={14} color="var(--text-tertiary)"/>
          <span>Search queue…</span>
        </div>
      </div>

      {/* Task table */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
          <thead>
            <tr style={{ color: 'var(--text-tertiary)', fontSize: 10.5, letterSpacing: '0.04em', textAlign: 'left', background: 'var(--surface-inset)' }}>
              <th style={{ padding: '10px 14px', fontWeight: 500 }}>TASK</th>
              <th style={{ padding: '10px 14px', fontWeight: 500 }}>AGENT</th>
              <th style={{ padding: '10px 14px', fontWeight: 500 }}>STATUS</th>
              <th style={{ padding: '10px 14px', fontWeight: 500, textAlign: 'right' }}>CONF</th>
              <th style={{ padding: '10px 14px', fontWeight: 500 }}>CONTEXT</th>
              <th style={{ padding: '10px 14px', fontWeight: 500, textAlign: 'right' }}>TOOLS</th>
              <th style={{ padding: '10px 14px', fontWeight: 500, textAlign: 'right' }}>AGE</th>
              <th style={{ padding: '10px 14px', fontWeight: 500 }}></th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(t => {
              const tn = tones[t.status];
              return (
                <tr key={t.id} style={{ borderTop: '1px solid var(--border-light)' }}>
                  <td style={{ padding: '12px 14px' }}>
                    <div style={{ fontSize: 12.5, color: 'var(--text-primary)', fontWeight: 500 }}>{t.title}</div>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{t.id} · by {t.owner}</div>
                  </td>
                  <td style={{ padding: '12px 14px' }}>
                    <span style={{
                      fontSize: 11.5, padding: '2px 8px', borderRadius: 999,
                      background: t.agentColor + '18', color: t.agentColor, fontWeight: 500,
                    }}>{t.agent}</span>
                  </td>
                  <td style={{ padding: '12px 14px' }}>
                    <span style={{
                      display: 'inline-flex', alignItems: 'center', gap: 6,
                      fontSize: 11, padding: '3px 9px', borderRadius: 999,
                      background: tn.bg, color: tn.fg, fontWeight: 500,
                    }}>
                      <span style={{ width: 6, height: 6, borderRadius: 999, background: tn.dot,
                        animation: t.status === 'running' ? 'pulse 1.6s infinite' : 'none' }}/>
                      {tn.label}
                    </span>
                  </td>
                  <td style={{ padding: '12px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11.5,
                    color: t.conf == null ? 'var(--text-tertiary)' : t.conf >= 0.95 ? 'var(--success, #1f7a5e)' : t.conf >= 0.9 ? 'var(--text-primary)' : '#b4741e',
                    fontWeight: 500,
                  }}>{t.conf == null ? '—' : t.conf.toFixed(2)}</td>
                  <td style={{ padding: '12px 14px', fontSize: 11.5, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>{t.ceiling}</td>
                  <td style={{ padding: '12px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{t.tools}</td>
                  <td style={{ padding: '12px 14px', textAlign: 'right', fontSize: 11.5, color: 'var(--text-tertiary)' }}>{t.wait}</td>
                  <td style={{ padding: '12px 14px', textAlign: 'right' }}>
                    {t.status === 'held' && <button className="btn btn-primary btn-sm"><Icon name="check" size={11}/> Review</button>}
                    {t.status === 'error' && <button className="btn btn-outline btn-sm"><Icon name="refresh" size={11}/> Retry</button>}
                    {t.status === 'running' && <button className="btn btn-ghost btn-sm"><Icon name="eye" size={11}/> Trace</button>}
                    {t.status === 'scheduled' && <button className="btn btn-ghost btn-sm"><Icon name="clock" size={11}/> Edit</button>}
                    {t.status === 'done' && <button className="btn btn-ghost btn-sm"><Icon name="arrowright" size={11}/></button>}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── AI Policies ─────────────────────────────────────────────────
function ScreenAiPolicies() {
  const [killSwitch, setKillSwitch] = React.useState(false);

  const policies = [
    { id: 'pol_ceiling',  name: 'Amount ceiling',       cat: 'Financial', sev: 'high',
      desc: 'Any single-action amount above the ceiling requires human confirmation before execution.',
      scope: 'All agents · Billing, Ledger, Payout, Close', value: '£50,000', enforcement: 'block',
      version: 'v4', triggered: 12, by: 'Maria Gomez', updated: '2d ago', on: true },
    { id: 'pol_dual',     name: 'Dual-control payouts', cat: 'Financial', sev: 'high',
      desc: 'Outbound payouts require two approvers from distinct roles before processing.',
      scope: 'Payout Router',                           value: '2 approvers', enforcement: 'block',
      version: 'v2', triggered: 4,  by: 'Rafa Q.',       updated: '5d ago', on: true },
    { id: 'pol_conf',     name: 'Confidence threshold', cat: 'Quality',   sev: 'med',
      desc: 'Auto-apply only when the agent reports confidence at or above the threshold.',
      scope: 'All agents',                              value: '≥ 0.95',    enforcement: 'hold',
      version: 'v3', triggered: 38, by: 'Aonik',        updated: '14h ago', on: true },
    { id: 'pol_fx',       name: 'FX band',              cat: 'Financial', sev: 'med',
      desc: 'Flag any FX execution whose rate deviates by more than the band from the reference mid.',
      scope: 'FX Agent · Payout Router',                value: '±2%',       enforcement: 'flag',
      version: 'v1', triggered: 7,  by: 'Maria Gomez', updated: '1w ago',  on: true },
    { id: 'pol_pii',      name: 'PII redaction',        cat: 'Privacy',   sev: 'high',
      desc: 'Strip PII (names, emails, numbers) from prompts before sending to models. Server-side only.',
      scope: 'All agents',                              value: 'On',        enforcement: 'block',
      version: 'v5', triggered: 0,  by: 'Sec Team',      updated: '3w ago', on: true },
    { id: 'pol_sanc',     name: 'Sanctions screen',     cat: 'Compliance', sev: 'high',
      desc: 'Cross-check every new counterparty against OFAC + UN + EU sanctions lists.',
      scope: 'Compliance Agent · onboarding',           value: 'Daily sync', enforcement: 'block',
      version: 'v2', triggered: 2,  by: 'Compliance',    updated: '6d ago',  on: true },
    { id: 'pol_retention',name: 'Audit retention',      cat: 'Compliance', sev: 'low',
      desc: 'Immutable log of every tool call. Available to auditors via read-only export.',
      scope: 'System-wide',                             value: '7 years',    enforcement: 'flag',
      version: 'v1', triggered: 0,  by: 'Compliance',    updated: '2mo ago', on: true },
    { id: 'pol_hours',    name: 'Business hours gate',   cat: 'Quality',   sev: 'low',
      desc: 'Outside 07:00–19:00 UTC, run tasks in dry-run mode unless flagged urgent.',
      scope: 'Billing · Dunning',                        value: '07:00–19:00', enforcement: 'hold',
      version: 'v1', triggered: 0,  by: 'Aonik',         updated: '—',       on: false },
  ];

  const catTone = {
    Financial:  '#eb5c37',
    Quality:    '#055a60',
    Privacy:    '#7b76b6',
    Compliance: '#3ab795',
  };
  const sevTone = {
    high: { bg: '#c4453618', fg: '#c44536', label: 'HIGH' },
    med:  { bg: '#b4741e18', fg: '#b4741e', label: 'MED'  },
    low:  { bg: 'var(--gray-200)', fg: 'var(--text-secondary)', label: 'LOW' },
  };
  const enfTone = {
    block: { bg: '#c4453614', fg: '#c44536', label: 'block' },
    hold:  { bg: '#b4741e14', fg: '#b4741e', label: 'hold'  },
    flag:  { bg: '#055a6014', fg: '#055a60', label: 'flag'  },
  };

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="AI · Governance" title="Policies"
        subtitle="Guardrails applied to every agent run · enforced before any tool executes"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="clock" size={12}/> History</button>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New policy</button>
        </>}/>

      {/* Kill-switch banner */}
      <div style={{
        background: killSwitch ? '#c4453612' : 'var(--surface)',
        border: '1px solid ' + (killSwitch ? '#c44536' : 'var(--border-light)'),
        borderRadius: 12, padding: '14px 18px',
        display: 'flex', alignItems: 'center', gap: 16,
      }}>
        <div style={{
          width: 40, height: 40, borderRadius: 10, flex: 'none',
          background: killSwitch ? '#c4453622' : 'var(--brand-primary-10)',
          color: killSwitch ? '#c44536' : 'var(--brand-primary)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>
          <Icon name="shield" size={20}/>
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>
            Global agent kill switch
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>
            {killSwitch
              ? 'All 7 agents suspended. In-flight runs completed; no new runs accepted.'
              : 'Pause every agent and reject all new runs. In-flight runs finish gracefully.'}
          </div>
        </div>
        <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>
          requires 2FA
        </div>
        <button onClick={() => setKillSwitch(!killSwitch)}
          className={'btn ' + (killSwitch ? 'btn-primary' : 'btn-outline') + ' btn-sm'}
          style={killSwitch ? {} : { borderColor: '#c44536', color: '#c44536' }}>
          {killSwitch ? 'Resume agents' : 'Engage kill switch'}
        </button>
      </div>

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        <KPI label="Active policies"   value="7"  delta="of 8"  deltaTone="neutral" spark="0,12 25,12 50,12 75,12 100,12" sparkColor="#055a60"/>
        <KPI label="Blocks · 7d"       value="16" delta="+4"    deltaTone="down"    spark="0,18 25,16 50,12 75,10 100,8"  sparkColor="#c44536"/>
        <KPI label="Holds · 7d"        value="38" delta="-6"    deltaTone="up"      spark="0,20 25,18 50,16 75,14 100,11" sparkColor="#b4741e"/>
        <KPI label="False-positive rate" value="2.1%" delta="-0.4%" deltaTone="up"  spark="0,10 25,9 50,8 75,7 100,6"    sparkColor="#3ab795"/>
      </div>

      {/* Policy list */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {policies.map(p => {
          const sv = sevTone[p.sev];
          const en = enfTone[p.enforcement];
          return (
            <div key={p.id} style={{
              background: 'var(--surface)', border: '1px solid var(--border-light)',
              borderRadius: 12, padding: '16px 20px',
              display: 'grid', gridTemplateColumns: 'auto 1fr auto auto auto',
              gap: 18, alignItems: 'center',
              opacity: p.on ? 1 : 0.62,
            }}>
              {/* Category tag */}
              <div style={{
                width: 4, height: 52, borderRadius: 4, background: catTone[p.cat],
              }}/>

              {/* Body */}
              <div style={{ minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                  <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{p.name}</div>
                  <span style={{
                    fontSize: 9.5, fontWeight: 700, letterSpacing: '0.04em',
                    padding: '2px 6px', borderRadius: 4,
                    background: sv.bg, color: sv.fg,
                  }}>{sv.label}</span>
                  <span style={{
                    fontSize: 10.5, padding: '2px 8px', borderRadius: 999,
                    background: catTone[p.cat] + '18', color: catTone[p.cat], fontWeight: 500,
                  }}>{p.cat}</span>
                  <span style={{
                    fontSize: 10, padding: '1px 6px', borderRadius: 4,
                    background: en.bg, color: en.fg, fontFamily: 'var(--font-mono)', fontWeight: 500,
                  }}>{en.label}</span>
                </div>
                <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: 4 }}>{p.desc}</div>
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', display: 'flex', gap: 14 }}>
                  <span>scope: {p.scope}</span>
                  <span>{p.version} · updated {p.updated} by {p.by}</span>
                </div>
              </div>

              {/* Value */}
              <div style={{ textAlign: 'right', minWidth: 100 }}>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 600, color: 'var(--text-primary)' }}>{p.value}</div>
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>threshold</div>
              </div>

              {/* Triggered */}
              <div style={{ textAlign: 'right', minWidth: 80 }}>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 500, color: p.triggered > 0 ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{p.triggered}</div>
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>triggered · 7d</div>
              </div>

              {/* Toggle + actions */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span style={{
                  width: 30, height: 16, borderRadius: 999,
                  background: p.on ? 'var(--brand-primary)' : 'var(--gray-200)',
                  position: 'relative', cursor: 'pointer', flex: 'none',
                }}>
                  <span style={{
                    position: 'absolute', top: 1, left: p.on ? 15 : 1,
                    width: 14, height: 14, borderRadius: 999, background: '#fff',
                    boxShadow: '0 1px 2px rgba(0,0,0,.18)', transition: 'left .15s',
                  }}/>
                </span>
                <button className="btn btn-ghost btn-sm" style={{ padding: '4px 8px' }}>
                  <Icon name="settings" size={13}/>
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

Object.assign(window, { ScreenAiTasks, ScreenAiPolicies });
