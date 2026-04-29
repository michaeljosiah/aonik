// ─── Tenant Setup Wizard — three editorial-calm directions ─────────────────
//
// All three variations share:
//   · Aonik teal + coral palette, Infra display + DM Sans body
//   · Same content domain (Company → Region → Features → Contact → Summary)
//   · Bundle-based feature picker (Starter / Growth / Enterprise + advanced)
//   · Agent assist that *whispers* — never blocks
//
// They DIFFER in form-factor:
//   A · Editorial split — left brand wall, right form, classic but premium
//   B · Single-page scroll — no step gating, anchored TOC on the side
//   C · Conversational — agent-led chat that reveals form chunks inline
//
// ─────────────────────────────────────────────────────────────────────────

// ─── Shared data ───────────────────────────────────────────────────────────

const REGIONS = [
  { id: 'emea', label: 'EMEA',     residency: 'eu-west-2 · London',     currencies: 'GBP · EUR · NGN', latency: '< 30ms' },
  { id: 'amer', label: 'Americas', residency: 'us-east-1 · Virginia',   currencies: 'USD · CAD · BRL', latency: '< 40ms' },
  { id: 'apac', label: 'APAC',     residency: 'ap-south-1 · Mumbai',    currencies: 'INR · SGD · IDR', latency: '< 60ms' },
  { id: 'wafr', label: 'W. Africa',residency: 'af-west-1 · Lagos',      currencies: 'NGN · GHS · XOF', latency: '< 20ms' },
];

const BUNDLES = [
  {
    id: 'starter',
    name: 'Starter',
    tagline: 'Everything to operate. Nothing you don’t need yet.',
    price: 'Included',
    seats: 'Up to 10 seats',
    modules: ['Ledger', 'Invoices', 'Bank feeds', 'Customers', 'Basic agents'],
    agents: ['Billing', 'Bookkeeping'],
    recommended: false,
  },
  {
    id: 'growth',
    name: 'Growth',
    tagline: 'Multi-currency, multi-product, full agent fleet.',
    price: '$2,400 / mo',
    seats: 'Up to 50 seats',
    modules: ['Everything in Starter', 'Bill Payments', 'Remittances', 'Approvals', 'Treasury'],
    agents: ['Billing', 'Bookkeeping', 'Compliance', 'Treasury', 'Customer Ops'],
    recommended: true,
  },
  {
    id: 'enterprise',
    name: 'Enterprise',
    tagline: 'Custom rails, SOC 2, regional data residency, dedicated trust.',
    price: 'Talk to us',
    seats: 'Unlimited',
    modules: ['Everything in Growth', 'Custom rails', 'Sandbox tenants', 'Audit packs'],
    agents: ['Full fleet', '+ custom agents', '+ on-prem MCP'],
    recommended: false,
  },
];

const ADVANCED_GROUPS = [
  { id: 'ledger',    label: 'Finance · Ledger',         items: ['Chart of accounts', 'Journal entries', 'Period close', 'Reconciliation'] },
  { id: 'billing',   label: 'Bill Payments',            items: ['Catalog', 'Approval routing', 'Receipts', 'Reconcile to invoices'] },
  { id: 'remit',     label: 'Remittances + FX',         items: ['Corridors', 'FX rates', 'Settlement', 'Drift alerts'] },
  { id: 'compliance',label: 'Compliance',               items: ['KYC / KYB', 'Sanctions screen', 'Audit retention', 'SOC 2 export'] },
  { id: 'agents',    label: 'AI · Agents',              items: ['Workflows', 'Policies', 'Tasks queue', 'Auto-apply (≤ ceiling)'] },
];

// ─── Small primitives shared across variants ───────────────────────────────

function StepDot({ done, active, label, idx }) {
  const bg = done ? 'var(--brand-primary)' : active ? 'var(--surface)' : 'transparent';
  const border = done ? 'var(--brand-primary)' : active ? 'var(--brand-primary)' : 'var(--border-medium)';
  const color = done ? '#fff' : active ? 'var(--brand-primary)' : 'var(--text-tertiary)';
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <span style={{
        width: 22, height: 22, borderRadius: 999,
        background: bg, border: `1px solid ${border}`, color,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600,
        transition: 'all 180ms ease',
      }}>
        {done ? <Icon name="check" size={11} color="#fff"/> : idx}
      </span>
      <span style={{
        fontSize: 12.5, fontWeight: active ? 600 : 500,
        color: active ? 'var(--text-primary)' : done ? 'var(--text-secondary)' : 'var(--text-tertiary)',
        letterSpacing: '-0.005em',
      }}>{label}</span>
    </div>
  );
}

function FieldRow({ label, hint, required, children, span = 1 }) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: 8, gridColumn: `span ${span}` }}>
      <span style={{
        fontSize: 11, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase',
        color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 6,
      }}>
        {label}
        {required && <span style={{ color: 'var(--brand-secondary)', fontSize: 9 }}>●</span>}
      </span>
      {children}
      {hint && <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>{hint}</span>}
    </label>
  );
}

function AgentWhisper({ children, action }) {
  return (
    <div style={{
      display: 'flex', gap: 12, padding: '12px 14px',
      background: 'var(--brand-primary-10)',
      border: '1px solid transparent',
      borderRadius: 10,
      alignItems: 'flex-start',
    }}>
      <div style={{
        width: 26, height: 26, borderRadius: 8,
        background: 'var(--brand-primary)', color: '#fff',
        display: 'flex', alignItems: 'center', justifyContent: 'center', flex: 'none',
      }}>
        <Icon name="sparkles" size={13} color="#fff"/>
      </div>
      <div style={{ flex: 1, fontSize: 12.5, color: 'var(--text-primary)', lineHeight: 1.6 }}>
        {children}
        {action && (
          <div style={{ marginTop: 8, display: 'flex', gap: 6 }}>
            {action}
          </div>
        )}
      </div>
    </div>
  );
}

function BundleCard({ bundle, selected, onSelect, dense = false }) {
  return (
    <div
      onClick={onSelect}
      style={{
        position: 'relative',
        background: 'var(--surface)',
        border: `1px solid ${selected ? 'var(--brand-primary)' : 'var(--border-light)'}`,
        boxShadow: selected
          ? '0 0 0 3px var(--brand-primary-10), 0 6px 20px -8px rgba(5,90,96,0.25)'
          : '0 1px 0 rgba(0,0,0,0.02)',
        borderRadius: 14,
        padding: dense ? 18 : 22,
        cursor: 'pointer',
        transition: 'all 160ms ease',
        display: 'flex', flexDirection: 'column', gap: dense ? 12 : 16,
      }}
    >
      {bundle.recommended && (
        <span style={{
          position: 'absolute', top: -10, left: 18,
          background: 'var(--brand-secondary)', color: '#fff',
          fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
          letterSpacing: '0.08em', textTransform: 'uppercase',
          padding: '3px 8px', borderRadius: 4,
        }}>Recommended</span>
      )}

      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
        <div>
          <div style={{
            fontFamily: 'var(--font-brand)', fontSize: dense ? 18 : 22, fontWeight: 700,
            letterSpacing: '-0.015em', color: 'var(--text-primary)',
          }}>{bundle.name}</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{bundle.seats}</div>
        </div>
        <div style={{
          fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600,
          color: selected ? 'var(--brand-primary)' : 'var(--text-secondary)',
        }}>{bundle.price}</div>
      </div>

      <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.55 }}>{bundle.tagline}</div>

      <div style={{ borderTop: '1px solid var(--border-light)', paddingTop: dense ? 10 : 14, display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{
          fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase',
          color: 'var(--text-tertiary)',
        }}>Modules</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-primary)', lineHeight: 1.6 }}>
          {bundle.modules.join(' · ')}
        </div>
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
        {bundle.agents.map(a => (
          <span key={a} style={{
            fontFamily: 'var(--font-mono)', fontSize: 10.5,
            padding: '3px 8px', borderRadius: 4,
            background: selected ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
            color: selected ? 'var(--brand-primary)' : 'var(--text-secondary)',
            border: '1px solid transparent',
          }}>{a}</span>
        ))}
      </div>

      <div style={{
        position: 'absolute', top: 18, right: 18,
        width: 18, height: 18, borderRadius: 999,
        border: `1.5px solid ${selected ? 'var(--brand-primary)' : 'var(--border-medium)'}`,
        background: selected ? 'var(--brand-primary)' : 'transparent',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        {selected && <span style={{ width: 7, height: 7, borderRadius: 999, background: '#fff' }}/>}
      </div>
    </div>
  );
}

// ─── A · Editorial split-screen ────────────────────────────────────────────

function ScreenTenantSignupSplit() {
  const [step, setStep] = React.useState(2); // Features step — most visually rich
  const [bundle, setBundle] = React.useState('growth');
  const [advanced, setAdvanced] = React.useState(false);

  const steps = ['Company', 'Region', 'Features', 'Contact', 'Summary'];

  return (
    <div style={{
      width: '100%', height: '100%',
      display: 'grid', gridTemplateColumns: '420px 1fr',
      background: 'var(--background)',
      fontFamily: 'var(--font-sans)',
    }}>
      {/* ─── Brand wall ─── */}
      <aside style={{
        position: 'relative',
        background: 'linear-gradient(165deg, #04494e 0%, #055a60 50%, #0a6e72 100%)',
        color: '#fff',
        padding: '36px 36px 36px 40px',
        display: 'flex', flexDirection: 'column', justifyContent: 'space-between',
        overflow: 'hidden',
      }}>
        <div style={{
          position: 'absolute', inset: 0,
          backgroundImage: 'radial-gradient(circle at 20% 25%, rgba(232,168,56,0.12) 0%, transparent 50%), linear-gradient(rgba(255,255,255,0.025) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.025) 1px, transparent 1px)',
          backgroundSize: 'auto, 32px 32px, 32px 32px',
          pointerEvents: 'none',
        }}/>

        <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 11 }}>
          <span style={{
            position: 'relative', display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            width: 32, height: 32, borderRadius: 8,
            background: '#fff', color: '#055a60',
            fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 19, lineHeight: 1, letterSpacing: '-0.04em',
          }}>
            A
            <span style={{ position: 'absolute', top: 4, right: 4, width: 5, height: 5, borderRadius: '50%', background: '#e8a838' }}/>
          </span>
          <span style={{ fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 19, letterSpacing: '-0.015em' }}>aonik</span>
        </div>

        <div style={{ position: 'relative', display: 'flex', flexDirection: 'column', gap: 32 }}>
          <div>
            <div style={{
              fontSize: 10.5, fontWeight: 600, letterSpacing: '0.14em', textTransform: 'uppercase',
              color: 'rgba(255,255,255,0.55)', marginBottom: 16,
              display: 'flex', alignItems: 'center', gap: 10,
            }}>
              <span style={{ width: 16, height: 1, background: 'rgba(255,255,255,0.4)' }}/>
              Tenant setup
            </div>
            <h1 style={{
              fontFamily: 'var(--font-brand)', fontWeight: 700,
              fontSize: 38, lineHeight: 1.08, letterSpacing: '-0.02em',
              margin: 0, color: '#fff',
            }}>
              A workspace<br/>
              <span style={{ color: '#e8a838' }}>built for the way</span><br/>
              you actually work.
            </h1>
            <p style={{
              fontSize: 14, lineHeight: 1.6, color: 'rgba(255,255,255,0.72)',
              marginTop: 18, maxWidth: 320,
            }}>
              Five quick steps. Take three minutes. We’ll provision agents, ledgers, and policies as you go — you can change anything later.
            </p>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {steps.map((s, i) => (
              <div key={s} style={{
                display: 'flex', alignItems: 'center', gap: 12,
                opacity: i > step ? 0.45 : 1,
                transition: 'opacity 200ms ease',
              }}>
                <span style={{
                  width: 22, height: 22, borderRadius: 999,
                  background: i < step ? '#e8a838' : i === step ? '#fff' : 'transparent',
                  border: `1px solid ${i <= step ? 'transparent' : 'rgba(255,255,255,0.3)'}`,
                  color: i === step ? '#055a60' : '#fff',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontFamily: 'var(--font-mono)', fontSize: 10.5, fontWeight: 700,
                }}>
                  {i < step ? <Icon name="check" size={10} color="#04494e"/> : i + 1}
                </span>
                <span style={{
                  fontFamily: 'var(--font-brand)', fontSize: 15, fontWeight: i === step ? 600 : 500,
                  color: i === step ? '#fff' : 'rgba(255,255,255,0.78)',
                  letterSpacing: '-0.01em',
                }}>{s}</span>
              </div>
            ))}
          </div>
        </div>

        <div style={{
          position: 'relative',
          fontSize: 11.5, color: 'rgba(255,255,255,0.55)',
          display: 'flex', alignItems: 'center', gap: 10,
        }}>
          <Icon name="lock" size={12} color="rgba(255,255,255,0.5)"/>
          SOC 2 Type II · ISO 27001 · GDPR-aligned
        </div>
      </aside>

      {/* ─── Form pane ─── */}
      <main style={{
        overflow: 'auto',
        padding: '64px 80px 48px',
        display: 'flex', flexDirection: 'column', gap: 28,
        maxWidth: 920,
        background: 'var(--background)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 24 }}>
          <div>
            <div style={{
              fontSize: 11, fontWeight: 600, letterSpacing: '0.1em', textTransform: 'uppercase',
              color: 'var(--brand-primary)', marginBottom: 8,
            }}>Step 03 of 05 · Features</div>
            <h2 style={{
              fontFamily: 'var(--font-brand)', fontWeight: 700,
              fontSize: 34, lineHeight: 1.12, letterSpacing: '-0.02em',
              margin: 0, color: 'var(--text-primary)',
            }}>What should be turned on?</h2>
            <p style={{
              fontSize: 14.5, lineHeight: 1.6, color: 'var(--text-secondary)',
              marginTop: 10, maxWidth: 540,
            }}>
              Pick a bundle to start with. Every feature can be toggled later in <span style={{ color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontSize: 13 }}>Settings · Feature flags</span>.
            </p>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button className="btn btn-ghost btn-sm">Save & exit</button>
          </div>
        </div>

        <AgentWhisper action={
          <>
            <button className="btn btn-primary btn-sm" style={{ height: 26, fontSize: 11.5 }}>Apply suggestion</button>
            <button className="btn btn-ghost btn-sm" style={{ height: 26, fontSize: 11.5 }}>Show reasoning</button>
          </>
        }>
          Based on <b>Primrose Logistics</b> being a logistics company in EMEA with multi-currency exposure (NGN · GBP · EUR), I’d recommend the <b>Growth</b> bundle — Bill Payments and Remittances are the two modules you’ll use weekly.
        </AgentWhisper>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16 }}>
          {BUNDLES.map(b => (
            <BundleCard key={b.id} bundle={b} selected={bundle === b.id} onSelect={() => setBundle(b.id)}/>
          ))}
        </div>

        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 14, padding: 0, overflow: 'hidden',
        }}>
          <button
            onClick={() => setAdvanced(a => !a)}
            style={{
              width: '100%', padding: '16px 22px',
              background: 'transparent', border: 'none', cursor: 'pointer',
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              fontFamily: 'inherit',
            }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <Icon name="settings" size={14} color="var(--text-secondary)"/>
              <span>
                <span style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>Advanced — toggle individual modules</span>
                <span style={{ fontSize: 12, color: 'var(--text-tertiary)', marginLeft: 8 }}>5 groups · 22 features</span>
              </span>
            </span>
            <Icon name={advanced ? 'chevron-up' : 'chevdown'} size={14} color="var(--text-tertiary)"/>
          </button>

          {advanced && (
            <div style={{
              borderTop: '1px solid var(--border-light)',
              padding: '18px 22px',
              display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 16,
            }}>
              {ADVANCED_GROUPS.map(g => (
                <div key={g.id} style={{
                  border: '1px solid var(--border-light)', borderRadius: 10,
                  padding: 14, background: 'var(--surface-inset)',
                }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
                    <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{g.label}</div>
                    <span style={{
                      width: 30, height: 16, borderRadius: 999,
                      background: 'var(--brand-primary)', position: 'relative',
                    }}>
                      <span style={{
                        position: 'absolute', top: 2, right: 2,
                        width: 12, height: 12, borderRadius: 999, background: '#fff',
                      }}/>
                    </span>
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                    {g.items.join(' · ')}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div style={{
          marginTop: 8, paddingTop: 24,
          borderTop: '1px solid var(--border-light)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        }}>
          <button className="btn btn-ghost btn-sm" onClick={() => setStep(s => Math.max(0, s - 1))}>
            <Icon name="chevron-left" size={12}/> Back to Region
          </button>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <span style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>Auto-saved · just now</span>
            <button className="btn btn-primary btn-sm" style={{ height: 36, padding: '0 20px' }} onClick={() => setStep(s => Math.min(4, s + 1))}>
              Continue to Contact <Icon name="chevron" size={12} color="#fff"/>
            </button>
          </div>
        </div>
      </main>
    </div>
  );
}

// ─── B · Single-page anchored scroll ───────────────────────────────────────

function ScreenTenantSignupSinglePage() {
  const [bundle, setBundle] = React.useState('growth');
  const [region, setRegion] = React.useState('emea');
  const sections = [
    { id: 'company',  label: 'Company',  num: '01' },
    { id: 'region',   label: 'Region',   num: '02' },
    { id: 'features', label: 'Features', num: '03' },
    { id: 'contact',  label: 'Contact',  num: '04' },
    { id: 'review',   label: 'Review',   num: '05' },
  ];
  const [active, setActive] = React.useState('region');

  const sectionStyle = {
    paddingBlock: 56,
    borderBottom: '1px solid var(--border-light)',
    scrollMarginTop: 80,
  };
  const sectionLabel = {
    fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600,
    color: 'var(--brand-primary)', letterSpacing: '0.1em',
    textTransform: 'uppercase', marginBottom: 6,
  };
  const sectionTitle = {
    fontFamily: 'var(--font-brand)', fontSize: 30, fontWeight: 700,
    letterSpacing: '-0.02em', color: 'var(--text-primary)',
    margin: '0 0 8px', lineHeight: 1.15,
  };
  const sectionSub = {
    fontSize: 14, color: 'var(--text-secondary)', lineHeight: 1.6,
    margin: '0 0 28px', maxWidth: 560,
  };

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'var(--background)',
      fontFamily: 'var(--font-sans)',
      display: 'grid', gridTemplateColumns: '1fr',
      overflow: 'hidden',
    }}>
      {/* Top bar */}
      <header style={{
        position: 'sticky', top: 0, zIndex: 5,
        height: 60, padding: '0 36px',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: 'rgba(245,242,235,0.92)',
        backdropFilter: 'blur(10px)',
        borderBottom: '1px solid var(--border-light)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <AonikMark size={26}/>
          <span style={{ fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 16, letterSpacing: '-0.015em' }}>aonik</span>
          <span style={{ width: 1, height: 18, background: 'var(--border-medium)', marginInline: 12 }}/>
          <span style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>New tenant · <b style={{ color: 'var(--text-primary)' }}>Primrose Logistics</b></span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>Signed in as oliver@primrose.co</span>
          <button className="btn btn-ghost btn-sm">Save & exit</button>
          <button className="btn btn-primary btn-sm">Provision tenant</button>
        </div>
      </header>

      {/* Body: sticky TOC + scrolling form */}
      <div style={{
        display: 'grid', gridTemplateColumns: '260px 1fr 320px',
        height: 'calc(100% - 60px)',
        overflow: 'hidden',
      }}>
        {/* Left: progress TOC */}
        <nav style={{
          padding: '52px 28px 28px 36px',
          borderRight: '1px solid var(--border-light)',
          display: 'flex', flexDirection: 'column', gap: 6,
          overflow: 'auto',
        }}>
          <div style={{
            fontSize: 10.5, fontWeight: 600, letterSpacing: '0.1em', textTransform: 'uppercase',
            color: 'var(--text-tertiary)', marginBottom: 14,
          }}>Contents</div>
          {sections.map(s => {
            const isActive = active === s.id;
            const isDone = sections.findIndex(x => x.id === active) > sections.findIndex(x => x.id === s.id);
            return (
              <a
                key={s.id}
                href={`#${s.id}`}
                onClick={() => setActive(s.id)}
                style={{
                  display: 'flex', alignItems: 'center', gap: 14,
                  padding: '12px 14px', borderRadius: 10,
                  textDecoration: 'none',
                  background: isActive ? 'var(--surface)' : 'transparent',
                  border: `1px solid ${isActive ? 'var(--border-light)' : 'transparent'}`,
                  boxShadow: isActive ? '0 1px 0 rgba(0,0,0,0.02)' : 'none',
                  position: 'relative',
                }}>
                {isActive && <span style={{
                  position: 'absolute', left: -36, top: '50%', transform: 'translateY(-50%)',
                  width: 24, height: 1, background: 'var(--brand-primary)',
                }}/>}
                <span style={{
                  fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600,
                  color: isDone ? 'var(--brand-primary)' : isActive ? 'var(--brand-primary)' : 'var(--text-tertiary)',
                  letterSpacing: '0.04em',
                }}>{s.num}</span>
                <span style={{
                  fontSize: 13, fontWeight: isActive ? 600 : 500,
                  color: isActive ? 'var(--text-primary)' : isDone ? 'var(--text-secondary)' : 'var(--text-secondary)',
                }}>{s.label}</span>
                {isDone && <Icon name="check" size={11} color="var(--brand-primary)"/>}
              </a>
            );
          })}

          <div style={{ flex: 1 }}/>

          <div style={{
            marginTop: 24, padding: '14px 16px',
            background: 'var(--surface)', borderRadius: 10,
            border: '1px solid var(--border-light)',
            display: 'flex', flexDirection: 'column', gap: 8,
          }}>
            <div style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>Estimated time</div>
            <div style={{ fontFamily: 'var(--font-brand)', fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>3 min</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.55 }}>
              Everything is editable later in Settings.
            </div>
          </div>
        </nav>

        {/* Middle: form */}
        <main style={{
          overflow: 'auto',
          padding: '0 64px',
          background: 'var(--background)',
        }}>
          {/* COMPANY */}
          <section id="company" style={{ ...sectionStyle, paddingTop: 64 }}>
            <div style={sectionLabel}>01 · Company</div>
            <h3 style={sectionTitle}>Tell us who’s setting up shop.</h3>
            <p style={sectionSub}>This becomes the workspace name and the legal name we put on receipts and audit exports.</p>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 22 }}>
              <FieldRow label="Workspace name" required hint="What appears in the sidebar and emails.">
                <input className="input" defaultValue="Primrose Logistics"/>
              </FieldRow>
              <FieldRow label="Legal entity">
                <input className="input" defaultValue="Primrose Logistics Ltd"/>
              </FieldRow>
              <FieldRow label="Industry" required>
                <select className="select" defaultValue="logistics">
                  <option value="logistics">Logistics & freight</option>
                  <option value="fintech">Fintech</option>
                  <option value="manufacturing">Manufacturing</option>
                  <option value="services">Professional services</option>
                </select>
              </FieldRow>
              <FieldRow label="Company size" required>
                <select className="select" defaultValue="50">
                  <option value="10">1 – 10</option>
                  <option value="50">11 – 50</option>
                  <option value="200">51 – 200</option>
                  <option value="1000">201 – 1,000</option>
                </select>
              </FieldRow>
              <FieldRow label="Website" span={2}>
                <input className="input" defaultValue="primrose.co"/>
              </FieldRow>
            </div>
          </section>

          {/* REGION */}
          <section id="region" style={sectionStyle}>
            <div style={sectionLabel}>02 · Region</div>
            <h3 style={sectionTitle}>Where should your data live?</h3>
            <p style={sectionSub}>This sets data residency, default currencies, and which clearing partners we route through.</p>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 14 }}>
              {REGIONS.map(r => {
                const sel = region === r.id;
                return (
                  <div key={r.id} onClick={() => setRegion(r.id)} style={{
                    padding: 18, borderRadius: 12, cursor: 'pointer',
                    background: 'var(--surface)',
                    border: `1px solid ${sel ? 'var(--brand-primary)' : 'var(--border-light)'}`,
                    boxShadow: sel ? '0 0 0 3px var(--brand-primary-10)' : 'none',
                    display: 'flex', alignItems: 'flex-start', gap: 14,
                  }}>
                    <span style={{
                      width: 40, height: 40, borderRadius: 10,
                      background: sel ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
                      color: sel ? 'var(--brand-primary)' : 'var(--text-secondary)',
                      display: 'flex', alignItems: 'center', justifyContent: 'center',
                      fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 14, letterSpacing: '0.01em',
                      flex: 'none',
                    }}>{r.label.split(/[ .]/)[0].slice(0,3).toUpperCase()}</span>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, justifyContent: 'space-between' }}>
                        <span style={{ fontSize: 14.5, fontWeight: 600, color: 'var(--text-primary)' }}>{r.label}</span>
                        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{r.latency}</span>
                      </div>
                      <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 3, fontFamily: 'var(--font-mono)' }}>
                        {r.residency}
                      </div>
                      <div style={{ fontSize: 12, color: 'var(--text-tertiary)', marginTop: 2 }}>
                        {r.currencies}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </section>

          {/* FEATURES */}
          <section id="features" style={sectionStyle}>
            <div style={sectionLabel}>03 · Features</div>
            <h3 style={sectionTitle}>Pick the right shape of workspace.</h3>
            <p style={sectionSub}>Bundles are starting points — every module can be turned on or off later. We tag what your industry typically needs.</p>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginBottom: 18 }}>
              {BUNDLES.map(b => (
                <BundleCard key={b.id} bundle={b} selected={bundle === b.id} onSelect={() => setBundle(b.id)} dense/>
              ))}
            </div>
            <a href="#advanced" style={{
              fontSize: 12.5, color: 'var(--brand-primary)', display: 'inline-flex', gap: 6, alignItems: 'center',
              fontWeight: 500,
            }}>
              <Icon name="settings" size={12} color="var(--brand-primary)"/>
              Configure individual modules instead
            </a>
          </section>

          {/* CONTACT */}
          <section id="contact" style={sectionStyle}>
            <div style={sectionLabel}>04 · Contact</div>
            <h3 style={sectionTitle}>Who do we talk to?</h3>
            <p style={sectionSub}>Primary admin and a billing contact. You can invite the rest of your team after setup.</p>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 22 }}>
              <FieldRow label="Admin name" required>
                <input className="input" defaultValue="Oliver Ikeda"/>
              </FieldRow>
              <FieldRow label="Admin email" required>
                <input className="input" defaultValue="oliver@primrose.co"/>
              </FieldRow>
              <FieldRow label="Billing contact">
                <input className="input" defaultValue="finance@primrose.co"/>
              </FieldRow>
              <FieldRow label="Phone">
                <input className="input" defaultValue="+44 20 7946 0123"/>
              </FieldRow>
            </div>
          </section>

          {/* REVIEW */}
          <section id="review" style={{ ...sectionStyle, borderBottom: 'none', paddingBottom: 80 }}>
            <div style={sectionLabel}>05 · Review</div>
            <h3 style={sectionTitle}>Ready to provision?</h3>
            <p style={sectionSub}>We’ll create the tenant, seed your chart of accounts, and turn on the agents you selected. Takes about 40 seconds.</p>
            <div style={{
              background: 'var(--surface)', borderRadius: 14,
              border: '1px solid var(--border-light)',
              padding: 24,
              display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20,
            }}>
              {[
                ['Workspace', 'Primrose Logistics'],
                ['Industry', 'Logistics & freight · 11–50'],
                ['Region', 'EMEA · eu-west-2 London'],
                ['Bundle', 'Growth · 5 agents · 12 modules'],
                ['Admin', 'Oliver Ikeda · oliver@primrose.co'],
                ['Billing', 'finance@primrose.co'],
              ].map(([k, v]) => (
                <div key={k}>
                  <div style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)', marginBottom: 4 }}>{k}</div>
                  <div style={{ fontSize: 14, fontWeight: 500, color: 'var(--text-primary)' }}>{v}</div>
                </div>
              ))}
            </div>
            <button className="btn btn-primary" style={{ height: 44, padding: '0 24px', marginTop: 24 }}>
              Provision tenant <Icon name="chevron" size={13} color="#fff"/>
            </button>
          </section>
        </main>

        {/* Right: live preview */}
        <aside style={{
          padding: '52px 28px',
          borderLeft: '1px solid var(--border-light)',
          background: 'var(--surface-inset)',
          display: 'flex', flexDirection: 'column', gap: 22,
          overflow: 'auto',
        }}>
          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.1em', textTransform: 'uppercase', color: 'var(--text-tertiary)', marginBottom: 8 }}>Live preview</div>
            <div style={{ fontFamily: 'var(--font-brand)', fontSize: 17, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.015em', lineHeight: 1.25 }}>
              Your workspace as it stands.
            </div>
          </div>

          {/* Mini sidebar mock */}
          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 10, padding: 12,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, paddingBottom: 10, borderBottom: '1px solid var(--border-light)', marginBottom: 10 }}>
              <Avatar name="Primrose Logistics" size={22} color="var(--brand-primary)" textColor="#fff"/>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>Primrose Logistics</div>
                <div style={{ fontSize: 9.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>EMEA · GBP·EUR·NGN</div>
              </div>
            </div>
            {['My Space', 'Ledger', 'Invoices', 'Bank feeds', 'Bill Payments', 'Remittances', 'Approvals', 'Agents'].map((n, i) => (
              <div key={n} style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '5px 8px', borderRadius: 6,
                fontSize: 11, color: i === 0 ? 'var(--text-primary)' : 'var(--text-secondary)',
                background: i === 0 ? 'var(--surface-inset)' : 'transparent',
                fontWeight: i === 0 ? 600 : 400,
              }}>
                <span style={{ width: 6, height: 6, borderRadius: 999, background: i < 6 ? 'var(--brand-primary)' : 'var(--border-medium)' }}/>
                {n}
              </div>
            ))}
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>Agents queued</div>
            {['Billing Agent', 'Bookkeeping Agent', 'Compliance Agent', 'Treasury Agent', 'Customer Ops Agent'].map(a => (
              <div key={a} style={{
                display: 'flex', alignItems: 'center', gap: 10,
                padding: '8px 10px', background: 'var(--surface)',
                border: '1px solid var(--border-light)', borderRadius: 8,
              }}>
                <Avatar name={a} size={20} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>
                <div style={{ flex: 1, fontSize: 12, color: 'var(--text-primary)' }}>{a}</div>
                <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success)' }}/>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}

// ─── C · Conversational with agent co-pilot ────────────────────────────────

function ScreenTenantSignupChat() {
  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'var(--background)',
      fontFamily: 'var(--font-sans)',
      display: 'grid', gridTemplateColumns: '1fr 380px',
      overflow: 'hidden',
    }}>
      {/* Main: chat thread */}
      <main style={{
        display: 'flex', flexDirection: 'column',
        overflow: 'hidden',
        background: 'var(--background)',
      }}>
        {/* Top bar */}
        <header style={{
          height: 60, padding: '0 36px',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          borderBottom: '1px solid var(--border-light)',
          background: 'var(--background)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <AonikMark size={26}/>
            <span style={{ fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: 16, letterSpacing: '-0.015em' }}>aonik</span>
            <span style={{ width: 1, height: 18, background: 'var(--border-medium)', marginInline: 12 }}/>
            <span style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>Setting up <b style={{ color: 'var(--text-primary)' }}>Primrose Logistics</b></span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <span style={{
              fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)',
              display: 'inline-flex', alignItems: 'center', gap: 6,
            }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success)' }}/>
              Onboarding Agent online
            </span>
            <button className="btn btn-ghost btn-sm">Switch to form</button>
          </div>
        </header>

        {/* Thread */}
        <div style={{
          flex: 1, overflow: 'auto',
          padding: '40px 80px 24px',
          display: 'flex', flexDirection: 'column', gap: 28,
        }}>
          {/* Hero greeting */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 8 }}>
            <div style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.1em', textTransform: 'uppercase', color: 'var(--brand-primary)' }}>
              ✦ Onboarding Agent
            </div>
            <h1 style={{
              fontFamily: 'var(--font-brand)', fontSize: 32, fontWeight: 700,
              letterSpacing: '-0.02em', lineHeight: 1.15,
              margin: 0, color: 'var(--text-primary)', maxWidth: 620,
            }}>
              Hi Oliver. Let’s build your workspace together.
            </h1>
            <p style={{ fontSize: 14, color: 'var(--text-secondary)', lineHeight: 1.6, margin: '6px 0 0', maxWidth: 540 }}>
              I’ll ask a few short questions and propose defaults as we go. You can edit anything and skip what’s already obvious.
            </p>
          </div>

          {/* Q1: agent message + auto-detected company card */}
          <ChatAgent
            text={<>From your sign-in I picked up <b>primrose.co</b>. I cross-checked it against Companies House and your DNS records — does this look right?</>}
          >
            <CompanyConfirmCard/>
          </ChatAgent>

          {/* User reply (compact) */}
          <ChatUser>Looks right. Use Primrose Logistics.</ChatUser>

          {/* Q2: region inferred */}
          <ChatAgent
            text={<>Got it. Based on your office in London + customers across Lagos and Accra, I’d default to <b>EMEA residency</b> with multi-currency on. Want me to switch to <b>W. Africa</b> for sub-30ms latency to Lagos?</>}
          >
            <RegionInlineCard/>
          </ChatAgent>

          <ChatUser>Stay on EMEA for now.</ChatUser>

          {/* Q3: feature recommendation as inline tool card */}
          <ChatAgent
            text={<>For a logistics company at your size with NGN exposure, I’d turn on the <b>Growth bundle</b>. That gets you Bill Payments and Remittances on day one. Here’s exactly what would be enabled:</>}
            streaming
          >
            <BundleProposalCard/>
          </ChatAgent>
        </div>

        {/* Composer */}
        <div style={{
          padding: '12px 80px 24px',
          background: 'var(--background)',
          borderTop: '1px solid var(--border-light)',
        }}>
          <div style={{
            display: 'flex', alignItems: 'flex-end', gap: 10,
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 14, padding: '10px 12px',
            boxShadow: '0 4px 16px -8px rgba(0,0,0,0.08)',
          }}>
            <div style={{ flex: 1 }}>
              <div style={{
                fontSize: 13.5, color: 'var(--text-tertiary)',
                padding: '8px 4px', minHeight: 24,
              }}>Reply or ask a question…</div>
              <div style={{ display: 'flex', gap: 6, paddingTop: 6 }}>
                {['Approve & continue', 'Show me the form instead', 'I want Enterprise tier'].map(s => (
                  <button key={s} style={{
                    fontSize: 11.5, padding: '4px 10px', borderRadius: 999,
                    background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
                    border: '1px solid transparent', cursor: 'pointer',
                    fontFamily: 'inherit',
                  }}>{s}</button>
                ))}
              </div>
            </div>
            <button className="btn btn-primary" style={{ height: 36, paddingInline: 16, alignSelf: 'flex-end' }}>
              <Icon name="send" size={13} color="#fff"/> Send
            </button>
          </div>
          <div style={{
            fontSize: 11, color: 'var(--text-tertiary)',
            display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8,
          }}>
            <span>Press ⏎ to send · ⇧⏎ for newline</span>
            <span>3 of 5 questions · ~90s left</span>
          </div>
        </div>
      </main>

      {/* Sidebar: living summary */}
      <aside style={{
        background: 'var(--surface-inset)',
        borderLeft: '1px solid var(--border-light)',
        padding: '40px 24px',
        display: 'flex', flexDirection: 'column', gap: 20,
        overflow: 'auto',
      }}>
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.1em', textTransform: 'uppercase', color: 'var(--text-tertiary)', marginBottom: 6 }}>What we’re building</div>
          <div style={{ fontFamily: 'var(--font-brand)', fontSize: 22, fontWeight: 700, letterSpacing: '-0.02em', color: 'var(--text-primary)', lineHeight: 1.15 }}>
            Primrose Logistics
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 4 }}>EMEA · GBP·EUR·NGN</div>
        </div>

        <SummaryItem label="Company" status="done">
          <div>Primrose Logistics Ltd</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>Logistics · 11–50 · primrose.co</div>
        </SummaryItem>

        <SummaryItem label="Region" status="done">
          <div>EMEA · eu-west-2</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>Multi-currency · GBP, EUR, NGN</div>
        </SummaryItem>

        <SummaryItem label="Features" status="proposed">
          <div>Growth bundle</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>5 agents · Bill Payments, Remittances, Approvals</div>
        </SummaryItem>

        <SummaryItem label="Contact" status="pending">
          <div style={{ color: 'var(--text-tertiary)' }}>Up next</div>
        </SummaryItem>

        <SummaryItem label="Review" status="pending">
          <div style={{ color: 'var(--text-tertiary)' }}>Provision the tenant</div>
        </SummaryItem>

        <div style={{ flex: 1 }}/>

        <div style={{
          padding: 14, background: 'var(--surface)', borderRadius: 10,
          border: '1px solid var(--border-light)',
          display: 'flex', flexDirection: 'column', gap: 8,
        }}>
          <div style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>Reasoning</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.55 }}>
            Defaults are inferred from your domain, location, and industry. Tap any item above to see <b style={{ color: 'var(--brand-primary)' }}>why I suggested it</b>.
          </div>
        </div>
      </aside>
    </div>
  );
}

// — Conversational sub-components —

function ChatAgent({ text, children, streaming }) {
  return (
    <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start', maxWidth: 760 }}>
      <div style={{
        width: 32, height: 32, borderRadius: 10, flex: 'none',
        background: 'var(--brand-primary)', color: '#fff',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        boxShadow: '0 2px 8px -2px rgba(5,90,96,0.4)',
      }}>
        <Icon name="sparkles" size={14} color="#fff"/>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, flex: 1 }}>
        <div style={{
          fontSize: 14.5, lineHeight: 1.6, color: 'var(--text-primary)',
        }}>
          {text}
          {streaming && <span style={{ display: 'inline-block', width: 7, height: 14, background: 'var(--brand-primary)', marginLeft: 4, verticalAlign: '-2px', animation: 'pulse 1.2s infinite' }}/>}
        </div>
        {children}
      </div>
    </div>
  );
}

function ChatUser({ children }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'flex-end', maxWidth: 760, marginLeft: 'auto' }}>
      <div style={{
        background: 'var(--surface)',
        border: '1px solid var(--border-light)',
        borderRadius: '14px 14px 4px 14px',
        padding: '10px 14px',
        fontSize: 13.5, lineHeight: 1.5, color: 'var(--text-primary)',
        maxWidth: 480,
      }}>{children}</div>
    </div>
  );
}

function CompanyConfirmCard() {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 16,
      display: 'flex', alignItems: 'center', gap: 14,
      maxWidth: 540,
    }}>
      <Avatar name="Primrose Logistics" size={44} color="var(--brand-primary)" textColor="#fff"/>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontSize: 14.5, fontWeight: 600, color: 'var(--text-primary)' }}>Primrose Logistics Ltd</div>
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>
          Logistics & freight · London, UK · ~30 employees
        </div>
        <div style={{ display: 'flex', gap: 6, marginTop: 8 }}>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, padding: '2px 7px', borderRadius: 4, background: 'var(--success-light)', color: 'var(--success)' }}>✓ Companies House</span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, padding: '2px 7px', borderRadius: 4, background: 'var(--success-light)', color: 'var(--success)' }}>✓ DNS verified</span>
        </div>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <button className="btn btn-primary btn-sm" style={{ height: 28, fontSize: 11.5 }}>Confirm</button>
        <button className="btn btn-ghost btn-sm" style={{ height: 28, fontSize: 11.5 }}>Edit</button>
      </div>
    </div>
  );
}

function RegionInlineCard() {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 14,
      display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10,
      maxWidth: 540,
    }}>
      {[
        { id: 'emea', label: 'EMEA', sub: 'eu-west-2 · London', latency: '< 30ms', sel: true },
        { id: 'wafr', label: 'W. Africa', sub: 'af-west-1 · Lagos', latency: '< 20ms', sel: false },
      ].map(r => (
        <div key={r.id} style={{
          padding: 12, borderRadius: 10,
          background: r.sel ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
          border: `1px solid ${r.sel ? 'var(--brand-primary)' : 'transparent'}`,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 }}>
            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{r.label}</span>
            {r.sel && <Icon name="check" size={12} color="var(--brand-primary)"/>}
          </div>
          <div style={{ fontSize: 11, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>{r.sub}</div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 2 }}>{r.latency}</div>
        </div>
      ))}
    </div>
  );
}

function BundleProposalCard() {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 14, padding: 18, maxWidth: 600,
      display: 'flex', flexDirection: 'column', gap: 14,
    }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 10 }}>
          <span style={{ fontFamily: 'var(--font-brand)', fontSize: 18, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.015em' }}>Growth bundle</span>
          <span style={{
            fontFamily: 'var(--font-mono)', fontSize: 10.5, padding: '2px 7px', borderRadius: 4,
            background: 'var(--brand-secondary-10)', color: 'var(--brand-secondary)', fontWeight: 600, letterSpacing: '0.06em',
          }}>RECOMMENDED</span>
        </div>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600, color: 'var(--text-secondary)' }}>$2,400 / mo</span>
      </div>

      <div style={{
        display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10,
        background: 'var(--surface-inset)', borderRadius: 10, padding: 12,
      }}>
        {[
          { k: 'Modules', v: '12 enabled', detail: 'Ledger · Bill Payments · Remittances · Approvals · Treasury' },
          { k: 'Agents',  v: '5 deployed', detail: 'Billing · Bookkeeping · Compliance · Treasury · Customer Ops' },
          { k: 'Seats',   v: 'Up to 50',   detail: 'Invite people after setup' },
          { k: 'Confidence', v: '0.92',    detail: 'Based on industry × size × FX exposure' },
        ].map(r => (
          <div key={r.k}>
            <div style={{ fontSize: 10, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>{r.k}</div>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginTop: 1 }}>{r.v}</div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 1, lineHeight: 1.45 }}>{r.detail}</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10 }}>
        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>You can downgrade or toggle individual modules later.</div>
        <div style={{ display: 'flex', gap: 6 }}>
          <button className="btn btn-ghost btn-sm" style={{ height: 30, fontSize: 12 }}>Show all bundles</button>
          <button className="btn btn-primary btn-sm" style={{ height: 30, fontSize: 12 }}>
            <Icon name="check" size={11} color="#fff"/> Apply Growth
          </button>
        </div>
      </div>
    </div>
  );
}

function SummaryItem({ label, status, children }) {
  const dotColor = status === 'done' ? 'var(--success)' : status === 'proposed' ? 'var(--brand-secondary)' : 'var(--border-medium)';
  const labelColor = status === 'pending' ? 'var(--text-tertiary)' : 'var(--text-secondary)';
  return (
    <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
      <span style={{
        width: 8, height: 8, borderRadius: 999,
        background: dotColor, marginTop: 7, flex: 'none',
        boxShadow: status !== 'pending' ? `0 0 0 3px ${dotColor}33` : 'none',
      }}/>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 3 }}>
          <span style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase', color: labelColor }}>{label}</span>
          {status === 'proposed' && (
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 9.5, color: 'var(--brand-secondary)', fontWeight: 600 }}>PROPOSED</span>
          )}
          {status === 'done' && (
            <Icon name="check" size={11} color="var(--success)"/>
          )}
        </div>
        <div style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500, lineHeight: 1.45 }}>{children}</div>
      </div>
    </div>
  );
}

// ─── Export to window ──────────────────────────────────────────────────────
Object.assign(window, {
  ScreenTenantSignupSplit,
  ScreenTenantSignupSinglePage,
  ScreenTenantSignupChat,
});
