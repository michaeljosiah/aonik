// My Space dashboard screen — greeting, KPIs, cash timeline, proposals, activity
function ScreenMySpace() {
  return (
    <div style={{ padding: '28px 32px', display: 'flex', flexDirection: 'column', gap: 24 }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between' }}>
        <div>
          <div className="eyebrow">Friday · 24 April 2026</div>
          <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 26, fontWeight: 700, marginTop: 6, letterSpacing: '-0.01em' }}>
            Morning, Oliver.
          </h1>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 4 }}>
            3 proposals waiting · 4 invoices unpaid · cash position updated 4 min ago
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-outline btn-sm"><Icon name="calendar" size={14}/> This month</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={14}/> New bill payment</button>
        </div>
      </div>

      {/* KPIs — from FinancialSnapshotCard */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
        <KPI label="Cash position" value="$74,300" delta="+9.1%" deltaTone="up"
             spark="0,24 15,22 30,18 45,14 60,11 75,8 90,5 100,4" sparkColor="#055a60"/>
        <KPI label="Revenue · Apr" value="$24,500" delta="+12.3%" deltaTone="up"
             spark="0,22 15,20 30,16 45,18 60,12 75,10 90,7 100,4" sparkColor="#3ab795"/>
        <KPI label="Outstanding invoices" value="$12,500" delta="2 overdue" deltaTone="down"
             spark="0,14 15,12 30,10 45,14 60,12 75,16 90,14 100,18" sparkColor="#eb5c37"/>
        <KPI label="Agent ops today" value="47" delta="+12" deltaTone="up"
             spark="0,24 10,20 20,22 30,18 40,14 50,16 60,10 70,12 80,8 90,6 100,4" sparkColor="#7b76b6"/>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 20 }}>
        {/* Cash timeline */}
        <Card
          title="Cash timeline · next 30 days"
          subtitle="Projected from scheduled invoices, payouts, and recurring entries"
          action={
            <div style={{ display: 'flex', gap: 4 }}>
              <button className="btn btn-ghost btn-sm" style={{ color: 'var(--brand-primary)' }}>NGN</button>
              <button className="btn btn-ghost btn-sm">USD</button>
              <button className="btn btn-ghost btn-sm">GBP</button>
            </div>
          }>
          <div style={{ height: 220, position: 'relative' }}>
            <svg viewBox="0 0 600 220" preserveAspectRatio="none" style={{ width: '100%', height: '100%' }}>
              <defs>
                <linearGradient id="ms-cashGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#055a60" stopOpacity="0.22"/>
                  <stop offset="100%" stopColor="#055a60" stopOpacity="0"/>
                </linearGradient>
                <pattern id="ms-grid" width="60" height="44" patternUnits="userSpaceOnUse">
                  <path d="M 60 0 L 0 0 0 44" fill="none" stroke="var(--border-light)" strokeWidth="1"/>
                </pattern>
              </defs>
              <rect width="600" height="220" fill="url(#ms-grid)"/>
              <polyline fill="none" stroke="#055a60" strokeWidth="2"
                points="0,120 40,112 80,115 120,100 160,105 200,92 240,94 280,80"/>
              <polygon fill="url(#ms-cashGrad)"
                points="0,120 40,112 80,115 120,100 160,105 200,92 240,94 280,80 280,220 0,220"/>
              <polyline fill="none" stroke="#055a60" strokeWidth="2" strokeDasharray="4 4"
                points="280,80 320,78 360,70 400,85 440,68 480,60 520,66 560,50 600,55"/>
              <path d="M280,88 L320,84 L360,76 L400,92 L440,74 L480,66 L520,72 L560,56 L600,61
                       L600,48 L560,43 L520,53 L480,47 L440,55 L400,79 L360,64 L320,72 L280,73 Z"
                    fill="#055a60" fillOpacity="0.08"/>
              <line x1="280" y1="20" x2="280" y2="200" stroke="#eb5c37" strokeWidth="1.5" strokeDasharray="3 3"/>
              <rect x="240" y="8" width="80" height="18" rx="3" fill="#eb5c3720"/>
              <text x="280" y="21" fill="#eb5c37" fontSize="10" fontFamily="monospace" textAnchor="middle" fontWeight="600">TODAY</text>
              {/* Event markers */}
              <circle cx="340" cy="72" r="4" fill="#3ab795"/>
              <circle cx="420" cy="82" r="4" fill="#eb5c37"/>
              <circle cx="500" cy="62" r="4" fill="#3ab795"/>
            </svg>
            <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, display: 'flex', justifyContent: 'space-between', fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
              <span>Mar 27</span><span>Apr 3</span><span>Apr 10</span><span>Apr 17</span><span>Apr 24</span><span>May 1</span><span>May 8</span>
            </div>
          </div>
          <div style={{
            marginTop: 14, padding: '10px 12px', background: 'var(--surface-inset)',
            borderRadius: 8, fontSize: 11, color: 'var(--text-secondary)',
            display: 'flex', gap: 18, flexWrap: 'wrap', fontFamily: 'var(--font-mono)',
          }}>
            <span>◆ Projected low: $58,100 · May 6</span>
            <span>◆ 3 revenue events</span>
            <span>◆ 1 payroll · Apr 30</span>
          </div>
        </Card>

        {/* Agent proposals */}
        <Card
          title="Agent proposals"
          subtitle="Pending your review"
          action={<Pill tone="pending" dot>3 pending</Pill>}
        >
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
            <ProposalCard
              agent="Billing" confidence={0.94} compact
              summary="Match bank txn from 19 Apr to INV-2041 · Primrose Logistics"
              reason="Reference + amount + counterparty all match."
            />
            <ProposalCard
              agent="Payout" confidence={0.88} compact
              summary="Route $48.2K payout batch via Flutterwave NGN rails"
              reason="Cheaper FX by 0.4% vs. default provider."
            />
            <ProposalCard
              agent="Ledger" confidence={0.97} compact
              summary="Accrue Apr rent ($8,200) against Office Expense"
              reason="Matches prior 11 months — recurring pattern."
            />
          </div>
        </Card>
      </div>

      {/* Activity list */}
      <Card
        title="Recent activity"
        subtitle="All agents · last 24 hours"
        action={<button className="btn btn-outline btn-sm"><Icon name="filter" size={12}/> Filter</button>}
      >
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          {[
            { dot: 'var(--success)',  t: 'INV-2041 posted to ledger',        d: 'Billing Agent · journal JE-88421 applied',              r: '12m ago' },
            { dot: 'var(--brand-secondary)', t: '3 invoice matches proposed', d: 'Billing Agent · confidence 0.94 avg · awaiting review', r: '22m ago' },
            { dot: 'var(--warning)',  t: 'FX drift on NGN → USD',             d: 'Policy monitor · 2.1% above band',                      r: '1h ago'  },
            { dot: 'var(--gray-400)', t: 'Partner sync · Flutterwave',        d: 'Ingest · 412 orders reconciled',                        r: '3h ago'  },
            { dot: 'var(--success)',  t: 'Payout batch PB-0042 settled',      d: 'Payout Router · $48,200 via FLW-NG',                    r: '4h ago'  },
            { dot: 'var(--gray-400)', t: 'Month-end close workflow started',  d: 'Orchestrator · 14/42 steps complete',                   r: '6h ago'  },
          ].map((r, i, arr) => (
            <div key={i} style={{
              display: 'grid', gridTemplateColumns: '20px 1fr auto',
              gap: 14, padding: '12px 4px', alignItems: 'center',
              borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
            }}>
              <span style={{ width: 8, height: 8, borderRadius: 999, background: r.dot, margin: '0 auto' }}/>
              <div>
                <div style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500 }}>{r.t}</div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>{r.d}</div>
              </div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{r.r}</div>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}

Object.assign(window, { ScreenMySpace });
