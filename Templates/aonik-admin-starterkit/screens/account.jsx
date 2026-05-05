// Account — workspace account hub: Plan, Usage, Billing, Profile, Security.
// Internal left sub-nav. Reached from the bottom-left profile popover.

const ACCOUNT_TABS = [
  { id: 'plan',     label: 'Plan',     icon: 'sparkles' },
  { id: 'usage',    label: 'Usage',    icon: 'chart' },
  { id: 'billing',  label: 'Billing',  icon: 'creditcard' },
  { id: 'profile',  label: 'Profile',  icon: 'user' },
  { id: 'security', label: 'Security', icon: 'shield' },
];

function AccountShell({ initial = 'plan' }) {
  const [tab, setTab] = React.useState(initial);

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
        <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Workspace</div>
        <div style={{ fontSize: 17, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Account</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.45, marginBottom: 18 }}>
          Plan, billing, profile, and security for Primrose Logistics.
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          {ACCOUNT_TABS.map(t => {
            const active = tab === t.id;
            return (
              <div key={t.id} onClick={() => setTab(t.id)} style={{
                display: 'flex', alignItems: 'center', gap: 10,
                padding: '8px 12px', borderRadius: 6, cursor: 'pointer',
                background: active ? 'var(--brand-primary-10)' : 'transparent',
                color: active ? 'var(--brand-primary)' : 'var(--text-primary)',
                fontWeight: active ? 600 : 500, fontSize: 13,
              }}>
                <Icon name={t.icon} size={14} color={active ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
                <span>{t.label}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Right column */}
      <div style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: '32px 40px' }}>
        {tab === 'plan'     && <AccountPlan onJump={setTab}/>}
        {tab === 'usage'    && <AccountUsage onJump={setTab}/>}
        {tab === 'billing'  && <AccountBilling onJump={setTab}/>}
        {tab === 'profile'  && <AccountProfile/>}
        {tab === 'security' && <AccountSecurity/>}
      </div>
    </div>
  );
}

// ─── Plan ────────────────────────────────────────────────────────────
// Cards only: Launch / Growth / Scale. No FAQ, no toggle, no meters.
function AccountPlan() {
  const tiers = [
    {
      id: 'launch',
      name: 'Launch',
      price: 'Free',
      cadence: 'forever',
      description: 'Get the platform running for a single workspace and a small team.',
      features: [
        'Up to 5 users',
        '200 AI messages per month',
        '1 connected payment gateway',
        'Community support',
      ],
      cta: 'Current plan',
      ctaTone: 'ghost',
      current: true,
    },
    {
      id: 'growth',
      name: 'Growth',
      price: '$49',
      cadence: 'per workspace · monthly',
      description: 'For finance teams running real volume across orders, invoicing, and FX.',
      features: [
        'Up to 25 users',
        '10,000 AI messages per month',
        'All payment gateways',
        'Approval workflows',
        'Priority support',
      ],
      cta: 'Upgrade to Growth',
      ctaTone: 'primary',
      featured: true,
    },
    {
      id: 'scale',
      name: 'Scale',
      price: 'Talk to us',
      cadence: 'custom contract',
      description: 'Multi-entity treasury, custom agents, and a dedicated success engineer.',
      features: [
        'Unlimited users',
        'Custom AI usage',
        'Multi-entity ledger',
        'Custom agents and tools',
        'SSO, SCIM, and audit export',
        'Named success contact',
      ],
      cta: 'Contact sales',
      ctaTone: 'outline',
    },
  ];

  return (
    <>
      <PageHeader
        eyebrow="Account"
        title="Plan"
        subtitle="Pick the plan that fits how your team uses AONIK. Switch at any time."
      />

      <div style={{ marginTop: 28, display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, maxWidth: 1080 }}>
        {tiers.map(t => (
          <PlanCard key={t.id} tier={t}/>
        ))}
      </div>

      <div style={{
        marginTop: 28, padding: '14px 16px',
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, maxWidth: 1080,
        display: 'flex', alignItems: 'center', gap: 12,
        fontSize: 12.5, color: 'var(--text-secondary)',
      }}>
        <Icon name="help" size={14} color="var(--text-tertiary)"/>
        <span style={{ flex: 1 }}>
          All plans include the full agent runtime, ledger, and audit log. Differences are quotas and support tier.
        </span>
        <button className="btn btn-ghost btn-sm">Compare in detail</button>
      </div>
    </>
  );
}

function PlanCard({ tier }) {
  const featured = !!tier.featured;
  return (
    <div style={{
      background: 'var(--surface)',
      border: featured ? '1px solid var(--brand-primary)' : '1px solid var(--border-light)',
      borderRadius: 14,
      padding: 24,
      display: 'flex', flexDirection: 'column', gap: 18,
      position: 'relative',
      boxShadow: featured ? '0 12px 28px -16px rgb(5 90 96 / 0.35)' : 'none',
    }}>
      {featured && (
        <span style={{
          position: 'absolute', top: -10, left: 24,
          background: 'var(--brand-primary)', color: '#fff',
          fontSize: 10.5, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase',
          padding: '3px 10px', borderRadius: 999,
        }}>Recommended</span>
      )}

      <div>
        <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{tier.name}</div>
        <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2, lineHeight: 1.5 }}>
          {tier.description}
        </div>
      </div>

      <div>
        <div style={{
          fontFamily: 'var(--font-brand)', fontSize: 30, fontWeight: 700,
          color: 'var(--text-primary)', letterSpacing: '-0.01em', lineHeight: 1.05,
        }}>{tier.price}</div>
        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 4 }}>{tier.cadence}</div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {tier.features.map(f => (
          <div key={f} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 12.5, color: 'var(--text-primary)', lineHeight: 1.5 }}>
            <Icon name="check" size={13} color="var(--brand-primary)"/>
            <span>{f}</span>
          </div>
        ))}
      </div>

      <button
        className={
          tier.ctaTone === 'primary' ? 'btn btn-primary'
          : tier.ctaTone === 'outline' ? 'btn btn-outline'
          : 'btn btn-ghost'
        }
        disabled={tier.current}
        style={{
          marginTop: 'auto', width: '100%', justifyContent: 'center',
          opacity: tier.current ? 0.6 : 1, cursor: tier.current ? 'default' : 'pointer',
        }}>
        {tier.cta}
      </button>
    </div>
  );
}

// ─── Usage ───────────────────────────────────────────────────────────
function AccountUsage({ onJump }) {
  const limits = [
    { label: 'AI messages',     used: 168,    limit: 200,    unit: '' },
    { label: 'Active users',    used: 4,      limit: 5,      unit: '' },
    { label: 'Orders this month', used: 312,  limit: 1000,   unit: '' },
    { label: 'Storage',          used: 1.4,   limit: 5,      unit: 'GB', formatter: v => v.toFixed(1) },
    { label: 'Webhooks',         used: 2,     limit: 5,      unit: '' },
    { label: 'Voice synthesis',  used: 142408, limit: 500000, unit: 'chars', formatter: v => v.toLocaleString() },
  ];

  return (
    <>
      <PageHeader
        eyebrow="Account"
        title="Usage"
        subtitle="What's been consumed against your plan limits this billing cycle."
        actions={<button className="btn btn-primary btn-sm" onClick={() => onJump?.('plan')}>
          <Icon name="sparkles" size={12}/> Upgrade plan
        </button>}
      />

      <div style={{ marginTop: 24, maxWidth: 980 }}>
        <CurrentPlanBanner onJump={onJump}/>

        <div style={{
          marginTop: 16,
          background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12,
          padding: 24, display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 22,
        }}>
          {limits.map(l => <UsageMeter key={l.label} {...l}/>)}
        </div>

        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 12, lineHeight: 1.5 }}>
          Cycle resets on the 1st. Quotas are tenant-wide and shared across all users.
        </div>
      </div>
    </>
  );
}

function CurrentPlanBanner({ onJump }) {
  return (
    <div style={{
      background: 'linear-gradient(135deg, var(--brand-primary) 0%, #077278 100%)',
      color: '#fff', borderRadius: 12, padding: '18px 22px',
      display: 'flex', alignItems: 'center', gap: 18,
    }}>
      <div style={{
        width: 38, height: 38, borderRadius: 10,
        background: 'rgba(255,255,255,0.18)',
        display: 'grid', placeItems: 'center', flex: 'none',
      }}>
        <Icon name="sparkles" size={18} color="#fff"/>
      </div>
      <div style={{ flex: 1 }}>
        <div style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, opacity: 0.85 }}>Current plan</div>
        <div style={{ fontSize: 17, fontWeight: 600, marginTop: 2 }}>Launch · free</div>
        <div style={{ fontSize: 12, opacity: 0.85, marginTop: 4, lineHeight: 1.5 }}>
          You're on the free tier. Upgrade for higher AI quotas and approval workflows.
        </div>
      </div>
      <button onClick={() => onJump?.('plan')} style={{
        height: 32, padding: '0 14px',
        background: '#fff', color: 'var(--brand-primary)',
        border: 'none', borderRadius: 8, fontWeight: 600, fontSize: 12.5, cursor: 'pointer',
        display: 'inline-flex', alignItems: 'center', gap: 6, flex: 'none',
      }}>Upgrade plan <Icon name="arrowright" size={12}/></button>
    </div>
  );
}

function UsageMeter({ label, used, limit, unit, formatter }) {
  const fmt = formatter || (v => v.toLocaleString());
  const pct = Math.min(100, Math.round((used / limit) * 100));
  const warn = pct >= 80;
  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 6 }}>
        <span style={{ fontSize: 12.5, color: 'var(--text-secondary)', fontWeight: 500 }}>{label}</span>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)' }}>
          {fmt(used)} <span style={{ color: 'var(--text-tertiary)' }}>/ {fmt(limit)}{unit ? ' ' + unit : ''}</span>
        </span>
      </div>
      <div style={{ height: 6, background: 'var(--surface-inset)', borderRadius: 999, overflow: 'hidden' }}>
        <div style={{
          width: `${pct}%`, height: '100%', borderRadius: 999,
          background: warn ? 'var(--brand-secondary, #eb5c37)' : 'var(--brand-primary)',
        }}/>
      </div>
      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 4 }}>
        {pct}% of limit used
      </div>
    </div>
  );
}

// ─── Billing ─────────────────────────────────────────────────────────
function AccountBilling({ onJump }) {
  const invoices = [
    { id: 'INV-AON-2604', date: 'May 1, 2026', amount: '$0.00',  status: 'Paid',   tone: 'success', desc: 'Launch plan · May 2026' },
    { id: 'INV-AON-2504', date: 'Apr 1, 2026', amount: '$0.00',  status: 'Paid',   tone: 'success', desc: 'Launch plan · April 2026' },
    { id: 'INV-AON-2404', date: 'Mar 1, 2026', amount: '$0.00',  status: 'Paid',   tone: 'success', desc: 'Launch plan · March 2026' },
    { id: 'INV-AON-2304', date: 'Feb 1, 2026', amount: '$0.00',  status: 'Paid',   tone: 'success', desc: 'Launch plan · February 2026' },
  ];

  return (
    <>
      <PageHeader
        eyebrow="Account"
        title="Billing"
        subtitle="Plan, payment method, and invoice history for this workspace."
        actions={<>
          <button className="btn btn-ghost btn-sm"><Icon name="download" size={12}/> Export invoices</button>
          <button className="btn btn-primary btn-sm" onClick={() => onJump?.('plan')}>
            <Icon name="sparkles" size={12}/> Upgrade plan
          </button>
        </>}
      />

      <div style={{ marginTop: 24, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, maxWidth: 1080 }}>
        {/* Current plan card */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 12, padding: 22,
        }}>
          <div style={{ fontSize: 10.5, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Current plan</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
            <Icon name="sparkles" size={16} color="var(--brand-primary)"/>
            <span style={{ fontSize: 18, fontWeight: 600, color: 'var(--text-primary)' }}>Launch</span>
            <Pill tone="default" size="sm">Free tier</Pill>
          </div>
          <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: 14 }}>
            Up to 5 users and 200 AI messages per month. Renews automatically on the 1st.
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-primary btn-sm" onClick={() => onJump?.('plan')}>Change plan</button>
            <button className="btn btn-ghost btn-sm" onClick={() => onJump?.('usage')}>View usage</button>
          </div>
        </div>

        {/* Payment method */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 12, padding: 22,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <div style={{ fontSize: 10.5, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)' }}>Payment method</div>
            <button className="btn btn-ghost btn-sm">Update</button>
          </div>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 14,
            padding: '12px 14px', borderRadius: 10,
            background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
          }}>
            <div style={{
              width: 42, height: 30, borderRadius: 6,
              background: 'linear-gradient(135deg, #1a1f3a 0%, #2d3a6b 100%)',
              display: 'grid', placeItems: 'center', color: '#fff',
              fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 11, flex: 'none',
            }}>VISA</div>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>
                •••• •••• •••• 4242
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>
                Expires 09 / 2028 · Oliver Chen
              </div>
            </div>
            <Pill tone="success" size="sm" dot>Default</Pill>
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 10, lineHeight: 1.5 }}>
            Charged on the 1st of each month for the prior cycle's plan and overages.
          </div>
        </div>
      </div>

      {/* Invoices */}
      <div style={{
        marginTop: 16, background: 'var(--surface)',
        border: '1px solid var(--border-light)', borderRadius: 12,
        maxWidth: 1080,
      }}>
        <div style={{
          padding: '16px 20px', borderBottom: '1px solid var(--border-light)',
          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        }}>
          <div>
            <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>Recent invoices</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>The last few billing cycles for this workspace.</div>
          </div>
          <button className="btn btn-ghost btn-sm">View all</button>
        </div>
        <div>
          {invoices.map((inv, i) => (
            <div key={inv.id} style={{
              display: 'grid', gridTemplateColumns: '160px 1fr 120px 110px 32px',
              padding: '14px 20px', alignItems: 'center', gap: 16,
              borderBottom: i === invoices.length - 1 ? 'none' : '1px solid var(--border-light)',
            }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>{inv.id}</div>
              <div>
                <div style={{ fontSize: 12.5, color: 'var(--text-primary)' }}>{inv.desc}</div>
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 2 }}>{inv.date}</div>
              </div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, color: 'var(--text-primary)' }}>{inv.amount}</div>
              <Pill tone={inv.tone} size="sm" dot>{inv.status}</Pill>
              <button className="btn btn-ghost btn-sm" style={{ padding: 4, justifySelf: 'end' }} title="Download PDF">
                <Icon name="download" size={13}/>
              </button>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}

// ─── Profile ─────────────────────────────────────────────────────────
function AccountProfile() {
  return (
    <>
      <PageHeader
        eyebrow="Account"
        title="Profile"
        subtitle="Your name, email, and how you appear to teammates."
        actions={<button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save changes</button>}
      />

      <div style={{ marginTop: 24, maxWidth: 760 }}>
        <SettingsSection title="Identity" description="Shown on your avatar, mentions, and approval activity.">
          <Field label="Photo" code="User.AvatarUrl" help="Square image, at least 96 by 96 pixels.">
            <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
              <Avatar name="Oliver Chen" size={56} color="#7b76b6" textColor="#fff"/>
              <button className="btn btn-ghost btn-sm"><Icon name="upload" size={12}/> Upload</button>
              <button className="btn btn-ghost btn-sm">Remove</button>
            </div>
          </Field>
          <Field label="Full name" code="User.DisplayName">
            <input className="input" defaultValue="Oliver Chen"/>
          </Field>
          <Field label="Email" code="User.Email" status={<Pill tone="success" size="sm" dot>Verified</Pill>}
            help="Used for sign-in, notifications, and audit attribution.">
            <input className="input" defaultValue="oliver@primrose.co"/>
          </Field>
          <Field label="Job title" code="User.Title">
            <input className="input" defaultValue="Head of Finance"/>
          </Field>
        </SettingsSection>

        <SettingsSection title="Locale" description="Affects how dates, currencies, and numbers are formatted for you.">
          <Field label="Language" code="User.Locale">
            <select className="select" defaultValue="English (UK)">
              <option>English (UK)</option>
              <option>English (US)</option>
              <option>Français</option>
              <option>Português</option>
            </select>
          </Field>
          <Field label="Time zone" code="User.Timezone">
            <select className="select" defaultValue="Europe/London — UTC+1">
              <option>Europe/London — UTC+1</option>
              <option>Africa/Lagos — UTC+1</option>
              <option>America/New_York — UTC-4</option>
            </select>
          </Field>
          <Field label="Theme" code="User.Theme" help="System follows your operating system setting.">
            <div style={{ display: 'flex', gap: 0, background: 'var(--surface-inset)', padding: 3, borderRadius: 8, width: 'fit-content' }}>
              {['Light', 'Dark', 'System'].map(m => {
                const active = m === 'System';
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
        </SettingsSection>
      </div>
    </>
  );
}

// ─── Security ────────────────────────────────────────────────────────
function AccountSecurity() {
  const sessions = [
    { device: 'MacBook Pro · Chrome', location: 'London, UK',  ip: '82.13.44.219', last: 'Active now',     current: true },
    { device: 'iPhone 15 · Safari',    location: 'London, UK',  ip: '82.13.44.219', last: '2 hours ago' },
    { device: 'Windows · Edge',         location: 'Lagos, NG',   ip: '102.89.6.18',  last: 'Yesterday' },
  ];

  return (
    <>
      <PageHeader
        eyebrow="Account"
        title="Security"
        subtitle="Sign-in methods, multi-factor authentication, and active sessions."
      />

      <div style={{ marginTop: 24, maxWidth: 760 }}>
        <SettingsSection title="Password" description="Used to sign in with email. Required even when SSO is enabled.">
          <Field label="Current password">
            <input className="input" type="password" defaultValue="••••••••••" style={{ fontFamily: 'var(--font-mono)' }}/>
          </Field>
          <Field label="New password" help="At least 12 characters with one number and one symbol.">
            <input className="input" type="password" placeholder="Enter new password"/>
          </Field>
          <Field label="Confirm new password">
            <input className="input" type="password" placeholder="Re-enter new password"/>
          </Field>
          <div>
            <button className="btn btn-primary btn-sm">Update password</button>
          </div>
        </SettingsSection>

        <SettingsSection title="Two-factor authentication" description="Require a second factor on every sign-in." action={<Pill tone="success" size="sm" dot>Enabled</Pill>}>
          <Field label="Authenticator app" help="Time-based codes from 1Password, Authy, or Google Authenticator.">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <Toggle on/>
              <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>Configured · 1Password</span>
            </div>
          </Field>
          <Field label="Recovery codes" help="Single-use codes for when you lose access to your authenticator.">
            <div style={{ display: 'flex', gap: 8 }}>
              <button className="btn btn-ghost btn-sm"><Icon name="download" size={12}/> Download codes</button>
              <button className="btn btn-ghost btn-sm"><Icon name="refresh" size={12}/> Regenerate</button>
            </div>
          </Field>
          <Field label="Trusted devices" help="Skip the second factor on devices you mark as trusted for 30 days.">
            <Toggle on={false}/>
          </Field>
        </SettingsSection>

        <SettingsSection title="Active sessions" description="Devices currently signed in to this workspace." action={<button className="btn btn-ghost btn-sm">Sign out everywhere</button>}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
            {sessions.map((s, i) => (
              <div key={i} style={{
                display: 'flex', alignItems: 'center', gap: 14,
                padding: '12px 0', borderBottom: i === sessions.length - 1 ? 'none' : '1px solid var(--border-light)',
              }}>
                <div style={{
                  width: 32, height: 32, borderRadius: 8,
                  background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', flex: 'none',
                }}>
                  <Icon name={s.device.includes('iPhone') ? 'phone' : s.device.includes('Windows') ? 'terminal' : 'fullscreen'} size={14} color="var(--text-secondary)"/>
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)', display: 'flex', gap: 8, alignItems: 'center' }}>
                    {s.device}
                    {s.current && <Pill tone="success" size="sm">This session</Pill>}
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 2, fontFamily: 'var(--font-mono)' }}>
                    {s.location} · {s.ip} · {s.last}
                  </div>
                </div>
                {!s.current && <button className="btn btn-ghost btn-sm">Sign out</button>}
              </div>
            ))}
          </div>
        </SettingsSection>

        <SettingsSection title="Danger zone" description="Permanent actions on this account.">
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
            <div>
              <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>Delete account</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 2 }}>
                Removes you from this workspace. Other admins keep access. Audit history is preserved.
              </div>
            </div>
            <button className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}>
              <Icon name="trash" size={12}/> Delete account
            </button>
          </div>
        </SettingsSection>
      </div>
    </>
  );
}

// Top-level screens for each artboard. Each opens AccountShell on a specific tab.
function ScreenAccountPlan()     { return <AccountShell initial="plan"/>; }
function ScreenAccountUsage()    { return <AccountShell initial="usage"/>; }
function ScreenAccountBilling()  { return <AccountShell initial="billing"/>; }
function ScreenAccountProfile()  { return <AccountShell initial="profile"/>; }
function ScreenAccountSecurity() { return <AccountShell initial="security"/>; }

Object.assign(window, {
  AccountShell,
  ScreenAccountPlan, ScreenAccountUsage, ScreenAccountBilling,
  ScreenAccountProfile, ScreenAccountSecurity,
});
