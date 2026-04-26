// AONIK Admin UI — Agent Command Center screen
// Multi-agent oversight: roster, live traces, performance stats.

function AgentCenterScreen() {
  const agents = [
    { id: 'ledger',   name: 'Ledger Agent',      color: '#055a60', role: 'Books + journal entries',    runs: 142, conf: 0.96, state: 'idle',    last: '2m ago' },
    { id: 'billing',  name: 'Billing Agent',     color: '#eb5c37', role: 'Invoices + matching',         runs: 318, conf: 0.94, state: 'running', last: 'now'    },
    { id: 'payout',   name: 'Payout Router',     color: '#3ab795', role: 'Rails + FX + partners',       runs:  84, conf: 0.91, state: 'idle',    last: '12m ago'},
    { id: 'compl',    name: 'Compliance Agent',  color: '#7b76b6', role: 'KYC + sanctions + audit',     runs:  42, conf: 0.98, state: 'idle',    last: '1h ago' },
    { id: 'close',    name: 'Close Agent',       color: '#0097a9', role: 'Month-end close orchestrator',runs:   6, conf: 0.89, state: 'running', last: 'now'    },
    { id: 'dunning',  name: 'Dunning Agent',     color: '#5facbd', role: 'Overdue outreach',            runs:  28, conf: 0.87, state: 'paused',  last: '3h ago' },
  ];

  const stateDot = (s) => ({
    running: { c: 'var(--success)',  t: 'Running'  },
    idle:    { c: 'var(--gray-400)', t: 'Idle'     },
    paused:  { c: 'var(--warning)',  t: 'Paused'   },
  }[s]);

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div>
          <div className="eyebrow">Agents</div>
          <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 24, fontWeight: 700, marginTop: 6, letterSpacing: '-0.01em' }}>Agent Command Center</h1>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 4 }}>
            7 agents · 620 ops today · 0.93 avg confidence · 3 awaiting review
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-outline btn-sm"><Icon name="refresh" size={14}/> Re-sync tools</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={14}/> New agent</button>
        </div>
      </div>

      {/* KPIs */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
        <KPI label="Ops today"          value="620"   delta="+18%" deltaTone="up"
             spark="0,22 10,20 20,18 30,16 40,17 50,14 60,12 70,10 80,9 90,6 100,5" sparkColor="#055a60"/>
        <KPI label="Avg confidence"     value="0.93"  delta="+0.02" deltaTone="up"
             spark="0,18 15,16 30,15 45,14 60,13 75,12 90,11 100,10" sparkColor="#3ab795"/>
        <KPI label="Auto-applied"       value="74%"   delta="+3%"  deltaTone="up"
             spark="0,20 15,18 30,15 45,16 60,12 75,10 90,8 100,7" sparkColor="#7b76b6"/>
        <KPI label="Human interventions" value="12"   delta="-4"   deltaTone="up"
             spark="0,10 15,12 30,10 45,14 60,12 75,15 90,18 100,20" sparkColor="#eb5c37"/>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20 }}>
        {/* Roster */}
        <Card title="Agent roster" subtitle="Domain agents routed by Orchestrator">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
            {agents.map(a => {
              const st = stateDot(a.state);
              return (
                <div key={a.id} style={{
                  display: 'grid', gridTemplateColumns: 'auto 1fr auto auto', gap: 14,
                  alignItems: 'center', padding: '12px 14px',
                  background: 'var(--surface)', border: '1px solid var(--border-light)',
                  borderRadius: 10,
                }}>
                  <Avatar name={a.name} size={34} color={a.color + '22'} textColor={a.color}/>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{a.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 1 }}>{a.role}</div>
                  </div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>
                    <div style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{a.runs} runs</div>
                    <div>conf {a.conf.toFixed(2)}</div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ width: 8, height: 8, borderRadius: 999, background: st.c }}/>
                    <span style={{ fontSize: 11, color: 'var(--text-secondary)', minWidth: 52 }}>{st.t}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </Card>

        {/* Live trace */}
        <Card
          title="Live trace · Billing Agent"
          subtitle="Run ra_88421 · matching INV-2041 ↔ bank_txn_9f2c1a"
          action={<Pill tone="tint" dot>running</Pill>}
        >
          <div style={{ display: 'flex', flexDirection: 'column', marginTop: 4 }}>
            {[
              { n: 1, t: 'search_invoices',        d: 'filter: status=open, ref~2041',             ms: '142ms', state: 'done'   },
              { n: 2, t: 'list_bank_transactions', d: 'window: 2024-11-14 → 2024-11-21, amt ≈ 12480', ms: '318ms', state: 'done'   },
              { n: 3, t: 'match_invoice_to_txn',   d: 'score: 0.94 · ref + amount + counterparty', ms: '211ms', state: 'done'   },
              { n: 4, t: 'draft_journal_entry',    d: 'composing balanced debit/credit…',           ms: '—',     state: 'active' },
              { n: 5, t: 'propose_apply',          d: 'awaiting human confirmation',                ms: '—',     state: 'pending'},
            ].map((s, i) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: '24px 1fr auto', gap: 12, alignItems: 'center',
                padding: '12px 4px',
                borderBottom: i < 4 ? '1px solid var(--border-light)' : 'none',
                background: s.state === 'active' ? 'var(--brand-primary-10)' : 'transparent',
                margin: s.state === 'active' ? '0 -12px' : '0',
                padding: s.state === 'active' ? '12px 12px' : '12px 4px',
                borderRadius: s.state === 'active' ? 8 : 0,
              }}>
                <span style={{
                  width: 22, height: 22, borderRadius: 999,
                  background: s.state === 'done' ? 'var(--success)' : s.state === 'active' ? 'var(--brand-primary)' : 'var(--gray-200)',
                  color: s.state === 'pending' ? 'var(--gray-500)' : '#fff',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 10, fontWeight: 600, fontFamily: 'var(--font-mono)',
                }}>
                  {s.state === 'done' ? '✓' : s.n}
                </span>
                <div>
                  <div style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500, fontFamily: 'var(--font-mono)' }}>{s.t}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2, fontFamily: 'var(--font-mono)' }}
                       className={s.state === 'active' ? 'shimmer' : ''}>{s.d}</div>
                </div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{s.ms}</div>
              </div>
            ))}
          </div>
        </Card>
      </div>

      {/* Policy guardrails */}
      <Card title="Policies in force" subtitle="Guardrails the Orchestrator applies to every run">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginTop: 4 }}>
          {[
            { t: 'Confidence threshold',     d: 'Auto-apply only when agent confidence ≥ 0.95',  v: '0.95' },
            { t: 'Amount ceiling',           d: 'Human review required above $50,000',           v: '$50K' },
            { t: 'Dual-control payouts',     d: 'Two approvers required for outbound payouts',   v: 'On'   },
            { t: 'FX policy band',           d: 'Flag if rate deviates > 2% from reference',     v: '2%'   },
            { t: 'PII redaction',            d: 'Customer PII stripped from all agent prompts',  v: 'On'   },
            { t: 'Audit log retention',      d: 'Immutable log of every tool call · 7 years',    v: '7y'   },
          ].map((p, i) => (
            <div key={i} style={{
              background: 'var(--surface)', border: '1px solid var(--border-light)',
              borderRadius: 10, padding: 14,
              display: 'flex', flexDirection: 'column', gap: 6,
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{p.t}</div>
                <span style={{
                  fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600,
                  color: 'var(--brand-primary)', background: 'var(--brand-primary-10)',
                  padding: '1px 8px', borderRadius: 999,
                }}>{p.v}</span>
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{p.d}</div>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}

Object.assign(window, { AgentCenterScreen });
