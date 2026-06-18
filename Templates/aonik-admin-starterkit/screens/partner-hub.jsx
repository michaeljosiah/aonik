// ─── Partner Network — Spec 031 ───────────────────────────────────
// Operator-facing hub for managing the partner connections that move
// money through Aonik: payouts, collections, bill payments, airtime.
// Internal left sub-nav mirrors AccountShell / SpeechShell.
//
// Spec 031 itself is backend-focused (contracts, entities, simulated
// connector, one migration). These screens are the operator surface
// for what the spec persists. Engineer terms (capability lanes,
// ConnectorBillerMapping, webhook events, vend tokens) are translated
// into plain language for non-technical operators; technical refs
// linger only as mono-font subtext for power users.
//
// Every list surface ships a card/list view toggle, mirroring the
// pattern established in screens/billers.jsx.

const PARTNER_HUB_TABS = [
  // Network — health and capability
  { id: 'overview',      label: 'Overview',     icon: 'pie',      group: 'network', subtitle: 'How are my partners doing today?' },
  { id: 'partners',      label: 'Partners',     icon: 'network',  group: 'network', subtitle: 'Who am I connected to?' },
  { id: 'coverage',      label: 'Coverage',     icon: 'globe',    group: 'network', subtitle: 'Where can each partner send money?' },
  // Money movement
  // NOTE: Beneficiaries live on the Customer detail page (Spec 031's
  // ExternalPayoutAccount is tenant-scoped + party-linked) — see
  // screens/customer-detail.jsx — Beneficiaries tab. Not a partner concern.
  { id: 'routes',        label: 'Routing',      icon: 'route',    group: 'money',   subtitle: 'How is money routed by default?' },
  { id: 'activity',      label: 'Activity',     icon: 'activity', group: 'money',   subtitle: 'Show me money in motion.' },
  { id: 'updates',       label: 'Updates',      icon: 'inbox',    group: 'money',   subtitle: 'Latest from my partners.' },
];

// ─── Shared mock data ─────────────────────────────────────────────

const PARTNERS = [
  { id: 'flw',  name: 'Flutterwave',  code: 'FLW',  color: '#f5a623', fg: '#fff',
    rails: ['Bank', 'Mobile money', 'Card'],
    countries: ['NG', 'GH', 'KE', 'UG', 'ZA'],
    currencies: ['NGN', 'GHS', 'KES', 'UGX', 'ZAR'],
    services: ['Payout', 'Collection', 'Bill payment', 'Airtime'],
    status: 'healthy', tone: 'success', statusLabel: 'Operational',
    throughput: '1,842/d', err: '0.3%', fee: '0.9%', latency: '1.2s', last: '2m ago',
    volMonth: '£1.84M', volDelta: '+12%', settlementOnTime: 99.2 },
  { id: 'wise', name: 'Wise',         code: 'WISE', color: '#9fe870', fg: '#0a3a3f',
    rails: ['Bank', 'Card'],
    countries: ['GB', 'US', 'EU', 'CA', 'AU'],
    currencies: ['GBP', 'USD', 'EUR', 'CAD', 'AUD'],
    services: ['Payout', 'Collection'],
    status: 'healthy', tone: 'success', statusLabel: 'Operational',
    throughput: '214/d', err: '0.1%', fee: '0.6%', latency: '2.1s', last: '6m ago',
    volMonth: '£612K', volDelta: '+8%', settlementOnTime: 99.6 },
  { id: 'psk',  name: 'Paystack',     code: 'PSK',  color: '#1a73e8', fg: '#fff',
    rails: ['Bank', 'Card', 'Mobile money'],
    countries: ['NG', 'GH', 'ZA'],
    currencies: ['NGN', 'GHS', 'ZAR'],
    services: ['Collection', 'Bill payment', 'Airtime'],
    status: 'healthy', tone: 'success', statusLabel: 'Operational',
    throughput: '940/d', err: '0.4%', fee: '1.0%', latency: '1.5s', last: '4m ago',
    volMonth: '£1.02M', volDelta: '+4%', settlementOnTime: 99.0 },
  { id: 'isw',  name: 'Interswitch',  code: 'ISW',  color: '#ed1c24', fg: '#fff',
    rails: ['Bank', 'Card'],
    countries: ['NG', 'GM', 'KE'],
    currencies: ['NGN', 'KES'],
    services: ['Bill payment', 'Airtime', 'Payout'],
    status: 'degraded', tone: 'warning', statusLabel: 'Slow today',
    throughput: '620/d', err: '2.1%', fee: '0.8%', latency: '3.4s', last: 'now',
    volMonth: '£492K', volDelta: '-3%', settlementOnTime: 96.4 },
  { id: 'strp', name: 'Stripe',       code: 'STRP', color: '#635bff', fg: '#fff',
    rails: ['Card', 'Bank'],
    countries: ['US', 'GB', 'EU', 'CA'],
    currencies: ['USD', 'GBP', 'EUR'],
    services: ['Collection'],
    status: 'healthy', tone: 'success', statusLabel: 'Operational',
    throughput: '182/d', err: '0.2%', fee: '0.8%', latency: '1.8s', last: '3m ago',
    volMonth: '£312K', volDelta: '+15%', settlementOnTime: 99.8 },
  { id: 'mtn',  name: 'MTN MoMo',     code: 'MTN',  color: '#ffcc00', fg: '#003c4a',
    rails: ['Mobile money'],
    countries: ['GH', 'UG', 'CI'],
    currencies: ['GHS', 'UGX', 'XOF'],
    services: ['Payout', 'Collection'],
    status: 'incident', tone: 'danger', statusLabel: 'Service incident',
    throughput: '—', err: '—', fee: '1.1%', latency: '—', last: '2h ago',
    volMonth: '£280K', volDelta: '—', settlementOnTime: 0 },
];

const ROUTES = [
  { id: 'r1', from: 'GBP', to: 'NGN',   service: 'Payout',      primary: 'flw',  backup: 'wise',  vol: '£412K/mo',   share: 64, status: 'healthy' },
  { id: 'r2', from: 'USD', to: 'NGN',   service: 'Payout',      primary: 'flw',  backup: 'isw',   vol: '$310K/mo',   share: 58, status: 'healthy' },
  { id: 'r3', from: 'GBP', to: 'KES',   service: 'Payout',      primary: 'wise', backup: 'flw',   vol: '£84K/mo',    share: 70, status: 'healthy' },
  { id: 'r4', from: 'NGN', to: 'NGN',   service: 'Bill payment',primary: 'isw',  backup: 'flw',   vol: '₦14.2M/mo',  share: 71, status: 'degraded' },
  { id: 'r5', from: 'NGN', to: 'NGN',   service: 'Airtime',     primary: 'flw',  backup: 'psk',   vol: '₦8.4M/mo',   share: 62, status: 'healthy' },
  { id: 'r6', from: 'GHS', to: 'GHS',   service: 'Collection',  primary: 'psk',  backup: 'mtn',   vol: 'GHS 2.1M/mo',share: 55, status: 'healthy' },
];

const PARTNER_ACTIVITY = [
  { id: 'pay-7421', kind: 'payout',     name: 'Vodafone Business',  amount: '£4,820',   partner: 'wise', status: 'succeeded',  tone: 'success', ref: 'AON-7421', when: '14m ago', service: 'Payout' },
  { id: 'col-3812', kind: 'collection', name: 'Acme Corp invoice',  amount: '$24,500',  partner: 'flw',  status: 'settled',    tone: 'success', ref: 'AON-3812', when: '32m ago', service: 'Collection' },
  { id: 'bil-4220', kind: 'bill',       name: 'Ikeja Electric',     amount: '₦18,400',  partner: 'isw',  status: 'succeeded',  tone: 'success', ref: 'AON-4220', when: '1h ago',  service: 'Bill payment', token: 'EKDC-1129-8842' },
  { id: 'air-2114', kind: 'airtime',    name: 'MTN airtime',        amount: '₦5,000',   partner: 'flw',  status: 'succeeded',  tone: 'success', ref: 'AON-2114', when: '1h ago',  service: 'Airtime', token: 'PIN 1234-5678-9012' },
  { id: 'pay-7418', kind: 'payout',     name: 'John Otieno',        amount: 'KES 124,800', partner: 'flw', status: 'processing',tone: 'warning', ref: 'AON-7418', when: '2h ago',  service: 'Payout' },
  { id: 'bil-4219', kind: 'bill',       name: 'GOtv subscription',  amount: '₦4,200',   partner: 'flw',  status: 'failed',     tone: 'danger',  ref: 'AON-4219', when: '3h ago',  service: 'Bill payment', failureReason: 'Customer ID not found' },
  { id: 'col-3811', kind: 'collection', name: 'Northstar Freight',  amount: '£12,840',  partner: 'strp', status: 'settled',    tone: 'success', ref: 'AON-3811', when: '4h ago',  service: 'Collection' },
  { id: 'pay-7415', kind: 'payout',     name: 'Maria Gomez',        amount: '₦184,200', partner: 'flw',  status: 'succeeded',  tone: 'success', ref: 'AON-7415', when: '5h ago',  service: 'Payout' },
];

const PARTNER_UPDATES = [
  { id: 'wh-9821', partner: 'wise', kind: 'settled',           subject: 'Payout to Vodafone Business settled',                  when: '14m ago', verified: true,  details: 'AON-7421 — £4,820',                            action: 'review' },
  { id: 'wh-9820', partner: 'flw',  kind: 'settled',           subject: 'Acme Corp collection cleared',                         when: '32m ago', verified: true,  details: 'AON-3812 — $24,500 USD → GBP',                action: null },
  { id: 'wh-9819', partner: 'isw',  kind: 'completed',         subject: 'Ikeja Electric prepaid token issued',                  when: '1h ago',  verified: true,  details: 'AON-4220 — Token EKDC-1129-8842 (valid 30 days)', action: null },
  { id: 'wh-9818', partner: 'flw',  kind: 'failed',            subject: 'GOtv bill payment failed',                             when: '3h ago',  verified: true,  details: 'AON-4219 — Customer ID not found',             action: 'retry' },
  { id: 'wh-9817', partner: 'mtn',  kind: 'duplicate',         subject: 'Duplicate MoMo callback ignored',                      when: '4h ago',  verified: true,  details: 'Same provider event seen 12 min earlier',      action: null },
  { id: 'wh-9816', partner: 'flw',  kind: 'settled',           subject: 'Refund returned to payer',                             when: '5h ago',  verified: true,  details: 'AON-3742 refund — £820',                       action: null },
  { id: 'wh-9815', partner: 'isw',  kind: 'signature_invalid', subject: 'Unverified callback signature — payload held',         when: '8h ago',  verified: false, details: 'Investigate before settling — log inspection', action: 'investigate' },
];

const COVERAGE_COUNTRIES = [
  { code: 'GB', flag: '🇬🇧', name: 'United Kingdom' },
  { code: 'US', flag: '🇺🇸', name: 'United States' },
  { code: 'EU', flag: '🇪🇺', name: 'Eurozone' },
  { code: 'NG', flag: '🇳🇬', name: 'Nigeria' },
  { code: 'KE', flag: '🇰🇪', name: 'Kenya' },
  { code: 'GH', flag: '🇬🇭', name: 'Ghana' },
  { code: 'ZA', flag: '🇿🇦', name: 'South Africa' },
  { code: 'UG', flag: '🇺🇬', name: 'Uganda' },
];

// Helpers
const partnerById = (id) => PARTNERS.find(p => p.id === id) || PARTNERS[0];

const serviceIcon = {
  Payout:         'send',
  Collection:     'inbox',
  'Bill payment': 'creditcard',
  Airtime:        'phone',
};

const updateIcon = {
  settled:           { icon: 'check',    color: 'var(--success)',         label: 'Settled' },
  completed:         { icon: 'check',    color: 'var(--success)',         label: 'Completed' },
  failed:            { icon: 'alertc',   color: 'var(--danger)',          label: 'Failed' },
  duplicate:         { icon: 'copy',     color: 'var(--text-tertiary)',   label: 'Duplicate' },
  signature_invalid: { icon: 'shield',   color: 'var(--danger)',          label: 'Unverified' },
};

// ─── Shell ────────────────────────────────────────────────────────
function PartnerHubShell({ initial = 'overview' }) {
  const [tab, setTab] = React.useState(initial);
  const active = PARTNER_HUB_TABS.find(t => t.id === tab) || PARTNER_HUB_TABS[0];

  return (
    <div style={{ display: 'flex', height: '100%', minHeight: 0 }}>
      {/* Inner left rail */}
      <div style={{
        width: 240, flex: 'none',
        borderRight: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', flexDirection: 'column',
        padding: 20,
      }}>
        <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Finance</div>
        <div style={{ fontSize: 17, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Partner Network</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.45, marginBottom: 22 }}>
          The payment partners that move money in and out of your workspace.
        </div>

        {['network', 'money'].map(group => {
          const items = PARTNER_HUB_TABS.filter(t => t.group === group);
          return (
            <div key={group} style={{ display: 'flex', flexDirection: 'column', gap: 1, marginBottom: 18 }}>
              <div style={{ fontSize: 10, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', padding: '4px 12px 8px' }}>
                {group === 'network' ? 'Your network' : 'Money movement'}
              </div>
              {items.map(t => {
                const isActive = tab === t.id;
                return (
                  <div key={t.id} onClick={() => setTab(t.id)} style={{
                    display: 'flex', alignItems: 'center', gap: 10,
                    padding: '8px 12px', borderRadius: 6, cursor: 'pointer',
                    background: isActive ? 'var(--brand-primary-10)' : 'transparent',
                    color: isActive ? 'var(--brand-primary)' : 'var(--text-primary)',
                    fontWeight: isActive ? 600 : 500, fontSize: 13,
                  }}>
                    <Icon name={t.icon} size={14} color={isActive ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
                    <span>{t.label}</span>
                  </div>
                );
              })}
            </div>
          );
        })}

        {/* Footer hint */}
        <div style={{
          marginTop: 'auto', padding: '12px',
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 8, fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5,
        }}>
          <Icon name="info" size={12} color="var(--brand-primary)"/> {' '}
          Need a new partner? Add it in <a href="#" style={{ color: 'var(--brand-primary)' }}>Settings → Gateways</a>.
        </div>
      </div>

      {/* Right column */}
      <div style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: '28px 36px' }}>
        {tab === 'overview'      && <PartnerOverview onJump={setTab}/>}
        {tab === 'partners'      && <PartnerListBody/>}
        {tab === 'coverage'      && <PartnerCoverageBody/>}
        {tab === 'routes'        && <PartnerRoutesBody/>}
        {tab === 'activity'      && <PartnerActivityBody/>}
        {tab === 'updates'       && <PartnerUpdatesBody/>}
      </div>
    </div>
  );
}

// View toggle (shared) — mirrors the billers.jsx pattern.
function ViewToggle({ view, setView, options = ['grid', 'list'] }) {
  return (
    <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
      <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginRight: 4 }}>View</span>
      {options.map(v => (
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
  );
}

// Re-usable partner avatar
function PartnerAvatar({ partner, size = 36 }) {
  return (
    <div style={{
      width: size, height: size, borderRadius: Math.round(size * 0.22),
      background: partner.color, color: partner.fg,
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--font-brand)', fontWeight: 700,
      fontSize: Math.round(size * 0.35), letterSpacing: '-0.02em',
      flex: 'none',
    }}>
      {partner.name.charAt(0)}
    </div>
  );
}

// Service chip — small coloured badge for service categories
function ServiceChip({ name, dense = false }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: dense ? '2px 7px' : '3px 9px', borderRadius: 999,
      background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
      fontSize: dense ? 10 : 11, fontWeight: 500, color: 'var(--text-secondary)',
    }}>
      <Icon name={serviceIcon[name] || 'package'} size={dense ? 9 : 10} color="var(--text-tertiary)"/>
      {name}
    </span>
  );
}

// ─── Tab — Overview ───────────────────────────────────────────────
function PartnerOverview({ onJump }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 22 }}>
      <PageHeader
        eyebrow="Finance — Network"
        title="Partner Network"
        subtitle="6 partners moving money on your behalf — last 30 days"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Refresh status</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add a partner</button>
          </>
        }
      />

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        {[
          { l: 'Money moved — 30 days', v: '£4.2M',  d: '+12% vs prev',     tone: 'var(--success)' },
          { l: 'On-time settlement',    v: '98.7%',  d: '−0.3% vs prev',    tone: 'var(--warning)' },
          { l: 'Partners healthy',      v: '4 / 6',  d: '1 incident',       tone: 'var(--danger)' },
          { l: 'Needs your attention',  v: '3',      d: 'updates pending',  tone: 'var(--warning)' },
        ].map((s, i) => (
          <div key={i} style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 12, padding: '18px 20px',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 11, color: 'var(--text-secondary)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: s.tone }}/>{s.l}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 26, fontWeight: 600, color: 'var(--text-primary)', marginTop: 8, lineHeight: 1.1 }}>{s.v}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 4 }}>{s.d}</div>
          </div>
        ))}
      </div>

      {/* Today's network */}
      <Card
        title="Today's network"
        subtitle="Live status across every partner"
        action={<button className="btn btn-ghost btn-sm" onClick={() => onJump && onJump('partners')}>Open all partners →</button>}
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10, margin: '14px 6px 4px' }}>
          {PARTNERS.map(p => <PartnerHealthRow key={p.id} partner={p}/>)}
        </div>
      </Card>

      {/* Latest in your network */}
      <Card
        title="Latest in your network"
        subtitle="The most recent events across every partner"
        action={<button className="btn btn-ghost btn-sm" onClick={() => onJump && onJump('updates')}>View all updates →</button>}
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, margin: '12px 6px 4px' }}>
          {PARTNER_UPDATES.slice(0, 4).map(u => <UpdateRowCompact key={u.id} update={u}/>)}
        </div>
      </Card>
    </div>
  );
}

function PartnerHealthRow({ partner }) {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: 'auto 1fr auto auto auto',
      alignItems: 'center', gap: 18, padding: '12px 14px',
      background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10,
    }}>
      <PartnerAvatar partner={partner} size={36}/>
      <div>
        <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{partner.name}</div>
        <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2, display: 'flex', gap: 8 }}>
          <span>{partner.volMonth} this month</span>
          <span style={{ color: 'var(--text-tertiary)' }}>—</span>
          <span>{partner.throughput} transactions</span>
        </div>
      </div>
      <span style={{
        fontFamily: 'var(--font-mono)', fontSize: 11.5,
        color: partner.tone === 'danger' ? 'var(--danger)' : partner.tone === 'warning' ? 'var(--warning)' : 'var(--success)',
      }}>{partner.settlementOnTime}% on time</span>
      <Pill tone={partner.tone} dot size="sm">{partner.statusLabel}</Pill>
      <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{partner.last}</span>
    </div>
  );
}

function UpdateRowCompact({ update }) {
  const u = updateIcon[update.kind] || updateIcon.completed;
  const partner = partnerById(update.partner);
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 14,
      alignItems: 'center', padding: '10px 14px',
      background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 9,
    }}>
      <div style={{
        width: 30, height: 30, borderRadius: '50%',
        background: u.color + '22', color: u.color,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      }}><Icon name={u.icon} size={14}/></div>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{update.subject}</div>
        <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>
          {partner.name} — <span style={{ fontFamily: 'var(--font-mono)' }}>{update.details}</span>
        </div>
      </div>
      <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{update.when}</span>
    </div>
  );
}

// ─── Tab — Partners (list) ────────────────────────────────────────
function PartnerListBody() {
  const [view, setView] = React.useState('grid');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance — Network"
        title="Partners"
        subtitle="6 partners — 4 operational — 1 slow today — 1 incident"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Re-sync</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add a partner</button>
          </>
        }
      />

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
          Showing <b style={{ color: 'var(--text-primary)' }}>{PARTNERS.length}</b> partners
        </div>
        <ViewToggle view={view} setView={setView}/>
      </div>

      {view === 'grid' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
          {PARTNERS.map(p => <PartnerCard key={p.id} partner={p}/>)}
          {/* Add card */}
          <div style={{
            border: '1.5px dashed var(--border-medium)', borderRadius: 12,
            minHeight: 220, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
            gap: 6, color: 'var(--text-tertiary)', cursor: 'pointer',
            background: 'var(--surface)',
          }}>
            <Icon name="plus" size={20}/>
            <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-secondary)' }}>Connect a new partner</div>
            <div style={{ fontSize: 11, padding: '0 24px', textAlign: 'center' }}>Flutterwave — Paystack — Wise — or add your own</div>
          </div>
        </div>
      )}

      {view === 'list' && <PartnerListTable/>}
    </div>
  );
}

function PartnerCard({ partner }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 18, display: 'flex', flexDirection: 'column', gap: 14,
      cursor: 'pointer', transition: 'border-color 120ms ease, box-shadow 120ms ease',
    }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <PartnerAvatar partner={partner} size={42}/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 14.5, fontWeight: 600, color: 'var(--text-primary)' }}>{partner.name}</div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', marginTop: 1 }}>{partner.code}</div>
        </div>
        <Pill tone={partner.tone} dot size="sm">{partner.statusLabel}</Pill>
      </div>

      {/* Services */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
        {partner.services.map(s => <ServiceChip key={s} name={s} dense/>)}
      </div>

      {/* Metrics */}
      <div style={{
        display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12,
        padding: '12px 0', borderTop: '1px dashed var(--border-light)', borderBottom: '1px dashed var(--border-light)',
      }}>
        <Metric label="This month" value={partner.volMonth} delta={partner.volDelta}/>
        <Metric label="On time"    value={`${partner.settlementOnTime}%`}/>
        <Metric label="Fee"        value={partner.fee}/>
        <Metric label="Avg latency"value={partner.latency}/>
      </div>

      {/* Footer */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
          heartbeat — {partner.last}
        </span>
        <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 9px' }}>Open →</button>
      </div>
    </div>
  );
}

function Metric({ label, value, delta }) {
  return (
    <div>
      <div style={{ fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.04em', textTransform: 'uppercase', fontWeight: 600 }}>{label}</div>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginTop: 3 }}>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{value}</span>
        {delta && <span style={{ fontSize: 10.5, color: delta.startsWith('+') ? 'var(--success)' : delta.startsWith('-') ? 'var(--danger)' : 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{delta}</span>}
      </div>
    </div>
  );
}

function PartnerListTable() {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
      <div style={{
        display: 'grid', gridTemplateColumns: '1.6fr 1.4fr 1fr 100px 90px 80px 130px 50px',
        padding: '11px 16px', background: 'var(--surface-inset)',
        borderBottom: '1px solid var(--border-light)',
        fontSize: 11, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
      }}>
        <div>Partner</div><div>Services</div><div>Volume / mo</div><div>On time</div><div>Fee</div><div>Latency</div><div>Status</div><div></div>
      </div>
      {PARTNERS.map((p, i) => (
        <div key={p.id} style={{
          display: 'grid', gridTemplateColumns: '1.6fr 1.4fr 1fr 100px 90px 80px 130px 50px',
          padding: '14px 16px', alignItems: 'center',
          borderBottom: i < PARTNERS.length - 1 ? '1px solid var(--border-light)' : 'none',
          fontSize: 12.5, cursor: 'pointer',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <PartnerAvatar partner={p} size={28}/>
            <div>
              <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{p.name}</div>
              <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{p.code}</div>
            </div>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
            {p.services.map(s => <ServiceChip key={s} name={s} dense/>)}
          </div>
          <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{p.volMonth}</span>
          <span style={{ fontFamily: 'var(--font-mono)', color: p.settlementOnTime >= 99 ? 'var(--success)' : p.settlementOnTime >= 97 ? 'var(--text-primary)' : 'var(--warning)' }}>{p.settlementOnTime}%</span>
          <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{p.fee}</span>
          <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{p.latency}</span>
          <Pill tone={p.tone} dot size="sm">{p.statusLabel}</Pill>
          <span className="hover-halo" style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6 }}>
            <Icon name="ellipsis" size={14} color="var(--text-secondary)"/>
          </span>
        </div>
      ))}
    </div>
  );
}

// ─── Tab — Coverage (matrix) ──────────────────────────────────────
function PartnerCoverageBody() {
  const [view, setView] = React.useState('grid'); // 'grid' = matrix; 'list' = flat table
  const [service, setService] = React.useState('All');

  const services = ['All', 'Payout', 'Collection', 'Bill payment', 'Airtime'];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance — Network"
        title="Coverage"
        subtitle="Where each partner can send money, and what they can do"
        actions={<button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>}
      />

      {/* Filter pills + view toggle */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, padding: '10px 14px',
      }}>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginRight: 4 }}>Service</span>
          {services.map(s => {
            const active = service === s;
            return (
              <button key={s} onClick={() => setService(s)} style={{
                background: active ? 'var(--brand-primary-10)' : 'transparent',
                color: active ? 'var(--brand-primary)' : 'var(--text-secondary)',
                border: 'none', borderRadius: 6, padding: '5px 10px', cursor: 'pointer',
                fontSize: 11.5, fontWeight: active ? 600 : 500,
              }}>{s}</button>
            );
          })}
        </div>
        <ViewToggle view={view} setView={setView}/>
      </div>

      {view === 'grid' && <CoverageMatrix service={service}/>}
      {view === 'list' && <CoverageList service={service}/>}
    </div>
  );
}

function CoverageMatrix({ service }) {
  const supports = (partner, country) => {
    if (!partner.countries.includes(country)) return null;
    if (service === 'All') return partner.services;
    return partner.services.includes(service) ? [service] : null;
  };

  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
      {/* Header — countries */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: `200px repeat(${COVERAGE_COUNTRIES.length}, 1fr)`,
        padding: '14px 16px', background: 'var(--surface-inset)',
        borderBottom: '1px solid var(--border-light)',
      }}>
        <div style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>Partner</div>
        {COVERAGE_COUNTRIES.map(c => (
          <div key={c.code} style={{ textAlign: 'center' }}>
            <div style={{ fontSize: 18, lineHeight: 1 }}>{c.flag}</div>
            <div style={{ fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', marginTop: 3 }}>{c.code}</div>
          </div>
        ))}
      </div>
      {/* Rows */}
      {PARTNERS.map((p, i) => (
        <div key={p.id} style={{
          display: 'grid',
          gridTemplateColumns: `200px repeat(${COVERAGE_COUNTRIES.length}, 1fr)`,
          padding: '14px 16px', alignItems: 'center',
          borderBottom: i < PARTNERS.length - 1 ? '1px solid var(--border-light)' : 'none',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <PartnerAvatar partner={p} size={28}/>
            <div style={{ fontSize: 13, fontWeight: 600 }}>{p.name}</div>
          </div>
          {COVERAGE_COUNTRIES.map(c => {
            const cell = supports(p, c.code);
            if (!cell) {
              return (
                <div key={c.code} style={{ textAlign: 'center' }}>
                  <span style={{ color: 'var(--border)', fontSize: 16 }}>—</span>
                </div>
              );
            }
            return (
              <div key={c.code} style={{ display: 'flex', justifyContent: 'center' }}>
                <div title={cell.join(' — ')} style={{
                  width: 30, height: 30, borderRadius: 8,
                  background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 11, fontFamily: 'var(--font-mono)', fontWeight: 600,
                }}>
                  {cell.length === p.services.length || service !== 'All' ? '✓' : cell.length}
                </div>
              </div>
            );
          })}
        </div>
      ))}
    </div>
  );
}

function CoverageList({ service }) {
  const rows = [];
  PARTNERS.forEach(p => {
    p.countries.forEach(c => {
      const country = COVERAGE_COUNTRIES.find(x => x.code === c);
      if (!country) return;
      p.services.forEach(s => {
        if (service !== 'All' && s !== service) return;
        rows.push({ partner: p, country, service: s, currency: p.currencies[0] });
      });
    });
  });

  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
      <div style={{
        display: 'grid', gridTemplateColumns: '1.6fr 1fr 1fr 1fr 1fr 60px',
        padding: '11px 16px', background: 'var(--surface-inset)',
        borderBottom: '1px solid var(--border-light)',
        fontSize: 11, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
      }}>
        <div>Partner</div><div>Country</div><div>Service</div><div>Currency</div><div>Methods</div><div></div>
      </div>
      {rows.slice(0, 18).map((r, i) => (
        <div key={i} style={{
          display: 'grid', gridTemplateColumns: '1.6fr 1fr 1fr 1fr 1fr 60px',
          padding: '12px 16px', alignItems: 'center',
          borderBottom: i < Math.min(17, rows.length - 1) ? '1px solid var(--border-light)' : 'none',
          fontSize: 12.5,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <PartnerAvatar partner={r.partner} size={26}/>
            <span style={{ fontWeight: 600 }}>{r.partner.name}</span>
          </div>
          <span><span style={{ fontSize: 16, marginRight: 6 }}>{r.country.flag}</span>{r.country.name}</span>
          <ServiceChip name={r.service} dense/>
          <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{r.currency}</span>
          <span style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{r.partner.rails.join(' — ')}</span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--success)' }}>● live</span>
        </div>
      ))}
    </div>
  );
}

// ─── Tab — Routing ────────────────────────────────────────────────
function PartnerRoutesBody() {
  const [view, setView] = React.useState('grid');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance — Money movement"
        title="Routing"
        subtitle="How money moves by default — which partner gets each route"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="sliders" size={12}/> Routing settings</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add rule</button>
          </>
        }
      />

      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, padding: '10px 14px',
      }}>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
          <b style={{ color: 'var(--text-primary)' }}>{ROUTES.length}</b> active rules — 1 needs attention
        </div>
        <ViewToggle view={view} setView={setView}/>
      </div>

      {view === 'grid' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14 }}>
          {ROUTES.map(r => <RouteCard key={r.id} route={r}/>)}
        </div>
      )}

      {view === 'list' && <RouteList/>}
    </div>
  );
}

function RouteCard({ route }) {
  const primary = partnerById(route.primary);
  const backup  = partnerById(route.backup);
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 18, display: 'flex', flexDirection: 'column', gap: 14,
    }}>
      {/* Top row — service + rule status */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <ServiceChip name={route.service}/>
        {route.status === 'degraded'
          ? <Pill tone="warning" dot size="sm">Backup active</Pill>
          : <Pill tone="success" dot size="sm">Healthy</Pill>}
      </div>

      {/* Flow */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 8,
          padding: '10px 14px', background: 'var(--surface-inset)',
          border: '1px solid var(--border-light)', borderRadius: 10,
        }}>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{route.from}</span>
        </div>
        <Icon name="fastforward" size={14} color="var(--text-tertiary)"/>
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 8,
          padding: '10px 14px', background: 'var(--brand-primary-10)',
          border: '1px solid transparent', borderRadius: 10,
        }}>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 600, color: 'var(--brand-primary)' }}>{route.to}</span>
        </div>
        <span style={{ marginLeft: 'auto', fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{route.vol}</span>
      </div>

      {/* Partner stack */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{
          display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 10, alignItems: 'center',
          padding: '10px 12px', background: 'var(--surface)',
          border: '1px solid var(--brand-primary-30, rgba(5,90,96,0.25))', borderRadius: 8,
        }}>
          <PartnerAvatar partner={primary} size={26}/>
          <div>
            <div style={{ fontSize: 12.5, fontWeight: 600 }}>{primary.name}</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>Primary — {route.share}% of volume</div>
          </div>
          <span style={{
            fontFamily: 'var(--font-mono)', fontSize: 9.5, fontWeight: 700, letterSpacing: '0.06em',
            textTransform: 'uppercase',
            padding: '2px 6px', borderRadius: 4,
            background: 'var(--brand-primary)', color: '#fff',
          }}>Primary</span>
        </div>
        <div style={{
          display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 10, alignItems: 'center',
          padding: '10px 12px', background: 'var(--surface)',
          border: '1px solid var(--border-light)', borderRadius: 8,
        }}>
          <PartnerAvatar partner={backup} size={26}/>
          <div>
            <div style={{ fontSize: 12.5, fontWeight: 600 }}>{backup.name}</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>Fallback — steps in if primary fails</div>
          </div>
          <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>Backup</span>
        </div>
      </div>
    </div>
  );
}

function RouteList() {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
      <div style={{
        display: 'grid', gridTemplateColumns: '1.2fr 1fr 1.2fr 1.2fr 1fr 130px 50px',
        padding: '11px 16px', background: 'var(--surface-inset)',
        borderBottom: '1px solid var(--border-light)',
        fontSize: 11, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
      }}>
        <div>Service</div><div>Route</div><div>Primary</div><div>Backup</div><div>Volume</div><div>Status</div><div></div>
      </div>
      {ROUTES.map((r, i) => {
        const primary = partnerById(r.primary);
        const backup  = partnerById(r.backup);
        return (
          <div key={r.id} style={{
            display: 'grid', gridTemplateColumns: '1.2fr 1fr 1.2fr 1.2fr 1fr 130px 50px',
            padding: '14px 16px', alignItems: 'center',
            borderBottom: i < ROUTES.length - 1 ? '1px solid var(--border-light)' : 'none',
            fontSize: 12.5,
          }}>
            <ServiceChip name={r.service} dense/>
            <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>
              {r.from} <Icon name="fastforward" size={10} color="var(--text-tertiary)"/> {r.to}
            </span>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
              <PartnerAvatar partner={primary} size={22}/>
              <span style={{ fontSize: 12 }}>{primary.name}</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
              <PartnerAvatar partner={backup} size={22}/>
              <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{backup.name}</span>
            </div>
            <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{r.vol}</span>
            {r.status === 'degraded'
              ? <Pill tone="warning" dot size="sm">Backup active</Pill>
              : <Pill tone="success" dot size="sm">Healthy</Pill>}
            <span className="hover-halo" style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6 }}>
              <Icon name="ellipsis" size={14} color="var(--text-secondary)"/>
            </span>
          </div>
        );
      })}
    </div>
  );
}

// ─── Tab — Activity ───────────────────────────────────────────────
function PartnerActivityBody() {
  const [view, setView] = React.useState('list');
  const [kind, setKind] = React.useState('All');
  const kinds = ['All', 'Payout', 'Collection', 'Bill payment', 'Airtime'];

  const filtered = kind === 'All' ? PARTNER_ACTIVITY : PARTNER_ACTIVITY.filter(a => a.service === kind);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance — Money movement"
        title="Activity"
        subtitle="Every transaction that moved through a partner — last 24 h"
        actions={<button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>}
      />

      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, padding: '10px 14px',
      }}>
        <div style={{ display: 'flex', gap: 6 }}>
          {kinds.map(k => {
            const active = kind === k;
            return (
              <button key={k} onClick={() => setKind(k)} style={{
                background: active ? 'var(--brand-primary-10)' : 'transparent',
                color: active ? 'var(--brand-primary)' : 'var(--text-secondary)',
                border: 'none', borderRadius: 6, padding: '5px 10px', cursor: 'pointer',
                fontSize: 11.5, fontWeight: active ? 600 : 500,
                display: 'inline-flex', alignItems: 'center', gap: 5,
              }}>
                {k !== 'All' && <Icon name={serviceIcon[k] || 'package'} size={11}/>}
                {k}
              </button>
            );
          })}
        </div>
        <ViewToggle view={view} setView={setView}/>
      </div>

      {view === 'grid' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14 }}>
          {filtered.map(a => <ActivityCard key={a.id} a={a}/>)}
        </div>
      )}

      {view === 'list' && <ActivityList rows={filtered}/>}
    </div>
  );
}

function ActivityCard({ a }) {
  const p = partnerById(a.partner);
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 16, display: 'flex', flexDirection: 'column', gap: 12,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <ServiceChip name={a.service} dense/>
        <Pill tone={a.tone} dot size="sm">{a.status}</Pill>
      </div>
      <div>
        <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{a.name}</div>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4 }}>{a.amount}</div>
      </div>
      {a.token && (
        <div style={{
          padding: '8px 10px', background: 'var(--surface-inset)',
          border: '1px dashed var(--border-light)', borderRadius: 8,
          fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)',
          display: 'flex', alignItems: 'center', gap: 8,
        }}>
          <Icon name="key" size={11} color="var(--text-tertiary)"/>
          {a.token}
        </div>
      )}
      {a.failureReason && (
        <div style={{
          padding: '8px 10px', background: 'rgba(217,122,108,0.06)',
          border: '1px solid rgba(217,122,108,0.18)', borderRadius: 8,
          fontSize: 11.5, color: 'var(--danger)',
        }}>
          ⚠ {a.failureReason}
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
          <PartnerAvatar partner={p} size={22}/>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>via {p.name}</span>
        </div>
        <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{a.ref} — {a.when}</span>
      </div>
    </div>
  );
}

function ActivityList({ rows }) {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
      <div style={{
        display: 'grid', gridTemplateColumns: '120px 1.6fr 1.2fr 1fr 1fr 130px 50px',
        padding: '11px 16px', background: 'var(--surface-inset)',
        borderBottom: '1px solid var(--border-light)',
        fontSize: 11, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
      }}>
        <div>Service</div><div>Name</div><div>Amount</div><div>Partner</div><div>Tracking</div><div>Status</div><div></div>
      </div>
      {rows.map((a, i) => {
        const p = partnerById(a.partner);
        return (
          <div key={a.id} style={{
            display: 'grid', gridTemplateColumns: '120px 1.6fr 1.2fr 1fr 1fr 130px 50px',
            padding: '14px 16px', alignItems: 'center',
            borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none',
            fontSize: 12.5,
          }}>
            <ServiceChip name={a.service} dense/>
            <div>
              <div style={{ fontWeight: 600 }}>{a.name}</div>
              {a.token && <div style={{ fontSize: 10.5, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', marginTop: 2 }}>{a.token}</div>}
              {a.failureReason && <div style={{ fontSize: 10.5, color: 'var(--danger)', marginTop: 2 }}>⚠ {a.failureReason}</div>}
            </div>
            <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{a.amount}</span>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
              <PartnerAvatar partner={p} size={20}/>
              <span style={{ fontSize: 12 }}>{p.name}</span>
            </div>
            <div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5 }}>{a.ref}</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{a.when}</div>
            </div>
            <Pill tone={a.tone} dot size="sm">{a.status}</Pill>
            <span className="hover-halo" style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6 }}>
              <Icon name="ellipsis" size={14} color="var(--text-secondary)"/>
            </span>
          </div>
        );
      })}
    </div>
  );
}

// ─── Tab — Updates ────────────────────────────────────────────────
function PartnerUpdatesBody() {
  const [view, setView] = React.useState('list');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance — Money movement"
        title="Updates"
        subtitle="Settlement notifications from your partners — 2 need a quick look"
        actions={<button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Refresh</button>}
      />

      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, padding: '10px 14px',
      }}>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
          <b style={{ color: 'var(--text-primary)' }}>{PARTNER_UPDATES.length}</b> notifications in the last 24 h
        </div>
        <ViewToggle view={view} setView={setView}/>
      </div>

      {view === 'grid' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14 }}>
          {PARTNER_UPDATES.map(u => <UpdateCard key={u.id} u={u}/>)}
        </div>
      )}

      {view === 'list' && <UpdateList/>}
    </div>
  );
}

function UpdateCard({ u }) {
  const meta = updateIcon[u.kind] || updateIcon.completed;
  const p = partnerById(u.partner);
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 16,
      borderLeft: `3px solid ${meta.color}`,
      display: 'flex', flexDirection: 'column', gap: 12,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <div style={{
            width: 26, height: 26, borderRadius: '50%',
            background: meta.color + '22', color: meta.color,
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          }}><Icon name={meta.icon} size={13}/></div>
          <span style={{ fontSize: 12, fontWeight: 600, color: meta.color, textTransform: 'uppercase', letterSpacing: '0.04em' }}>{meta.label}</span>
        </div>
        <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{u.when}</span>
      </div>
      <div style={{ fontSize: 13.5, fontWeight: 500, color: 'var(--text-primary)', lineHeight: 1.4 }}>
        {u.subject}
      </div>
      <div style={{ fontSize: 11.5, fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{u.details}</div>
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        paddingTop: 10, borderTop: '1px solid var(--border-light)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
          <PartnerAvatar partner={p} size={20}/>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{p.name}</span>
          {u.verified
            ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, marginLeft: 6, fontSize: 10.5, color: 'var(--success)' }}>
                <Icon name="shield" size={10}/> Verified
              </span>
            : <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, marginLeft: 6, fontSize: 10.5, color: 'var(--danger)' }}>
                <Icon name="alertc" size={10}/> Signature off
              </span>}
        </div>
        {u.action === 'review' && <button className="btn btn-outline btn-sm">Review</button>}
        {u.action === 'retry'  && <button className="btn btn-outline btn-sm">Retry</button>}
        {u.action === 'investigate' && <button className="btn btn-outline btn-sm" style={{ color: 'var(--danger)' }}>Investigate</button>}
      </div>
    </div>
  );
}

function UpdateList() {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
      <div style={{
        display: 'grid', gridTemplateColumns: '130px 2fr 1.4fr 100px 110px 110px',
        padding: '11px 16px', background: 'var(--surface-inset)',
        borderBottom: '1px solid var(--border-light)',
        fontSize: 11, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
      }}>
        <div>Type</div><div>Update</div><div>Partner</div><div>Verified</div><div>When</div><div></div>
      </div>
      {PARTNER_UPDATES.map((u, i) => {
        const meta = updateIcon[u.kind] || updateIcon.completed;
        const p = partnerById(u.partner);
        return (
          <div key={u.id} style={{
            display: 'grid', gridTemplateColumns: '130px 2fr 1.4fr 100px 110px 110px',
            padding: '14px 16px', alignItems: 'center',
            borderBottom: i < PARTNER_UPDATES.length - 1 ? '1px solid var(--border-light)' : 'none',
            fontSize: 12.5,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <div style={{
                width: 22, height: 22, borderRadius: '50%',
                background: meta.color + '22', color: meta.color,
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              }}><Icon name={meta.icon} size={11}/></div>
              <span style={{ fontSize: 11.5, fontWeight: 600, color: meta.color }}>{meta.label}</span>
            </div>
            <div>
              <div style={{ fontWeight: 500 }}>{u.subject}</div>
              <div style={{ fontSize: 10.5, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', marginTop: 2 }}>{u.details}</div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
              <PartnerAvatar partner={p} size={20}/>
              <span style={{ fontSize: 12 }}>{p.name}</span>
            </div>
            {u.verified
              ? <Pill tone="success" dot size="sm">Yes</Pill>
              : <Pill tone="danger" dot size="sm">Off</Pill>}
            <span style={{ fontSize: 11.5, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{u.when}</span>
            {u.action
              ? <button className="btn btn-ghost btn-sm">{u.action[0].toUpperCase() + u.action.slice(1)}</button>
              : <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>—</span>}
          </div>
        );
      })}
    </div>
  );
}

// ─── Standalone — Partner detail (Flutterwave) ────────────────────
function ScreenPartnerDetail() {
  const p = partnerById('flw');
  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 22 }}>
      <PageHeader
        eyebrow="Finance — Network — Partners"
        title={p.name}
        subtitle={
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 10 }}>
            <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{p.code}</span>
            <span style={{ color: 'var(--border)' }}>—</span>
            <Pill tone={p.tone} dot size="sm">{p.statusLabel}</Pill>
            <span style={{ color: 'var(--border)' }}>—</span>
            <span style={{ color: 'var(--text-tertiary)' }}>last heartbeat {p.last}</span>
          </span>
        }
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="terminal" size={12}/> Test sandbox</button>
            <button className="btn btn-outline btn-sm"><Icon name="cog" size={12}/> Configure</button>
            <button className="btn btn-outline btn-sm" style={{ color: 'var(--danger)' }}><Icon name="ban" size={12}/> Disable</button>
          </>
        }
      />

      {/* Identity strip */}
      <div style={{
        display: 'grid', gridTemplateColumns: 'auto 1fr auto auto auto auto',
        gap: 24, padding: '22px 24px',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 14, alignItems: 'center',
      }}>
        <PartnerAvatar partner={p} size={64}/>
        <div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>Services offered</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5, marginTop: 8 }}>
            {p.services.map(s => <ServiceChip key={s} name={s}/>)}
          </div>
        </div>
        {[
          { l: 'This month',  v: p.volMonth, d: p.volDelta },
          { l: 'On time',     v: `${p.settlementOnTime}%` },
          { l: 'Avg fee',     v: p.fee },
          { l: 'Avg latency', v: p.latency },
        ].map((s, i) => (
          <div key={i}>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600 }}>{s.l}</div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 17, fontWeight: 600, marginTop: 4 }}>{s.v}</div>
            {s.d && <div style={{ fontSize: 10.5, color: s.d.startsWith('+') ? 'var(--success)' : 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{s.d}</div>}
          </div>
        ))}
      </div>

      {/* Two-column body */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20 }}>
        {/* What Flutterwave can do — Coverage */}
        <Card title="What Flutterwave can do" subtitle="Countries and currencies it supports today">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14, margin: '12px 6px 4px' }}>
            <div>
              <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 8 }}>Countries</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {p.countries.map(c => {
                  const country = COVERAGE_COUNTRIES.find(x => x.code === c);
                  return (
                    <span key={c} style={{
                      display: 'inline-flex', alignItems: 'center', gap: 6,
                      padding: '5px 10px', borderRadius: 999,
                      background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
                      fontSize: 12, fontWeight: 500,
                    }}>
                      <span>{country?.flag}</span>{country?.name || c}
                    </span>
                  );
                })}
              </div>
            </div>
            <div>
              <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 8 }}>Currencies</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
                {p.currencies.map(c => (
                  <span key={c} style={{
                    fontFamily: 'var(--font-mono)', fontSize: 11.5, fontWeight: 600,
                    padding: '4px 9px', borderRadius: 4,
                    background: 'var(--surface-inset)', color: 'var(--text-primary)',
                    border: '1px solid var(--border-light)',
                  }}>{c}</span>
                ))}
              </div>
            </div>
            <div>
              <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 8 }}>Methods</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {p.rails.map(r => (
                  <span key={r} style={{
                    display: 'inline-flex', alignItems: 'center', gap: 5,
                    padding: '5px 10px', borderRadius: 999,
                    background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
                    fontSize: 11.5, color: 'var(--text-primary)',
                  }}>
                    <Icon name={r === 'Bank' ? 'landmark' : r === 'Card' ? 'creditcard' : 'mobile'} size={11} color="var(--text-secondary)"/>
                    {r}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </Card>

        {/* Connected billers */}
        <Card title="Connected billers" subtitle="Bills and airtime Flutterwave can pay" action={<button className="btn btn-ghost btn-sm">Manage</button>}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, margin: '12px 6px 4px' }}>
            {[
              { sym: 'MT', name: 'MTN Nigeria',     cat: 'Telco — airtime/data', color: '#e6b800' },
              { sym: 'IE', name: 'Ikeja Electric',  cat: 'Utilities — electric', color: '#1e4d8c' },
              { sym: 'DS', name: 'DSTV',            cat: 'TV & media',           color: '#003087' },
              { sym: 'AT', name: 'Airtel',          cat: 'Telco — airtime/data', color: '#cc0000' },
              { sym: 'LR', name: 'LIRS Lagos Tax',  cat: 'Tax & government',     color: '#1b6e3f' },
            ].map((b, i) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 10, alignItems: 'center',
                padding: '8px 10px', borderRadius: 8,
                background: 'var(--surface)', border: '1px solid var(--border-light)',
              }}>
                <div style={{
                  width: 28, height: 28, borderRadius: 6,
                  background: b.color, color: '#fff',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 11,
                }}>{b.sym}</div>
                <div>
                  <div style={{ fontSize: 12.5, fontWeight: 600 }}>{b.name}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{b.cat}</div>
                </div>
                <Pill tone="success" dot size="sm">Live</Pill>
              </div>
            ))}
            <button className="btn btn-ghost btn-sm" style={{ marginTop: 4 }}>+ View all 38 billers</button>
          </div>
        </Card>

        {/* Connected banks */}
        <Card title="Connected banks & wallets" subtitle="Where Flutterwave can route money" action={<button className="btn btn-ghost btn-sm">Manage</button>}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, margin: '12px 6px 4px' }}>
            {[
              { code: '044', name: 'Access Bank',         country: 'NG' },
              { code: '058', name: 'Guaranty Trust Bank', country: 'NG' },
              { code: '011', name: 'First Bank',          country: 'NG' },
              { code: 'MPS', name: 'M-Pesa',              country: 'KE' },
              { code: 'GHA', name: 'GCB Bank',            country: 'GH' },
            ].map((b, i) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: 'auto 1fr auto auto', gap: 10, alignItems: 'center',
                padding: '8px 10px', borderRadius: 8,
                background: 'var(--surface)', border: '1px solid var(--border-light)',
              }}>
                <Icon name={b.code === 'MPS' ? 'mobile' : 'landmark'} size={16} color="var(--text-secondary)"/>
                <div>
                  <div style={{ fontSize: 12.5, fontWeight: 600 }}>{b.name}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>Provider code — {b.code}</div>
                </div>
                <span style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{b.country}</span>
                <Pill tone="success" dot size="sm">Verified</Pill>
              </div>
            ))}
            <button className="btn btn-ghost btn-sm" style={{ marginTop: 4 }}>+ View all 142 institutions</button>
          </div>
        </Card>

        {/* Recent activity */}
        <Card title="Recent activity through Flutterwave" subtitle="Last 5 transactions">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, margin: '12px 6px 4px' }}>
            {PARTNER_ACTIVITY.filter(a => a.partner === 'flw').slice(0, 5).map(a => (
              <div key={a.id} style={{
                display: 'grid', gridTemplateColumns: 'auto 1fr auto auto', gap: 10, alignItems: 'center',
                padding: '8px 10px', borderRadius: 8,
                background: 'var(--surface)', border: '1px solid var(--border-light)',
              }}>
                <Icon name={serviceIcon[a.service] || 'package'} size={14} color="var(--text-secondary)"/>
                <div>
                  <div style={{ fontSize: 12.5, fontWeight: 600 }}>{a.name}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{a.ref} — {a.when}</div>
                </div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600 }}>{a.amount}</span>
                <Pill tone={a.tone} dot size="sm">{a.status}</Pill>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </div>
  );
}

// ─── Screen exports ───────────────────────────────────────────────
function ScreenPartnerHubOverview() { return <PartnerHubShell initial="overview"/>; }
function ScreenPartnerHubPartners() { return <PartnerHubShell initial="partners"/>; }
function ScreenPartnerHubCoverage() { return <PartnerHubShell initial="coverage"/>; }
function ScreenPartnerHubRoutes()   { return <PartnerHubShell initial="routes"/>;   }
function ScreenPartnerHubActivity() { return <PartnerHubShell initial="activity"/>; }
function ScreenPartnerHubUpdates()  { return <PartnerHubShell initial="updates"/>;  }

Object.assign(window, {
  ScreenPartnerHubOverview,
  ScreenPartnerHubPartners,
  ScreenPartnerHubCoverage,
  ScreenPartnerHubRoutes,
  ScreenPartnerHubActivity,
  ScreenPartnerHubUpdates,
  ScreenPartnerDetail,
});
