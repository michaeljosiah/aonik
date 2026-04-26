// Journal Entries — debit/credit lines with proposal support
function ScreenJournal() {
  const entries = [
    { id: 'JE-88421', date: '24 Apr', memo: 'Apply bank txn → INV-2041', debit: '$12,480.00', credit: '$12,480.00', status: 'posted', tone: 'success', agent: 'Billing', conf: 0.94,
      lines: [
        { acc: '1110 · Operating · Chase USD',  dr: '$12,480.00', cr: '' },
        { acc: '1200 · Accounts receivable',     dr: '',           cr: '$12,480.00' },
      ],
    },
    { id: 'JE-88420', date: '24 Apr', memo: 'Accrue April office rent', debit: '$8,200.00', credit: '$8,200.00', status: 'proposed', tone: 'pending', agent: 'Ledger', conf: 0.97,
      proposal: 'Post recurring rent accrual — matches prior 11 months pattern.',
      lines: [
        { acc: '5110 · Office expense',  dr: '$8,200.00', cr: '' },
        { acc: '2200 · Accrued expenses', dr: '',          cr: '$8,200.00' },
      ],
    },
    { id: 'JE-88419', date: '23 Apr', memo: 'Payout batch PB-0042 settled', debit: '$48,200.00', credit: '$48,200.00', status: 'posted', tone: 'success' },
    { id: 'JE-88418', date: '23 Apr', memo: 'FX revaluation · NGN holdings', debit: '$1,240.00', credit: '$1,240.00', status: 'posted', tone: 'success' },
    { id: 'JE-88417', date: '22 Apr', memo: 'Bill payment fee recognition', debit: '$820.00', credit: '$820.00', status: 'posted', tone: 'success' },
    { id: 'JE-88416', date: '22 Apr', memo: 'Failed payout — reversal', debit: '$45.00', credit: '$45.00', status: 'reversed', tone: 'danger' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance · Ledger"
        title="Journal Entries"
        subtitle="1,842 entries this period · 1 proposed · trial balance: balanced ✓"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New entry</button>
          </>
        }
      />

      <FilterBar
        tabs={['All', 'Posted', 'Proposed', 'Draft', 'Reversed']}
        active="All"
        counts={{ 'Proposed': 1 }}
        search="Filter by entry, memo, account…"
      />

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {entries.map(e => (
          <div key={e.id} style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderLeft: e.agent ? '3px solid var(--brand-secondary)' : '1px solid var(--border-light)',
            borderRadius: 10, overflow: 'hidden',
          }}>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 14,
              padding: '12px 16px',
              background: e.agent ? '#eb5c3708' : 'var(--surface-inset)',
              borderBottom: '1px solid var(--border-light)',
            }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600, color: 'var(--brand-primary)' }}>{e.id}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{e.date}</span>
              <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)', flex: 1 }}>{e.memo}</span>
              {e.agent && (
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 11, color: 'var(--brand-primary)' }}>
                  <Icon name="sparkles" size={12}/>
                  {e.agent} · {e.conf}
                </span>
              )}
              <Pill tone={e.tone} dot>{e.status}</Pill>
            </div>
            {e.lines && (
              <div>
                <div style={{
                  display: 'grid', gridTemplateColumns: '1fr 130px 130px',
                  padding: '8px 16px', gap: 14,
                  fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase',
                  color: 'var(--text-tertiary)', borderBottom: '1px solid var(--border-light)',
                  background: 'var(--surface)',
                }}>
                  <div>Account</div>
                  <div style={{ textAlign: 'right' }}>Debit</div>
                  <div style={{ textAlign: 'right' }}>Credit</div>
                </div>
                {e.lines.map((l, i) => (
                  <div key={i} style={{
                    display: 'grid', gridTemplateColumns: '1fr 130px 130px',
                    padding: '10px 16px', gap: 14, alignItems: 'center',
                    borderBottom: i < e.lines.length - 1 ? '1px solid var(--border-light)' : 'none',
                  }}>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{l.acc}</div>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, textAlign: 'right', color: l.dr ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{l.dr || '—'}</div>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, textAlign: 'right', color: l.cr ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{l.cr || '—'}</div>
                  </div>
                ))}
                {e.status === 'proposed' && (
                  <div style={{ padding: '10px 16px', background: '#eb5c3708', borderTop: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span style={{ fontSize: 11, color: 'var(--text-secondary)', flex: 1 }}>{e.proposal}</span>
                    <button className="btn btn-secondary btn-sm">Apply</button>
                    <button className="btn btn-outline btn-sm">Review</button>
                    <button className="btn btn-ghost btn-sm">Dismiss</button>
                  </div>
                )}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

// Network / Partners
function ScreenPartners() {
  const partners = [
    { name: 'Flutterwave',   code: 'FLW',  rails: ['NGN', 'GHS', 'KES'], status: 'healthy', tone: 'success', throughput: '1,842/d', err: '0.3%', fee: '0.9%', latency: '1.2s', last: '2m ago' },
    { name: 'Paystack',      code: 'PSK',  rails: ['NGN', 'GHS'],        status: 'healthy', tone: 'success', throughput: '940/d',   err: '0.4%', fee: '1.0%', latency: '1.5s', last: '4m ago' },
    { name: 'Interswitch',   code: 'ISW',  rails: ['NGN'],               status: 'degraded',tone: 'warning', throughput: '620/d',   err: '2.1%', fee: '0.8%', latency: '3.4s', last: 'now' },
    { name: 'Wise',          code: 'WISE', rails: ['USD', 'GBP', 'EUR'], status: 'healthy', tone: 'success', throughput: '214/d',   err: '0.1%', fee: '0.6%', latency: '2.1s', last: '6m ago' },
    { name: 'Stripe · ACH',  code: 'STRP', rails: ['USD'],               status: 'healthy', tone: 'success', throughput: '182/d',   err: '0.2%', fee: '0.8%', latency: '1.8s', last: '3m ago' },
    { name: 'MTN MoMo',      code: 'MTN',  rails: ['GHS', 'UGX', 'CFA'], status: 'incident',tone: 'danger',  throughput: '0/d',     err: '—',    fee: '1.1%', latency: '—',    last: '2h ago' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance · Network"
        title="Partners"
        subtitle="6 active · 1 incident · 1 degraded · routing agent watching all rails"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Re-sync</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add partner</button>
          </>
        }
      />

      <FilterBar
        tabs={['All', 'Healthy', 'Degraded', 'Incident']}
        active="All"
        counts={{ 'Degraded': 1, 'Incident': 1 }}
        search="Filter partners by name, rail, country…"
      />

      {/* Partners grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
        {partners.map(p => (
          <div key={p.code} style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 12, padding: 18, display: 'flex', flexDirection: 'column', gap: 14,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <Avatar name={p.name} size={36} color={agentColor(p.name) + '22'} textColor={agentColor(p.name)}/>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{p.name}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{p.code}</div>
              </div>
              <Pill tone={p.tone} dot>{p.status}</Pill>
            </div>

            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
              {p.rails.map(r => (
                <span key={r} style={{
                  fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
                  padding: '2px 7px', borderRadius: 4,
                  background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
                }}>{r}</span>
              ))}
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, paddingTop: 10, borderTop: '1px solid var(--border-light)' }}>
              {[
                { l: 'Throughput', v: p.throughput },
                { l: 'Error rate', v: p.err },
                { l: 'Fee',        v: p.fee },
                { l: 'Latency',    v: p.latency },
              ].map((s, i) => (
                <div key={i}>
                  <div style={{ fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.04em', textTransform: 'uppercase', marginBottom: 2 }}>{s.l}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, color: 'var(--text-primary)', fontWeight: 500 }}>{s.v}</div>
                </div>
              ))}
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
              <span>heartbeat · {p.last}</span>
              <button className="btn btn-ghost btn-sm" style={{ height: 22, padding: '0 8px' }}>
                <Icon name="terminal" size={11}/> trace
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

Object.assign(window, { ScreenJournal, ScreenPartners });
