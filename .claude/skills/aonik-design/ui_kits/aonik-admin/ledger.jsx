// AONIK Admin UI — Ledger / Invoices screen (data table with inline agent proposals)

function LedgerScreen() {
  const rows = [
    { id: 'INV-2041', party: 'Primrose Logistics',   amount: '$12,480.00', status: 'proposed', date: '19 Nov', memo: 'Matched to bank_txn_9f2c1a', agent: 'Billing', conf: 0.94 },
    { id: 'INV-2040', party: 'Apex Fabrication Ltd', amount: '$4,290.00',  status: 'paid',     date: '18 Nov', memo: 'Paid via ACH' },
    { id: 'INV-2039', party: 'Meridian Studio',      amount: '$8,750.00',  status: 'paid',     date: '18 Nov', memo: 'Paid via wire' },
    { id: 'INV-2038', party: 'Northstar Freight',    amount: '$22,100.00', status: 'proposed', date: '17 Nov', memo: 'Partial payment detected', agent: 'Billing', conf: 0.82 },
    { id: 'INV-2037', party: 'Blue Harbor Co',       amount: '$3,190.00',  status: 'overdue',  date: '12 Nov', memo: 'Dunning letter queued' },
    { id: 'INV-2036', party: 'Quill & Co',           amount: '$1,840.00',  status: 'draft',    date: '16 Nov', memo: 'Awaiting review' },
    { id: 'INV-2035', party: 'Cedar Analytics',      amount: '$14,600.00', status: 'paid',     date: '14 Nov', memo: 'Paid via card' },
    { id: 'INV-2034', party: 'Orinoco Textiles',     amount: '$6,430.00',  status: 'pending',  date: '14 Nov', memo: 'Awaiting FX confirmation' },
  ];

  const statusPill = (s) => {
    const map = {
      paid:     { tone: 'success', label: 'Paid' },
      overdue:  { tone: 'danger',  label: 'Overdue' },
      pending:  { tone: 'warning', label: 'Pending' },
      draft:    { tone: 'default', label: 'Draft' },
      proposed: { tone: 'pending', label: 'Proposed' },
    }[s];
    return <Pill tone={map.tone} dot>{map.label}</Pill>;
  };

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div>
          <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 22, fontWeight: 700, letterSpacing: '-0.01em' }}>Invoices</h1>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>
            324 open · $128,431 outstanding · 2 agent proposals
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={14}/> Export</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={14}/> New invoice</button>
        </div>
      </div>

      {/* Filter bar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '10px 14px',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10,
      }}>
        <div style={{ display: 'flex', gap: 4 }}>
          {['All', 'Proposed', 'Open', 'Overdue', 'Paid'].map((t, i) => (
            <button key={t} className={'btn ' + (i === 0 ? 'btn-ghost' : 'btn-ghost')}
              style={{
                height: 28, padding: '0 10px', fontSize: 12,
                background: i === 0 ? 'var(--brand-primary-10)' : 'transparent',
                color: i === 0 ? 'var(--brand-primary)' : 'var(--text-secondary)',
                fontWeight: i === 0 ? 600 : 400,
              }}>
              {t}{i === 1 && <span style={{ marginLeft: 6, fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--brand-secondary)' }}>2</span>}
            </button>
          ))}
        </div>
        <div style={{ width: 1, height: 20, background: 'var(--border-light)' }}/>
        <div style={{ flex: 1, position: 'relative' }}>
          <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}>
            <Icon name="search" size={13}/>
          </span>
          <input className="input" placeholder="Filter by party, ref, amount…"
            style={{ paddingLeft: 30, height: 30, fontSize: 12, background: 'var(--surface-inset)', border: 'none' }}/>
        </div>
        <button className="btn btn-ghost btn-sm"><Icon name="filter" size={12}/> Filters</button>
        <button className="btn btn-ghost btn-sm"><Icon name="calendar" size={12}/> Nov 2024</button>
      </div>

      {/* Table */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, overflow: 'hidden',
      }}>
        {/* header */}
        <div style={{
          display: 'grid',
          gridTemplateColumns: '100px 1fr 1fr 120px 140px 100px 40px',
          padding: '10px 16px', gap: 14,
          background: 'var(--surface-inset)',
          fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase',
          color: 'var(--text-tertiary)',
          borderBottom: '1px solid var(--border-light)',
        }}>
          <div>Invoice</div><div>Counterparty</div><div>Memo</div>
          <div>Date</div><div>Status</div><div style={{ textAlign: 'right' }}>Amount</div><div/>
        </div>

        {rows.map((r, i) => (
          <React.Fragment key={r.id}>
            <div style={{
              display: 'grid',
              gridTemplateColumns: '100px 1fr 1fr 120px 140px 100px 40px',
              padding: '12px 16px', gap: 14,
              alignItems: 'center',
              borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none',
              background: r.status === 'proposed' ? '#eb5c3708' : 'transparent',
            }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>{r.id}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Avatar name={r.party} size={26} color={['#055a6015','#eb5c3715','#7b76b615','#3ab79515'][i % 4]}
                  textColor={['#055a60','#eb5c37','#7b76b6','#3ab795'][i % 4]}/>
                <span style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500 }}>{r.party}</span>
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 6 }}>
                {r.agent && <Icon name="sparkles" size={12} color="var(--brand-primary)"/>}
                {r.memo}
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>{r.date}</div>
              <div>{statusPill(r.status)}</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, textAlign: 'right', color: 'var(--text-primary)' }}>{r.amount}</div>
              <div style={{ display: 'flex', justifyContent: 'center' }}>
                <span className="hover-halo"><Icon name="more" size={14}/></span>
              </div>
            </div>

            {/* Inline proposal row */}
            {r.agent && (
              <div style={{
                padding: '0 16px 14px 126px',
                borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none',
                background: '#eb5c3708',
              }}>
                <div style={{
                  background: 'var(--surface)',
                  border: '1px solid var(--border-light)',
                  borderLeft: '3px solid var(--brand-secondary)',
                  borderRadius: 8, padding: '10px 14px',
                  display: 'flex', alignItems: 'center', gap: 12,
                }}>
                  <Avatar name={r.agent} size={22} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 12, color: 'var(--text-primary)' }}>
                      <b>{r.agent} Agent</b> proposes: {r.status === 'proposed' && r.id === 'INV-2041'
                        ? 'Apply $12,480 bank txn to this invoice and post journal JE-88421.'
                        : 'Split bank txn $18,100 → this invoice ($14,100) + INV-2032 ($4,000).'}
                    </div>
                    <div style={{ fontSize: 10, color: 'var(--text-secondary)', marginTop: 2, fontFamily: 'var(--font-mono)' }}>
                      confidence · {r.conf.toFixed(2)} · based on reference, amount, counterparty match
                    </div>
                  </div>
                  <button className="btn btn-secondary btn-sm">Apply</button>
                  <button className="btn btn-outline btn-sm">Review</button>
                  <button className="btn btn-ghost btn-sm"><Icon name="close" size={12}/></button>
                </div>
              </div>
            )}
          </React.Fragment>
        ))}

        {/* Footer / pagination */}
        <div style={{
          padding: '10px 16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center',
          background: 'var(--surface-inset)', borderTop: '1px solid var(--border-light)',
          fontSize: 11, color: 'var(--text-secondary)',
        }}>
          <div>Showing <b style={{ color: 'var(--text-primary)' }}>1–8</b> of 324 invoices</div>
          <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
            <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 6px' }}>←</button>
            <span style={{ fontFamily: 'var(--font-mono)' }}>1 / 41</span>
            <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 6px' }}>→</button>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { LedgerScreen });
