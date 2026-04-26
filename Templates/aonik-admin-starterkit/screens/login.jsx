// ─── Login page ─────────────────────────────────────────────────────────
// Two-pane layout: left = brand/marketing wall, right = sign-in form.
// Surfaces the same Aonik tone-of-voice ("Agents propose. Systems apply.")
// and reuses tokens + the AonikLoadingMark glyph from the splash.

function ScreenLogin() {
  const [email, setEmail] = React.useState('oliver@primrose.co');
  const [pw, setPw] = React.useState('••••••••••••');
  const [showSso, setShowSso] = React.useState(false);

  return (
    <div style={{
      width: '100%', height: '100%',
      display: 'grid', gridTemplateColumns: '1.05fr 1fr',
      background: 'var(--background)',
      fontFamily: 'var(--font-sans)',
    }}>
      {/* ───────── LEFT PANE — brand wall ───────── */}
      <div style={{
        position: 'relative',
        background: 'linear-gradient(155deg, #04494e 0%, #055a60 45%, #0a6e72 100%)',
        color: '#fff',
        padding: '40px 56px',
        display: 'flex', flexDirection: 'column', justifyContent: 'space-between',
        overflow: 'hidden',
      }}>
        {/* Decorative grid + glow */}
        <div style={{
          position: 'absolute', inset: 0,
          backgroundImage: 'radial-gradient(circle at 30% 30%, rgba(232,168,56,0.18) 0%, transparent 45%), linear-gradient(rgba(255,255,255,0.04) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.04) 1px, transparent 1px)',
          backgroundSize: 'auto, 28px 28px, 28px 28px',
          pointerEvents: 'none',
        }}/>

        {/* Top: brand */}
        <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{
            position: 'relative', display: 'inline-flex',
            alignItems: 'center', justifyContent: 'center',
            width: 36, height: 36, borderRadius: 9,
            background: '#fff', color: '#055a60',
            fontFamily: 'var(--font-brand)', fontWeight: 700,
            fontSize: 22, letterSpacing: '-0.04em', lineHeight: 1,
            boxShadow: '0 4px 16px -4px rgba(0,0,0,.3)',
          }}>
            A
            <span style={{
              position: 'absolute', top: 4, right: 4,
              width: 6, height: 6, borderRadius: '50%',
              background: 'var(--brand-mark-dot, #e8a838)',
            }}/>
          </span>
          <span style={{
            fontFamily: 'var(--font-brand)', fontWeight: 700,
            fontSize: 22, letterSpacing: '-0.015em',
          }}>aonik</span>
        </div>

        {/* Middle: tagline + agent proposal preview */}
        <div style={{ position: 'relative', display: 'flex', flexDirection: 'column', gap: 32, maxWidth: 520 }}>
          <div>
            <div style={{
              fontSize: 11, fontWeight: 600, letterSpacing: '0.12em', textTransform: 'uppercase',
              color: 'rgba(255,255,255,0.6)', marginBottom: 14,
              display: 'flex', alignItems: 'center', gap: 8,
            }}>
              <span style={{ width: 18, height: 1, background: 'rgba(255,255,255,0.4)' }}/>
              Admin · Operator console
            </div>
            <h1 style={{
              fontFamily: 'var(--font-brand)', fontWeight: 700,
              fontSize: 44, lineHeight: 1.08, letterSpacing: '-0.02em',
              margin: 0, color: '#fff',
            }}>
              Agents propose.<br/>
              <span style={{ color: 'var(--brand-mark-dot, #e8a838)' }}>Systems apply.</span>
            </h1>
            <p style={{
              fontSize: 15, lineHeight: 1.55, color: 'rgba(255,255,255,0.78)',
              marginTop: 16, maxWidth: 460,
            }}>
              The control plane for your operations team — orders, ledger, payouts and AI agents working together under policy.
            </p>
          </div>

          {/* Mini proposal card — alive snippet of the product */}
          <div style={{
            background: 'rgba(255,255,255,0.06)',
            backdropFilter: 'blur(12px)',
            border: '1px solid rgba(255,255,255,0.14)',
            borderRadius: 12,
            padding: 16,
            display: 'flex', flexDirection: 'column', gap: 10,
            boxShadow: '0 20px 50px -20px rgba(0,0,0,0.4)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <span style={{
                width: 26, height: 26, borderRadius: '50%',
                background: 'var(--brand-mark-dot, #e8a838)', color: '#3a2a05',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 12, fontWeight: 700,
              }}>B</span>
              <span style={{ fontSize: 12.5, fontWeight: 600 }}>Billing Agent</span>
              <span style={{
                fontSize: 10, fontFamily: 'var(--font-mono)', letterSpacing: '0.05em',
                color: 'rgba(255,255,255,0.55)', marginLeft: 'auto',
              }}>2s ago</span>
            </div>
            <div style={{ fontSize: 13.5, lineHeight: 1.5 }}>
              I matched <b>3 invoices</b> to last week's bank transactions — £42,180 total. Drafting journal entries.
            </div>
            <div style={{
              display: 'flex', gap: 6, flexWrap: 'wrap',
              fontFamily: 'var(--font-mono)', fontSize: 10.5,
            }}>
              {['search_invoices', 'list_bank_transactions', 'match_invoice_to_txn'].map(t => (
                <span key={t} style={{
                  padding: '3px 7px', borderRadius: 4,
                  background: 'rgba(255,255,255,0.08)', color: 'rgba(255,255,255,0.85)',
                  border: '1px solid rgba(255,255,255,0.1)',
                }}>{t}</span>
              ))}
            </div>
          </div>
        </div>

        {/* Bottom: trust strip */}
        <div style={{
          position: 'relative', display: 'flex', alignItems: 'center', gap: 24,
          fontSize: 11, color: 'rgba(255,255,255,0.55)',
          fontFamily: 'var(--font-mono)', letterSpacing: '0.05em',
        }}>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success, #6abf6e)' }}/>
            All systems operational
          </span>
          <span>SOC 2 · Type II</span>
          <span>PCI DSS</span>
          <span style={{ marginLeft: 'auto' }}>v 26.4.1</span>
        </div>
      </div>

      {/* ───────── RIGHT PANE — sign-in ───────── */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        padding: '48px 56px', position: 'relative',
      }}>
        {/* Top-right helper link */}
        <div style={{
          position: 'absolute', top: 28, right: 32,
          fontSize: 12.5, color: 'var(--text-secondary)',
        }}>
          Don't have an account?{' '}
          <a href="#" style={{ color: 'var(--brand-primary)', fontWeight: 600, textDecoration: 'none' }}>
            Request access
          </a>
        </div>

        <div style={{ width: '100%', maxWidth: 380, display: 'flex', flexDirection: 'column', gap: 20 }}>
          <div>
            <div style={{
              fontSize: 11, fontWeight: 600, letterSpacing: '0.1em', textTransform: 'uppercase',
              color: 'var(--brand-primary)', marginBottom: 10,
            }}>
              Sign in
            </div>
            <h2 style={{
              fontFamily: 'var(--font-brand)', fontWeight: 700,
              fontSize: 28, letterSpacing: '-0.015em',
              margin: 0, color: 'var(--text-primary)',
            }}>
              Welcome back, Oliver
            </h2>
            <p style={{
              fontSize: 13.5, color: 'var(--text-secondary)',
              marginTop: 6, lineHeight: 1.5,
            }}>
              Continue to <b style={{ color: 'var(--text-primary)' }}>Primrose Logistics</b> · Production
            </p>
          </div>

          {/* SSO buttons */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <SsoButton provider="google" label="Continue with Google"/>
            <SsoButton provider="microsoft" label="Continue with Microsoft"/>
            <button
              onClick={() => setShowSso(s => !s)}
              style={{
                display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
                width: '100%', height: 38, borderRadius: 8,
                border: '1px solid var(--border-light)',
                background: 'var(--surface)', color: 'var(--text-primary)',
                fontSize: 13, fontWeight: 500, cursor: 'pointer',
              }}>
              <Icon name="building" size={14} color="var(--text-secondary)"/>
              Continue with SSO
            </button>
          </div>

          {/* Divider */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ height: 1, flex: 1, background: 'var(--border-light)' }}/>
            <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>or</span>
            <span style={{ height: 1, flex: 1, background: 'var(--border-light)' }}/>
          </div>

          {/* Email + password */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <Field label="Work email">
              <input className="input" value={email} onChange={e => setEmail(e.target.value)}
                style={{ width: '100%', height: 38, fontSize: 13.5, padding: '0 12px' }}/>
            </Field>
            <Field label="Password" trailing={
              <a href="#" style={{ fontSize: 11.5, color: 'var(--brand-primary)', fontWeight: 500, textDecoration: 'none' }}>
                Forgot?
              </a>
            }>
              <div style={{ position: 'relative' }}>
                <input className="input" type="password" value={pw} onChange={e => setPw(e.target.value)}
                  style={{ width: '100%', height: 38, fontSize: 13.5, padding: '0 38px 0 12px' }}/>
                <span style={{
                  position: 'absolute', right: 10, top: '50%', transform: 'translateY(-50%)',
                  color: 'var(--text-tertiary)', cursor: 'pointer',
                }}>
                  <Icon name="eye" size={14}/>
                </span>
              </div>
            </Field>
          </div>

          {/* Remember + submit */}
          <label style={{
            display: 'flex', alignItems: 'center', gap: 8,
            fontSize: 12.5, color: 'var(--text-secondary)', cursor: 'pointer',
          }}>
            <input type="checkbox" defaultChecked style={{ accentColor: 'var(--brand-primary)' }}/>
            Keep me signed in for 30 days
          </label>

          <button className="btn btn-primary" style={{
            width: '100%', height: 42, fontSize: 14, fontWeight: 600,
            display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
          }}>
            Sign in
            <Icon name="arrowright" size={14}/>
          </button>

          {/* Footer */}
          <div style={{
            marginTop: 4, paddingTop: 16, borderTop: '1px solid var(--border-light)',
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            fontSize: 11, color: 'var(--text-tertiary)',
          }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <Icon name="shield" size={11}/>
              Protected by Aonik Trust
            </span>
            <span style={{ display: 'flex', gap: 14 }}>
              <a href="#" style={{ color: 'inherit', textDecoration: 'none' }}>Terms</a>
              <a href="#" style={{ color: 'inherit', textDecoration: 'none' }}>Privacy</a>
              <a href="#" style={{ color: 'inherit', textDecoration: 'none' }}>Status</a>
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}

function Field({ label, trailing, children }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <label style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-secondary)', letterSpacing: '0.02em' }}>
          {label}
        </label>
        {trailing}
      </div>
      {children}
    </div>
  );
}

function SsoButton({ provider, label }) {
  const logos = {
    google: (
      <svg width="14" height="14" viewBox="0 0 48 48" aria-hidden>
        <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3c-1.6 4.6-6 8-11.3 8-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.1 8 3l5.7-5.7C33.6 6.1 29 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.2-.1-2.4-.4-3.5z"/>
        <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.1 8 3l5.7-5.7C33.6 6.1 29 4 24 4 16.3 4 9.7 8.3 6.3 14.7z"/>
        <path fill="#4CAF50" d="M24 44c5 0 9.5-1.9 12.9-5l-6-4.9c-2 1.5-4.5 2.4-7 2.4-5.3 0-9.7-3.4-11.3-8l-6.5 5C9.5 39.8 16.2 44 24 44z"/>
        <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4-4.1 5.3l6 4.9C40.8 35 44 30 44 24c0-1.2-.1-2.4-.4-3.5z"/>
      </svg>
    ),
    microsoft: (
      <svg width="14" height="14" viewBox="0 0 24 24" aria-hidden>
        <rect x="1" y="1" width="10" height="10" fill="#F25022"/>
        <rect x="13" y="1" width="10" height="10" fill="#7FBA00"/>
        <rect x="1" y="13" width="10" height="10" fill="#00A4EF"/>
        <rect x="13" y="13" width="10" height="10" fill="#FFB900"/>
      </svg>
    ),
  };
  return (
    <button style={{
      display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
      width: '100%', height: 38, borderRadius: 8,
      border: '1px solid var(--border-light)',
      background: 'var(--surface)', color: 'var(--text-primary)',
      fontSize: 13, fontWeight: 500, cursor: 'pointer',
      transition: 'background 120ms ease',
    }}
      onMouseEnter={e => e.currentTarget.style.background = 'var(--surface-inset)'}
      onMouseLeave={e => e.currentTarget.style.background = 'var(--surface)'}>
      {logos[provider]}
      {label}
    </button>
  );
}

Object.assign(window, { ScreenLogin });
