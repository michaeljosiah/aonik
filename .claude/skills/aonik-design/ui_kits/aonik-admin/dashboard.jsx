// AONIK Admin UI — My Space dashboard screen

function DashboardScreen() {
  return (
    <div style={{ padding: '28px 32px', display: 'flex', flexDirection: 'column', gap: 24 }}>
      {/* greeting */}
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between' }}>
        <div>
          <div className="eyebrow">Thursday · 21 November</div>
          <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 26, fontWeight: 700, marginTop: 6, letterSpacing: '-0.01em' }}>
            Morning, Ada.
          </h1>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 4 }}>
            3 proposals waiting · cash position updated 4 minutes ago
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-outline btn-sm"><Icon name="calendar" size={14}/> This month</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={14}/> New invoice</button>
        </div>
      </div>

      {/* KPIs */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
        <KPI label="Cash position"        value="$2,481,032" delta="+4.2%" deltaTone="up"
             spark="0,22 10,18 20,20 30,14 40,16 50,10 60,12 70,6 80,8 90,4 100,6"
             sparkColor="#055a60"/>
        <KPI label="Outstanding invoices" value="$128,431"   delta="-2.1%" deltaTone="down"
             spark="0,10 10,12 20,8 30,14 40,12 50,18 60,16 70,20 80,18 90,22 100,20"
             sparkColor="#eb5c37"/>
        <KPI label="Runway"               value="18.4 mo"    delta="+0.6"  deltaTone="up"
             spark="0,20 15,18 30,16 45,14 60,12 75,10 90,8 100,7"
             sparkColor="#3ab795"/>
        <KPI label="Agent ops today"      value="47"         delta="+12"   deltaTone="up"
             spark="0,24 10,20 20,22 30,18 40,14 50,16 60,10 70,12 80,8 90,6 100,4"
             sparkColor="#7b76b6"/>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 20 }}>
        {/* cash timeline */}
        <Card
          title="Cash timeline · next 30 days"
          subtitle="Projected based on scheduled invoices, payouts, and recurring entries"
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
                <linearGradient id="cashGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%"   stopColor="#055a60" stopOpacity="0.22"/>
                  <stop offset="100%" stopColor="#055a60" stopOpacity="0"/>
                </linearGradient>
                <pattern id="grid" width="60" height="44" patternUnits="userSpaceOnUse">
                  <path d="M 60 0 L 0 0 0 44" fill="none" stroke="var(--border-light)" strokeWidth="1"/>
                </pattern>
              </defs>
              <rect width="600" height="220" fill="url(#grid)"/>
              {/* actual */}
              <polyline fill="none" stroke="#055a60" strokeWidth="2"
                points="0,120 40,110 80,115 120,100 160,108 200,90 240,95 280,80"/>
              <polygon fill="url(#cashGrad)"
                points="0,120 40,110 80,115 120,100 160,108 200,90 240,95 280,80 280,220 0,220"/>
              {/* projected (dashed) */}
              <polyline fill="none" stroke="#055a60" strokeWidth="2" strokeDasharray="4 4"
                points="280,80 320,78 360,70 400,85 440,68 480,60 520,66 560,50 600,55"/>
              {/* projected band */}
              <path d="M280,88 L320,84 L360,76 L400,92 L440,74 L480,66 L520,72 L560,56 L600,61
                       L600,48 L560,43 L520,53 L480,47 L440,55 L400,79 L360,64 L320,72 L280,73 Z"
                    fill="#055a60" fillOpacity="0.08"/>
              {/* today marker */}
              <line x1="280" y1="20" x2="280" y2="200" stroke="#eb5c37" strokeWidth="1.5" strokeDasharray="3 3"/>
              <rect x="240" y="8" width="80" height="18" rx="3" fill="#eb5c3720"/>
              <text x="280" y="21" fill="#eb5c37" fontSize="10" fontFamily="monospace" textAnchor="middle" fontWeight="600">TODAY</text>
            </svg>
            <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, display: 'flex', justifyContent: 'space-between', fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
              <span>Oct 23</span><span>Nov 1</span><span>Nov 8</span><span>Nov 15</span><span>Nov 21</span><span>Nov 28</span><span>Dec 5</span>
            </div>
          </div>
        </Card>

        {/* Agent proposals feed */}
        <Card
          title="Agent proposals"
          subtitle="Pending your review"
          action={<Pill tone="pending" dot>3 pending</Pill>}
        >
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
            <ProposalCard
              agent="Billing" confidence={0.94} compact
              summary="Match bank txn from 19 Nov to INV-2041 · Primrose Logistics"
              reason="Reference + amount + counterparty all match."
            />
            <ProposalCard
              agent="Payout" confidence={0.88} compact
              summary="Route $48.2K payout batch via Flutterwave NGN rails"
              reason="Cheaper FX by 0.4% vs. default provider."
            />
            <ProposalCard
              agent="Ledger" confidence={0.97} compact
              summary="Accrue Nov rent ($8,200) against Office Expense"
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
            { dot: 'var(--success)',  t: 'INV-2041 posted to ledger',       d: 'Billing Agent · journal JE-88421 applied',             r: '12m ago' },
            { dot: 'var(--pending)',  t: '3 invoice matches proposed',       d: 'Billing Agent · confidence 0.94 avg · awaiting review', r: '22m ago' },
            { dot: 'var(--warning)',  t: 'FX drift on NGN → USD',            d: 'Policy monitor · 2.1% above band',                      r: '1h ago'  },
            { dot: 'var(--gray-400)', t: 'Partner sync · Flutterwave',       d: 'Ingest · 412 orders reconciled',                        r: '3h ago'  },
            { dot: 'var(--success)',  t: 'Payout batch PB-0042 settled',     d: 'Payout Router · $48,200 via FLW-NG',                    r: '4h ago'  },
            { dot: 'var(--gray-400)', t: 'Month-end close workflow started', d: 'Orchestrator · 14/42 steps complete',                   r: '6h ago'  },
          ].map((r, i) => (
            <div key={i} style={{
              display: 'grid', gridTemplateColumns: '20px 1fr auto',
              gap: 14, padding: '12px 4px', alignItems: 'center',
              borderBottom: i < 5 ? '1px solid var(--border-light)' : 'none',
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

Object.assign(window, { DashboardScreen });
