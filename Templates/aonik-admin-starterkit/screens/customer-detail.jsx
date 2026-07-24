// Customer Detail screen — mirrors src/pages/customers/CustomerDetailPage.tsx
// Tabs: Overview — Finance — Insights — Documents — Orders — Beneficiaries — Activity

// Beneficiaries scoped to this customer (Spec 031 — ExternalPayoutAccount).
// Tenant-scoped + Party-linked: each row is a destination Primrose pays
// through Aonik partners. Not a global registry — the customer owns them.
const PRIMROSE_BENEFICIARIES = [
  { id: 'pba_3f9c2a', name: 'Hassan Aliyu',       role: 'Driver — Lagos route',    partner: 'Flutterwave', partnerColor: '#f5a623', type: 'mobile', destination: 'OPay — +234 8•• ••• 218', country: 'NG', currency: 'NGN', verified: true,  lastPaid: '₦184,200 — 2d ago', payments: 14 },
  { id: 'pba_7d40b1', name: 'Total Energies UK',  role: 'Fuel supplier',           partner: 'Wise',        partnerColor: '#9fe870', type: 'bank',   destination: 'Lloyds — ••• 1042',       country: 'GB', currency: 'GBP', verified: true,  lastPaid: '£12,480 — 5h ago', payments: 38 },
  { id: 'pba_9c2d1e', name: 'Tunde Adebayo',      role: 'Contractor — mechanic',   partner: 'Flutterwave', partnerColor: '#f5a623', type: 'mobile', destination: 'MTN MoMo — +234 8•• ••• 884', country: 'NG', currency: 'NGN', verified: true,  lastPaid: '₦92,400 — 1w ago', payments: 22 },
  { id: 'pba_2b88f0', name: 'Northstar Freight',  role: 'Shipping partner',        partner: 'Wise',        partnerColor: '#9fe870', type: 'bank',   destination: 'GTBank — ••• 7741',       country: 'NG', currency: 'NGN', verified: true,  lastPaid: '₦18.2M — 3d ago', payments: 7 },
  { id: 'pba_e4d219', name: 'Maersk UK Ltd',      role: 'Container shipping',      partner: 'Wise',        partnerColor: '#9fe870', type: 'bank',   destination: 'Barclays — ••• 0241',     country: 'GB', currency: 'GBP', verified: true,  lastPaid: '£8,420 — 12d ago',payments: 11 },
  { id: 'pba_18f9aa', name: 'LIRS — Lagos Tax',   role: 'Tax authority',           partner: 'Interswitch', partnerColor: '#ed1c24', type: 'bank',   destination: 'Direct — LIRS-9821',      country: 'NG', currency: 'NGN', verified: true,  lastPaid: '₦284,000 — 1m ago', payments: 4 },
  { id: 'pba_44a8cc', name: 'Chioma Okeke',       role: 'Accountant — contractor', partner: 'Paystack',    partnerColor: '#1a73e8', type: 'bank',   destination: 'Access Bank — ••• 5092',  country: 'NG', currency: 'NGN', verified: false, lastPaid: 'never',           payments: 0 },
];

// ─── Individual customer — Payabo user (Adaeze Nwosu) ─────────────
// Household applies here because the customer is an individual.
// Some household members are also beneficiaries (Tobi, Ada, Nkechi)
// — they're linked via householdLink to show the overlap clearly.
const ADAEZE_CUSTOMER = {
  name: 'Adaeze Nwosu',
  legalName: 'Adaeze Chioma Nwosu',
  type: 'Individual',
  status: 'Active',
  country: 'United Kingdom — Nigeria',
  customerId: 'PAY-9821',
  since: 'Apr 2, 2024',
  tier: 'Premium — Payabo',
  email: 'adaeze@example.com',
  phone: '+44 7700 900142',
  address: '24 Hampstead Road, London NW1 7DZ',
  occupation: 'Senior Nurse — NHS',
};

const HOUSEHOLD_MEMBERS = [
  { id: 'hm-1', name: 'Adaeze Nwosu',   role: 'You',     relationship: 'Lead',         age: 34, account: 'Primary current — GBP',   perms: 'Full',      color: '#055a60', tag: 'YOU' },
  { id: 'hm-2', name: 'Chinedu Nwosu',  role: 'Spouse',  relationship: 'Co-signer',    age: 36, account: 'Joint savings — GBP',     perms: 'Full',      color: '#e8a838', monthlyContribution: '£840' },
  { id: 'hm-3', name: 'Tobi Nwosu',     role: 'Son',     relationship: 'Dependent',    age: 12, account: 'Allowance — GBP',         perms: 'View-only', color: '#3ab795', allowance: '£25/wk' },
  { id: 'hm-4', name: 'Ada Nwosu',      role: 'Daughter',relationship: 'Dependent',    age:  8, account: 'Allowance — GBP',         perms: 'View-only', color: '#7b76b6', allowance: '£15/wk' },
  { id: 'hm-5', name: 'Nkechi Nwosu',   role: 'Mother',  relationship: 'Supported',    age: 64, account: 'Recipient — Lagos, NGN',  perms: 'External',  color: '#eb5c37', monthlyTransfer: '₦580,000', extLocation: 'Lagos, NG' },
];

const ADAEZE_BENEFICIARIES = [
  { id: 'pba_aa1', name: 'Nkechi Nwosu',           role: 'Mother — Lagos',                  partner: 'Flutterwave', partnerColor: '#f5a623', type: 'bank',   destination: 'GTBank — ••• 8821',   country: 'NG', currency: 'NGN', verified: true,  lastPaid: '₦580,000 — 3d ago', payments: 28, householdLink: 'hm-5' },
  { id: 'pba_aa2', name: 'Tobi Nwosu',             role: 'Son — weekly allowance',          partner: 'Aonik',       partnerColor: '#055a60', type: 'bank',   destination: 'Aonik — TBN-09812',   country: 'GB', currency: 'GBP', verified: true,  lastPaid: '£25 — 2d ago',      payments: 18, householdLink: 'hm-3' },
  { id: 'pba_aa3', name: 'Ada Nwosu',              role: 'Daughter — weekly allowance',     partner: 'Aonik',       partnerColor: '#055a60', type: 'bank',   destination: 'Aonik — ADN-09813',   country: 'GB', currency: 'GBP', verified: true,  lastPaid: '£15 — 2d ago',      payments: 18, householdLink: 'hm-4' },
  { id: 'pba_aa4', name: 'Foxtons Properties',     role: 'Landlord — monthly rent',         partner: 'Wise',        partnerColor: '#9fe870', type: 'bank',   destination: 'Lloyds — ••• 4421',   country: 'GB', currency: 'GBP', verified: true,  lastPaid: '£1,840 — 4d ago',   payments: 14 },
  { id: 'pba_aa5', name: 'Kingdom Heights Primary',role: 'School — termly fees',            partner: 'Wise',        partnerColor: '#9fe870', type: 'bank',   destination: 'NatWest — ••• 9282',  country: 'GB', currency: 'GBP', verified: true,  lastPaid: '£3,400 — 2mo ago',  payments: 4 },
  { id: 'pba_aa6', name: 'Lagos Pharmacy',         role: "Healthcare — for Mum's meds",     partner: 'Flutterwave', partnerColor: '#f5a623', type: 'bank',   destination: 'Direct — LP-2812',    country: 'NG', currency: 'NGN', verified: true,  lastPaid: '₦42,000 — 1mo ago', payments: 6 },
  { id: 'pba_aa7', name: 'British Gas',            role: 'Utility — monthly',               partner: 'Direct',      partnerColor: '#999999', type: 'bank',   destination: 'Direct biller',       country: 'GB', currency: 'GBP', verified: true,  lastPaid: '£180 — 1w ago',     payments: 12 },
];

function ScreenCustomerDetail() {
  const [tab, setTab] = React.useState('Overview');
  const [fin, setFin] = React.useState('Accounts');

  const c = {
    name: 'Primrose Logistics Ltd',
    legalName: 'Primrose Logistics Limited',
    type: 'Corporate',
    status: 'Active',
    country: 'Nigeria — United Kingdom',
    rc: 'RC-1842991',
    since: 'Jan 12, 2025',
    tier: 'Enterprise',
    arr: '£48,240',
    accountMgr: 'Maria Gomez',
    email: 'ops@primrose.co',
    phone: '+44 20 7946 0018',
    address: '14 Dock Road, London E16 1AD',
    mrr: 4200,
    ltv: 52480,
    runway: 186,
    kycState: 'verified',
  };

  const accounts = [
    { name: 'Operating — GBP', inst: 'Barclays',    bal: 128_420.14, cur: 'GBP', last: '14m ago' },
    { name: 'Payroll — GBP',   inst: 'Barclays',    bal:  42_108.00, cur: 'GBP', last: '2h ago' },
    { name: 'FX Buffer — USD', inst: 'Wise',        bal:  86_410.22, cur: 'USD', last: '38m ago' },
    { name: 'NGN Settlement',  inst: 'Zenith Bank', bal:  41_820_000, cur: 'NGN', last: '1h ago' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Breadcrumb is in TopBar; here: header card */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 12, padding: 20, display: 'flex', gap: 20, alignItems: 'center',
      }}>
        <div style={{
          width: 68, height: 68, borderRadius: 14, flex: 'none',
          background: 'linear-gradient(135deg, #055a60 0%, #077a82 100%)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: '#fff', fontFamily: 'var(--font-brand)', fontSize: 26, fontWeight: 600, letterSpacing: '-0.02em',
        }}>PL</div>
        <div style={{ flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ fontFamily: 'var(--font-brand)', fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>{c.name}</div>
            <Pill tone="success" dot>Active</Pill>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', background: 'var(--surface-inset)', padding: '2px 8px', borderRadius: 4, border: '1px solid var(--border-light)' }}>{c.rc}</span>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 6, display: 'flex', gap: 14, flexWrap: 'wrap' }}>
            <span><Icon name="building" size={11}/> {c.type} — {c.tier}</span>
            <span><Icon name="globe" size={11}/> {c.country}</span>
            <span style={{ fontFamily: 'var(--font-mono)' }}>customer since — {c.since}</span>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 6 }}>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
          <button className="btn btn-outline btn-sm"><Icon name="sparkles" size={12} color="var(--brand-primary)"/> Generate insight</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New order</button>
        </div>
      </div>

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12 }}>
        {[
          { l: 'ARR',        v: c.arr,       sub: 'NGN—GBP—USD', tone: 'var(--brand-primary)' },
          { l: 'MRR',        v: '£4,200',    sub: '+£340 this mo', tone: 'var(--success)' },
          { l: 'LTV',        v: '£52.4K',    sub: '16 mo tenure',  tone: 'var(--accent-violet)' },
          { l: 'Runway',     v: '186 days',  sub: 'at current burn', tone: 'var(--warning)' },
          { l: 'Open orders',v: '4',         sub: '£12,480 open',    tone: 'var(--brand-secondary)' },
        ].map((k, i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: 14 }}>
            <div style={{ fontSize: 10, letterSpacing: '0.06em', color: 'var(--text-tertiary)', textTransform: 'uppercase', display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 5, height: 5, borderRadius: 999, background: k.tone }}/>
              {k.l}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 20, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>{k.v}</div>
            <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>{k.sub}</div>
          </div>
        ))}
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: 2, borderBottom: '1px solid var(--border-light)', padding: '0 2px' }}>
        {['Overview', 'Finance', 'Insights', 'Documents', 'Orders', 'Beneficiaries', 'Activity'].map(t => {
          const a = t === tab;
          return (
            <button key={t} onClick={() => setTab(t)} className="btn btn-ghost"
              style={{
                height: 38, padding: '0 14px', fontSize: 13, borderRadius: 0,
                borderBottom: a ? '2px solid var(--brand-primary)' : '2px solid transparent',
                color: a ? 'var(--text-primary)' : 'var(--text-secondary)',
                fontWeight: a ? 600 : 400, marginBottom: -1,
              }}>{t}</button>
          );
        })}
      </div>

      {tab === 'Overview' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 18 }}>
          <Card title="Details">
            {[
              ['Legal name', c.legalName],
              ['Type', c.type + ' — ' + c.tier],
              ['Registration', c.rc],
              ['Primary contact', c.accountMgr],
              ['Email', c.email],
              ['Phone', c.phone],
              ['Registered address', c.address],
            ].map(([k, v], i) => (
              <div key={i} style={{ display: 'grid', gridTemplateColumns: '140px 1fr', gap: 12, padding: '8px 0', borderBottom: i < 6 ? '1px solid var(--border-light)' : 'none' }}>
                <span style={{ fontSize: 11, color: 'var(--text-tertiary)', letterSpacing: '0.02em' }}>{k}</span>
                <span style={{ fontSize: 12.5, color: 'var(--text-primary)', fontFamily: k === 'Registration' || k === 'Phone' ? 'var(--font-mono)' : 'inherit' }}>{v}</span>
              </div>
            ))}
          </Card>

          <Card title="Compliance" subtitle="KYB — sanctions — docs"
            action={<Pill tone="success" dot>Verified</Pill>}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
              {[
                { label: 'Certificate of incorporation', status: 'Verified', tone: 'success', when: '14 Apr' },
                { label: 'Beneficial ownership',         status: 'Verified', tone: 'success', when: '14 Apr' },
                { label: 'Proof of address',             status: 'Verified', tone: 'success', when: '11 Apr' },
                { label: 'OFAC / UN sanctions screen',   status: 'Passed',   tone: 'success', when: '22 Apr' },
                { label: 'Annual re-screen',             status: 'Due Jun 1',tone: 'warning', when: '38 days' },
              ].map((d, i) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '6px 0' }}>
                  <Icon name="check" size={14} color={d.tone === 'success' ? 'var(--success)' : 'var(--warning)'}/>
                  <span style={{ fontSize: 12.5, color: 'var(--text-primary)', flex: 1 }}>{d.label}</span>
                  <Pill tone={d.tone} size="sm">{d.status}</Pill>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', minWidth: 60, textAlign: 'right' }}>{d.when}</span>
                </div>
              ))}
            </div>
          </Card>

          <Card title="Recent activity" style={{ gridColumn: '1 / -1' }}>
            <div style={{ display: 'flex', flexDirection: 'column' }}>
              {[
                { ic: 'sparkles', t: 'Billing Agent drafted journal entry for INV-2041', w: '2m ago', c: 'var(--brand-primary)' },
                { ic: 'invoice',  t: 'Invoice INV-2041 marked paid — £12,480', w: '14m ago', c: 'var(--success)' },
                { ic: 'payout',   t: 'Payout PO-0871 settled — £8,400 → Wise', w: '2h ago', c: 'var(--text-secondary)' },
                { ic: 'shield',   t: 'Annual KYB re-screen scheduled — June 1', w: '3h ago', c: 'var(--warning)' },
                { ic: 'invoice',  t: 'Order ORD-1284 created — £4,220',        w: 'yesterday', c: 'var(--text-secondary)' },
              ].map((a, i, arr) => (
                <div key={i} style={{
                  display: 'grid', gridTemplateColumns: '28px 1fr auto', alignItems: 'center', gap: 10,
                  padding: '10px 0', borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
                }}>
                  <div style={{ width: 28, height: 28, borderRadius: 8, background: a.c + '18', color: a.c, display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                    <Icon name={a.ic} size={14}/>
                  </div>
                  <div style={{ fontSize: 12.5, color: 'var(--text-primary)' }}>{a.t}</div>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{a.w}</span>
                </div>
              ))}
            </div>
          </Card>
        </div>
      )}

      {tab === 'Finance' && (
        <>
          <div style={{ display: 'flex', gap: 2 }}>
            {['Accounts', 'Transactions', 'Budgets', 'Commitments', 'Graph'].map(t => {
              const a = t === fin;
              return (
                <button key={t} onClick={() => setFin(t)} className="btn btn-ghost"
                  style={{
                    height: 30, padding: '0 12px', fontSize: 12, borderRadius: 6,
                    background: a ? 'var(--brand-primary-10)' : 'transparent',
                    color: a ? 'var(--brand-primary)' : 'var(--text-secondary)',
                    fontWeight: a ? 600 : 400,
                  }}>{t}</button>
              );
            })}
          </div>

          {fin === 'Accounts' && (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              {accounts.map((a, i) => (
                <div key={i} style={{
                  background: 'var(--surface)', border: '1px solid var(--border-light)',
                  borderRadius: 10, padding: 16, display: 'flex', flexDirection: 'column', gap: 10,
                }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <div style={{ width: 32, height: 32, borderRadius: 8, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Icon name="bank" size={15}/>
                    </div>
                    <div style={{ flex: 1 }}>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{a.name}</div>
                      <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{a.inst} — synced {a.last}</div>
                    </div>
                    <Pill tone="success" dot size="sm">live</Pill>
                  </div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)' }}>
                    {a.cur} {a.bal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </div>
                </div>
              ))}
            </div>
          )}

          {fin === 'Budgets' && (
            <ToolCardBudget
              period="April 2026"
              totalBudget={60000}
              totalSpent={42180}
              currency="GBP"
              categories={[
                { name: 'Fuel — fleet',    budgeted: 18000, spent: 19200, status: 'over' },
                { name: 'Contractors',     budgeted: 14000, spent:  8420, status: 'under' },
                { name: 'Warehousing',     budgeted: 12000, spent: 10100, status: 'on_track' },
                { name: 'Insurance',       budgeted:  8000, spent:  3960, status: 'under' },
                { name: 'Software — SaaS', budgeted:  4000, spent:   500, status: 'under' },
                { name: 'Admin',           budgeted:  4000, spent:     0, status: 'under' },
              ]}
            />
          )}

          {fin === 'Transactions' && (
            <Card>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
                <thead>
                  <tr style={{ textAlign: 'left', color: 'var(--text-tertiary)', fontSize: 11, letterSpacing: '0.04em' }}>
                    <th style={{ padding: '10px 8px', fontWeight: 500 }}>DATE</th>
                    <th style={{ padding: '10px 8px', fontWeight: 500 }}>DESCRIPTION</th>
                    <th style={{ padding: '10px 8px', fontWeight: 500 }}>ACCOUNT</th>
                    <th style={{ padding: '10px 8px', fontWeight: 500, textAlign: 'right' }}>AMOUNT</th>
                  </tr>
                </thead>
                <tbody>
                  {[
                    { d: '22 Apr', t: 'Wise — USD inbound — INV-2041',    a: 'FX Buffer',  amt: '+$16,120.00', pos: true },
                    { d: '21 Apr', t: 'Shell — fuel purchase',             a: 'Operating',  amt: '−£2,480.00', pos: false },
                    { d: '20 Apr', t: 'Payroll — April batch',             a: 'Payroll',    amt: '−£38,400.00',pos: false },
                    { d: '18 Apr', t: 'Northstar Freight — settlement',    a: 'NGN Settle', amt: '+₦18.2M',    pos: true },
                    { d: '17 Apr', t: 'AWS — infrastructure',              a: 'Operating',  amt: '−£820.00',   pos: false },
                    { d: '15 Apr', t: 'Barclays — interest',               a: 'Operating',  amt: '+£128.42',   pos: true },
                  ].map((r, i) => (
                    <tr key={i} style={{ borderTop: '1px solid var(--border-light)' }}>
                      <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{r.d}</td>
                      <td style={{ padding: '10px 8px', color: 'var(--text-primary)' }}>{r.t}</td>
                      <td style={{ padding: '10px 8px', color: 'var(--text-secondary)' }}>{r.a}</td>
                      <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', textAlign: 'right', color: r.pos ? 'var(--success)' : 'var(--text-primary)', fontWeight: 500 }}>{r.amt}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          )}

          {fin === 'Commitments' && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
              {[
                { t: 'Rent — Dock Road',      due: 'May 1',  amt: '£4,800',  freq: 'monthly' },
                { t: 'Fleet lease — 6 units', due: 'May 5',  amt: '£8,200',  freq: 'monthly' },
                { t: 'Insurance — liability', due: 'Jun 15', amt: '£3,960',  freq: 'quarterly' },
                { t: 'Payroll — April',       due: 'May 28', amt: '£38,400', freq: 'monthly' },
                { t: 'Software — Xero + AWS', due: 'May 10', amt: '£1,420',  freq: 'monthly' },
                { t: 'Loan servicing',        due: 'May 20', amt: '£2,180',  freq: 'monthly' },
              ].map((c, i) => (
                <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: 14 }}>
                  <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{c.t}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, fontWeight: 600, color: 'var(--text-primary)', marginTop: 6 }}>{c.amt}</div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 4, fontSize: 11, color: 'var(--text-tertiary)' }}>
                    <span>due — {c.due}</span>
                    <span>{c.freq}</span>
                  </div>
                </div>
              ))}
            </div>
          )}

          {fin === 'Graph' && (
            <Card title="Financial graph" subtitle="Cash in — cash out — net — last 90 days">
              <svg viewBox="0 0 600 180" style={{ width: '100%', height: 200 }}>
                {[0, 45, 90, 135, 180].map(y => <line key={y} x1="0" y1={y} x2="600" y2={y} stroke="var(--border-light)" strokeDasharray="2 4"/>)}
                <polyline points="0,110 50,95 100,100 150,80 200,70 250,85 300,60 350,55 400,65 450,50 500,40 550,35 600,30" stroke="var(--success)" strokeWidth="2" fill="none"/>
                <polyline points="0,130 50,135 100,125 150,140 200,130 250,135 300,120 350,125 400,115 450,130 500,120 550,125 600,115" stroke="var(--danger)" strokeWidth="2" fill="none"/>
                <polyline points="0,160 50,150 100,155 150,140 200,130 250,140 300,125 350,120 400,115 450,110 500,105 550,100 600,95" stroke="var(--brand-primary)" strokeWidth="2.5" fill="none"/>
              </svg>
              <div style={{ display: 'flex', gap: 20, justifyContent: 'center', marginTop: 8, fontSize: 11 }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><span style={{ width: 12, height: 2, background: 'var(--success)' }}/> Cash in</span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><span style={{ width: 12, height: 2, background: 'var(--danger)' }}/> Cash out</span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><span style={{ width: 12, height: 2, background: 'var(--brand-primary)' }}/> Net position</span>
              </div>
            </Card>
          )}
        </>
      )}

      {tab === 'Insights' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 18 }}>
          <Card title="AI summary" subtitle="Generated 14m ago — Insights Agent — conf 0.92"
            action={<Pill tone="tint" dot>fresh</Pill>}>
            <div style={{ marginTop: 4 }}>
              <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)', lineHeight: 1.4 }}>
                Primrose is trending to 14% over April fuel budget, but overall margin is up 3.2% YoY.
              </div>
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 10, lineHeight: 1.6 }}>
                Cashflow is healthy with 186 days of runway at current burn. Outbound FX exposure to NGN has doubled quarter-over-quarter; consider a forward hedge if Northstar volumes continue.
              </div>

              <div style={{ marginTop: 16, fontSize: 11, letterSpacing: '0.06em', color: 'var(--text-tertiary)', textTransform: 'uppercase' }}>Key observations</div>
              <ul style={{ margin: '8px 0 0 0', paddingLeft: 18, fontSize: 12.5, color: 'var(--text-primary)', lineHeight: 1.8 }}>
                <li>Fuel category is 107% through budget on day 22 of 30.</li>
                <li>Largest inbound: Northstar Freight, ₦18.2M settled 18 Apr.</li>
                <li>Payroll timing shifted +3 days vs last month — check with ops.</li>
              </ul>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginTop: 18 }}>
                <div>
                  <div style={{ fontSize: 11, letterSpacing: '0.06em', color: 'var(--success)', textTransform: 'uppercase', fontWeight: 600 }}>Positive patterns</div>
                  <ul style={{ margin: '6px 0 0 0', paddingLeft: 16, fontSize: 12, color: 'var(--text-primary)', lineHeight: 1.7 }}>
                    <li>Receivables aging improving</li>
                    <li>FX settlements on time</li>
                  </ul>
                </div>
                <div>
                  <div style={{ fontSize: 11, letterSpacing: '0.06em', color: 'var(--danger)', textTransform: 'uppercase', fontWeight: 600 }}>Risk patterns</div>
                  <ul style={{ margin: '6px 0 0 0', paddingLeft: 16, fontSize: 12, color: 'var(--text-primary)', lineHeight: 1.7 }}>
                    <li>Fuel category trending over</li>
                    <li>NGN exposure concentration</li>
                  </ul>
                </div>
              </div>

              <div style={{ marginTop: 16, fontSize: 11, letterSpacing: '0.06em', color: 'var(--text-tertiary)', textTransform: 'uppercase' }}>Recommended focus</div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 6 }}>
                {['Set fuel alert threshold', 'Consider NGN forward hedge', 'Review Northstar pricing'].map(r =>
                  <Pill key={r} tone="tint" size="sm">{r}</Pill>
                )}
              </div>
            </div>
          </Card>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <ToolCardPie
              title="Spending — April 2026"
              totalSpent={42180}
              currency="GBP"
              categories={[
                { name: 'Fuel — fleet',    amount: 19200, percentage: 46 },
                { name: 'Warehousing',     amount: 10100, percentage: 24 },
                { name: 'Contractors',     amount:  8420, percentage: 20 },
                { name: 'Insurance',       amount:  3960, percentage:  9 },
                { name: 'Other',           amount:   500, percentage:  1 },
              ]}
            />
            <ToolCardFx
              base="GBP" target="NGN" signal="buy"
              signalReason="Rate is near 30-day high and NGN inflows concentrated next week — buying now locks in favourable margin."
              rates={[
                { date: '25 Mar', rate: 1908 }, { date: '30 Mar', rate: 1920 },
                { date: '5 Apr',  rate: 1945 }, { date: '10 Apr', rate: 1930 },
                { date: '15 Apr', rate: 1965 }, { date: '18 Apr', rate: 1980 },
                { date: '22 Apr', rate: 2012 },
              ]}
            />
          </div>
        </div>
      )}

      {tab === 'Documents' && (
        <Card>
          <div style={{ display: 'flex', flexDirection: 'column' }}>
            {[
              { n: 'Certificate of incorporation', t: 'KYB', s: 'Verified', tone: 'success', u: '14 Apr', x: '2028-03-14' },
              { n: 'Beneficial ownership',          t: 'KYB', s: 'Verified', tone: 'success', u: '14 Apr', x: '—' },
              { n: 'Proof of address — utility',   t: 'KYB', s: 'Verified', tone: 'success', u: '11 Apr', x: '2026-10-11' },
              { n: 'Tax certificate',               t: 'Tax', s: 'Rejected', tone: 'danger',  u: '19 Apr', x: '—' },
              { n: 'Director IDs — 2 files',        t: 'KYC', s: 'Verified', tone: 'success', u: '15 Apr', x: '2030-02-15' },
            ].map((d, i, arr) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: 'auto 1fr auto auto auto auto',
                alignItems: 'center', gap: 14, padding: '12px 8px',
                borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
              }}>
                <Icon name="invoice" size={18} color="var(--text-tertiary)"/>
                <div>
                  <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{d.n}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{d.t}</div>
                </div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>uploaded {d.u}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>expires {d.x}</span>
                <Pill tone={d.tone} dot>{d.s}</Pill>
                <span className="hover-halo"><Icon name="download" size={13}/></span>
              </div>
            ))}
          </div>
        </Card>
      )}

      {tab === 'Orders' && (
        <Card>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr style={{ color: 'var(--text-tertiary)', fontSize: 11, letterSpacing: '0.04em', textAlign: 'left' }}>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>ORDER</th>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>DATE</th>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>SERVICE</th>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>STATUS</th>
                <th style={{ padding: '10px 8px', fontWeight: 500, textAlign: 'right' }}>AMOUNT</th>
              </tr>
            </thead>
            <tbody>
              {[
                { id: 'ORD-1291', d: '22 Apr', s: 'Freight — LHR→LAG', st: 'In transit', tone: 'tint',    amt: '£4,220' },
                { id: 'ORD-1288', d: '21 Apr', s: 'Bill payment',      st: 'Paid',       tone: 'success', amt: '£820' },
                { id: 'ORD-1284', d: '20 Apr', s: 'Freight — LHR→LAG', st: 'Settled',    tone: 'success', amt: '£12,480' },
                { id: 'ORD-1280', d: '18 Apr', s: 'FX — GBP→USD',      st: 'Settled',    tone: 'success', amt: '$16,120' },
                { id: 'ORD-1276', d: '15 Apr', s: 'Freight — MAN→ABJ', st: 'Awaiting',   tone: 'warning', amt: '£6,840' },
              ].map((r, i) => (
                <tr key={i} style={{ borderTop: '1px solid var(--border-light)' }}>
                  <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--brand-primary)' }}>{r.id}</td>
                  <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{r.d}</td>
                  <td style={{ padding: '10px 8px', color: 'var(--text-primary)' }}>{r.s}</td>
                  <td style={{ padding: '10px 8px' }}><Pill tone={r.tone} dot size="sm">{r.st}</Pill></td>
                  <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', textAlign: 'right', color: 'var(--text-primary)', fontWeight: 500 }}>{r.amt}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {tab === 'Beneficiaries' && <CustomerBeneficiaries customer={c} beneficiaries={PRIMROSE_BENEFICIARIES}/>}

      {tab === 'Activity' && (
        <Card>
          <div style={{ padding: 12, fontSize: 12, color: 'var(--text-secondary)' }}>Full audit trail — 842 events — exportable.</div>
        </Card>
      )}
    </div>
  );
}

// ─── Beneficiaries tab body ───────────────────────────────────────
// Saved destinations Primrose pays through partners. Grid ↔ list
// toggle matches the billers.jsx pattern. Each beneficiary card
// shows who, where the money goes, which partner routes it, and
// recent payment history.
function CustomerBeneficiaries({ customer, beneficiaries }) {
  const [view, setView] = React.useState('grid');
  const verifiedCount = beneficiaries.filter(b => b.verified).length;
  const totalPayments = beneficiaries.reduce((s, b) => s + b.payments, 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
      {/* Helper banner — explains scope */}
      <div style={{
        padding: '12px 14px', background: 'var(--brand-primary-10)',
        border: '1px solid transparent', borderRadius: 10,
        display: 'flex', alignItems: 'center', gap: 10,
        fontSize: 12.5, color: 'var(--text-primary)',
      }}>
        <Icon name="info" size={14} color="var(--brand-primary)"/>
        <span style={{ flex: 1 }}>
          Saved destinations {customer.name} pays through Aonik partners.
          {' '}<b style={{ color: 'var(--brand-primary)' }}>{beneficiaries.length}</b> beneficiaries
          {' — '}<b>{verifiedCount}</b> verified
          {' — '}<b>{totalPayments}</b> total payments to date.
        </span>
        <button className="btn btn-primary btn-sm"><Icon name="userplus" size={12}/> Add beneficiary</button>
      </div>

      {/* Filter / search / view toggle */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, padding: '10px 14px',
      }}>
        <div style={{ position: 'relative', flex: 1, maxWidth: 320 }}>
          <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}>
            <Icon name="search" size={13}/>
          </span>
          <input className="input" placeholder="Search by name, account, country…"
            style={{ paddingLeft: 30, height: 30, fontSize: 12, background: 'var(--surface-inset)', border: 'none', width: '100%' }}/>
        </div>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginRight: 4 }}>View</span>
          {['grid', 'list'].map(v => (
            <button key={v} onClick={() => setView(v)} style={{
              background: view === v ? 'var(--surface-inset)' : 'transparent',
              color: view === v ? 'var(--text-primary)' : 'var(--text-tertiary)',
              border: '1px solid ' + (view === v ? 'var(--border-medium)' : 'var(--border-light)'),
              borderRadius: 6, padding: '5px 10px', cursor: 'pointer',
              display: 'flex', alignItems: 'center', gap: 5,
              fontSize: 11.5, fontWeight: 500,
            }}>
              <Icon name={v} size={12}/>
              {v[0].toUpperCase() + v.slice(1)}
            </button>
          ))}
        </div>
      </div>

      {view === 'grid' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
          {beneficiaries.map(b => <CustomerBeneficiaryCard key={b.id} ben={b}/>)}
          <div style={{
            border: '1.5px dashed var(--border-medium)', borderRadius: 12,
            minHeight: 200, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
            gap: 6, color: 'var(--text-tertiary)', cursor: 'pointer',
            background: 'var(--surface)',
          }}>
            <Icon name="userplus" size={20}/>
            <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-secondary)' }}>Add a beneficiary</div>
            <div style={{ fontSize: 11, padding: '0 24px', textAlign: 'center' }}>Bank account — Mobile wallet — Direct biller</div>
          </div>
        </div>
      )}

      {view === 'list' && <CustomerBeneficiaryList beneficiaries={beneficiaries}/>}
    </div>
  );
}

function CustomerBeneficiaryCard({ ben }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 18, display: 'flex', flexDirection: 'column', gap: 12,
      cursor: 'pointer',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <Avatar name={ben.name} size={40} color={agentColor(ben.name) + '22'} textColor={agentColor(ben.name)}/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{ben.name}</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{ben.role}</div>
        </div>
        {ben.verified
          ? <Pill tone="success" dot size="sm">Verified</Pill>
          : <Pill tone="warning" dot size="sm">Unverified</Pill>}
      </div>

      <div style={{
        padding: '11px 12px', background: 'var(--surface-inset)',
        border: '1px solid var(--border-light)', borderRadius: 8,
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <Icon name={ben.type === 'mobile' ? 'mobile' : 'landmark'} size={16} color="var(--text-secondary)"/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>{ben.destination}</div>
          <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2, display: 'flex', gap: 6 }}>
            <span style={{ fontFamily: 'var(--font-mono)' }}>{ben.currency}</span>
            <span>—</span>
            <span>{ben.country}</span>
          </div>
        </div>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        paddingTop: 8, borderTop: '1px solid var(--border-light)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{
            width: 16, height: 16, borderRadius: 4,
            background: ben.partnerColor, color: '#fff',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 9,
          }}>{ben.partner.charAt(0)}</span>
          <span style={{ fontSize: 11, color: 'var(--text-secondary)' }}>via {ben.partner}</span>
        </div>
        <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>
          <div>{ben.lastPaid}</div>
          {ben.payments > 0 && <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, marginTop: 1 }}>{ben.payments} total payments</div>}
        </div>
      </div>
    </div>
  );
}

function CustomerBeneficiaryList({ beneficiaries }) {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
      <div style={{
        display: 'grid', gridTemplateColumns: '1.4fr 1.4fr 1fr 90px 110px 130px 50px',
        padding: '11px 16px', background: 'var(--surface-inset)',
        borderBottom: '1px solid var(--border-light)',
        fontSize: 11, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
      }}>
        <div>Beneficiary</div><div>Destination</div><div>Partner</div><div>Verified</div><div>Payments</div><div>Last paid</div><div></div>
      </div>
      {beneficiaries.map((b, i) => (
        <div key={b.id} style={{
          display: 'grid', gridTemplateColumns: '1.4fr 1.4fr 1fr 90px 110px 130px 50px',
          padding: '14px 16px', alignItems: 'center',
          borderBottom: i < beneficiaries.length - 1 ? '1px solid var(--border-light)' : 'none',
          fontSize: 12.5,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Avatar name={b.name} size={28} color={agentColor(b.name) + '22'} textColor={agentColor(b.name)}/>
            <div>
              <div style={{ fontWeight: 600 }}>{b.name}</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{b.role}</div>
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Icon name={b.type === 'mobile' ? 'mobile' : 'landmark'} size={13} color="var(--text-tertiary)"/>
            <div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12 }}>{b.destination}</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{b.currency} — {b.country}</div>
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
            <span style={{
              width: 18, height: 18, borderRadius: 4,
              background: b.partnerColor, color: '#fff',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 10,
            }}>{b.partner.charAt(0)}</span>
            <span style={{ fontSize: 12 }}>{b.partner}</span>
          </div>
          {b.verified
            ? <Pill tone="success" dot size="sm">Yes</Pill>
            : <Pill tone="warning" dot size="sm">Pending</Pill>}
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600 }}>{b.payments}</span>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{b.lastPaid}</span>
          <span className="hover-halo" style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6 }}>
            <Icon name="ellipsis" size={14} color="var(--text-secondary)"/>
          </span>
        </div>
      ))}
    </div>
  );
}

// ─── Individual Customer Detail — the unified customer view ───────
// Adaeze Nwosu, a senior NHS nurse in London with family in Lagos —
// AND an AbbysTable storefront customer. One party, every lens:
// domain tabs render per the tenant's enabled modules (config packs),
// so she shows Commerce + Payabo surfaces while Primrose (billing-only,
// above) shows neither — never a second customer view. Orders unifies
// on the order spine (ADR-011): boxes, bill payments and transfers are
// one list with type chips, because the architecture made them one thing.
function ScreenCustomerIndividual() {
  const [tab, setTab] = React.useState('Commerce');
  const c = ADAEZE_CUSTOMER;

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header card */}
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 12, padding: 20, display: 'flex', gap: 20, alignItems: 'center',
      }}>
        <Avatar name={c.name} size={68} color="#055a60" textColor="#fff"/>
        <div style={{ flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ fontFamily: 'var(--font-brand)', fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>{c.name}</div>
            <Pill tone="success" dot>Active</Pill>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', background: 'var(--surface-inset)', padding: '2px 8px', borderRadius: 4, border: '1px solid var(--border-light)' }}>{c.customerId}</span>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 6, display: 'flex', gap: 14, flexWrap: 'wrap' }}>
            <span><Icon name="user" size={11}/> {c.type} — {c.tier}</span>
            <span><Icon name="globe" size={11}/> {c.country}</span>
            <span><Icon name="badge" size={11}/> {c.occupation}</span>
            <span style={{ fontFamily: 'var(--font-mono)' }}>customer since — {c.since}</span>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 6 }}>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
          <button className="btn btn-outline btn-sm"><Icon name="sparkles" size={12} color="var(--brand-primary)"/> Generate insight</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New transfer</button>
        </div>
      </div>

      {/* KPI strip — personal-finance-flavoured */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12 }}>
        {[
          { l: 'Monthly inflow',  v: '£4,840',  sub: 'salary + side income',  tone: 'var(--success)' },
          { l: 'Monthly spend',   v: '£3,210',  sub: '−£1,630 vs inflow',     tone: 'var(--brand-primary)' },
          { l: 'Savings rate',    v: '34%',     sub: '£12,420 total',         tone: 'var(--accent-violet)' },
          { l: 'Send to family',  v: '₦580K/mo',sub: 'to Lagos — ~£420',      tone: 'var(--warning)' },
          { l: 'Household',       v: '5',       sub: '2 adults — 2 kids — 1 supported', tone: 'var(--brand-secondary)' },
        ].map((k, i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: 14 }}>
            <div style={{ fontSize: 10, letterSpacing: '0.06em', color: 'var(--text-tertiary)', textTransform: 'uppercase', display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 5, height: 5, borderRadius: 999, background: k.tone }}/>
              {k.l}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 20, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>{k.v}</div>
            <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>{k.sub}</div>
          </div>
        ))}
      </div>

      {/* Tabs — domain lenses on ONE customer, gated by enabled modules */}
      <div style={{ display: 'flex', gap: 2, borderBottom: '1px solid var(--border-light)', padding: '0 2px' }}>
        {['Overview', 'Orders', 'Commerce', 'Finance', 'Insights', 'Household', 'Beneficiaries', 'Activity'].map(t => {
          const a = t === tab;
          return (
            <button key={t} onClick={() => setTab(t)} className="btn btn-ghost"
              style={{
                height: 38, padding: '0 14px', fontSize: 13, borderRadius: 0,
                borderBottom: a ? '2px solid var(--brand-primary)' : '2px solid transparent',
                color: a ? 'var(--text-primary)' : 'var(--text-secondary)',
                fontWeight: a ? 600 : 400, marginBottom: -1,
              }}>{t}</button>
          );
        })}
      </div>

      {tab === 'Overview' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 18 }}>
          <Card title="Details">
            {[
              ['Legal name', c.legalName],
              ['Type', c.type + ' — ' + c.tier],
              ['Customer ID', c.customerId],
              ['Email', c.email],
              ['Phone', c.phone],
              ['Address', c.address],
              ['Occupation', c.occupation],
            ].map(([k, v], i) => (
              <div key={i} style={{ display: 'grid', gridTemplateColumns: '140px 1fr', gap: 12, padding: '8px 0', borderBottom: i < 6 ? '1px solid var(--border-light)' : 'none' }}>
                <span style={{ fontSize: 11, color: 'var(--text-tertiary)', letterSpacing: '0.02em' }}>{k}</span>
                <span style={{ fontSize: 12.5, color: 'var(--text-primary)', fontFamily: k === 'Customer ID' || k === 'Phone' ? 'var(--font-mono)' : 'inherit' }}>{v}</span>
              </div>
            ))}
          </Card>

          <Card title="Identity verification" subtitle="KYC — sanctions — proof of address"
            action={<Pill tone="success" dot>Verified</Pill>}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
              {[
                { label: 'Passport — UK',               status: 'Verified', tone: 'success', when: '2 Apr 24' },
                { label: 'Proof of address — utility',  status: 'Verified', tone: 'success', when: '2 Apr 24' },
                { label: 'NIN — Nigerian ID',           status: 'Verified', tone: 'success', when: '2 Apr 24' },
                { label: 'Source-of-funds declaration', status: 'Verified', tone: 'success', when: '8 May 25' },
                { label: 'Annual re-screen',            status: 'Due Apr 2',tone: 'warning', when: '320 days' },
              ].map((d, i) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '6px 0' }}>
                  <Icon name="check" size={14} color={d.tone === 'success' ? 'var(--success)' : 'var(--warning)'}/>
                  <span style={{ fontSize: 12.5, color: 'var(--text-primary)', flex: 1 }}>{d.label}</span>
                  <Pill tone={d.tone} size="sm">{d.status}</Pill>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', minWidth: 60, textAlign: 'right' }}>{d.when}</span>
                </div>
              ))}
            </div>
          </Card>

          <Card title="Recent activity" style={{ gridColumn: '1 / -1' }}>
            <div style={{ display: 'flex', flexDirection: 'column' }}>
              {[
                { ic: 'sparkles', t: 'Insights Agent: you saved £214 vs last April — strongest month yet', w: '5m ago',   c: 'var(--brand-primary)' },
                { ic: 'send',     t: 'Sent ₦580,000 to Mum — settled via Flutterwave',                       w: '3d ago',   c: 'var(--success)' },
                { ic: 'send',     t: "Tobi's weekly allowance — £25",                                         w: '2d ago',   c: 'var(--text-secondary)' },
                { ic: 'creditcard', t: 'Rent paid — Foxtons £1,840',                                          w: '4d ago',   c: 'var(--text-secondary)' },
                { ic: 'shield',   t: 'Annual KYC re-screen reminder — due Apr 2',                             w: '1w ago',   c: 'var(--warning)' },
              ].map((a, i, arr) => (
                <div key={i} style={{
                  display: 'grid', gridTemplateColumns: '28px 1fr auto', alignItems: 'center', gap: 10,
                  padding: '10px 0', borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
                }}>
                  <div style={{ width: 28, height: 28, borderRadius: 8, background: a.c + '18', color: a.c, display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                    <Icon name={a.ic} size={14}/>
                  </div>
                  <div style={{ fontSize: 12.5, color: 'var(--text-primary)' }}>{a.t}</div>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{a.w}</span>
                </div>
              ))}
            </div>
          </Card>
        </div>
      )}

      {tab === 'Orders' && (
        <Card title="Every order, one spine" subtitle="ProductPurchase boxes, bill payments and transfers share the Order record — filter by type, never by screen">
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr style={{ color: 'var(--text-tertiary)', fontSize: 11, letterSpacing: '0.04em', textAlign: 'left' }}>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>ORDER</th>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>TYPE</th>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>WHAT</th>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>DATE</th>
                <th style={{ padding: '10px 8px', fontWeight: 500 }}>STATUS</th>
                <th style={{ padding: '10px 8px', fontWeight: 500, textAlign: 'right' }}>AMOUNT</th>
              </tr>
            </thead>
            <tbody>
              {[
                // The complete spine — 7 orders, £1,073.00: five boxes (£629.00,
                // the Commerce tab's party-scoped subset) + two Payabo orders.
                // The registry row derives its count and value from this set.
                { id: 'ord_2044', ty: 'ProductPurchase', tone: 'tint',    w: "Abby's Box — 8 dishes + 2 extras", d: 'Today',  st: 'Paid',      stone: 'success', amt: '£129.00' },
                { id: 'ORD-9101', ty: 'MoneyTransfer',   tone: 'pending', w: 'GBP→NGN — to Mum, Lagos',          d: '2d ago', st: 'Settled',   stone: 'success', amt: '£420.00' },
                { id: 'ord_1990', ty: 'ProductPurchase', tone: 'tint',    w: "Abby's Box — 6 dishes",            d: '28 Jul', st: 'Fulfilled', stone: 'success', amt: '£95.00' },
                { id: 'ORD-9084', ty: 'BillPayment',     tone: 'muted',   w: 'MTN Nigeria — airtime top-up',     d: '21 Jul', st: 'Settled',   stone: 'success', amt: '£24.00' },
                { id: 'ord_1875', ty: 'ProductPurchase', tone: 'tint',    w: "Abby's Box — 12 dishes (party)",   d: '4 Jul',  st: 'Fulfilled', stone: 'success', amt: '£183.50' },
                { id: 'ord_1799', ty: 'ProductPurchase', tone: 'tint',    w: "Abby's Box — 8 dishes + chin chin", d: '19 Jun', st: 'Fulfilled', stone: 'success', amt: '£126.50' },
                { id: 'ord_1701', ty: 'ProductPurchase', tone: 'tint',    w: "Abby's Box — 6 dishes",            d: '30 Jan', st: 'Fulfilled', stone: 'success', amt: '£95.00' },
              ].map((r, i) => (
                <tr key={i} style={{ borderTop: '1px solid var(--border-light)' }}>
                  <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--brand-primary)' }}>{r.id}</td>
                  <td style={{ padding: '10px 8px' }}><Pill tone={r.tone} size="sm">{r.ty}</Pill></td>
                  <td style={{ padding: '10px 8px', color: 'var(--text-primary)' }}>{r.w}</td>
                  <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{r.d}</td>
                  <td style={{ padding: '10px 8px' }}><Pill tone={r.stone} dot size="sm">{r.st}</Pill></td>
                  <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', textAlign: 'right', color: 'var(--text-primary)', fontWeight: 500 }}>{r.amt}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {tab === 'Commerce' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr', gap: 18 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
            <Card title="Guest → account" subtitle="Spec 072 — how her first box became hers">
              <div style={{ display: 'flex', flexDirection: 'column', gap: 0, marginTop: 4 }}>
                {[
                  { ic: 'cart',     t: 'Built her first box as a guest', s: 'Fri 20:12 · anonymous cart token', done: true },
                  { ic: 'userplus', t: 'Registered', s: 'Sat 09:05 · adaeze@nwosu.co', done: true },
                  { ic: 'check2',   t: 'Cart adopted — guest token retired', s: 'the leaked pre-login token died at sign-in', done: true },
                ].map((s, i, arr) => (
                  <div key={i} style={{ display: 'grid', gridTemplateColumns: '26px 1fr', gap: 12 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                      <span style={{ width: 24, height: 24, borderRadius: 999, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name={s.ic} size={12}/></span>
                      {i < arr.length - 1 && <span style={{ width: 2, flex: 1, background: 'var(--brand-primary-20)', minHeight: 14 }}/>}
                    </div>
                    <div style={{ paddingBottom: i < arr.length - 1 ? 14 : 0 }}>
                      <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{s.t}</div>
                      <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 1 }}>{s.s}</div>
                    </div>
                  </div>
                ))}
              </div>
            </Card>
            <Card title="Storefront profile">
              {[
                ['Boxes ordered', '5'],
                ['Storefront value', '£629.00'],
                ['Usual size', '8 dishes'],
                ['Most-ordered item', 'Jollof Rice & Chicken'],
                ['Active cart', 'None'],
              ].map(([k, v], i) => (
                <div key={k} style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: 12, padding: '8px 0', borderBottom: i < 4 ? '1px solid var(--border-light)' : 'none' }}>
                  <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{k}</span>
                  <span style={{ fontSize: 12.5, color: 'var(--text-primary)', fontFamily: k === 'Boxes ordered' || k === 'Storefront value' ? 'var(--font-mono)' : 'inherit', fontWeight: k === 'Storefront value' ? 600 : 400 }}>{v}</span>
                </div>
              ))}
            </Card>
          </div>
          <Card title="Box history" subtitle="Party-scoped — exactly what she sees under /account/orders">
            {[
              { id: 'ord_2044', d: 'Today',  size: '8-box', extras: 'Puff Puff ×2', total: '£129.00', st: 'Paid',      tone: 'success' },
              { id: 'ord_1990', d: '28 Jul', size: '6-box', extras: null,           total: '£95.00',  st: 'Fulfilled', tone: 'success' },
              { id: 'ord_1875', d: '4 Jul',  size: '12-box',extras: 'Zobo ×4',      total: '£183.50', st: 'Fulfilled', tone: 'success' },
              { id: 'ord_1799', d: '19 Jun', size: '8-box', extras: 'Chin Chin',    total: '£126.50', st: 'Fulfilled', tone: 'success' },
              { id: 'ord_1701', d: '30 Jan', size: '6-box', extras: null,           total: '£95.00',  st: 'Fulfilled', tone: 'success' },
            ].map((b, i, arr) => (
              <div key={b.id} style={{ display: 'grid', gridTemplateColumns: '86px 70px 1fr 90px 84px', gap: 10, alignItems: 'center', padding: '10px 0', borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none' }}>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--brand-primary)' }}>{b.id}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{b.d}</span>
                <div>
                  <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{b.size}</span>
                  {b.extras && <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 8 }}>+ {b.extras}</span>}
                </div>
                <div><Pill tone={b.tone} dot size="sm">{b.st}</Pill></div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', textAlign: 'right' }}>{b.total}</span>
              </div>
            ))}
          </Card>
        </div>
      )}

      {tab === 'Finance' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14 }}>
          {[
            { name: 'Primary current — GBP',     inst: 'Aonik', bal: 4_840.18, cur: 'GBP', last: '12m ago' },
            { name: 'Joint savings — GBP',       inst: 'Aonik', bal: 12_420.00,cur: 'GBP', last: '1h ago'  },
            { name: "Tobi's allowance — GBP",    inst: 'Aonik', bal:    142.50,cur: 'GBP', last: '2d ago'  },
            { name: "Ada's allowance — GBP",     inst: 'Aonik', bal:     78.00,cur: 'GBP', last: '2d ago'  },
          ].map((a, i) => (
            <div key={i} style={{
              background: 'var(--surface)', border: '1px solid var(--border-light)',
              borderRadius: 10, padding: 16, display: 'flex', flexDirection: 'column', gap: 10,
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div style={{ width: 32, height: 32, borderRadius: 8, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Icon name="landmark" size={15}/>
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{a.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{a.inst} — synced {a.last}</div>
                </div>
                <Pill tone="success" dot size="sm">live</Pill>
              </div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)' }}>
                {a.cur} {a.bal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </div>
            </div>
          ))}
        </div>
      )}

      {tab === 'Insights' && (
        <Card title="AI summary" subtitle="Generated 5m ago — Insights Agent — conf 0.94"
          action={<Pill tone="tint" dot>fresh</Pill>}>
          <div style={{ marginTop: 4 }}>
            <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)', lineHeight: 1.4 }}>
              You're saving 34% of your inflow — your strongest April yet. Send-home to Lagos is stable; consider scheduling Tobi's school fees to ride a stronger GBP→NGN window in 6 weeks.
            </div>
          </div>
        </Card>
      )}

      {tab === 'Household' && <HouseholdTab customer={c} members={HOUSEHOLD_MEMBERS}/>}

      {tab === 'Beneficiaries' && <CustomerBeneficiaries customer={c} beneficiaries={ADAEZE_BENEFICIARIES}/>}

      {tab === 'Activity' && (
        <Card>
          <div style={{ padding: 12, fontSize: 12, color: 'var(--text-secondary)' }}>Full personal-finance audit trail — 384 events — exportable.</div>
        </Card>
      )}
    </div>
  );
}

// ─── Household tab body ───────────────────────────────────────────
// Warm, family-feeling layout. Members are people, not data rows.
// The "YOU" card has a teal ring so the operator/user can find
// themselves first. External members (Mum in Lagos) get a location
// pin and a warmer treatment — they're family, not just a recipient.
function HouseholdTab({ customer, members }) {
  const adults = members.filter(m => m.age >= 18 && m.perms !== 'External');
  const children = members.filter(m => m.age < 18);
  const supported = members.filter(m => m.perms === 'External');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
      {/* Warm hero banner with avatar group + summary */}
      <div style={{
        padding: '18px 20px',
        background: 'linear-gradient(135deg, rgba(5,90,96,0.10) 0%, rgba(232,168,56,0.12) 100%)',
        borderRadius: 12,
        display: 'flex', alignItems: 'center', gap: 18,
      }}>
        {/* Overlapping avatar group */}
        <div style={{ display: 'flex' }}>
          {members.map((m, i) => (
            <div key={m.id} style={{
              marginLeft: i === 0 ? 0 : -10,
              borderRadius: '50%',
              border: '2px solid var(--surface)',
              zIndex: members.length - i,
            }}>
              <Avatar name={m.name} size={44} color={m.color} textColor="#fff"/>
            </div>
          ))}
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>
            {customer.name}'s household
          </div>
          <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', marginTop: 3 }}>
            <b style={{ color: 'var(--text-primary)' }}>{members.length}</b> members
            {' — '}<b>{adults.length}</b> adults
            {' — '}<b>{children.length}</b> children
            {supported.length > 0 && <>{' — '}<b>{supported.length}</b> supported relative{supported.length > 1 ? 's' : ''}</>}
          </div>
        </div>
        <button className="btn btn-primary btn-sm"><Icon name="userplus" size={12}/> Add household member</button>
      </div>

      {/* Section — Your household */}
      <div>
        <SectionHeader label="In your household" hint="People who share your day-to-day finances"/>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14, marginTop: 12 }}>
          {[...adults, ...children].map(m => <HouseholdMemberCard key={m.id} m={m}/>)}
        </div>
      </div>

      {/* Section — Supported relatives (different vibe — they're family but not in the home) */}
      {supported.length > 0 && (
        <div>
          <SectionHeader label="Supported relatives" hint="Family abroad you send money to regularly"/>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14, marginTop: 12 }}>
            {supported.map(m => <HouseholdMemberCard key={m.id} m={m} external/>)}
          </div>
        </div>
      )}

      {/* Helper card — explaining the relationship */}
      <div style={{
        padding: '12px 14px', background: 'var(--surface-inset)',
        border: '1px dashed var(--border-light)', borderRadius: 10,
        display: 'flex', alignItems: 'center', gap: 10,
        fontSize: 12, color: 'var(--text-secondary)',
      }}>
        <Icon name="info" size={13} color="var(--brand-primary)"/>
        <span>
          <b style={{ color: 'var(--text-primary)' }}>Household ≠ beneficiaries.</b>
          {' '}A household member is someone in your financial life. A beneficiary is someone you send money to.
          They overlap when you pay a household member — those rows are
          {' '}<a href="#" style={{ color: 'var(--brand-primary)' }}>cross-linked under Beneficiaries</a>.
        </span>
      </div>
    </div>
  );
}

function SectionHeader({ label, hint }) {
  return (
    <div>
      <div style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)' }}>{label}</div>
      {hint && <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 3 }}>{hint}</div>}
    </div>
  );
}

function HouseholdMemberCard({ m, external = false }) {
  const isYou = m.tag === 'YOU';
  return (
    <div style={{
      background: 'var(--surface)',
      border: isYou ? '2px solid var(--brand-primary)' : '1px solid var(--border-light)',
      borderRadius: 12,
      padding: 18,
      display: 'flex', flexDirection: 'column', gap: 14,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <Avatar name={m.name} size={48} color={m.color} textColor="#fff"/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>{m.name}</div>
            {isYou && <span style={{
              fontFamily: 'var(--font-mono)', fontSize: 9.5, fontWeight: 700, letterSpacing: '0.08em',
              padding: '2px 7px', borderRadius: 4,
              background: 'var(--brand-primary)', color: '#fff',
            }}>YOU</span>}
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>
            {m.role}{m.age ? ` — ${m.age} years old` : ''}
          </div>
        </div>
        <Pill tone={m.perms === 'Full' ? 'success' : m.perms === 'View-only' ? 'tint' : 'warning'} dot size="sm">
          {m.perms}
        </Pill>
      </div>

      {/* Account / location row */}
      <div style={{
        padding: '11px 12px',
        background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
        borderRadius: 8,
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <Icon name={external ? 'mappin' : 'landmark'} size={16} color="var(--text-secondary)"/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{m.account}</div>
          {m.extLocation && <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 2 }}>{m.extLocation}</div>}
        </div>
      </div>

      {/* Footer — allowance, contribution, or transfer */}
      {(m.allowance || m.monthlyTransfer || m.monthlyContribution) && (
        <div style={{
          paddingTop: 10, borderTop: '1px solid var(--border-light)',
          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        }}>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>
            {m.allowance ? 'Weekly allowance' : m.monthlyContribution ? 'Monthly contribution' : 'Monthly transfer'}
          </span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 600, color: 'var(--brand-primary)' }}>
            {m.allowance || m.monthlyContribution || m.monthlyTransfer}
          </span>
        </div>
      )}
    </div>
  );
}

Object.assign(window, { ScreenCustomerDetail, ScreenCustomerIndividual });
