// ─── Agent Extensions — Spec 033 ──────────────────────────────────
// Direction 2 (Library + Drawer), built out as the real hub.
//
// One flat library of extensions; click any card → a right-side
// drawer carries the full detail + lifecycle + test harness for that
// one extension. A role lens ("My extensions" / "Review queue") folds
// the PlatformAdmin approvals queue into the same spine. "Add" and the
// full "Test harness" are drawer modes too — one design language for
// every state.
//
// Per the spec (and the team decision to keep gates until the skill-
// script sandbox question is settled), ALL THREE surfaces — Skills,
// MCP servers, HTTP tools — share ONE uniform approval lifecycle:
//   Draft → In review → Approved → Active   (Rejected is a side-exit)
// Tenant mutating tools default to High; only a PlatformAdmin lowers a
// tier or enables a skill's scripts. Operator language up front; the
// engineer detail (slugs, endpoints, tiers, tool names) lives in
// mono-font subtext.

// ─── Data ─────────────────────────────────────────────────────────
const EXTENSIONS = [
  // ── Skills ──
  { id: 'ext-01', type: 'skill', name: 'Invoice reconciliation', slug: 'invoice-reconciliation',
    desc: 'Match incoming bank transactions to open invoices and draft the journal entry when confidence is high.',
    state: 'active', tier: 'na', owner: 'Maria Gomez', usage: 412, lastUsed: '2m ago',
    meta: { scripts: true, scriptsEnabled: true, allowedTools: ['search_invoices', 'list_bank_transactions', 'match_invoice_to_txn', 'draft_journal_entry'], sizeKb: 1.8 } },
  { id: 'ext-02', type: 'skill', name: 'AR aging summary', slug: 'ar-aging-summary',
    desc: 'Produce an aging summary across the receivables ledger with sub-totals by tier and a chase-list.',
    state: 'active', tier: 'na', owner: 'Maria Gomez', usage: 88, lastUsed: '38m ago',
    meta: { scripts: false, scriptsEnabled: false, allowedTools: ['query_ledger', 'group_by_age'], sizeKb: 0.9 } },
  { id: 'ext-03', type: 'skill', name: 'Dunning cadence', slug: 'dunning-cadence',
    desc: 'Choose a dunning template and channel for an overdue invoice using customer tier and prior-contact history.',
    state: 'draft', tier: 'na', owner: 'David Lynn', usage: 0, lastUsed: 'never',
    meta: { scripts: false, scriptsEnabled: false, allowedTools: ['list_overdue', 'pick_template'], sizeKb: 1.6 } },
  { id: 'ext-04', type: 'skill', name: 'VAT return helper', slug: 'vat-return-helper',
    desc: 'Assemble a quarterly VAT return from posted transactions and run the box-by-box validation script.',
    state: 'review', tier: 'na', owner: 'Maria Gomez', usage: 0, lastUsed: 'never',
    meta: { scripts: true, scriptsEnabled: false, allowedTools: ['query_ledger', 'sum_vat_boxes'], sizeKb: 2.2, reviewReason: 'Contains executable scripts — needs platform review' } },

  // ── MCP servers ──
  { id: 'ext-05', type: 'mcp', name: 'Open Banking UK', slug: 'open-banking-uk',
    desc: 'Live account balances and transaction feeds from the tenant’s connected UK banks.',
    state: 'active', tier: 'mixed', owner: 'Oliver Chen', usage: 1840, lastUsed: 'now',
    meta: { endpoint: 'https://mcp.openbanking-uk.io/sse', auth: 'OAuth2', authSet: true, egressOk: true,
      tools: [
        { name: 'get_account_balance', tier: 'readonly' },
        { name: 'list_transactions',   tier: 'readonly' },
        { name: 'initiate_payment',    tier: 'high' },
        { name: 'cancel_payment',      tier: 'high' },
      ] } },
  { id: 'ext-06', type: 'mcp', name: 'Companies House', slug: 'companies-house',
    desc: 'UK company registry lookups — officers, filing history, and registered addresses.',
    state: 'approved', tier: 'readonly', owner: 'Oliver Chen', usage: 0, lastUsed: 'never',
    meta: { endpoint: 'https://mcp.company-info.gov.uk/sse', auth: 'API key', authSet: true, egressOk: true,
      tools: [
        { name: 'search_companies',  tier: 'readonly' },
        { name: 'get_officers',      tier: 'readonly' },
        { name: 'get_filings',       tier: 'readonly' },
        { name: 'get_address',       tier: 'readonly' },
      ] } },
  { id: 'ext-07', type: 'mcp', name: 'OFAC sanctions screen', slug: 'ofac-sanctions',
    desc: 'Screen a counterparty name against OFAC and UN consolidated sanctions lists.',
    state: 'review', tier: 'readonly', owner: 'Raj Patel', usage: 0, lastUsed: 'never',
    meta: { endpoint: 'https://mcp.sanctions-screen.io/sse', auth: 'mTLS', authSet: true, egressOk: true,
      tools: [
        { name: 'screen_name',   tier: 'readonly' },
        { name: 'get_match',     tier: 'readonly' },
        { name: 'log_decision',  tier: 'medium' },
      ], reviewReason: 'New network destination — egress host pending platform approval' } },

  // ── HTTP tools ──
  { id: 'ext-08', type: 'http', name: 'Internal pricing API', slug: 'internal-pricing-api',
    desc: 'Look up the current contract rate for a route from the internal pricing service.',
    state: 'active', tier: 'readonly', owner: 'David Lynn', usage: 248, lastUsed: '14m ago',
    meta: { method: 'GET', url: 'https://pricing.primrose.internal/rates/{routeId}', auth: 'API key', authSet: true, params: 1, egressOk: true } },
  { id: 'ext-09', type: 'http', name: 'FX quote lookup', slug: 'fx-quote-lookup',
    desc: 'Fetch a live mid-market FX quote for a currency pair from the treasury rate feed.',
    state: 'active', tier: 'readonly', owner: 'David Lynn', usage: 162, lastUsed: '5m ago',
    meta: { method: 'GET', url: 'https://rates.primrose.internal/fx/{base}/{quote}', auth: 'API key', authSet: true, params: 2, egressOk: true } },
  { id: 'ext-10', type: 'http', name: 'CRM — create contact', slug: 'crm-create-contact',
    desc: 'Create a new contact record in the tenant CRM when a customer is onboarded.',
    state: 'review', tier: 'high', owner: 'Kiran Desai', usage: 0, lastUsed: 'never',
    meta: { method: 'POST', url: 'https://api.crm-vendor.com/v2/contacts', auth: 'OAuth2', authSet: true, params: 4, egressOk: true,
      reviewReason: 'Writes to an external system — defaults to High' } },
  { id: 'ext-11', type: 'http', name: 'Warehouse stock adjust', slug: 'warehouse-stock-adjust',
    desc: 'Adjust the on-hand stock count for a SKU in the warehouse management system.',
    state: 'draft', tier: 'high', owner: 'Kiran Desai', usage: 0, lastUsed: 'never',
    meta: { method: 'POST', url: 'https://wms.primrose.internal/stock/{sku}/adjust', auth: 'API key', authSet: false, params: 2, egressOk: true } },
  { id: 'ext-12', type: 'http', name: 'Slack notify', slug: 'slack-notify',
    desc: 'Post a message to a Slack channel when an agent needs a human.',
    state: 'rejected', tier: 'high', owner: 'David Lynn', usage: 0, lastUsed: 'never',
    meta: { method: 'POST', url: 'https://hooks.slack.com/services/•••', auth: 'Webhook', authSet: true, params: 2, egressOk: false,
      rejectReason: 'Egress host hooks.slack.com is not on the platform allow-list' } },
];

const EXT_ACTIVITY = [
  { who: 'Oliver Chen', action: 'approved', target: 'Companies House', kind: 'mcp', when: '2h ago' },
  { who: 'Maria Gomez', action: 'submitted', target: 'VAT return helper', kind: 'skill', when: '3h ago' },
  { who: 'Platform',    action: 'rejected', target: 'Slack notify', kind: 'http', when: '5h ago' },
  { who: 'David Lynn',  action: 'activated', target: 'FX quote lookup', kind: 'http', when: '1d ago' },
];

// ─── Meta + helpers ───────────────────────────────────────────────
const EXT_TYPE = {
  skill: { label: 'Skill',      icon: 'book',   color: '#055a60', addLabel: 'Upload skill' },
  mcp:   { label: 'MCP server', icon: 'server', color: '#7b76b6', addLabel: 'Connect server' },
  http:  { label: 'HTTP tool',  icon: 'plug',   color: '#b4741e', addLabel: 'Declare tool' },
};
const EXT_STATE = {
  draft:    { label: 'Draft',     tone: 'muted',   color: '#8a97a3' },
  review:   { label: 'In review', tone: 'warning', color: '#b4741e' },
  approved: { label: 'Approved',  tone: 'tint',    color: '#055a60' },
  active:   { label: 'Active',    tone: 'success', color: '#1f7a5e' },
  rejected: { label: 'Rejected',  tone: 'danger',  color: '#c44536' },
};
const EXT_TIER = {
  readonly: { label: 'Read only', color: '#5a6a76', bg: '#eef0f2' },
  medium:   { label: 'Medium',    color: '#7a5a10', bg: '#fff5d9' },
  high:     { label: 'High',      color: '#b3261e', bg: '#fbe2dd' },
  mixed:    { label: 'Mixed',     color: '#5a6a76', bg: '#eef0f2' },
  na:       { label: '',          color: '#5a6a76', bg: '#eef0f2' },
};
const extById = (id) => EXTENSIONS.find(e => e.id === id);
const extCount = (pred) => EXTENSIONS.filter(pred).length;
const TYPE_COUNTS = {
  all: EXTENSIONS.length,
  skill: extCount(e => e.type === 'skill'),
  mcp: extCount(e => e.type === 'mcp'),
  http: extCount(e => e.type === 'http'),
};

// ─── Atoms ────────────────────────────────────────────────────────
function ExtTile({ type, size = 40 }) {
  const t = EXT_TYPE[type];
  return (
    <div style={{
      width: size, height: size, borderRadius: Math.round(size * 0.24),
      background: t.color + '18', color: t.color,
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center', flex: 'none',
    }}><Icon name={t.icon} size={Math.round(size * 0.5)}/></div>
  );
}
function ExtTypeChip({ type, dense }) {
  const t = EXT_TYPE[type];
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5,
      padding: dense ? '2px 7px' : '3px 9px', borderRadius: 999,
      background: t.color + '14', color: t.color,
      fontSize: dense ? 10 : 11, fontWeight: 600, letterSpacing: '0.02em',
    }}><Icon name={t.icon} size={dense ? 9 : 10}/>{t.label}</span>
  );
}
function ExtStateBadge({ state, size }) {
  const s = EXT_STATE[state];
  return <Pill tone={s.tone} dot size={size}>{s.label}</Pill>;
}
function ExtTierPill({ tier, size }) {
  if (!tier || tier === 'na') return null;
  const t = EXT_TIER[tier];
  return (
    <span style={{
      fontFamily: 'var(--font-mono)', fontSize: size === 'sm' ? 9.5 : 10.5, fontWeight: 700,
      letterSpacing: '0.04em', textTransform: 'uppercase',
      padding: size === 'sm' ? '1px 6px' : '2px 7px', borderRadius: 4,
      background: t.bg, color: t.color,
    }}>{t.label}</span>
  );
}
function extFactLine(e) {
  if (e.type === 'skill') return `${e.meta.allowedTools.length} allowed tools — ${e.meta.sizeKb} KB${e.meta.scripts ? ' — has scripts' : ''}`;
  if (e.type === 'mcp') return `${e.meta.tools.length} tools — ${e.meta.auth}`;
  return `${e.meta.method} — ${e.meta.params} params — ${e.meta.auth}`;
}
function ExtViewToggle({ view, setView }) {
  return (
    <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
      <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginRight: 4 }}>View</span>
      {['grid', 'list'].map(v => (
        <button key={v} onClick={() => setView(v)} style={{
          background: view === v ? 'var(--surface-inset)' : 'transparent',
          color: view === v ? 'var(--text-primary)' : 'var(--text-tertiary)',
          border: '1px solid ' + (view === v ? 'var(--border-medium)' : 'var(--border-light)'),
          borderRadius: 6, padding: '5px 10px', cursor: 'pointer',
          display: 'flex', alignItems: 'center', gap: 5, fontSize: 11.5, fontWeight: 500,
        }}><Icon name={v} size={12}/>{v[0].toUpperCase() + v.slice(1)}</button>
      ))}
    </div>
  );
}
function ExtTypeFilter({ value, onChange, counts }) {
  const opts = [{ id: 'all', label: 'All' }, { id: 'skill', label: 'Skills' }, { id: 'mcp', label: 'MCP servers' }, { id: 'http', label: 'HTTP tools' }];
  return (
    <div style={{ display: 'flex', gap: 4 }}>
      {opts.map(o => {
        const active = value === o.id;
        return (
          <button key={o.id} onClick={() => onChange(o.id)} style={{
            background: active ? 'var(--brand-primary-10)' : 'transparent',
            color: active ? 'var(--brand-primary)' : 'var(--text-secondary)',
            border: 'none', borderRadius: 6, padding: '5px 11px', cursor: 'pointer',
            fontSize: 12, fontWeight: active ? 600 : 500, display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            {o.label}
            {counts && <span style={{
              fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600, padding: '0 5px', borderRadius: 4,
              background: active ? 'var(--surface)' : 'var(--surface-inset)',
              color: active ? 'var(--brand-primary)' : 'var(--text-tertiary)',
            }}>{counts[o.id]}</span>}
          </button>
        );
      })}
    </div>
  );
}

// ─── Hub orchestrator ─────────────────────────────────────────────
function ExtHub({ initialLens = 'tenant', initialDrawer = 'detail', initialSel = 'ext-01' }) {
  const [lens, setLens] = React.useState(initialLens);
  const [type, setType] = React.useState('all');
  const [view, setView] = React.useState('grid');
  const [drawer, setDrawer] = React.useState(initialDrawer);
  const [sel, setSel] = React.useState(initialSel);

  const isPlatform = lens === 'platform';
  let items = type === 'all' ? EXTENSIONS : EXTENSIONS.filter(e => e.type === type);
  if (isPlatform) items = items.filter(e => e.state === 'review');

  const e = sel ? extById(sel) : null;
  const reviewCount = extCount(x => x.state === 'review');

  const openCard = (id) => { setSel(id); setDrawer(isPlatform ? 'review' : 'detail'); };
  const close = () => setDrawer(null);
  const switchLens = (l) => { setLens(l); setDrawer(null); };

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`@keyframes extDrawerIn { from { transform: translateX(24px); opacity: 0; } to { transform: translateX(0); opacity: 1; } }`}</style>

      <div style={{ height: '100%', overflow: 'auto', padding: '28px 36px', filter: drawer ? 'saturate(0.97)' : 'none' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20, maxWidth: 1120 }}>
          <PageHeader
            eyebrow="AI — Agents"
            title="Agent Extensions"
            subtitle={`${EXTENSIONS.length} extensions — ${extCount(x => x.state === 'active')} active — ${reviewCount} in review — ${extCount(x => x.state === 'draft')} drafts`}
            actions={<>
              <RoleLens lens={lens} setLens={switchLens} reviewCount={reviewCount}/>
              <button className="btn btn-outline btn-sm" onClick={() => setDrawer('harness')}><Icon name="beaker" size={12}/> Test harness</button>
              <button className="btn btn-primary btn-sm" onClick={() => setDrawer('add')}><Icon name="plus" size={12}/> Add extension</button>
            </>}
          />

          {/* Review-queue banner */}
          {isPlatform && (
            <div style={{
              padding: '12px 14px', background: 'rgba(180,116,30,0.07)', border: '1px solid rgba(180,116,30,0.25)',
              borderRadius: 10, display: 'flex', alignItems: 'center', gap: 10, fontSize: 12.5, color: 'var(--text-primary)',
            }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 700, padding: '2px 7px', borderRadius: 4, background: '#f3e7fb', color: '#6a2c8a' }}>PLATFORM ADMIN</span>
              <span style={{ flex: 1 }}>
                <b>{reviewCount}</b> extension{reviewCount === 1 ? '' : 's'} awaiting review — code execution, money tools, and new network destinations cross the tenant trust boundary.
              </span>
            </div>
          )}

          {/* Controls */}
          <div style={{
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '10px 14px',
          }}>
            <ExtTypeFilter value={type} onChange={setType} counts={TYPE_COUNTS}/>
            <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
              <div style={{ position: 'relative', width: 200 }}>
                <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}><Icon name="search" size={13}/></span>
                <input className="input" placeholder="Search…" style={{ paddingLeft: 30, height: 30, fontSize: 12, background: 'var(--surface-inset)', border: 'none', width: '100%' }}/>
              </div>
              <ExtViewToggle view={view} setView={setView}/>
            </div>
          </div>

          {/* Grid / list */}
          {view === 'grid' && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
              {items.map(x => <ExtLibCard key={x.id} e={x} selected={x.id === sel && !!drawer} platform={isPlatform} onClick={() => openCard(x.id)}/>)}
              {items.length === 0 && <ExtEmpty/>}
            </div>
          )}
          {view === 'list' && (
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
              {items.map((x, i) => (
                <div key={x.id} onClick={() => openCard(x.id)} style={{
                  display: 'grid', gridTemplateColumns: '40px 1fr auto auto auto', gap: 14, alignItems: 'center',
                  padding: '13px 16px', borderBottom: i < items.length - 1 ? '1px solid var(--border-light)' : 'none',
                  cursor: 'pointer', background: x.id === sel && drawer ? 'var(--brand-primary-10)' : 'transparent',
                }}>
                  <ExtTile type={x.type} size={32}/>
                  <div><div style={{ fontSize: 13, fontWeight: 600 }}>{x.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{extFactLine(x)}</div></div>
                  <ExtTypeChip type={x.type} dense/>
                  <ExtTierPill tier={x.tier} size="sm"/>
                  <ExtStateBadge state={x.state} size="sm"/>
                </div>
              ))}
              {items.length === 0 && <div style={{ padding: 24 }}><ExtEmpty/></div>}
            </div>
          )}
        </div>
      </div>

      {/* Drawer modes */}
      {drawer === 'detail'  && e && <ExtDetailDrawer e={e} onClose={close}/>}
      {drawer === 'review'  && e && <ExtDetailDrawer e={e} review onClose={close}/>}
      {drawer === 'add'     && <ExtAddDrawer onClose={close}/>}
      {drawer === 'harness' && <ExtHarnessDrawer initial={e && e.state !== 'active' ? e.id : 'ext-10'} onClose={close}/>}
    </div>
  );
}

function RoleLens({ lens, setLens, reviewCount }) {
  return (
    <div style={{ display: 'flex', gap: 4, padding: 3, background: 'var(--surface-inset)', borderRadius: 8 }}>
      {[
        { id: 'tenant',   label: 'My extensions' },
        { id: 'platform', label: 'Review queue', badge: reviewCount },
      ].map(l => {
        const on = lens === l.id;
        return (
          <button key={l.id} onClick={() => setLens(l.id)} style={{
            border: 'none', background: on ? 'var(--surface)' : 'transparent',
            padding: '5px 11px', borderRadius: 6, cursor: 'pointer',
            fontSize: 11.5, fontWeight: on ? 600 : 500,
            color: on ? 'var(--text-primary)' : 'var(--text-secondary)',
            boxShadow: on ? '0 1px 2px rgba(0,0,0,0.04)' : 'none',
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            {l.label}
            {l.badge ? <span style={{ fontFamily: 'var(--font-mono)', fontSize: 9.5, fontWeight: 700, minWidth: 15, textAlign: 'center', padding: '0 4px', borderRadius: 999, background: 'var(--warning)', color: '#fff' }}>{l.badge}</span> : null}
          </button>
        );
      })}
    </div>
  );
}

function ExtEmpty() {
  return (
    <div style={{ gridColumn: '1 / -1', padding: '40px 10px', textAlign: 'center', color: 'var(--text-tertiary)' }}>
      <Icon name="check" size={22} color="var(--success)"/>
      <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-secondary)', marginTop: 8 }}>Nothing in the review queue</div>
      <div style={{ fontSize: 11.5, marginTop: 2 }}>Every extension is either live or still a draft.</div>
    </div>
  );
}

function ExtLibCard({ e, selected, platform, onClick }) {
  return (
    <div onClick={onClick} style={{
      background: 'var(--surface)',
      border: '1px solid ' + (selected ? EXT_TYPE[e.type].color : 'var(--border-light)'),
      boxShadow: selected ? `0 0 0 1px ${EXT_TYPE[e.type].color}` : 'none',
      borderRadius: 12, padding: 16, display: 'flex', flexDirection: 'column', gap: 12, cursor: 'pointer',
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <ExtTile type={e.type} size={40}/>
        <ExtStateBadge state={e.state} size="sm"/>
      </div>
      <div>
        <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{e.name}</div>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{e.slug}</div>
      </div>
      <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.5, minHeight: 54 }}>{e.desc}</div>
      {/* Review reason in platform lens */}
      {platform && e.meta.reviewReason && (
        <div style={{ fontSize: 11, color: 'var(--warning)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="alertc" size={11}/> {e.meta.reviewReason}
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingTop: 10, borderTop: '1px solid var(--border-light)' }}>
        <ExtTypeChip type={e.type} dense/>
        {platform
          ? <div style={{ display: 'flex', gap: 6 }}>
              <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 9px', color: 'var(--danger)' }} onClick={ev => ev.stopPropagation()}>Reject</button>
              <button className="btn btn-primary btn-sm" style={{ height: 24, padding: '0 10px' }} onClick={ev => ev.stopPropagation()}>Approve</button>
            </div>
          : <ExtTierPill tier={e.tier} size="sm"/>}
      </div>
    </div>
  );
}

// ─── Drawer shell + sections ──────────────────────────────────────
function DrawerShell({ width = 520, onClose, children }) {
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,20,28,0.18)', zIndex: 5 }}/>
      <div style={{
        position: 'absolute', top: 0, right: 0, bottom: 0, width, zIndex: 6,
        background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
        boxShadow: '-20px 0 50px -20px rgba(0,0,0,0.25)',
        display: 'flex', flexDirection: 'column',
        animation: 'extDrawerIn 220ms cubic-bezier(0.2,0.8,0.2,1) both',
      }}>{children}</div>
    </>
  );
}
function DrawerSection({ title, hint, children }) {
  return (
    <div>
      <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)' }}>{title}</div>
      {hint && <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 3, marginBottom: 8 }}>{hint}</div>}
      <div style={{ marginTop: hint ? 0 : 8 }}>{children}</div>
    </div>
  );
}

// ─── Detail / Review drawer ───────────────────────────────────────
function ExtDetailDrawer({ e, review, onClose }) {
  return (
    <DrawerShell onClose={onClose}>
      {/* Header */}
      <div style={{ padding: '20px 22px 16px', borderBottom: '1px solid var(--border-light)' }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 14 }}>
          <ExtTile type={e.type} size={46}/>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 17, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>{e.name}</div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{e.slug}</div>
          </div>
          <span className="hover-halo" onClick={onClose} style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6, cursor: 'pointer' }}><Icon name="close" size={14}/></span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 14 }}>
          <ExtTypeChip type={e.type}/>
          <ExtStateBadge state={e.state}/>
          <ExtTierPill tier={e.tier}/>
          <div style={{ flex: 1 }}/>
          {!review && (
            e.state === 'active' ? <button className="btn btn-outline btn-sm"><Icon name="ban" size={11}/> Deactivate</button>
            : e.state === 'approved' ? <button className="btn btn-primary btn-sm"><Icon name="check" size={11}/> Activate</button>
            : e.state === 'draft' ? <button className="btn btn-primary btn-sm"><Icon name="upload" size={11}/> Submit for review</button>
            : e.state === 'rejected' ? <button className="btn btn-outline btn-sm"><Icon name="edit" size={11}/> Edit &amp; resubmit</button>
            : <button className="btn btn-outline btn-sm" disabled>Awaiting review</button>
          )}
        </div>
      </div>

      {/* Body */}
      <div style={{ flex: 1, overflow: 'auto', padding: '18px 22px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Platform review banner */}
        {review && (
          <div style={{ padding: '12px 14px', borderRadius: 10, background: 'rgba(180,116,30,0.07)', border: '1px solid rgba(180,116,30,0.25)' }}>
            <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, color: '#b4741e', marginBottom: 4 }}>Why this needs review</div>
            <div style={{ fontSize: 12.5, color: 'var(--text-primary)' }}>{e.meta.reviewReason}</div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 6 }}>Submitted by {e.owner}</div>
          </div>
        )}

        <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.55 }}>{e.desc}</div>

        {/* Type-specific */}
        {e.type === 'skill' && (
          <DrawerSection title="Allowed tools" hint="Intersected with the agent's existing tools — never widens authority">
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {e.meta.allowedTools.map(t => (
                <span key={t} style={{ fontFamily: 'var(--font-mono)', fontSize: 11, padding: '3px 8px', borderRadius: 4, background: 'var(--surface-inset)', border: '1px solid var(--border-light)', color: 'var(--text-primary)' }}>{t}</span>
              ))}
            </div>
            {e.meta.scripts && (
              <div style={{ marginTop: 12, padding: '10px 12px', borderRadius: 8, display: 'flex', alignItems: 'center', gap: 10,
                background: e.meta.scriptsEnabled ? 'rgba(31,122,94,0.06)' : 'rgba(180,116,30,0.06)',
                border: '1px solid ' + (e.meta.scriptsEnabled ? 'rgba(31,122,94,0.2)' : 'rgba(180,116,30,0.2)') }}>
                <Icon name="terminal" size={14} color={e.meta.scriptsEnabled ? '#1f7a5e' : '#b4741e'}/>
                <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', flex: 1 }}>
                  {e.meta.scriptsEnabled ? 'Scripts enabled by platform admin — runs under ScriptApproval.' : 'Scripts present but off — a platform admin must review and enable.'}
                </span>
                {review && !e.meta.scriptsEnabled && <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 8px', color: '#b4741e' }}>Enable</button>}
              </div>
            )}
          </DrawerSection>
        )}
        {e.type === 'mcp' && (
          <DrawerSection title="Discovered tools" hint="Each auto-classified by the gate at connect time">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {e.meta.tools.map(t => (
                <div key={t.name} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 10px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8 }}>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{t.name}</span>
                  <ExtTierPill tier={t.tier} size="sm"/>
                </div>
              ))}
            </div>
          </DrawerSection>
        )}
        {e.type === 'http' && (
          <DrawerSection title="Request">
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 700, padding: '2px 8px', borderRadius: 4,
                background: e.meta.method === 'GET' ? 'rgba(31,122,94,0.12)' : 'rgba(196,69,54,0.12)',
                color: e.meta.method === 'GET' ? '#1f7a5e' : '#c44536' }}>{e.meta.method}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)', wordBreak: 'break-all' }}>{e.meta.url}</span>
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 6 }}>{e.meta.params} declared parameters — the model can't smuggle extra fields</div>
          </DrawerSection>
        )}

        {/* Credentials */}
        {(e.type === 'mcp' || e.type === 'http') && (
          <DrawerSection title="Credentials">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8 }}>
              <Icon name="lock" size={14} color="var(--text-secondary)"/>
              <span style={{ fontSize: 12, color: 'var(--text-primary)', flex: 1 }}>{e.meta.auth}</span>
              {e.meta.authSet ? <Pill tone="success" dot size="sm">Configured</Pill> : <Pill tone="warning" dot size="sm">Not set</Pill>}
            </div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 6 }}>Write-only — the value is encrypted and never returned to the UI.</div>
          </DrawerSection>
        )}

        {/* Lifecycle (uniform across all three surfaces) */}
        <DrawerSection title="Lifecycle" hint="Every extension follows the same path: Draft → In review → Approved → Active">
          <ExtTimeline e={e}/>
        </DrawerSection>

        {/* Mini harness */}
        <DrawerSection title="Test harness" hint="Runs the real server code paths">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {['Validate', e.type === 'mcp' ? 'Dry-run connect & list' : 'Dry-run call', 'Sandbox invoke'].map((s, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '9px 11px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8 }}>
                <Icon name={i === 2 ? 'beaker' : 'check'} size={13} color={i === 2 ? 'var(--brand-primary)' : 'var(--success)'}/>
                <span style={{ fontSize: 12, color: 'var(--text-primary)', flex: 1 }}>{s}</span>
                {i === 2 ? <button className="btn btn-outline btn-sm" style={{ height: 24, padding: '0 9px' }}>Run</button> : <span style={{ fontSize: 11, color: 'var(--success)' }}>passed</span>}
              </div>
            ))}
          </div>
        </DrawerSection>
      </div>

      {/* Platform review footer */}
      {review && (
        <div style={{ padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)', flex: 1 }}>Approving makes it eligible — the tenant still chooses when to activate.</span>
          <button className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}>Reject</button>
          <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Approve</button>
        </div>
      )}
    </DrawerShell>
  );
}

function ExtTimeline({ e }) {
  const order = ['draft', 'review', 'approved', 'active'];
  const rejected = e.state === 'rejected';
  const curIdx = rejected ? 1 : order.indexOf(e.state);
  const labels = { draft: 'Created', review: 'Submitted for review', approved: 'Approved by platform', active: 'Activated' };
  return (
    <div style={{ display: 'flex', flexDirection: 'column' }}>
      {order.map((st, i) => {
        const done = i <= curIdx && !(rejected && i > 0);
        const isRejectStop = rejected && i === 1;
        return (
          <div key={st} style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
              <div style={{
                width: 20, height: 20, borderRadius: 999,
                background: isRejectStop ? 'var(--danger)' : done ? 'var(--brand-primary)' : 'var(--surface-inset)',
                color: '#fff', display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                border: done || isRejectStop ? 'none' : '1px solid var(--border-medium)',
              }}>{isRejectStop ? <Icon name="close" size={11}/> : done ? <Icon name="check" size={11}/> : null}</div>
              {i < order.length - 1 && <div style={{ width: 2, height: 22, background: done && i < curIdx ? 'var(--brand-primary)' : 'var(--border-light)' }}/>}
            </div>
            <div style={{ paddingBottom: 14 }}>
              <div style={{ fontSize: 12.5, fontWeight: done || isRejectStop ? 600 : 500, color: done || isRejectStop ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>
                {isRejectStop ? 'Rejected' : labels[st]}
              </div>
              {isRejectStop && <div style={{ fontSize: 11, color: 'var(--danger)', marginTop: 2 }}>{e.meta.rejectReason}</div>}
              {st === 'review' && e.state === 'review' && <div style={{ fontSize: 11, color: 'var(--warning)', marginTop: 2 }}>{e.meta.reviewReason}</div>}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ─── Add-extension drawer ─────────────────────────────────────────
function ExtAddDrawer({ onClose }) {
  const [surface, setSurface] = React.useState('http');
  return (
    <DrawerShell onClose={onClose}>
      <div style={{ padding: '20px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ width: 38, height: 38, borderRadius: 10, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><Icon name="plus" size={18}/></div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>Add extension</div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>Pick a surface — it saves as a draft you submit for review.</div>
        </div>
        <span className="hover-halo" onClick={onClose} style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6, cursor: 'pointer' }}><Icon name="close" size={14}/></span>
      </div>

      <div style={{ flex: 1, overflow: 'auto', padding: '18px 22px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Surface chooser */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {['skill', 'mcp', 'http'].map(s => {
            const t = EXT_TYPE[s];
            const on = surface === s;
            const blurb = { skill: 'A SKILL.md package — procedural knowledge for the agent.', mcp: 'A remote MCP server whose tools become callable.', http: 'One declared REST call exposed as a single tool.' }[s];
            return (
              <div key={s} onClick={() => setSurface(s)} style={{
                display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px', borderRadius: 10, cursor: 'pointer',
                background: on ? t.color + '0c' : 'var(--surface)',
                border: '1px solid ' + (on ? t.color : 'var(--border-light)'),
                boxShadow: on ? `0 0 0 1px ${t.color}` : 'none',
              }}>
                <ExtTile type={s} size={38}/>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{t.label}</div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{blurb}</div>
                </div>
                <div style={{ width: 18, height: 18, borderRadius: 999, border: '2px solid ' + (on ? t.color : 'var(--border-medium)'), display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                  {on && <span style={{ width: 8, height: 8, borderRadius: 999, background: t.color }}/>}
                </div>
              </div>
            );
          })}
        </div>

        {/* Surface-specific form */}
        <div style={{ height: 1, background: 'var(--border-light)' }}/>
        {surface === 'skill' && <AddSkillForm/>}
        {surface === 'mcp' && <AddMcpForm/>}
        {surface === 'http' && <AddHttpForm/>}
      </div>

      <div style={{ padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ fontSize: 11, color: 'var(--text-tertiary)', flex: 1 }}>Mutating tools default to High — a platform admin reviews before it goes live.</span>
        <button className="btn btn-ghost btn-sm" onClick={onClose}>Cancel</button>
        <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save draft</button>
      </div>
    </DrawerShell>
  );
}

function FormField({ label, hint, children }) {
  return (
    <div>
      <div style={{ fontSize: 11, letterSpacing: '0.04em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 6 }}>{label}</div>
      {children}
      {hint && <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 5 }}>{hint}</div>}
    </div>
  );
}
const fieldStyle = { width: '100%', padding: '9px 11px', border: '1px solid var(--border-light)', borderRadius: 8, fontSize: 13, background: 'var(--surface)', color: 'var(--text-primary)', outline: 'none', fontFamily: 'var(--font-sans)' };

function AddSkillForm() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ border: '1.5px dashed var(--border-medium)', borderRadius: 10, padding: '28px 16px', textAlign: 'center', color: 'var(--text-tertiary)' }}>
        <Icon name="upload" size={22}/>
        <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-secondary)', marginTop: 8 }}>Drop a SKILL.md package</div>
        <div style={{ fontSize: 11, marginTop: 2 }}>Folder with SKILL.md + optional scripts / references / assets</div>
      </div>
      <div style={{ padding: '10px 12px', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, fontSize: 11.5, color: 'var(--text-secondary)', display: 'flex', gap: 8 }}>
        <Icon name="info" size={13} color="var(--brand-primary)"/>
        <span>Frontmatter is validated and <b>allowed-tools</b> intersected with the agent's tools on upload. Scripts stay off until a platform admin enables them.</span>
      </div>
    </div>
  );
}
function AddMcpForm() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <FormField label="Name"><input style={fieldStyle} defaultValue="" placeholder="e.g. Companies House"/></FormField>
      <FormField label="Endpoint" hint="Remote HTTP/SSE only — no local processes. Host must be on the platform allow-list.">
        <input style={{ ...fieldStyle, fontFamily: 'var(--font-mono)', fontSize: 12 }} placeholder="https://mcp.example.com/sse"/>
      </FormField>
      <FormField label="Auth" hint="Stored encrypted — write-only, never shown again.">
        <div style={{ display: 'flex', gap: 8 }}>
          <select style={{ ...fieldStyle, width: 130 }}><option>OAuth2</option><option>API key</option><option>mTLS</option></select>
          <input style={{ ...fieldStyle, flex: 1 }} type="password" placeholder="••••••••"/>
        </div>
      </FormField>
    </div>
  );
}
function AddHttpForm() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <FormField label="Name"><input style={fieldStyle} placeholder="e.g. CRM — create contact"/></FormField>
      <FormField label="Request">
        <div style={{ display: 'flex', gap: 8 }}>
          <select style={{ ...fieldStyle, width: 100, fontFamily: 'var(--font-mono)', fontSize: 12 }}><option>GET</option><option>POST</option><option>PUT</option><option>PATCH</option><option>DELETE</option></select>
          <input style={{ ...fieldStyle, flex: 1, fontFamily: 'var(--font-mono)', fontSize: 12 }} placeholder="https://api.example.com/v2/{id}"/>
        </div>
      </FormField>
      <FormField label="Parameter schema" hint="The fixed surface the model sees — it can't add fields.">
        <textarea rows={4} style={{ ...fieldStyle, fontFamily: 'var(--font-mono)', fontSize: 12, resize: 'vertical' }} defaultValue={'{\n  "id": "string",\n  "name": "string"\n}'}/>
      </FormField>
      <FormField label="Auth" hint="Stored encrypted — write-only.">
        <div style={{ display: 'flex', gap: 8 }}>
          <select style={{ ...fieldStyle, width: 130 }}><option>API key</option><option>OAuth2</option><option>Bearer</option></select>
          <input style={{ ...fieldStyle, flex: 1 }} type="password" placeholder="••••••••"/>
        </div>
      </FormField>
      <div style={{ padding: '10px 12px', background: 'rgba(196,69,54,0.05)', border: '1px solid rgba(196,69,54,0.16)', borderRadius: 8, fontSize: 11.5, color: 'var(--text-secondary)', display: 'flex', gap: 8 }}>
        <Icon name="alertc" size={13} color="var(--danger)"/>
        <span>A non-GET call writes to an external system, so it defaults to <b style={{ color: '#b3261e' }}>HIGH</b> — a durable proposal that never runs in-band.</span>
      </div>
    </div>
  );
}

// ─── Test-harness drawer ──────────────────────────────────────────
function ExtHarnessDrawer({ initial = 'ext-10', onClose }) {
  const [sel, setSel] = React.useState(initial);
  const e = extById(sel);
  const steps = [
    { key: 'validate', icon: 'check', label: 'Validate', desc: e.type === 'skill' ? 'Frontmatter + allowed-tools intersection' : 'Schema + auth reference', state: 'pass' },
    { key: 'dryrun', icon: 'plug', label: e.type === 'mcp' ? 'Dry-run connect & list' : 'Dry-run call', desc: e.type === 'mcp' ? 'Connect, list tools, classify each' : 'One sandbox request, no side effects', state: 'pass' },
    ...(e.type === 'skill' ? [{ key: 'preview', icon: 'eye', label: 'Preview injected text', desc: 'Exactly what the model will see', state: 'pass' }] : []),
    { key: 'sandbox', icon: 'beaker', label: 'Sandbox invoke', desc: 'Run in a throwaway thread — observe the gate', state: 'run' },
  ];
  const tier = e.tier === 'na' ? 'readonly' : e.tier;

  return (
    <DrawerShell width={560} onClose={onClose}>
      <div style={{ padding: '20px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ width: 38, height: 38, borderRadius: 10, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><Icon name="beaker" size={18}/></div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>Test harness</div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>Server-truthful — the same code paths production runs.</div>
        </div>
        <span className="hover-halo" onClick={onClose} style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6, cursor: 'pointer' }}><Icon name="close" size={14}/></span>
      </div>

      <div style={{ flex: 1, overflow: 'auto', padding: '18px 22px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Picker */}
        <div>
          <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Testing</div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {EXTENSIONS.filter(x => x.state !== 'active').slice(0, 5).map(x => {
              const on = x.id === sel;
              return (
                <button key={x.id} onClick={() => setSel(x.id)} style={{
                  display: 'inline-flex', alignItems: 'center', gap: 6, padding: '5px 10px', borderRadius: 999, cursor: 'pointer',
                  border: '1px solid ' + (on ? EXT_TYPE[x.type].color : 'var(--border-light)'),
                  background: on ? EXT_TYPE[x.type].color + '12' : 'var(--surface)',
                  color: on ? EXT_TYPE[x.type].color : 'var(--text-secondary)', fontSize: 11.5, fontWeight: on ? 600 : 500,
                }}><Icon name={EXT_TYPE[x.type].icon} size={11}/>{x.name}</button>
              );
            })}
          </div>
        </div>

        {/* Steps */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {steps.map(s => (
            <div key={s.key} style={{ display: 'grid', gridTemplateColumns: '34px 1fr auto', gap: 12, alignItems: 'center', padding: '12px 14px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10 }}>
              <div style={{ width: 30, height: 30, borderRadius: 8, background: s.state === 'pass' ? 'rgba(31,122,94,0.12)' : 'var(--brand-primary-10)', color: s.state === 'pass' ? '#1f7a5e' : 'var(--brand-primary)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                <Icon name={s.state === 'pass' ? 'check' : s.icon} size={14}/>
              </div>
              <div>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{s.label}</div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 1 }}>{s.desc}</div>
              </div>
              {s.state === 'pass' ? <Pill tone="success" dot size="sm">Passed</Pill> : <button className="btn btn-outline btn-sm"><Icon name="play" size={11}/> Run</button>}
            </div>
          ))}
        </div>

        {/* Gate verdict */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
            <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Gate verdict</span>
            <ExtTierPill tier={tier}/>
          </div>
          <div style={{ padding: '10px 12px', borderRadius: 8, fontSize: 11.5, lineHeight: 1.5,
            background: e.tier === 'high' ? 'rgba(196,69,54,0.06)' : 'var(--surface-inset)',
            border: '1px solid ' + (e.tier === 'high' ? 'rgba(196,69,54,0.18)' : 'var(--border-light)'),
            color: 'var(--text-secondary)' }}>
            {e.tier === 'high'
              ? 'A High tool is marshalled into a durable proposal — the sandbox returns “queued, requires approval” rather than mutating. Proves the gate is in force before anything goes live.'
              : e.type === 'skill'
                ? 'A skill adds no new tool — it can only reference tools the agent already has. Nothing mutates in the sandbox.'
                : 'A read-only tool passes through and executes directly in the sandbox thread.'}
          </div>
        </div>
      </div>
    </DrawerShell>
  );
}

// ─── Screen exports (entry states of one hub) ─────────────────────
function ScreenExtHub()       { return <ExtHub initialLens="tenant"   initialDrawer="detail"  initialSel="ext-01"/>; }
function ScreenExtAdd()       { return <ExtHub initialLens="tenant"   initialDrawer="add"     initialSel={null}/>; }
function ScreenExtHarness()   { return <ExtHub initialLens="tenant"   initialDrawer="harness" initialSel="ext-10"/>; }
function ScreenExtApprovals() { return <ExtHub initialLens="platform" initialDrawer="review"  initialSel="ext-07"/>; }

Object.assign(window, { ScreenExtHub, ScreenExtAdd, ScreenExtHarness, ScreenExtApprovals });
