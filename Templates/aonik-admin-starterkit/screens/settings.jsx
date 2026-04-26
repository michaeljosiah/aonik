// Settings — landing + multi-tab platform settings + TTS preview

// ─── Landing ─────────────────────────────────────────────────────────
function ScreenSettingsLanding() {
  const platformTiles = [
    { title: 'Platform Settings',  desc: 'Workspace profile, AI provider, storage, communication, feature flags.', icon: 'settings', badge: 'Configuration', href: 'platform' },
    { title: 'Authentication',    desc: 'Identity providers, SSO, OAuth callbacks, key rotation.',                   icon: 'shield',   badge: 'Security',      href: 'auth' },
    { title: 'Audit Logs',         desc: 'Operator actions, authentication events, security decisions.',             icon: 'invoice',  badge: 'Observability', href: 'audit' },
    { title: 'System Tools',       desc: 'Maintenance utilities, cache invalidation, demo seed datasets.',           icon: 'wrench',   badge: 'Ops',           href: 'tools' },
  ];
  const financeTiles = [
    { title: 'Payment Gateways',  desc: 'Configure Stripe, Paystack, Wise, Flutterwave routing & credentials.', icon: 'bank',     badge: 'Integrations', href: 'gateways' },
    { title: 'FX Rates',           desc: 'Manage FX quote sources and exchange rate governance.',                icon: 'arrows',   badge: 'Pricing',      href: 'fx' },
    { title: 'Autonumbering',      desc: 'Reference generation strategy and sequence profiles.',                  icon: 'invoice',  badge: 'References',    href: 'numbering' },
  ];
  const aiTiles = [
    { title: 'Text-to-Speech',     desc: 'ElevenLabs / Mistral provider credentials, voices, output format.',     icon: 'globe',    badge: 'AI',           href: 'tts' },
    { title: 'AI Policies',        desc: 'Approval thresholds, kill switch, rate limits, blocked tools.',          icon: 'shield',   badge: 'Governance',   href: 'policies' },
    { title: 'Tool Catalog',       desc: 'Browse, enable, and version the tools available to agents.',             icon: 'layers',   badge: 'AI',           href: 'tools-catalog' },
  ];

  const Tile = ({ t }) => (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 20, cursor: 'pointer',
      display: 'flex', flexDirection: 'column', gap: 12,
      transition: 'all 150ms ease',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{
          width: 38, height: 38, borderRadius: 8,
          background: 'var(--brand-primary-10)',
          display: 'grid', placeItems: 'center',
        }}>
          <Icon name={t.icon} size={18} color="var(--brand-primary)"/>
        </div>
        <Icon name="arrowright" size={14} color="var(--text-tertiary)"/>
      </div>
      <div>
        <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>{t.title}</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{t.desc}</div>
      </div>
      <div><Pill tone="default">{t.badge}</Pill></div>
    </div>
  );

  const Group = ({ title, tiles }) => (
    <div style={{ marginBottom: 32 }}>
      <div style={{ fontSize: 11, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 12 }}>{title}</div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
        {tiles.map(t => <Tile key={t.title} t={t}/>)}
      </div>
    </div>
  );

  return (
    <div style={{ padding: '24px 32px', overflow: 'auto', height: '100%' }}>
      <PageHeader
        eyebrow="Admin"
        title="Settings"
        subtitle="Centralized controls for workspace behavior, integration security, and operational governance."
        actions={<>
          <button className="btn btn-ghost btn-sm"><Icon name="search" size={12}/> Search settings</button>
          <button className="btn btn-ghost btn-sm"><Icon name="invoice" size={12}/> Settings docs</button>
        </>}
      />
      <div style={{ marginTop: 28 }}>
        <Group title="Platform"  tiles={platformTiles}/>
        <Group title="Finance"   tiles={financeTiles}/>
        <Group title="AI & Agents" tiles={aiTiles}/>
      </div>
    </div>
  );
}

// ─── Platform settings (multi-tab, mirrors GlobalSettingsPage) ─────────
function ScreenSettingsPlatform() {
  const [tab, setTab] = React.useState('ai');
  const tabs = [
    { id: 'ai',           label: 'AI',                badge: null },
    { id: 'storage',      label: 'Storage',            badge: null },
    { id: 'comms',        label: 'Communication',      badge: null },
    { id: 'features',     label: 'Feature Flags',      badge: '24' },
    { id: 'workspace',    label: 'Workspace',          badge: null },
    { id: 'platform-ops', label: 'Platform Ops',       badge: 'host' },
  ];

  return (
    <div style={{ display: 'flex', height: '100%', minHeight: 0 }}>
      {/* Inner left rail (sub-navigation) */}
      <div style={{
        width: 260, flex: 'none',
        borderRight: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', flexDirection: 'column',
        padding: 20,
      }}>
        <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Platform</div>
        <div style={{ fontSize: 17, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Global settings</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.45, marginBottom: 18 }}>
          Workspace identity, AI provider, storage, communication, and feature configuration.
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          {tabs.map(t => {
            const active = tab === t.id;
            return (
              <div key={t.id} onClick={() => setTab(t.id)} style={{
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                padding: '8px 12px', borderRadius: 6, cursor: 'pointer',
                background: active ? 'var(--brand-primary-10)' : 'transparent',
                color: active ? 'var(--brand-primary)' : 'var(--text-primary)',
                fontWeight: active ? 600 : 500, fontSize: 13,
              }}>
                <span>{t.label}</span>
                {t.badge && <span style={{
                  fontSize: 10, padding: '1px 6px', borderRadius: 999,
                  background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
                  border: '1px solid var(--border-light)',
                  color: 'var(--text-secondary)',
                }}>{t.badge}</span>}
              </div>
            );
          })}
        </div>

        <div style={{ marginTop: 'auto', padding: '12px 14px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
            <Icon name="warn" size={13} color="var(--warning, #d97706)"/>
            <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>Unsaved changes</div>
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: 8 }}>
            3 fields have pending edits across AI and Communication tabs.
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            <button className="btn btn-primary btn-sm" style={{ flex: 1 }}>Save</button>
            <button className="btn btn-ghost btn-sm">Discard</button>
          </div>
        </div>
      </div>

      {/* Right column */}
      <div style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: '24px 32px' }}>
        {tab === 'ai'       && <SettingsAiTab/>}
        {tab === 'storage'  && <SettingsStorageTab/>}
        {tab === 'comms'    && <SettingsCommsTab/>}
        {tab === 'features' && <SettingsFeaturesTab/>}
        {tab === 'workspace' && <SettingsWorkspaceTab/>}
        {tab === 'platform-ops' && <SettingsPlatformOpsTab/>}
      </div>
    </div>
  );
}

// ── Settings primitives ──────────────────────────────────────────────
function SettingsSection({ title, description, children, action }) {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, marginBottom: 16 }}>
      <div style={{ padding: '18px 20px 14px', borderBottom: '1px solid var(--border-light)', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 16 }}>
        <div>
          <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</div>
          {description && <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5, maxWidth: 600 }}>{description}</div>}
        </div>
        {action}
      </div>
      <div style={{ padding: 20, display: 'flex', flexDirection: 'column', gap: 16 }}>{children}</div>
    </div>
  );
}

function Field({ label, help, code, children, status }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '260px 1fr', gap: 24, alignItems: 'flex-start' }}>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <label style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{label}</label>
          {status}
        </div>
        {code && <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 4 }}>{code}</div>}
        {help && <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 6, lineHeight: 1.5 }}>{help}</div>}
      </div>
      <div>{children}</div>
    </div>
  );
}

function MaskedInput({ value = '••••••••••••••••••••', placeholder = 'sk-...', actions }) {
  return (
    <div style={{ display: 'flex', gap: 6 }}>
      <input className="input" defaultValue={value} placeholder={placeholder} style={{ flex: 1, fontFamily: 'var(--font-mono)', fontSize: 12 }}/>
      {actions || (
        <>
          <button className="btn btn-ghost btn-sm" title="Show"><Icon name="globe" size={12}/></button>
          <button className="btn btn-ghost btn-sm" title="Rotate"><Icon name="refresh" size={12}/></button>
        </>
      )}
    </div>
  );
}

function Toggle({ on = true, onChange }) {
  const [v, setV] = React.useState(on);
  return (
    <span onClick={() => { setV(!v); onChange?.(!v); }} style={{
      width: 36, height: 20, borderRadius: 999,
      background: v ? 'var(--brand-primary)' : 'var(--gray-300, #cbd5e1)',
      position: 'relative', cursor: 'pointer', display: 'inline-block', flex: 'none',
      transition: 'background 150ms ease',
    }}>
      <span style={{
        position: 'absolute', top: 2, left: v ? 18 : 2,
        width: 16, height: 16, borderRadius: '50%', background: '#fff',
        transition: 'left 150ms ease', boxShadow: '0 1px 2px rgba(0,0,0,0.1)',
      }}/>
    </span>
  );
}

// ── AI tab ───────────────────────────────────────────────────────────
function SettingsAiTab() {
  return (
    <>
      <PageHeader
        eyebrow="Settings · Platform"
        title="AI"
        subtitle="LLM provider, model selection, and memory backend for agent runs."
        actions={<>
          <button className="btn btn-ghost btn-sm">Reset to defaults</button>
          <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save changes</button>
        </>}
      />
      <div style={{ marginTop: 24 }}>
        <SettingsSection
          title="Provider"
          description="Select the AI provider powering LLM features. Set to Stub for development without API keys.">
          <Field label="AI Provider" code="Ai.Provider"
            help="Determines which credentials and endpoints are used for all LLM, embedding, and image-generation calls.">
            <div style={{ display: 'flex', gap: 10 }}>
              {['Stub', 'OpenAI', 'Anthropic', 'Mistral'].map((opt, i) => {
                const active = opt === 'OpenAI';
                return (
                  <div key={opt} style={{
                    flex: 1, padding: '12px 14px', borderRadius: 8,
                    border: active ? '2px solid var(--brand-primary)' : '1px solid var(--border-light)',
                    background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
                    cursor: 'pointer', position: 'relative',
                  }}>
                    {active && (
                      <span style={{ position: 'absolute', top: 8, right: 8, color: 'var(--brand-primary)' }}>
                        <Icon name="check" size={13}/>
                      </span>
                    )}
                    <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{opt}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>
                      {opt === 'Stub' ? 'No API · echo' : opt === 'OpenAI' ? 'GPT-5 family' : opt === 'Anthropic' ? 'Claude family' : 'Voxtral · Mistral'}
                    </div>
                  </div>
                );
              })}
            </div>
          </Field>
        </SettingsSection>

        <SettingsSection
          title="OpenAI Configuration"
          description="API key and model settings for OpenAI. Only used when AI Provider is set to OpenAI."
          action={<Pill tone="success" dot>Active</Pill>}>
          <Field label="API Key" code="Ai.OpenAI.ApiKey"
            help="Encrypted at rest. Leave blank to keep current value."
            status={<Pill tone="success" size="sm">Encrypted</Pill>}>
            <MaskedInput placeholder="sk-..."/>
          </Field>
          <Field label="Chat Model" code="Ai.OpenAI.Model"
            help="Primary model used for agent conversations, tool calls, and content generation.">
            <select className="select" defaultValue="gpt-5-mini">
              <option>gpt-5-mini</option>
              <option>gpt-4.1-mini</option>
              <option>gpt-4.1-nano</option>
              <option>gpt-4o</option>
              <option>gpt-4o-mini</option>
            </select>
          </Field>
          <Field label="Image Model" code="Ai.OpenAI.ImageModel"
            help="Used for AI-generated images such as content block illustrations.">
            <select className="select" defaultValue="dall-e-3">
              <option>dall-e-3</option>
              <option>dall-e-2</option>
              <option>gpt-image-1</option>
            </select>
          </Field>
        </SettingsSection>

        <SettingsSection
          title="User Memory"
          description="Controls how agent user memory is stored and retrieved.">
          <Field label="Memory Backend" code="Ai.UserMemory.Backend"
            help="Qdrant enables semantic vector search over memories, allowing agents to recall relevant context by meaning rather than exact key match.">
            <select className="select" defaultValue="Qdrant (vector search)">
              <option>SQL Server</option>
              <option>Qdrant (vector search)</option>
            </select>
          </Field>
          <Field label="Memory TTL" code="Ai.UserMemory.TtlDays"
            help="Days a memory is retained before automatic purge. 0 = never expire.">
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input className="input" defaultValue="180" style={{ width: 120, fontFamily: 'var(--font-mono)' }}/>
              <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>days</span>
            </div>
          </Field>
        </SettingsSection>
      </div>
    </>
  );
}

// ── Storage tab ──────────────────────────────────────────────────────
function SettingsStorageTab() {
  return (
    <>
      <PageHeader eyebrow="Settings · Platform" title="Storage" subtitle="Blob storage backend for file uploads and generated artifacts."
        actions={<button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save</button>}/>
      <div style={{ marginTop: 24 }}>
        <SettingsSection title="Provider" description="Select the blob storage backend for file uploads.">
          <Field label="Storage Provider" code="BlobStorage.Provider"
            help="LocalDisk is for development only. Use Azure or S3 in production.">
            <select className="select" defaultValue="Azure">
              <option>LocalDisk</option>
              <option>Azure</option>
              <option>AWS S3</option>
              <option>GCS</option>
            </select>
          </Field>
        </SettingsSection>
        <SettingsSection title="Azure Storage" action={<Pill tone="success" dot>Connected</Pill>}>
          <Field label="Connection String" code="BlobStorage.Azure.ConnectionString" status={<Pill tone="success" size="sm">Encrypted</Pill>}>
            <MaskedInput placeholder="DefaultEndpointsProtocol=https;..."/>
          </Field>
          <Field label="Container Name" code="BlobStorage.Azure.Container">
            <input className="input" defaultValue="aonik-prod-uploads" style={{ fontFamily: 'var(--font-mono)' }}/>
          </Field>
          <Field label="CDN Endpoint" code="BlobStorage.Azure.CdnUrl" help="Optional. Public CDN for serving signed URLs.">
            <input className="input" defaultValue="https://cdn.aonik.com"/>
          </Field>
        </SettingsSection>
      </div>
    </>
  );
}

// ── Comms ────────────────────────────────────────────────────────────
function SettingsCommsTab() {
  return (
    <>
      <PageHeader eyebrow="Settings · Platform" title="Communication" subtitle="Email, SMS, and webhook delivery."
        actions={<button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save</button>}/>
      <div style={{ marginTop: 24 }}>
        <SettingsSection title="Email" description="Outbound transactional email." action={<Pill tone="success" dot>Healthy</Pill>}>
          <Field label="SMTP Provider" code="Email.Provider">
            <select className="select" defaultValue="SendGrid">
              <option>SMTP</option><option>SendGrid</option><option>SES</option><option>Resend</option>
            </select>
          </Field>
          <Field label="API Key" code="Email.SendGrid.ApiKey">
            <MaskedInput placeholder="SG..."/>
          </Field>
          <Field label="From Address" code="Email.From">
            <input className="input" defaultValue="ops@primrose.aonik.com"/>
          </Field>
        </SettingsSection>
        <SettingsSection title="SMS" action={<Pill tone="warning" dot>Sandbox</Pill>}>
          <Field label="Twilio Account SID" code="Sms.Twilio.AccountSid"><MaskedInput placeholder="AC..."/></Field>
          <Field label="Twilio Auth Token" code="Sms.Twilio.AuthToken"><MaskedInput/></Field>
        </SettingsSection>
      </div>
    </>
  );
}

// ── Feature flags ────────────────────────────────────────────────────
function SettingsFeaturesTab() {
  const flags = [
    { name: 'Invoice Creation',           on: true,  group: 'Bill Payments', key: 'FeatureManagement.BillPayments.Invoicing.Create' },
    { name: 'Invoice Issuing',            on: true,  group: 'Bill Payments', key: 'FeatureManagement.BillPayments.Invoicing.Issue' },
    { name: 'Invoice Payment',            on: true,  group: 'Bill Payments', key: 'FeatureManagement.BillPayments.Invoicing.Payment' },
    { name: 'Discounts',                  on: false, group: 'Bill Payments', key: 'FeatureManagement.BillPayments.Invoicing.Discounts' },
    { name: 'Allocations',                on: true,  group: 'Bill Payments', key: 'FeatureManagement.BillPayments.Invoicing.Allocations' },
    { name: 'Customer Account Management', on: true, group: 'Bill Payments', key: 'FeatureManagement.BillPayments.CustomerAccounts.Management' },
    { name: 'Cross-border Payments',      on: true,  group: 'FX',            key: 'FeatureManagement.FX.CrossBorder' },
    { name: 'Hedging',                    on: false, group: 'FX',            key: 'FeatureManagement.FX.Hedging', rolloutPct: 18 },
    { name: 'Auto-apply (≤ £50K)',        on: true,  group: 'Agents',        key: 'FeatureManagement.Agents.AutoApply' },
    { name: 'Agent Sandbox Mode',         on: false, group: 'Agents',        key: 'FeatureManagement.Agents.Sandbox' },
    { name: 'Setup Wizard',               on: false, group: 'Platform',      key: 'FeatureManagement.Platform.SetupWizard', help: 'Only enable during initial deployment.' },
  ];
  const groups = ['Bill Payments', 'FX', 'Agents', 'Platform'];

  return (
    <>
      <PageHeader eyebrow="Settings · Platform" title="Feature flags" subtitle="Toggle product surfaces. Changes apply to the active tenant only."
        actions={<>
          <input className="input" placeholder="Search flags…" style={{ width: 220, height: 32 }}/>
          <button className="btn btn-ghost btn-sm">Export</button>
        </>}/>
      <div style={{ marginTop: 24 }}>
        {groups.map(g => (
          <SettingsSection key={g} title={g}>
            {flags.filter(f => f.group === g).map(f => (
              <div key={f.name} style={{
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                padding: '14px 0', borderBottom: '1px solid var(--border-light)',
              }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)', display: 'flex', gap: 8, alignItems: 'center' }}>
                    {f.name}
                    {f.rolloutPct != null && <Pill tone="warning" size="sm">Rollout {f.rolloutPct}%</Pill>}
                  </div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{f.key}</div>
                  {f.help && <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 4 }}>{f.help}</div>}
                </div>
                <Toggle on={f.on}/>
              </div>
            ))}
          </SettingsSection>
        ))}
      </div>
    </>
  );
}

// ── Workspace ────────────────────────────────────────────────────────
function SettingsWorkspaceTab() {
  return (
    <>
      <PageHeader eyebrow="Settings · Platform" title="Workspace" subtitle="Identity, locale, and presentation."
        actions={<button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save</button>}/>
      <div style={{ marginTop: 24 }}>
        <SettingsSection title="Identity">
          <Field label="Workspace name" code="Workspace.Name"><input className="input" defaultValue="Primrose Logistics"/></Field>
          <Field label="Subdomain" code="Workspace.Subdomain">
            <div style={{ display: 'flex', alignItems: 'stretch' }}>
              <input className="input" defaultValue="primrose" style={{ borderTopRightRadius: 0, borderBottomRightRadius: 0, fontFamily: 'var(--font-mono)' }}/>
              <span style={{ padding: '0 12px', display: 'flex', alignItems: 'center', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderLeft: 'none', borderTopRightRadius: 6, borderBottomRightRadius: 6, fontSize: 12, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>.aonik.com</span>
            </div>
          </Field>
          <Field label="Logo" code="Workspace.LogoUrl">
            <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
              <Avatar name="Primrose Logistics" size={42} color="#055a60" textColor="#fff"/>
              <button className="btn btn-ghost btn-sm"><Icon name="upload" size={12}/> Upload</button>
              <button className="btn btn-ghost btn-sm">Remove</button>
            </div>
          </Field>
        </SettingsSection>
        <SettingsSection title="Locale & finance">
          <Field label="Default currency" code="Workspace.DefaultCurrency">
            <select className="select" defaultValue="GBP — British Pound"><option>GBP — British Pound</option><option>USD — US Dollar</option><option>NGN — Naira</option><option>EUR — Euro</option></select>
          </Field>
          <Field label="Reporting currencies" code="Workspace.ReportingCurrencies"
            help="Currencies surfaced in dashboards alongside the default.">
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {['USD', 'NGN', 'EUR', 'KES'].map(c => (
                <span key={c} style={{ padding: '4px 8px 4px 10px', borderRadius: 6, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', fontSize: 12, fontWeight: 500, display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                  {c} <Icon name="x" size={11}/>
                </span>
              ))}
              <button className="btn btn-ghost btn-sm" style={{ padding: '4px 8px' }}><Icon name="plus" size={11}/></button>
            </div>
          </Field>
          <Field label="Time zone" code="Workspace.Timezone">
            <select className="select" defaultValue="Europe/London — UTC+1"><option>Europe/London — UTC+1</option><option>Africa/Lagos — UTC+1</option><option>America/New_York — UTC-4</option></select>
          </Field>
          <Field label="Fiscal year start" code="Workspace.FiscalYearStart">
            <select className="select" defaultValue="January"><option>January</option><option>April</option><option>July</option><option>October</option></select>
          </Field>
        </SettingsSection>
      </div>
    </>
  );
}

// ── Platform Ops ─────────────────────────────────────────────────────
function SettingsPlatformOpsTab() {
  return (
    <>
      <PageHeader eyebrow="Settings · Platform · Host admin" title="Platform Ops" subtitle="Host-level controls. Available to platform admins only."
        actions={<Pill tone="warning" dot>Host scope</Pill>}/>
      <div style={{ marginTop: 24 }}>
        <SettingsSection title="Setup Wizard" description="Only enable this during initial deployment or when re-configuring the platform from scratch. Disable after setup is complete." action={<Pill tone="default">disabled</Pill>}>
          <Field label="Wizard enabled" code="Platform.SetupWizard.Enabled" help="Allows creating tenants, seeding data, and setting up identity providers.">
            <Toggle on={false}/>
          </Field>
        </SettingsSection>
        <SettingsSection title="Maintenance" description="Run host-wide cache and index operations.">
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12 }}>
            {[
              { name: 'Invalidate config cache', desc: 'Force all running services to re-read settings.', icon: 'refresh' },
              { name: 'Rebuild search index',    desc: 'Re-index ledger, customers, and tools.',          icon: 'search' },
              { name: 'Purge expired memory',    desc: 'Remove agent memory beyond TTL.',                 icon: 'trash' },
              { name: 'Snapshot tenant',          desc: 'Export full tenant state for backup.',           icon: 'download' },
            ].map(a => (
              <div key={a.name} style={{ background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 10, padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <Icon name={a.icon} size={14} color="var(--text-secondary)"/>
                  <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{a.name}</div>
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{a.desc}</div>
                <button className="btn btn-ghost btn-sm" style={{ alignSelf: 'flex-start' }}>Run</button>
              </div>
            ))}
          </div>
        </SettingsSection>
      </div>
    </>
  );
}

// ─── Payment Gateways ────────────────────────────────────────────────
function ScreenSettingsGateways() {
  const providers = [
    { id: 'stripe',   name: 'Stripe',       desc: 'Cards · ACH · SEPA in 40 countries',         status: 'Active',     tone: 'success', logo: 'S', color: '#635bff', volume: '£312K',  fee: '1.4% + 20p', region: 'Global' },
    { id: 'paystack', name: 'Paystack',     desc: 'Cards · bank · USSD · QR — Nigeria',          status: 'Active',     tone: 'success', logo: 'P', color: '#00c3f7', volume: '₦142M',  fee: '1.5%',        region: 'NGN' },
    { id: 'wise',     name: 'Wise Business', desc: 'Multi-currency payouts in 50+ currencies',   status: 'Active',     tone: 'success', logo: 'W', color: '#9fe870', volume: '£204K',  fee: '0.43%',       region: 'Global' },
    { id: 'flw',      name: 'Flutterwave',   desc: 'Cards · bank · mobile money — Africa',       status: 'Sandbox',    tone: 'warning', logo: 'F', color: '#f5a623', volume: '—',       fee: '1.4%',        region: 'NGN · KES' },
    { id: 'ach',      name: 'Modern Treasury', desc: 'ACH origination, RTP — US',                status: 'Disabled',   tone: 'default', logo: 'M', color: '#1e2228', volume: '—',       fee: 'flat $0.50',  region: 'USD' },
  ];
  const [sel, setSel] = React.useState('stripe');
  const provider = providers.find(p => p.id === sel);

  return (
    <div style={{ display: 'flex', height: '100%', minHeight: 0 }}>
      {/* Provider list */}
      <div style={{ width: 320, flex: 'none', borderRight: '1px solid var(--border-light)', background: 'var(--surface-inset)', overflow: 'auto', padding: 18 }}>
        <div style={{ fontSize: 17, fontWeight: 600, color: 'var(--text-primary)' }}>Payment gateways</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5, marginBottom: 14 }}>
          Configure providers, routing, and credentials per region.
        </div>
        <button className="btn btn-primary btn-sm" style={{ width: '100%', justifyContent: 'center', marginBottom: 14 }}>
          <Icon name="plus" size={12}/> Add provider
        </button>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {providers.map(p => {
            const active = p.id === sel;
            return (
              <div key={p.id} onClick={() => setSel(p.id)} style={{
                padding: 12, borderRadius: 10, cursor: 'pointer',
                background: active ? 'var(--surface)' : 'transparent',
                border: active ? '1px solid var(--brand-primary)' : '1px solid transparent',
                display: 'flex', gap: 10, alignItems: 'center',
              }}>
                <div style={{ width: 32, height: 32, borderRadius: 6, background: p.color, color: '#fff', display: 'grid', placeItems: 'center', fontWeight: 700, fontSize: 13, flex: 'none' }}>{p.logo}</div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 6 }}>
                    <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{p.name}</span>
                    <Pill tone={p.tone} size="sm" dot>{p.status}</Pill>
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{p.desc}</div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Detail */}
      <div style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: '24px 32px' }}>
        <PageHeader
          eyebrow={`Settings · Gateways · ${provider.region}`}
          title={provider.name}
          subtitle={provider.desc}
          actions={<>
            <button className="btn btn-ghost btn-sm"><Icon name="globe" size={12}/> Test connection</button>
            <button className="btn btn-ghost btn-sm">Disable</button>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save</button>
          </>}
        />
        <div style={{ marginTop: 18, display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          <KPI label="Volume · 30d" value={provider.volume} delta="+18%"/>
          <KPI label="Avg fee" value={provider.fee}/>
          <KPI label="Success rate" value="99.4%" delta="+0.2pp"/>
          <KPI label="Last failure" value="2h ago"/>
        </div>

        <div style={{ marginTop: 16 }}>
          <SettingsSection
            title="Credentials"
            description={`API credentials for ${provider.name}. Keys are encrypted at rest and never exposed to the browser.`}
            action={<Pill tone="success" size="sm" dot>Encrypted at rest</Pill>}>
            <Field label="Mode" code="Gateway.Mode" help="Live mode routes real money. Test mode uses sandbox endpoints.">
              <div style={{ display: 'flex', gap: 0, background: 'var(--surface-inset)', padding: 3, borderRadius: 8, width: 'fit-content' }}>
                {['Live', 'Test'].map(m => {
                  const active = m === 'Live';
                  return (
                    <span key={m} style={{
                      padding: '5px 14px', borderRadius: 6, fontSize: 12.5, fontWeight: 500, cursor: 'pointer',
                      background: active ? 'var(--surface)' : 'transparent',
                      color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                      boxShadow: active ? '0 1px 2px rgba(0,0,0,0.06)' : 'none',
                    }}>{m}</span>
                  );
                })}
              </div>
            </Field>
            <Field label="Publishable key" code={`${provider.id}.publishable_key`}>
              <div style={{ display: 'flex', gap: 6 }}>
                <input className="input" defaultValue={`pk_live_51M${provider.id}9aBcD3eFgHiJkL...`} style={{ flex: 1, fontFamily: 'var(--font-mono)', fontSize: 12 }}/>
                <button className="btn btn-ghost btn-sm"><Icon name="link" size={12}/></button>
              </div>
            </Field>
            <Field label="Secret key" code={`${provider.id}.secret_key`} status={<Pill tone="success" size="sm">Encrypted</Pill>}>
              <MaskedInput placeholder={`sk_live_${provider.id}...`}/>
            </Field>
            <Field label="Webhook signing secret" code={`${provider.id}.webhook_secret`} help="Required for verifying incoming webhook integrity.">
              <MaskedInput placeholder="whsec_..."/>
            </Field>
            <Field label="Webhook URL" code={`${provider.id}.webhook_url`}>
              <div style={{ display: 'flex', gap: 6 }}>
                <input className="input" defaultValue={`https://api.aonik.com/webhooks/${provider.id}/primrose`} style={{ flex: 1, fontFamily: 'var(--font-mono)', fontSize: 12 }} readOnly/>
                <button className="btn btn-ghost btn-sm">Copy</button>
              </div>
            </Field>
          </SettingsSection>

          <SettingsSection title="Routing" description="Decides which provider receives a payment, by currency and method.">
            <Field label="Default for GBP">
              <select className="select" defaultValue="Stripe (live)"><option>Stripe (live)</option><option>Wise Business</option></select>
            </Field>
            <Field label="Default for NGN" help="Native NGN settlement; auto-converted to GBP at end-of-day.">
              <select className="select" defaultValue="Paystack"><option>Paystack</option><option>Flutterwave (sandbox)</option></select>
            </Field>
            <Field label="Default for USD payouts">
              <select className="select" defaultValue="Wise Business"><option>Wise Business</option><option>Modern Treasury (disabled)</option></select>
            </Field>
            <Field label="Fallback strategy" help="If the default provider fails, retry on this one.">
              <select className="select" defaultValue="Wise Business → Modern Treasury">
                <option>None — fail fast</option>
                <option>Wise Business → Modern Treasury</option>
                <option>Stripe → Wise Business</option>
              </select>
            </Field>
          </SettingsSection>

          <SettingsSection title="Limits & risk" description="Per-transaction thresholds and risk gates.">
            <Field label="Max single payment" code={`${provider.id}.limits.max_payment`}>
              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>£</span>
                <input className="input" defaultValue="250,000.00" style={{ width: 160, fontFamily: 'var(--font-mono)' }}/>
              </div>
            </Field>
            <Field label="Daily volume cap" code={`${provider.id}.limits.daily_cap`}>
              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>£</span>
                <input className="input" defaultValue="2,000,000.00" style={{ width: 160, fontFamily: 'var(--font-mono)' }}/>
              </div>
            </Field>
            <Field label="3DS challenge" code={`${provider.id}.risk.3ds`} help="Trigger 3D Secure on cards above the threshold.">
              <select className="select" defaultValue="Above £500"><option>Always</option><option>Above £500</option><option>Above £1,000</option><option>Never</option></select>
            </Field>
            <Field label="Block country list" code={`${provider.id}.risk.blocklist`}>
              <textarea className="input" rows={2} defaultValue="IR, KP, RU, SY" style={{ fontFamily: 'var(--font-mono)', fontSize: 12, resize: 'vertical' }}/>
            </Field>
          </SettingsSection>
        </div>
      </div>
    </div>
  );
}

// ─── TTS settings ────────────────────────────────────────────────────
function ScreenSettingsTts() {
  const [provider, setProvider] = React.useState('ElevenLabs');
  const voices = [
    { id: 'rachel',  name: 'Rachel',  desc: 'American · Calm narration',          duration: '0:08' },
    { id: 'aria',    name: 'Aria',    desc: 'British · Conversational',           duration: '0:09' },
    { id: 'antoni',  name: 'Antoni',  desc: 'American · Warm, professional',      duration: '0:11' },
    { id: 'sarah',   name: 'Sarah',   desc: 'British · News-anchor delivery',     duration: '0:07' },
    { id: 'george',  name: 'George',  desc: 'British · Mature, authoritative',    duration: '0:10' },
    { id: 'custom1', name: 'Maria · custom', desc: 'Cloned voice · 22s sample',   duration: '0:09', custom: true },
  ];
  const [voice, setVoice] = React.useState('aria');

  return (
    <div style={{ padding: '24px 32px', overflow: 'auto', height: '100%' }}>
      <PageHeader
        eyebrow="Settings · AI"
        title="Text-to-Speech"
        subtitle="Provider credentials, voice selection, playback behavior, and usage limits."
        actions={<>
          <button className="btn btn-ghost btn-sm"><Icon name="upload" size={12}/> Upload sample</button>
          <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save changes</button>
        </>}
      />

      <div style={{ marginTop: 24, display: 'grid', gridTemplateColumns: '1fr 360px', gap: 16 }}>
        <div>
          <SettingsSection title="Provider & Playback" description="Choose your TTS provider and enable speech synthesis for this tenant. The provider selection determines credentials, voices, and options below.">
            <Field label="Provider" code="Tts.Provider"
              help="ElevenLabs offers high-quality multilingual voices. Mistral (Voxtral) supports zero-shot voice cloning.">
              <div style={{ display: 'flex', gap: 10 }}>
                {['ElevenLabs', 'Mistral'].map(p => {
                  const active = p === provider;
                  return (
                    <div key={p} onClick={() => setProvider(p)} style={{
                      flex: 1, padding: '12px 14px', borderRadius: 8,
                      border: active ? '2px solid var(--brand-primary)' : '1px solid var(--border-light)',
                      background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
                      cursor: 'pointer',
                    }}>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{p}</div>
                      <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>
                        {p === 'ElevenLabs' ? 'eleven_multilingual_v2 · 29 voices' : 'voxtral-mini-tts-2603 · zero-shot clone'}
                      </div>
                    </div>
                  );
                })}
              </div>
            </Field>
            <Field label="Speech enabled" code="Tts.Enabled">
              <Toggle on/>
            </Field>
          </SettingsSection>

          <SettingsSection title="Tenant credential"
            description={`Your ${provider} API key for the currently selected tenant.`}
            action={<Pill tone="success" size="sm" dot>Set</Pill>}>
            <Field label={`${provider} API Key`} code={`Tts.${provider}.ApiKey`}
              help="Update only. Submit blank to keep current value."
              status={<Pill tone="success" size="sm">Encrypted</Pill>}>
              <MaskedInput/>
            </Field>
            <div style={{ display: 'flex', gap: 8 }}>
              <button className="btn btn-ghost btn-sm">Clear stored value</button>
              <button className="btn btn-ghost btn-sm">Test credential</button>
            </div>
          </SettingsSection>

          <SettingsSection title="Voice selection"
            description="The voice used for playback. The list comes from the provider API for the currently effective credential."
            action={<button className="btn btn-ghost btn-sm"><Icon name="refresh" size={12}/> Refresh voices</button>}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
              {voices.map(v => {
                const active = v.id === voice;
                return (
                  <div key={v.id} onClick={() => setVoice(v.id)} style={{
                    padding: 12, borderRadius: 10, cursor: 'pointer',
                    border: active ? '2px solid var(--brand-primary)' : '1px solid var(--border-light)',
                    background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
                    display: 'flex', alignItems: 'center', gap: 12,
                  }}>
                    <div style={{
                      width: 36, height: 36, borderRadius: '50%',
                      background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', flex: 'none',
                      border: '1px solid var(--border-light)',
                    }}>
                      <Icon name={active ? 'check' : 'arrowright'} size={13} color={active ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 6 }}>
                        {v.name}
                        {v.custom && <Pill tone="success" size="sm">cloned</Pill>}
                      </div>
                      <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>{v.desc}</div>
                    </div>
                    <button className="btn btn-ghost btn-sm" style={{ padding: '4px 8px' }}>
                      <Icon name="globe" size={12}/>
                    </button>
                  </div>
                );
              })}
            </div>
          </SettingsSection>

          <SettingsSection title="Synthesis options" description="Provider-specific tuning. Affects timbre and consistency.">
            <Field label="Model" code="Tts.ModelId">
              <select className="select" defaultValue={provider === 'ElevenLabs' ? 'eleven_multilingual_v2' : 'voxtral-mini-tts-2603'}>
                {provider === 'ElevenLabs'
                  ? <><option>eleven_multilingual_v2</option><option>eleven_turbo_v2_5</option><option>eleven_flash_v2_5</option></>
                  : <><option>voxtral-mini-tts-2603</option></>}
              </select>
            </Field>
            <Field label="Output format" code="Tts.OutputFormat">
              <select className="select" defaultValue={provider === 'ElevenLabs' ? 'mp3_44100_128' : 'mp3'}>
                {provider === 'ElevenLabs'
                  ? <><option>mp3_44100_128</option><option>mp3_44100_192</option><option>pcm_22050</option></>
                  : <><option>mp3</option><option>wav</option></>}
              </select>
            </Field>
            <Field label="Stability" code="Tts.Stability" help="Lower = more emotive but inconsistent. Higher = monotone but stable.">
              <RangeRow value={0.6} suffix="0.6"/>
            </Field>
            <Field label="Similarity boost" code="Tts.SimilarityBoost" help="How closely to match the reference voice.">
              <RangeRow value={0.75} suffix="0.75"/>
            </Field>
            <Field label="Optimize streaming latency" code="Tts.StreamingLatency" help="0 = highest quality, 4 = lowest latency.">
              <select className="select" defaultValue="2"><option>0</option><option>1</option><option>2</option><option>3</option><option>4</option></select>
            </Field>
          </SettingsSection>
        </div>

        {/* Right rail — preview */}
        <div style={{ position: 'sticky', top: 0, alignSelf: 'flex-start', display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Preview</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 12, lineHeight: 1.5 }}>
              Validates provider access, stores an AiRun for audit, and plays the synthesized audio in-browser.
            </div>
            <textarea className="input" rows={4}
              defaultValue="Good afternoon. Three invoices are awaiting your review, and April fuel spending is trending 12% above plan."
              style={{ fontSize: 13, lineHeight: 1.5, resize: 'vertical', marginBottom: 12 }}/>
            <button className="btn btn-primary btn-sm" style={{ width: '100%', justifyContent: 'center', marginBottom: 12 }}>
              <Icon name="globe" size={12}/> Synthesize & play
            </button>

            {/* Waveform mock */}
            <div style={{ background: 'var(--surface-inset)', borderRadius: 8, padding: 14, marginBottom: 10 }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                <button className="btn btn-ghost btn-sm" style={{ padding: 4 }}><Icon name="check" size={14}/></button>
                <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>0:00 / 0:09</div>
              </div>
              <svg viewBox="0 0 200 32" style={{ width: '100%', height: 32 }}>
                {Array.from({ length: 60 }).map((_, i) => {
                  const h = 6 + Math.abs(Math.sin(i * 0.6) * 12) + (i % 3) * 2;
                  return <rect key={i} x={i * 3.3} y={(32 - h) / 2} width="2" height={h} rx="1" fill="var(--brand-primary)" opacity={i < 24 ? 1 : 0.35}/>;
                })}
              </svg>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
              <span>AiRunId: 7f9a-21c</span>
              <span>312ms · 14kb</span>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 12 }}>Usage · this month</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <UsageRow label="Characters" value="142,408 / 500,000" pct={28}/>
              <UsageRow label="Cost"        value="$11.40 / $40 limit" pct={28} color="var(--brand-secondary, #eb5c37)"/>
              <UsageRow label="Requests"    value="1,204"               pct={null}/>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Voice cloning</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 12, lineHeight: 1.5 }}>
              Upload a 30-second clean sample. Supports MP3, WAV, FLAC, OGG.
            </div>
            <div style={{
              border: '2px dashed var(--border-light)', borderRadius: 10, padding: 18,
              textAlign: 'center', cursor: 'pointer',
            }}>
              <Icon name="upload" size={20} color="var(--text-tertiary)"/>
              <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)', marginTop: 8 }}>Drop sample or click to upload</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 4 }}>Max 25MB · stereo OK</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function UsageRow({ label, value, pct, color = 'var(--brand-primary)' }) {
  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 4 }}>
        <span style={{ color: 'var(--text-secondary)' }}>{label}</span>
        <span style={{ color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontSize: 11.5 }}>{value}</span>
      </div>
      {pct != null && (
        <div style={{ height: 4, borderRadius: 2, background: 'var(--surface-inset)', overflow: 'hidden' }}>
          <div style={{ height: '100%', width: `${pct}%`, background: color }}/>
        </div>
      )}
    </div>
  );
}

function RangeRow({ value = 0.5, suffix }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
      <div style={{ flex: 1, height: 4, background: 'var(--surface-inset)', borderRadius: 2, position: 'relative' }}>
        <div style={{ position: 'absolute', left: 0, top: 0, height: '100%', width: `${value * 100}%`, background: 'var(--brand-primary)', borderRadius: 2 }}/>
        <div style={{ position: 'absolute', left: `calc(${value * 100}% - 7px)`, top: -5, width: 14, height: 14, borderRadius: '50%', background: '#fff', border: '2px solid var(--brand-primary)' }}/>
      </div>
      <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', width: 40, textAlign: 'right' }}>{suffix}</span>
    </div>
  );
}

Object.assign(window, { ScreenSettingsLanding, ScreenSettingsPlatform, ScreenSettingsGateways, ScreenSettingsTts });
