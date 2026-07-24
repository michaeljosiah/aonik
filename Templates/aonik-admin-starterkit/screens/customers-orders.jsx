// Customers — the TOP-LEVEL registry (Transact layer, beside Orders).
// One customer base for every product a tenant runs: a business selling
// merchandise and financial services sees ONE list, never two views. Domain
// lenses (Billing · Storefront · Payabo) are chips here and TABS on the
// detail, gated by the tenant's enabled modules. KYC/compliance columns moved
// into the detail's Finance tab — they are domain content, not registry content.
const CUS_DOMAIN_TONE = { Billing: 'muted', Storefront: 'tint', Payabo: 'pending' };

function ScreenCustomers() {
  const rows = [
    { id: 'CUS-00142', name: 'Primrose Logistics', type: 'Business', country: 'NG', domains: ['Billing'],               orders: 48, spend: '$128,430', flag: 'Key account' },
    { id: 'CUS-00141', name: 'Apex Fabrication',   type: 'Business', country: 'GB', domains: ['Billing'],               orders: 12, spend: '$42,100',  flag: null },
    { id: 'CUS-00151', name: 'Adaeze Nwosu',       type: 'Person',   country: 'NG', domains: ['Storefront', 'Payabo'],  orders: 7,  spend: '£1,073.00', flag: null },
    { id: 'CUS-00150', name: 'Femi Adesanya',      type: 'Person',   country: 'GB', domains: ['Storefront'],            orders: 4,  spend: '£505.00',  flag: null },
    { id: 'CUS-00140', name: 'Maria Obi',          type: 'Person',   country: 'NG', domains: ['Billing'],               orders: 3,  spend: '$8,600',   flag: 'Agent-onboarded' },
    { id: 'CUS-00138', name: 'Northstar Freight',  type: 'Business', country: 'KE', domains: ['Billing'],               orders: 18, spend: '$36,900',  flag: 'Sanctions flag · review' },
    { id: 'CUS-00149', name: 'Halima Yusuf',       type: 'Person',   country: 'GB', domains: ['Storefront'],            orders: 2,  spend: '£260.00',  flag: 'Open cart · 8-box' },
    { id: 'CUS-00137', name: 'Samuel Okoro',       type: 'Person',   country: 'NG', domains: ['Billing'],               orders: 7,  spend: '$14,200',  flag: null },
  ];

  const cols = [
    { key: 'id',   label: 'ID',    w: '120px', mono: true, weight: 500, fontSize: 12 },
    { key: 'name', label: 'Customer', w: '1.4fr',
      render: r => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Avatar name={r.name} size={26} color={agentColor(r.name) + '22'} textColor={agentColor(r.name)}/>
          <div style={{ display: 'flex', flexDirection: 'column', minWidth: 0 }}>
            <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.name}</span>
            {r.flag && (
              <span style={{ fontSize: 10, color: r.flag.includes('Sanctions') ? 'var(--danger)' : 'var(--brand-secondary)', fontFamily: 'var(--font-mono)' }}>
                {r.flag}
              </span>
            )}
          </div>
        </div>
      ),
    },
    { key: 'type',    label: 'Type',    w: '80px',
      render: r => <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{r.type}</span> },
    { key: 'country', label: 'Country', w: '70px',
      render: r => <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{r.country}</span> },
    { key: 'domains', label: 'Products', w: '190px',
      render: r => (
        <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {r.domains.map(d => <Pill key={d} tone={CUS_DOMAIN_TONE[d] || 'muted'} size="sm">{d}</Pill>)}
        </div>
      ) },
    { key: 'orders',  label: 'Orders',  w: '70px', align: 'right', mono: true, fontSize: 12 },
    { key: 'spend',   label: 'Total value', w: '120px', align: 'right', mono: true, weight: 600 },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Customers"
        title="Customers"
        subtitle="1,982 parties across every product line · orders count the whole spine — boxes, bill payments and transfers alike"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="upload" size={12}/> Import</button>
            <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New customer</button>
          </>
        }
      />

      <FilterBar
        tabs={['All', 'Business', 'Person', 'Billing', 'Storefront', 'Payabo', 'Flagged']}
        active="All"
        counts={{ 'Storefront': 214, 'Flagged': 3 }}
        search="Filter by name, ID, email, ledger ref…"
        extra={<button className="btn btn-ghost btn-sm"><Icon name="globe" size={12}/> All countries</button>}
      />

      <DataTable
        cols={cols}
        rows={rows}
        rowHighlight={r => r.flag && r.flag.includes('Sanctions') ? '#cc2e2e08' : null}
        footer={<TableFooter showing="1–8" total="1,982 customers" page={1} pages={248}/>}
      />
    </div>
  );
}

// Orders — activity list, multi-rail status tracking
function ScreenOrders() {
  const rows = [
    { id: 'ORD-8821', type: 'Bill payment', party: 'EKEDC',                amt: '$1,240',  ccy: 'NGN→USD', status: 'settled',   statusTone: 'success', rail: 'FLW·NG', date: '24 Apr · 09:12', agent: null },
    { id: 'ORD-8820', type: 'Bill payment', party: 'MTN Nigeria',          amt: '$80',     ccy: 'NGN',     status: 'settled',   statusTone: 'success', rail: 'FLW·NG', date: '24 Apr · 09:08', agent: null },
    { id: 'ORD-8819', type: 'Payout batch', party: '42 recipients',        amt: '$48,200', ccy: 'USD→NGN', status: 'in-flight', statusTone: 'info',    rail: 'FLW·NG', date: '24 Apr · 08:54', agent: 'Payout', conf: 0.88 },
    { id: 'ORD-8818', type: 'Bill payment', party: 'Ikeja Electric',       amt: '$420',    ccy: 'NGN',     status: 'pending',   statusTone: 'warning', rail: 'PSB·NG', date: '24 Apr · 08:41', agent: null },
    { id: 'ORD-8817', type: 'Collection',   party: 'Primrose Logistics',   amt: '$12,480', ccy: 'USD',     status: 'matched',   statusTone: 'pending', rail: 'ACH·US', date: '24 Apr · 08:22', agent: 'Billing', conf: 0.94 },
    { id: 'ORD-8816', type: 'Bill payment', party: 'DStv',                 amt: '$45',     ccy: 'NGN',     status: 'failed',    statusTone: 'danger',  rail: 'PSB·NG', date: '24 Apr · 08:11', agent: null },
    { id: 'ORD-8815', type: 'Payout',       party: 'Meridian Studio',      amt: '$8,750',  ccy: 'USD',     status: 'settled',   statusTone: 'success', rail: 'WIRE',   date: '23 Apr · 17:44', agent: null },
    { id: 'ORD-8814', type: 'Bill payment', party: 'Eko Electricity',      amt: '$210',    ccy: 'NGN',     status: 'settled',   statusTone: 'success', rail: 'FLW·NG', date: '23 Apr · 17:02', agent: null },
  ];

  const cols = [
    { key: 'id',     label: 'Order',    w: '110px', mono: true, weight: 500, fontSize: 12 },
    { key: 'type',   label: 'Type',     w: '120px',
      render: r => <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{r.type}</span> },
    { key: 'party',  label: 'Counterparty', w: '1fr',
      render: r => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Avatar name={r.party} size={22} color={agentColor(r.party) + '22'} textColor={agentColor(r.party)}/>
          <span style={{ fontSize: 13, color: 'var(--text-primary)' }}>{r.party}</span>
        </div>
      ),
    },
    { key: 'ccy',    label: 'Rail / FX', w: '110px',
      render: r => (
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>
          {r.rail} · {r.ccy}
        </span>
      ),
    },
    { key: 'date',   label: 'Submitted', w: '130px',
      render: r => <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{r.date}</span> },
    { key: 'status', label: 'Status',    w: '120px',
      render: r => <Pill tone={r.statusTone} dot>{r.status}</Pill> },
    { key: 'amt',    label: 'Amount',    w: '110px', align: 'right', mono: true, weight: 600 },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance · Orders"
        title="Orders"
        subtitle="Bill payments, payouts, and collections · 412 in the last 24 hours"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Sync partners</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New bill payment</button>
          </>
        }
      />

      {/* Mini stats */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        {[
          { l: 'Settled today',   v: '318', sub: '$184,200 · 92% rate', tone: 'var(--success)' },
          { l: 'In flight',       v: '48',  sub: '$62,100 · avg 14m',    tone: 'var(--brand-primary)' },
          { l: 'Pending review',  v: '12',  sub: '3 flagged · 9 awaiting', tone: 'var(--warning)' },
          { l: 'Failed · today',  v: '6',   sub: '4 retried · 2 manual',  tone: 'var(--danger)' },
        ].map((s, i) => (
          <div key={i} style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 10, padding: '14px 16px',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 11, color: 'var(--text-secondary)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: s.tone }}/>
              {s.l}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>{s.v}</div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', marginTop: 2 }}>{s.sub}</div>
          </div>
        ))}
      </div>

      <FilterBar
        tabs={['All', 'Bill payments', 'Payouts', 'Collections', 'Failed']}
        active="All"
        counts={{ 'Failed': 6 }}
        search="Filter by order, ref, party, amount…"
        extra={<button className="btn btn-ghost btn-sm"><Icon name="calendar" size={12}/> 24 Apr</button>}
      />

      <DataTable
        cols={cols}
        rows={rows}
        footer={<TableFooter showing="1–8" total="3,241 orders" page={1} pages={406}/>}
      />
    </div>
  );
}

Object.assign(window, { ScreenCustomers, ScreenOrders });
