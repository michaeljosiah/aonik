// Invoices (ledger billing) — with inline agent proposals
function ScreenInvoices() {
  const rows = [
    { id: 'INV-2041', party: 'Primrose Logistics', amount: '$12,480.00', status: 'proposed', tone: 'pending', date: '19 Apr', memo: 'Matched to bank_txn_9f2c1a', agent: 'Billing', conf: 0.94,
      proposal: 'Apply $12,480 bank txn to this invoice and post journal JE-88421.' },
    { id: 'INV-2040', party: 'Apex Fabrication Ltd', amount: '$4,290.00',  status: 'paid',     tone: 'success', date: '18 Apr', memo: 'Paid via ACH' },
    { id: 'INV-2039', party: 'Meridian Studio',      amount: '$8,750.00',  status: 'paid',     tone: 'success', date: '18 Apr', memo: 'Paid via wire' },
    { id: 'INV-2038', party: 'Northstar Freight',    amount: '$22,100.00', status: 'proposed', tone: 'pending', date: '17 Apr', memo: 'Partial payment detected', agent: 'Billing', conf: 0.82,
      proposal: 'Split bank txn $18,100 → this invoice ($14,100) + INV-2032 ($4,000).' },
    { id: 'INV-2037', party: 'Blue Harbor Co',       amount: '$3,190.00',  status: 'overdue',  tone: 'danger',  date: '12 Apr', memo: 'Dunning letter queued' },
    { id: 'INV-2036', party: 'Quill & Co',           amount: '$1,840.00',  status: 'draft',    tone: 'muted',   date: '16 Apr', memo: 'Awaiting review' },
    { id: 'INV-2035', party: 'Cedar Analytics',      amount: '$14,600.00', status: 'paid',     tone: 'success', date: '14 Apr', memo: 'Paid via card' },
    { id: 'INV-2034', party: 'Orinoco Textiles',     amount: '$6,430.00',  status: 'pending',  tone: 'warning', date: '14 Apr', memo: 'Awaiting FX confirmation' },
  ];

  const cols = [
    { key: 'id', label: 'Invoice', w: '100px', mono: true, weight: 500 },
    { key: 'party', label: 'Counterparty', w: '1fr',
      render: r => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Avatar name={r.party} size={26} color={agentColor(r.party) + '22'} textColor={agentColor(r.party)}/>
          <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{r.party}</span>
        </div>
      ),
    },
    { key: 'memo', label: 'Memo', w: '1fr',
      render: r => (
        <span style={{ fontSize: 12, color: 'var(--text-secondary)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          {r.agent && <Icon name="sparkles" size={12} color="var(--brand-primary)"/>}
          {r.memo}
        </span>
      ),
    },
    { key: 'date', label: 'Date', w: '90px', mono: true, fontSize: 12,
      render: r => <span style={{ color: 'var(--text-secondary)' }}>{r.date}</span> },
    { key: 'status', label: 'Status', w: '120px',
      render: r => <Pill tone={r.tone} dot>{r.status}</Pill> },
    { key: 'amount', label: 'Amount', w: '120px', align: 'right', mono: true, weight: 600 },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance · Ledger"
        title="Invoices"
        subtitle="324 open · $128,431 outstanding · 2 agent proposals"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New invoice</button>
          </>
        }
      />

      <FilterBar
        tabs={['All', 'Proposed', 'Open', 'Overdue', 'Paid']}
        active="All"
        counts={{ 'Proposed': 2, 'Overdue': 14 }}
        search="Filter by party, ref, amount…"
        extra={<button className="btn btn-ghost btn-sm"><Icon name="calendar" size={12}/> Apr 2026</button>}
      />

      <DataTable
        cols={cols}
        rows={rows}
        rowHighlight={r => r.agent ? '#eb5c3708' : null}
        inlineAfter={r => r.agent ? <InlineProposal agent={r.agent} confidence={r.conf} summary={r.proposal} indent={116}/> : null}
        footer={<TableFooter showing="1–8" total="324 invoices" page={1} pages={41}/>}
      />
    </div>
  );
}

// Accounts (ledger chart of accounts) — hierarchical with balances
function ScreenAccounts() {
  const accounts = [
    { code: '1000', name: 'Assets', level: 0, balance: '$2,481,032.00', delta: '+4.2%', trend: 'up' },
    { code: '1100', name: 'Cash & equivalents', level: 1, balance: '$74,300.00', delta: '+9.1%', trend: 'up' },
    { code: '1110', name: 'Operating · Chase USD', level: 2, balance: '$42,180.00', delta: '+12%', trend: 'up', bank: 'Chase·5421' },
    { code: '1120', name: 'Operating · GTBank NGN', level: 2, balance: '$24,100.00', delta: '+3%', trend: 'up', bank: 'GTBank·9812' },
    { code: '1130', name: 'Reserve · HSBC GBP', level: 2, balance: '$8,020.00', delta: '—', trend: 'flat', bank: 'HSBC·3351' },
    { code: '1200', name: 'Accounts receivable', level: 1, balance: '$128,431.00', delta: '-2.1%', trend: 'down' },
    { code: '1300', name: 'Prepaid expenses', level: 1, balance: '$18,400.00', delta: '—', trend: 'flat' },
    { code: '2000', name: 'Liabilities', level: 0, balance: '$482,100.00', delta: '+1.2%', trend: 'down' },
    { code: '2100', name: 'Accounts payable', level: 1, balance: '$94,200.00', delta: '+6%', trend: 'down' },
    { code: '2200', name: 'Accrued expenses', level: 1, balance: '$42,100.00', delta: '—', trend: 'flat' },
    { code: '3000', name: 'Equity', level: 0, balance: '$1,998,932.00', delta: '+5.1%', trend: 'up' },
    { code: '4000', name: 'Revenue', level: 0, balance: '$284,200.00', delta: '+12.3%', trend: 'up' },
    { code: '4100', name: 'Bill payment fees', level: 1, balance: '$184,100.00', delta: '+14%', trend: 'up' },
    { code: '4200', name: 'FX spread', level: 1, balance: '$82,400.00', delta: '+8%', trend: 'up' },
    { code: '5000', name: 'Expenses', level: 0, balance: '$142,800.00', delta: '+3.4%', trend: 'down' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance · Ledger"
        title="Accounts"
        subtitle="Chart of accounts · Main ledger · FY2026 · as of 24 Apr"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Rebalance</button>
            <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New account</button>
          </>
        }
      />

      <FilterBar
        tabs={['All', 'Assets', 'Liabilities', 'Equity', 'Revenue', 'Expenses']}
        active="All"
        search="Filter accounts…"
        extra={<button className="btn btn-ghost btn-sm"><Icon name="eye" size={12}/> Zero balances</button>}
      />

      {/* Accounts list */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, overflow: 'hidden',
      }}>
        <div style={{
          display: 'grid', gridTemplateColumns: '80px 1fr 140px 120px 120px 40px',
          padding: '10px 16px', gap: 14,
          background: 'var(--surface-inset)',
          fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase',
          color: 'var(--text-tertiary)', borderBottom: '1px solid var(--border-light)',
        }}>
          <div>Code</div><div>Account</div><div>Bank</div>
          <div style={{ textAlign: 'right' }}>Balance</div>
          <div style={{ textAlign: 'right' }}>Δ vs prior</div>
          <div/>
        </div>
        {accounts.map((a, i) => {
          const isHeader = a.level === 0;
          return (
            <div key={a.code} style={{
              display: 'grid', gridTemplateColumns: '80px 1fr 140px 120px 120px 40px',
              padding: isHeader ? '14px 16px' : '10px 16px', gap: 14,
              alignItems: 'center',
              borderBottom: i < accounts.length - 1 ? '1px solid var(--border-light)' : 'none',
              background: isHeader ? 'var(--surface-inset)' : 'transparent',
            }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11,
                color: isHeader ? 'var(--brand-primary)' : 'var(--text-tertiary)',
                fontWeight: isHeader ? 700 : 500,
              }}>{a.code}</div>
              <div style={{
                paddingLeft: a.level * 20,
                fontSize: isHeader ? 13 : 13,
                fontWeight: isHeader ? 700 : (a.level === 1 ? 500 : 400),
                color: 'var(--text-primary)',
                display: 'flex', alignItems: 'center', gap: 8,
              }}>
                {isHeader && <Icon name="chevdown" size={12} color="var(--text-tertiary)"/>}
                {!isHeader && a.level === 1 && <Icon name="chevdown" size={10} color="var(--text-tertiary)"/>}
                {a.name}
              </div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{a.bank || ''}</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: isHeader ? 700 : 500, textAlign: 'right', color: 'var(--text-primary)' }}>{a.balance}</div>
              <div style={{ textAlign: 'right' }}>
                <span style={{
                  fontSize: 11, fontWeight: 600,
                  padding: '2px 8px', borderRadius: 999,
                  display: 'inline-flex', alignItems: 'center', gap: 4,
                  background: a.trend === 'up' ? 'var(--success-light, #4caf5020)' : a.trend === 'down' ? 'var(--danger-light, #cc2e2e20)' : 'var(--surface-inset)',
                  color: a.trend === 'up' ? 'var(--success)' : a.trend === 'down' ? 'var(--danger)' : 'var(--text-tertiary)',
                }}>
                  {a.trend !== 'flat' && <Icon name={a.trend === 'up' ? 'arrowup' : 'arrowdown'} size={10}/>}
                  {a.delta}
                </span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'center' }}>
                <span className="hover-halo"><Icon name="more" size={14}/></span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

Object.assign(window, { ScreenInvoices, ScreenAccounts });
