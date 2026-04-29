// Agent Workflows — registry of reusable, agent-runnable procedures.
//
// Mental model:
//   • A *workflow* is an ordered sequence of steps an agent (or chain of
//     agents) executes when a trigger fires. Same workflow can be wired to
//     many triggers and many agents.
//   • Steps are typed (tool / sub-agent / decision / human-approval / wait).
//   • Each workflow shows a horizontal step rail you can scan at a glance
//     plus liveness data (last run, success rate, runs today).
//
// Layout: two-pane.
//   Left: PageHeader + KPIs + filter bar + scrollable workflow list (cards
//         with the inline step rail).
//   Right: detail rail — the selected workflow's full step diagram, the
//          agents that own it, the triggers wired to it, recent runs.

// ─── Step type → presentation ────────────────────────────────────
const STEP_KIND = {
  tool:      { icon: 'wrench',   label: 'Tool call',      tint: '#055a60' },
  agent:     { icon: 'sparkles', label: 'Sub-agent',      tint: '#7b76b6' },
  decision:  { icon: 'gitfork',  label: 'Decision',       tint: '#b4741e' },
  human:     { icon: 'users',    label: 'Human approval', tint: '#c44536' },
  wait:      { icon: 'clock',    label: 'Wait',           tint: '#5facbd' },
  end:       { icon: 'check',    label: 'End',            tint: '#1f7a5e' },
  start:     { icon: 'play',     label: 'Start',          tint: '#055a60' },
  notify:    { icon: 'send',     label: 'Notify',         tint: '#3ab795' },
  emit:      { icon: 'bolt',     label: 'Emit event',     tint: '#d4a843' },
  ledger:    { icon: 'columns',  label: 'Post to ledger', tint: '#055a60' },
};

// Fallback icons not in the kit; resolve with a graceful fallback.
function StepIcon({ kind, size = 11 }) {
  const ic = STEP_KIND[kind]?.icon || 'bolt';
  return <Icon name={ic} size={size}/>;
}

// ─── Workflow registry ───────────────────────────────────────────
const WORKFLOWS = [
  {
    id: 'match_and_apply',
    name: 'Match & apply',
    desc: 'Reconcile invoice → bank txn, draft an entry, surface it for review when over policy ceiling.',
    owner: 'Billing Agent', ownerColor: '#eb5c37',
    contributors: ['Ledger Agent', 'Compliance Agent'],
    triggers: 4, runsToday: 318, success: 0.962, avgMs: 2400,
    state: 'active', version: 'v1.4', updated: '3d ago',
    autoRetry: true,
    steps: [
      { kind: 'start',   label: 'On bank txn' },
      { kind: 'tool',    label: 'search_invoices', meta: 'amount ± £0.01' },
      { kind: 'agent',   label: 'Billing · score match', meta: 'confidence ≥ 0.85' },
      { kind: 'decision',label: 'Above ceiling?', meta: '£50,000' },
      { kind: 'human',   label: 'Treasury approval', meta: 'if breached' },
      { kind: 'ledger',  label: 'Draft journal entry', meta: 'AR · 1200' },
      { kind: 'notify',  label: 'Notify customer', meta: 'receipt email' },
      { kind: 'end',     label: 'Match applied' },
    ],
  },
  {
    id: 'sweep_unmatched',
    name: 'Sweep unmatched',
    desc: 'Hourly retry pass for invoices that fell through earlier. Loosens fuzzy matching as time passes.',
    owner: 'Billing Agent', ownerColor: '#eb5c37',
    contributors: [],
    triggers: 1, runsToday: 24, success: 0.71, avgMs: 18200,
    state: 'active', version: 'v0.9', updated: '1w ago',
    autoRetry: false,
    steps: [
      { kind: 'start',   label: 'Hourly tick' },
      { kind: 'tool',    label: 'list_open_invoices', meta: 'aged > 24h' },
      { kind: 'agent',   label: 'Billing · fuzzy match', meta: 'tier escalates' },
      { kind: 'decision',label: 'Match found?' },
      { kind: 'tool',    label: 'apply_match',  meta: 'auto-apply' },
      { kind: 'end',     label: 'Sweep complete' },
    ],
  },
  {
    id: 'dunning_cadence',
    name: 'Dunning cadence',
    desc: 'Send overdue reminders on a per-customer rhythm. Escalates tone every 7 days, hands to phone after day 21.',
    owner: 'Dunning Agent', ownerColor: '#5facbd',
    contributors: ['Compliance Agent'],
    triggers: 2, runsToday: 14, success: 0.88, avgMs: 4100,
    state: 'paused', version: 'v2.0', updated: '11d ago',
    autoRetry: true,
    steps: [
      { kind: 'start',   label: 'Invoice overdue' },
      { kind: 'tool',    label: 'lookup_customer', meta: 'segment + tier' },
      { kind: 'decision',label: 'Days overdue', meta: '7 / 14 / 21' },
      { kind: 'agent',   label: 'Dunning · compose', meta: 'tone keyed to days' },
      { kind: 'human',   label: 'Approve outbound', meta: 'tier-1 only' },
      { kind: 'notify',  label: 'Send email',  meta: 'or SMS' },
      { kind: 'wait',    label: 'Wait 7 days', meta: 'or until paid' },
      { kind: 'end',     label: 'Cadence step done' },
    ],
  },
  {
    id: 'forward_quote',
    name: 'Forward quote',
    desc: 'Quote a forward FX contract for cross-border invoices. Fetches rate fixings, calculates markup, drafts the contract.',
    owner: 'FX Agent', ownerColor: '#3ab795',
    contributors: ['Compliance Agent'],
    triggers: 2, runsToday: 8, success: 0.99, avgMs: 1800,
    state: 'active', version: 'v1.1', updated: '6d ago',
    autoRetry: false,
    steps: [
      { kind: 'start',   label: 'Cross-border invoice' },
      { kind: 'tool',    label: 'fetch_fx_fix', meta: 'CME · WMR' },
      { kind: 'agent',   label: 'FX · price quote', meta: '+spread' },
      { kind: 'tool',    label: 'draft_forward_contract' },
      { kind: 'human',   label: 'Counterparty signs' },
      { kind: 'end',     label: 'Quote delivered' },
    ],
  },
  {
    id: 'kyc_recheck',
    name: 'KYC re-check',
    desc: 'Re-screen counterparty against sanctions and PEP lists. Triggered on a 90-day rotation or risk-flag changes.',
    owner: 'Compliance Agent', ownerColor: '#7b76b6',
    contributors: [],
    triggers: 3, runsToday: 6, success: 0.99, avgMs: 920,
    state: 'active', version: 'v3.2', updated: '2d ago',
    autoRetry: true,
    steps: [
      { kind: 'start',   label: 'On schedule · or flag' },
      { kind: 'tool',    label: 'fetch_sanctions_lists' },
      { kind: 'tool',    label: 'screen_counterparty', meta: 'OFAC · UN · EU · UK' },
      { kind: 'decision',label: 'Hit?' },
      { kind: 'human',   label: 'Compliance review', meta: 'if hit' },
      { kind: 'emit',    label: 'compliance.recheck.done' },
      { kind: 'end',     label: 'Cleared' },
    ],
  },
  {
    id: 'monthly_close',
    name: 'Month-end close',
    desc: 'Sequences the close playbook end-to-end. Accruals, FX revaluation, intercompany eliminations, sign-off.',
    owner: 'Close Agent', ownerColor: '#0097a9',
    contributors: ['Ledger Agent', 'FX Agent'],
    triggers: 1, runsToday: 0, success: 0.93, avgMs: 384000,
    state: 'active', version: 'v2.7', updated: '17d ago',
    autoRetry: false,
    steps: [
      { kind: 'start',   label: 'Last business day' },
      { kind: 'agent',   label: 'Ledger · post accruals' },
      { kind: 'agent',   label: 'FX · revalue balances' },
      { kind: 'agent',   label: 'Ledger · intercompany' },
      { kind: 'human',   label: 'Controller sign-off', meta: 'mandatory' },
      { kind: 'tool',    label: 'lock_period' },
      { kind: 'notify',  label: 'Close package · email' },
      { kind: 'end',     label: 'Period closed' },
    ],
  },
  {
    id: 'spend_anomaly',
    name: 'Spend anomaly review',
    desc: 'When a spend category exceeds its 30-day rolling average by more than σ, surface a narrative for review.',
    owner: 'Insights Agent', ownerColor: '#d4a843',
    contributors: [],
    triggers: 1, runsToday: 3, success: 0.85, avgMs: 5400,
    state: 'draft', version: 'v0.3', updated: '4h ago',
    autoRetry: false,
    steps: [
      { kind: 'start',   label: 'Daily roll-up' },
      { kind: 'tool',    label: 'aggregate_spend', meta: 'by category' },
      { kind: 'decision',label: 'Anomaly?', meta: '> 2σ' },
      { kind: 'agent',   label: 'Insights · narrative' },
      { kind: 'notify',  label: 'Post to My Space' },
      { kind: 'end',     label: 'Review filed' },
    ],
  },
];

// ─── Step rail (compact, horizontal) ─────────────────────────────
function StepRail({ steps, dense = false }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 0, flexWrap: 'nowrap', overflow: 'hidden' }}>
      {steps.map((s, i) => {
        const k = STEP_KIND[s.kind] || STEP_KIND.tool;
        const last = i === steps.length - 1;
        return (
          <React.Fragment key={i}>
            <div style={{
              display: 'inline-flex', alignItems: 'center', gap: 6,
              padding: dense ? '4px 8px' : '6px 10px',
              background: k.tint + '14', color: k.tint,
              border: '1px solid ' + k.tint + '30',
              borderRadius: 6, flex: 'none', whiteSpace: 'nowrap',
            }}>
              <StepIcon kind={s.kind} size={dense ? 10 : 11}/>
              <span style={{
                fontSize: dense ? 10.5 : 11.5, fontWeight: 500,
                color: 'var(--text-primary)',
                fontFamily: s.kind === 'tool' ? 'var(--font-mono)' : 'inherit',
              }}>{s.label}</span>
            </div>
            {!last && (
              <span style={{
                width: dense ? 12 : 16, height: 1,
                background: 'var(--border-medium, #c8cdd3)', flex: 'none',
                position: 'relative',
              }}>
                <span style={{
                  position: 'absolute', right: -3, top: -2,
                  width: 0, height: 0,
                  borderLeft: '4px solid var(--border-medium, #c8cdd3)',
                  borderTop: '3px solid transparent', borderBottom: '3px solid transparent',
                }}/>
              </span>
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
}

// ─── Workflow list card ──────────────────────────────────────────
function WorkflowCard({ wf, active, onClick }) {
  const stateTone = {
    active: { c: 'var(--success, #1f7a5e)', label: 'active', dot: true  },
    paused: { c: '#b4741e',                  label: 'paused', dot: false },
    draft:  { c: 'var(--text-tertiary)',     label: 'draft',  dot: false },
  }[wf.state];

  return (
    <div onClick={onClick} style={{
      padding: '16px 18px', cursor: 'pointer',
      background: active ? 'var(--surface)' : 'var(--surface)',
      border: '1px solid ' + (active ? 'var(--brand-primary)' : 'var(--border-light)'),
      boxShadow: active ? '0 0 0 3px var(--brand-primary-10)' : 'none',
      borderRadius: 10, transition: 'border-color .15s, box-shadow .15s',
      display: 'flex', flexDirection: 'column', gap: 12,
    }}>
      {/* Header row */}
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{wf.name}</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{wf.id}</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', padding: '1px 6px', background: 'var(--surface-inset)', borderRadius: 3 }}>{wf.version}</span>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5 }}>{wf.desc}</div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flex: 'none' }}>
          <span style={{
            display: 'inline-flex', alignItems: 'center', gap: 5,
            fontSize: 10.5, padding: '3px 9px', borderRadius: 999, fontWeight: 500,
            color: stateTone.c, background: stateTone.c + '18',
          }}>
            <span style={{
              width: 6, height: 6, borderRadius: 999, background: stateTone.c,
              animation: stateTone.dot ? 'pulse 1.6s infinite' : 'none',
            }}/>
            {stateTone.label}
          </span>
        </div>
      </div>

      {/* Step rail */}
      <StepRail steps={wf.steps}/>

      {/* Footer row — owner, stats */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, fontSize: 11.5, color: 'var(--text-secondary)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ width: 8, height: 8, borderRadius: 999, background: wf.ownerColor, flex: 'none' }}/>
          <span style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{wf.owner}</span>
          {wf.contributors.length > 0 && (
            <span style={{ color: 'var(--text-tertiary)' }}>· +{wf.contributors.length}</span>
          )}
        </div>
        <span style={{ width: 1, height: 14, background: 'var(--border-light)' }}/>
        <div><Icon name="bolt" size={11}/> <span style={{ fontFamily: 'var(--font-mono)' }}>{wf.triggers}</span> trigger{wf.triggers === 1 ? '' : 's'}</div>
        <div><Icon name="play" size={11}/> <span style={{ fontFamily: 'var(--font-mono)' }}>{wf.runsToday}</span> runs today</div>
        <div>Success <span style={{ fontFamily: 'var(--font-mono)', color: wf.success >= 0.95 ? 'var(--success, #1f7a5e)' : wf.success >= 0.85 ? 'var(--text-primary)' : '#b4741e', fontWeight: 500 }}>{(wf.success * 100).toFixed(1)}%</span></div>
        <div>Avg <span style={{ fontFamily: 'var(--font-mono)' }}>{formatDuration(wf.avgMs)}</span></div>
        <div style={{ flex: 1 }}/>
        <span style={{ color: 'var(--text-tertiary)', fontSize: 11 }}>updated {wf.updated}</span>
      </div>
    </div>
  );
}

function formatDuration(ms) {
  if (ms < 1000) return ms + 'ms';
  if (ms < 60000) return (ms / 1000).toFixed(1) + 's';
  if (ms < 3600000) return Math.round(ms / 60000) + 'm';
  return (ms / 3600000).toFixed(1) + 'h';
}

// ─── Detail rail (right pane) ────────────────────────────────────
function WorkflowDetail({ wf }) {
  // Synthesize plausible recent runs for the selected workflow
  const recent = [
    { id: 'run_8421', when: '2m ago',  status: 'ok',     ms: wf.avgMs - 200, by: 'auto · banking.transaction.received' },
    { id: 'run_8418', when: '14m ago', status: 'ok',     ms: wf.avgMs + 80,  by: 'auto · banking.transaction.received' },
    { id: 'run_8412', when: '38m ago', status: 'review', ms: wf.avgMs * 3.2, by: 'held · over ceiling' },
    { id: 'run_8407', when: '1h ago',  status: 'ok',     ms: wf.avgMs - 50,  by: 'auto' },
    { id: 'run_8402', when: '2h ago',  status: 'fail',   ms: wf.avgMs * 0.4, by: 'tool: read_timeout' },
    { id: 'run_8395', when: '3h ago',  status: 'ok',     ms: wf.avgMs + 30,  by: 'auto' },
  ];
  const statusTone = {
    ok:     { c: 'var(--success, #1f7a5e)', label: 'ok' },
    review: { c: '#b4741e',                  label: 'held' },
    fail:   { c: '#c44536',                  label: 'fail' },
  };

  return (
    <div style={{
      width: 420, flex: 'none',
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 10, overflow: 'hidden',
      display: 'flex', flexDirection: 'column',
      maxHeight: 'calc(100vh - 200px)',
    }}>
      {/* Header */}
      <div style={{ padding: 18, borderBottom: '1px solid var(--border-light)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', padding: '2px 7px', background: 'var(--surface-inset)', borderRadius: 4 }}>WORKFLOW</span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{wf.id}</span>
        </div>
        <div style={{ fontSize: 18, fontWeight: 600, color: 'var(--text-primary)' }}>{wf.name}</div>
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 6, lineHeight: 1.5 }}>{wf.desc}</div>

        <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
          <button className="btn btn-primary btn-sm"><Icon name="play" size={11}/> Run now</button>
          <button className="btn btn-outline btn-sm"><Icon name="edit" size={11}/> Open editor</button>
          <button className="hover-halo" style={{ padding: 6, marginLeft: 'auto' }}>
            <Icon name="more" size={12} color="var(--text-secondary)"/>
          </button>
        </div>
      </div>

      <div style={{ flex: 1, overflow: 'auto', padding: 18, display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Vertical step diagram */}
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 10 }}>
            Steps · {wf.steps.length}
          </div>
          <div style={{ position: 'relative' }}>
            {wf.steps.map((s, i) => {
              const k = STEP_KIND[s.kind] || STEP_KIND.tool;
              const last = i === wf.steps.length - 1;
              return (
                <div key={i} style={{ position: 'relative', paddingLeft: 36, paddingBottom: last ? 0 : 14 }}>
                  {/* connector line */}
                  {!last && (
                    <span style={{
                      position: 'absolute', left: 13, top: 26, bottom: 0,
                      width: 1.5, background: 'var(--border-medium, #c8cdd3)',
                    }}/>
                  )}
                  {/* node */}
                  <span style={{
                    position: 'absolute', left: 0, top: 2,
                    width: 28, height: 28, borderRadius: 7,
                    background: k.tint + '18', color: k.tint,
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                    border: '1px solid ' + k.tint + '40',
                  }}>
                    <StepIcon kind={s.kind} size={12}/>
                  </span>
                  <div style={{
                    display: 'flex', alignItems: 'center', gap: 8, height: 18,
                  }}>
                    <span style={{
                      fontSize: 9.5, fontWeight: 600, color: k.tint,
                      letterSpacing: '0.06em', textTransform: 'uppercase',
                    }}>{k.label}</span>
                  </div>
                  <div style={{
                    fontSize: 12.5, color: 'var(--text-primary)', fontWeight: 500,
                    fontFamily: s.kind === 'tool' ? 'var(--font-mono)' : 'inherit',
                    marginTop: 2,
                  }}>{s.label}</div>
                  {s.meta && (
                    <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 1 }}>{s.meta}</div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Owner + contributors */}
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>
            Owned by
          </div>
          <div style={{
            padding: '10px 12px', background: 'var(--surface-inset)',
            border: '1px solid var(--border-light)', borderRadius: 8,
            display: 'flex', alignItems: 'center', gap: 10,
          }}>
            <span style={{
              width: 28, height: 28, borderRadius: 8, flex: 'none',
              background: wf.ownerColor + '20', color: wf.ownerColor,
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              fontWeight: 700, fontSize: 11, letterSpacing: '0.04em',
            }}>{wf.owner.split(' ').map(w => w[0]).join('').slice(0,2)}</span>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{wf.owner}</div>
              {wf.contributors.length > 0 && (
                <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>
                  with {wf.contributors.join(' · ')}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Stats */}
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>
            Performance · last 24h
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
            {[
              { l: 'Runs',    v: wf.runsToday },
              { l: 'Success', v: (wf.success * 100).toFixed(1) + '%' },
              { l: 'Avg',     v: formatDuration(wf.avgMs) },
              { l: 'p95',     v: formatDuration(wf.avgMs * 1.8) },
            ].map(s => (
              <div key={s.l} style={{
                padding: '10px 12px', background: 'var(--surface-inset)',
                border: '1px solid var(--border-light)', borderRadius: 8,
              }}>
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.04em', textTransform: 'uppercase' }}>{s.l}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 600, color: 'var(--text-primary)', marginTop: 2 }}>{s.v}</div>
              </div>
            ))}
          </div>
        </div>

        {/* Recent runs */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', marginBottom: 8 }}>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase' }}>Recent runs</div>
            <div style={{ flex: 1 }}/>
            <a style={{ fontSize: 11, color: 'var(--brand-primary)', fontWeight: 500 }}>Open in Traces →</a>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {recent.map(r => {
              const s = statusTone[r.status];
              return (
                <div key={r.id} style={{
                  display: 'grid', gridTemplateColumns: '60px 1fr auto auto', gap: 10, alignItems: 'center',
                  padding: '8px 10px',
                  border: '1px solid var(--border-light)', borderRadius: 6,
                  background: 'var(--surface)',
                }}>
                  <span style={{
                    display: 'inline-flex', alignItems: 'center', gap: 5,
                    fontSize: 10.5, color: s.c, fontWeight: 500,
                  }}>
                    <span style={{ width: 6, height: 6, borderRadius: 999, background: s.c }}/>
                    {s.label}
                  </span>
                  <span style={{ fontSize: 11, color: 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.by}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{formatDuration(Math.max(80, r.ms))}</span>
                  <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{r.when}</span>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Top-level screen ────────────────────────────────────────────
function ScreenWorkflows() {
  const [selectedId, setSelectedId] = React.useState('match_and_apply');
  const [filter, setFilter] = React.useState('All');
  const [sort, setSort] = React.useState('Most run');

  const counts = {
    All:     WORKFLOWS.length,
    Active:  WORKFLOWS.filter(w => w.state === 'active').length,
    Paused:  WORKFLOWS.filter(w => w.state === 'paused').length,
    Draft:   WORKFLOWS.filter(w => w.state === 'draft').length,
  };
  const filterMap = { All: null, Active: 'active', Paused: 'paused', Draft: 'draft' };
  let list = filter === 'All' ? WORKFLOWS : WORKFLOWS.filter(w => w.state === filterMap[filter]);
  if (sort === 'Most run')      list = [...list].sort((a, b) => b.runsToday - a.runsToday);
  else if (sort === 'Recent')   list = [...list].sort((a, b) => a.updated.localeCompare(b.updated));
  else if (sort === 'Success')  list = [...list].sort((a, b) => b.success - a.success);

  const selected = WORKFLOWS.find(w => w.id === selectedId) || WORKFLOWS[0];

  // KPI summary
  const totalRuns = WORKFLOWS.reduce((acc, w) => acc + w.runsToday, 0);
  const wAvgSuccess = (WORKFLOWS.reduce((acc, w) => acc + w.success * w.runsToday, 0) / Math.max(1, totalRuns));
  const totalTriggers = WORKFLOWS.reduce((acc, w) => acc + w.triggers, 0);

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="AI · Workflows"
        title="Agent Workflows"
        subtitle="Reusable procedures that agents run when triggered. Wire them to events, schedules, or human actions."
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="search" size={12}/> Browse library</button>
          <button className="btn btn-outline btn-sm"><Icon name="upload" size={12}/> Import</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New workflow</button>
        </>}/>

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        <KPI label="Workflows · active" value={counts.Active} delta={`${counts.Draft} draft`} deltaTone="neutral"
             spark="0,12 25,10 50,11 75,9 100,8" sparkColor="#055a60"/>
        <KPI label="Runs · today" value={totalRuns.toLocaleString()} delta="+218 vs. yesterday" deltaTone="up"
             spark="0,18 25,15 50,12 75,10 100,7" sparkColor="#1f7a5e"/>
        <KPI label="Wired triggers" value={totalTriggers} delta={`across ${WORKFLOWS.length} workflows`} deltaTone="neutral"
             spark="0,11 25,11 50,10 75,9 100,9" sparkColor="#3ab795"/>
        <KPI label="Weighted success" value={(wAvgSuccess * 100).toFixed(1) + '%'} delta="+0.4%" deltaTone="up"
             spark="0,8 25,7 50,6 75,5 100,4" sparkColor="#1f7a5e"/>
      </div>

      {/* Filter + sort bar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6,
        borderBottom: '1px solid var(--border-light)', paddingBottom: 12,
      }}>
        {['All', 'Active', 'Paused', 'Draft'].map(f => {
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
        <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>Sort by</span>
        <select value={sort} onChange={e => setSort(e.target.value)}
          style={{
            fontSize: 12, padding: '4px 8px',
            background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6,
          }}>
          <option>Most run</option>
          <option>Recent</option>
          <option>Success</option>
        </select>
        <button className="btn btn-ghost btn-sm" style={{ marginLeft: 4 }}><Icon name="search" size={12}/> Filter…</button>
      </div>

      {/* Two-pane: list + detail */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 420px', gap: 16, alignItems: 'flex-start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10, minWidth: 0 }}>
          {list.map(wf => (
            <WorkflowCard key={wf.id} wf={wf} active={wf.id === selectedId} onClick={() => setSelectedId(wf.id)}/>
          ))}
          {list.length === 0 && (
            <div style={{
              padding: 40, textAlign: 'center',
              background: 'var(--surface-inset)', border: '1px dashed var(--border-light)', borderRadius: 10,
              color: 'var(--text-tertiary)', fontSize: 12.5,
            }}>
              No workflows in this state yet.
            </div>
          )}
        </div>
        <WorkflowDetail wf={selected}/>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenWorkflows });
