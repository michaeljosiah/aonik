// 1:1 port of Templates/aonik-admin-starterkit/screens/login.jsx, adapted to
// our redirect-based Auth0 flow:
//   - email-only field (no password) — forwarded to the IdP as login_hint
//   - tenant org rendered as an inline pill above the subtitle: clickable
//     selector on apex domains, static text on tenant subdomains
//   - SSO buttons render in template style but currently call the generic
//     login() (Auth0 picker). Per-connection wiring is a follow-up.
//
// Tokens: uses --color-* names from src/index.css @theme; the template's
// unprefixed names are not defined in this app.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { AlertCircle, ArrowRight, Building2, Check, ChevronDown, Info, ShieldCheck } from 'lucide-react';
import { useAuth } from '@/auth';
import { tenantService } from '@/services/tenantService';
import type { TenantListItemForLogin } from '@/types';
import { getSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import { isTenantScopedHostname } from '@/lib/tenantRouting';
import { LoadingScreen } from '@/components/layout';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';

const APP_VERSION = __APP_VERSION__;

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, isLoading, login, authError } = useAuth();

  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  const [tenants, setTenants] = useState<TenantListItemForLogin[]>([]);
  const [selectedTenantId, setSelectedTenantId] = useState<string>('');
  const [isLoadingTenants, setIsLoadingTenants] = useState(false);
  const [showTenantSelector, setShowTenantSelector] = useState(false);
  const [tenantPickerOpen, setTenantPickerOpen] = useState(false);

  useEffect(() => {
    setShowTenantSelector(!isTenantScopedHostname(window.location.hostname));
  }, []);

  useEffect(() => {
    if (!showTenantSelector) return;

    const loadTenants = async () => {
      setIsLoadingTenants(true);
      try {
        const response = await tenantService.listForLogin();
        setTenants(response.tenants);

        const previous = getSelectedTenant();
        if (previous?.tenantId && response.tenants.some(t => t.tenantId === previous.tenantId)) {
          setSelectedTenantId(previous.tenantId);
          return;
        }
        if (response.tenants.length > 0) {
          setSelectedTenantId(response.tenants[0].tenantId);
        }
      } catch (err) {
        // Don't block sign-in if the public list endpoint fails — user can
        // still proceed via Auth0 and we'll resolve their tenant post-redirect.
        console.error('Failed to load tenants:', err);
      } finally {
        setIsLoadingTenants(false);
      }
    };

    loadTenants();
  }, [showTenantSelector]);

  useEffect(() => {
    if (authError) {
      setError(authError.message || 'Authentication failed. Please try again.');
      setIsLoggingIn(false);
    }
  }, [authError]);

  const query = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const reason = query.get('reason');
  const returnTo = query.get('returnTo');
  const from =
    (returnTo && returnTo.startsWith('/') && !returnTo.startsWith('/login') ? returnTo : null) ??
    (location.state as { from?: { pathname: string } } | null)?.from?.pathname ??
    '/';

  useEffect(() => {
    if (!reason) return;
    if (reason === 'session-expired') setNotice('Your session expired. Please sign in again.');
    if (reason === 'tenant-missing') setNotice('Select an organization to continue.');
  }, [reason]);

  useEffect(() => {
    // Don't auto-bounce if we landed here because of an expired session or
    // missing tenant context — that causes a redirect loop.
    if (reason === 'session-expired' || reason === 'tenant-missing') return;
    if (isAuthenticated && !isLoading) {
      navigate(from, { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, from, reason]);

  const selectedTenant = tenants.find(t => t.tenantId === selectedTenantId);

  const persistTenantSelection = useCallback(() => {
    if (!selectedTenantId) return;
    setSelectedTenant({
      tenantId: selectedTenantId,
      name: selectedTenant?.name,
      subdomain: selectedTenant?.subdomain,
      environment: selectedTenant?.environment,
    });
  }, [selectedTenantId, selectedTenant?.name, selectedTenant?.subdomain, selectedTenant?.environment]);

  const initiateLogin = useCallback(
    async (loginHint?: string) => {
      if (showTenantSelector && !selectedTenantId && tenants.length > 0) {
        setError('Please select an organization to continue.');
        return;
      }

      setError(null);
      setIsLoggingIn(true);

      try {
        persistTenantSelection();

        if (isAuthenticated && reason === 'tenant-missing') {
          // Already authenticated; just bounce back into the app with the
          // freshly-selected tenant context.
          navigate(from, { replace: true });
          return;
        }

        await login(loginHint ? { loginHint } : undefined);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to sign in. Please try again.');
        setIsLoggingIn(false);
      }
    },
    [
      showTenantSelector,
      selectedTenantId,
      tenants.length,
      persistTenantSelection,
      isAuthenticated,
      reason,
      navigate,
      from,
      login,
    ],
  );

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    void initiateLogin(email.trim() || undefined);
  };

  if (isLoading) {
    return <LoadingScreen phase="authenticating" />;
  }

  return (
    <div
      className="aonik-login-grid"
      style={{
        width: '100%',
        minHeight: '100vh',
        display: 'grid',
        gridTemplateColumns: '1.05fr 1fr',
        background: 'var(--color-background)',
        fontFamily: 'var(--font-sans)',
      }}
    >
      <BrandPane />

      <section
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: '48px 56px',
          position: 'relative',
        }}
      >
        <div
          className="aonik-login-helper"
          style={{
            position: 'absolute',
            top: 28,
            right: 32,
            fontSize: 12.5,
            color: 'var(--color-text-secondary)',
          }}
        >
          Don't have an account?{' '}
          <a
            href="#"
            onClick={(e) => e.preventDefault()}
            style={{ color: 'var(--color-brand-primary)', fontWeight: 600, textDecoration: 'none' }}
          >
            Request access
          </a>
        </div>

        <form
          onSubmit={handleSubmit}
          style={{ width: '100%', maxWidth: 380, display: 'flex', flexDirection: 'column', gap: 20 }}
        >
          <header>
            <div
              style={{
                fontSize: 11,
                fontWeight: 600,
                letterSpacing: '0.1em',
                textTransform: 'uppercase',
                color: 'var(--color-brand-primary)',
                marginBottom: 10,
              }}
            >
              Sign in
            </div>
            <h2
              style={{
                fontFamily: 'var(--font-brand)',
                fontWeight: 700,
                fontSize: 28,
                letterSpacing: '-0.015em',
                margin: 0,
                color: 'var(--color-text-primary)',
              }}
            >
              Welcome back
            </h2>

            <TenantLine
              showSelector={showTenantSelector}
              tenants={tenants}
              selectedTenantId={selectedTenantId}
              onSelect={setSelectedTenantId}
              isLoadingTenants={isLoadingTenants}
              open={tenantPickerOpen}
              onOpenChange={setTenantPickerOpen}
              selectedTenant={selectedTenant ?? null}
            />
          </header>

          <Banners error={error} notice={notice} />

          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <SsoButton provider="google" label="Continue with Google" onClick={() => initiateLogin()} disabled={isLoggingIn} />
            <SsoButton provider="microsoft" label="Continue with Microsoft" onClick={() => initiateLogin()} disabled={isLoggingIn} />
            <button
              type="button"
              onClick={() => initiateLogin()}
              disabled={isLoggingIn}
              style={ssoButtonStyle}
              onMouseEnter={(e) => { e.currentTarget.style.background = 'var(--color-surface-inset)'; }}
              onMouseLeave={(e) => { e.currentTarget.style.background = 'var(--color-surface)'; }}
            >
              <Building2 size={14} color="var(--color-text-secondary)" />
              Continue with SSO
            </button>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ height: 1, flex: 1, background: 'var(--color-border-light)' }} />
            <span
              style={{
                fontSize: 10.5,
                color: 'var(--color-text-tertiary)',
                fontFamily: 'var(--font-mono)',
                letterSpacing: '0.08em',
                textTransform: 'uppercase',
              }}
            >
              or
            </span>
            <span style={{ height: 1, flex: 1, background: 'var(--color-border-light)' }} />
          </div>

          <Field label="Work email">
            <input
              className="aonik-input"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@company.com"
              style={{
                width: '100%',
                height: 38,
                fontSize: 13.5,
                padding: '0 12px',
                borderRadius: 8,
                border: '1px solid var(--color-form-field-border)',
                background: 'var(--color-form-field-bg)',
                color: 'var(--color-form-field-text)',
                outline: 'none',
                transition: 'border-color 150ms ease, box-shadow 150ms ease',
              }}
            />
          </Field>

          <label
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              fontSize: 12.5,
              color: 'var(--color-text-secondary)',
              cursor: 'pointer',
              userSelect: 'none',
            }}
          >
            {/* Visual only for v1; Auth0 session lifetime is server-configured. */}
            <input type="checkbox" defaultChecked style={{ accentColor: 'var(--color-brand-primary)' }} />
            Keep me signed in for 30 days
          </label>

          <button
            type="submit"
            disabled={isLoggingIn || (showTenantSelector && isLoadingTenants)}
            style={{
              width: '100%',
              height: 42,
              fontSize: 14,
              fontWeight: 600,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
              borderRadius: 8,
              border: 'none',
              background: 'var(--color-brand-primary)',
              color: '#fff',
              cursor: isLoggingIn ? 'progress' : 'pointer',
              opacity: isLoggingIn || (showTenantSelector && isLoadingTenants) ? 0.7 : 1,
              transition: 'opacity 120ms ease, transform 80ms ease',
            }}
            onMouseDown={(e) => { e.currentTarget.style.transform = 'translateY(1px)'; }}
            onMouseUp={(e) => { e.currentTarget.style.transform = 'translateY(0)'; }}
            onMouseLeave={(e) => { e.currentTarget.style.transform = 'translateY(0)'; }}
          >
            {isLoggingIn ? 'Redirecting' : 'Sign in'}
            <ArrowRight size={14} />
          </button>

          <footer
            style={{
              marginTop: 4,
              paddingTop: 16,
              borderTop: '1px solid var(--color-border-light)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              fontSize: 11,
              color: 'var(--color-text-tertiary)',
            }}
          >
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <ShieldCheck size={11} />
              Protected by Aonik Trust
            </span>
            <span style={{ display: 'flex', gap: 14 }}>
              <a href="#" onClick={(e) => e.preventDefault()} style={{ color: 'inherit', textDecoration: 'none' }}>Terms</a>
              <a href="#" onClick={(e) => e.preventDefault()} style={{ color: 'inherit', textDecoration: 'none' }}>Privacy</a>
              <a href="#" onClick={(e) => e.preventDefault()} style={{ color: 'inherit', textDecoration: 'none' }}>Status</a>
            </span>
          </footer>
        </form>
      </section>

      <style>{`
        .aonik-input:focus {
          border-color: var(--color-brand-primary) !important;
          box-shadow: var(--shadow-focus);
        }
        @media (max-width: 1024px) {
          .aonik-login-grid { grid-template-columns: 1fr !important; }
          .aonik-login-brand { display: none !important; }
          .aonik-login-helper { position: static !important; margin-bottom: 16px; text-align: center; }
        }
      `}</style>
    </div>
  );
}

// ─── Brand pane ───────────────────────────────────────────────────────────

function BrandPane() {
  return (
    <aside
      className="aonik-login-brand"
      style={{
        position: 'relative',
        background: 'linear-gradient(155deg, #04494e 0%, #055a60 45%, #0a6e72 100%)',
        color: '#fff',
        padding: '40px 56px',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        overflow: 'hidden',
      }}
    >
      <div
        style={{
          position: 'absolute',
          inset: 0,
          backgroundImage:
            'radial-gradient(circle at 30% 30%, rgba(232,168,56,0.18) 0%, transparent 45%), linear-gradient(rgba(255,255,255,0.04) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.04) 1px, transparent 1px)',
          backgroundSize: 'auto, 28px 28px, 28px 28px',
          pointerEvents: 'none',
        }}
      />

      <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 12 }}>
        <span
          style={{
            position: 'relative',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 36,
            height: 36,
            borderRadius: 9,
            background: '#fff',
            color: '#055a60',
            fontFamily: 'var(--font-brand)',
            fontWeight: 700,
            fontSize: 22,
            letterSpacing: '-0.04em',
            lineHeight: 1,
            boxShadow: '0 4px 16px -4px rgba(0,0,0,.3)',
          }}
        >
          A
          <span
            style={{
              position: 'absolute',
              top: 4,
              right: 4,
              width: 6,
              height: 6,
              borderRadius: '50%',
              background: 'var(--color-brand-mark-dot)',
            }}
          />
        </span>
        <span
          style={{
            fontFamily: 'var(--font-brand)',
            fontWeight: 700,
            fontSize: 22,
            letterSpacing: '-0.015em',
          }}
        >
          aonik
        </span>
      </div>

      <div style={{ position: 'relative', display: 'flex', flexDirection: 'column', gap: 32, maxWidth: 520 }}>
        <div>
          <div
            style={{
              fontSize: 11,
              fontWeight: 600,
              letterSpacing: '0.12em',
              textTransform: 'uppercase',
              color: 'rgba(255,255,255,0.6)',
              marginBottom: 14,
              display: 'flex',
              alignItems: 'center',
              gap: 8,
            }}
          >
            <span style={{ width: 18, height: 1, background: 'rgba(255,255,255,0.4)' }} />
            Admin · Operator console
          </div>
          <h1
            style={{
              fontFamily: 'var(--font-brand)',
              fontWeight: 700,
              fontSize: 44,
              lineHeight: 1.08,
              letterSpacing: '-0.02em',
              margin: 0,
              color: '#fff',
            }}
          >
            Agents propose.<br />
            <span style={{ color: 'var(--color-brand-mark-dot)' }}>Systems apply.</span>
          </h1>
          <p
            style={{
              fontSize: 15,
              lineHeight: 1.55,
              color: 'rgba(255,255,255,0.78)',
              marginTop: 16,
              maxWidth: 460,
            }}
          >
            The control plane for your operations team — orders, ledger, payouts and AI agents working together under policy.
          </p>
        </div>

        <ProposalPreviewCard />
      </div>

      <div
        style={{
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          gap: 24,
          fontSize: 11,
          color: 'rgba(255,255,255,0.55)',
          fontFamily: 'var(--font-mono)',
          letterSpacing: '0.05em',
        }}
      >
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          <span
            style={{
              width: 6,
              height: 6,
              borderRadius: 999,
              background: 'var(--color-success)',
            }}
          />
          All systems operational
        </span>
        <span>SOC 2 · Type II</span>
        <span>PCI DSS</span>
        {APP_VERSION && <span style={{ marginLeft: 'auto' }}>v {APP_VERSION}</span>}
      </div>
    </aside>
  );
}

function ProposalPreviewCard() {
  return (
    <div
      style={{
        background: 'rgba(255,255,255,0.06)',
        backdropFilter: 'blur(12px)',
        border: '1px solid rgba(255,255,255,0.14)',
        borderRadius: 12,
        padding: 16,
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        boxShadow: '0 20px 50px -20px rgba(0,0,0,0.4)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <span
          style={{
            width: 26,
            height: 26,
            borderRadius: '50%',
            background: 'var(--color-brand-mark-dot)',
            color: '#3a2a05',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 12,
            fontWeight: 700,
          }}
        >
          B
        </span>
        <span style={{ fontSize: 12.5, fontWeight: 600 }}>Billing Agent</span>
        <span
          style={{
            fontSize: 10,
            fontFamily: 'var(--font-mono)',
            letterSpacing: '0.05em',
            color: 'rgba(255,255,255,0.55)',
            marginLeft: 'auto',
          }}
        >
          2s ago
        </span>
      </div>
      <div style={{ fontSize: 13.5, lineHeight: 1.5 }}>
        I matched <b>3 invoices</b> to last week's bank transactions — £42,180 total. Drafting journal entries.
      </div>
      <div
        style={{
          display: 'flex',
          gap: 6,
          flexWrap: 'wrap',
          fontFamily: 'var(--font-mono)',
          fontSize: 10.5,
        }}
      >
        {['search_invoices', 'list_bank_transactions', 'match_invoice_to_txn'].map((t) => (
          <span
            key={t}
            style={{
              padding: '3px 7px',
              borderRadius: 4,
              background: 'rgba(255,255,255,0.08)',
              color: 'rgba(255,255,255,0.85)',
              border: '1px solid rgba(255,255,255,0.1)',
            }}
          >
            {t}
          </span>
        ))}
      </div>
    </div>
  );
}

// ─── Right-pane helpers ───────────────────────────────────────────────────

interface TenantLineProps {
  showSelector: boolean;
  tenants: TenantListItemForLogin[];
  selectedTenantId: string;
  onSelect: (tenantId: string) => void;
  isLoadingTenants: boolean;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  selectedTenant: TenantListItemForLogin | null;
}

function TenantLine({
  showSelector,
  tenants,
  selectedTenantId,
  onSelect,
  isLoadingTenants,
  open,
  onOpenChange,
  selectedTenant,
}: TenantLineProps) {
  const subtitleStyle: React.CSSProperties = {
    fontSize: 13.5,
    color: 'var(--color-text-secondary)',
    marginTop: 6,
    lineHeight: 1.5,
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: 4,
  };

  // Tenant subdomain — render the selected tenant statically.
  if (!showSelector) {
    if (!selectedTenant) {
      return (
        <p style={{ ...subtitleStyle, color: 'var(--color-text-secondary)' }}>
          Continue to your workspace
        </p>
      );
    }
    return (
      <p style={subtitleStyle}>
        <span>Continue to&nbsp;</span>
        <b style={{ color: 'var(--color-text-primary)' }}>{selectedTenant.name}</b>
        {selectedTenant.environment && (
          <>
            <span>&nbsp;·&nbsp;</span>
            <span>{selectedTenant.environment}</span>
          </>
        )}
      </p>
    );
  }

  // Apex domain — pill is interactive.
  if (isLoadingTenants && tenants.length === 0) {
    return <p style={{ ...subtitleStyle, color: 'var(--color-text-tertiary)' }}>Loading organizations…</p>;
  }

  if (tenants.length === 0) {
    return <p style={{ ...subtitleStyle, color: 'var(--color-text-tertiary)' }}>No organizations available</p>;
  }

  return (
    <p style={subtitleStyle}>
      <span>Continue to&nbsp;</span>
      <Popover open={open} onOpenChange={onOpenChange}>
        <PopoverTrigger asChild>
          <button
            type="button"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
              padding: '3px 10px',
              borderRadius: 999,
              border: '1px solid var(--color-border)',
              background: 'var(--color-surface-inset)',
              color: 'var(--color-text-primary)',
              fontSize: 13,
              fontWeight: 600,
              cursor: 'pointer',
              transition: 'background 120ms ease, border-color 120ms ease',
            }}
            onMouseEnter={(e) => { e.currentTarget.style.borderColor = 'var(--color-brand-primary-20)'; }}
            onMouseLeave={(e) => { e.currentTarget.style.borderColor = 'var(--color-border)'; }}
          >
            <Building2 size={12} />
            {selectedTenant?.name ?? 'Select organization'}
            <ChevronDown size={12} style={{ opacity: 0.6 }} />
          </button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-72 p-2">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            {tenants.map((t) => {
              const isSelected = t.tenantId === selectedTenantId;
              return (
                <button
                  key={t.tenantId}
                  type="button"
                  onClick={() => {
                    onSelect(t.tenantId);
                    onOpenChange(false);
                  }}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: 8,
                    padding: '8px 10px',
                    borderRadius: 6,
                    border: 'none',
                    background: isSelected ? 'var(--color-brand-primary-10)' : 'transparent',
                    color: 'var(--color-text-primary)',
                    fontSize: 13,
                    fontWeight: 500,
                    textAlign: 'left',
                    cursor: 'pointer',
                  }}
                  onMouseEnter={(e) => {
                    if (!isSelected) e.currentTarget.style.background = 'var(--color-surface-inset)';
                  }}
                  onMouseLeave={(e) => {
                    if (!isSelected) e.currentTarget.style.background = 'transparent';
                  }}
                >
                  <span style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <span>{t.name}</span>
                    <span style={{ fontSize: 11, color: 'var(--color-text-tertiary)' }}>
                      {t.environment}
                    </span>
                  </span>
                  {isSelected && <Check size={14} color="var(--color-brand-primary)" />}
                </button>
              );
            })}
          </div>
        </PopoverContent>
      </Popover>
      {selectedTenant?.environment && (
        <>
          <span>&nbsp;·&nbsp;</span>
          <span>{selectedTenant.environment}</span>
        </>
      )}
    </p>
  );
}

const ssoButtonStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 10,
  width: '100%',
  height: 38,
  borderRadius: 8,
  border: '1px solid var(--color-border-light)',
  background: 'var(--color-surface)',
  color: 'var(--color-text-primary)',
  fontSize: 13,
  fontWeight: 500,
  cursor: 'pointer',
  transition: 'background 120ms ease',
};

interface SsoButtonProps {
  provider: 'google' | 'microsoft';
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

function SsoButton({ provider, label, onClick, disabled }: SsoButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      style={{ ...ssoButtonStyle, opacity: disabled ? 0.6 : 1 }}
      onMouseEnter={(e) => { if (!disabled) e.currentTarget.style.background = 'var(--color-surface-inset)'; }}
      onMouseLeave={(e) => { e.currentTarget.style.background = 'var(--color-surface)'; }}
    >
      {provider === 'google' ? <GoogleLogo /> : <MicrosoftLogo />}
      {label}
    </button>
  );
}

function GoogleLogo() {
  return (
    <svg width="14" height="14" viewBox="0 0 48 48" aria-hidden>
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3c-1.6 4.6-6 8-11.3 8-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.1 8 3l5.7-5.7C33.6 6.1 29 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.2-.1-2.4-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.1 8 3l5.7-5.7C33.6 6.1 29 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5 0 9.5-1.9 12.9-5l-6-4.9c-2 1.5-4.5 2.4-7 2.4-5.3 0-9.7-3.4-11.3-8l-6.5 5C9.5 39.8 16.2 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4-4.1 5.3l6 4.9C40.8 35 44 30 44 24c0-1.2-.1-2.4-.4-3.5z" />
    </svg>
  );
}

function MicrosoftLogo() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" aria-hidden>
      <rect x="1" y="1" width="10" height="10" fill="#F25022" />
      <rect x="13" y="1" width="10" height="10" fill="#7FBA00" />
      <rect x="1" y="13" width="10" height="10" fill="#00A4EF" />
      <rect x="13" y="13" width="10" height="10" fill="#FFB900" />
    </svg>
  );
}

interface FieldProps {
  label: string;
  trailing?: React.ReactNode;
  children: React.ReactNode;
}

function Field({ label, trailing, children }: FieldProps) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <label
          style={{
            fontSize: 11.5,
            fontWeight: 600,
            color: 'var(--color-text-secondary)',
            letterSpacing: '0.02em',
          }}
        >
          {label}
        </label>
        {trailing}
      </div>
      {children}
    </div>
  );
}

interface BannersProps {
  error: string | null;
  notice: string | null;
}

function Banners({ error, notice }: BannersProps) {
  if (!error && !notice) return null;

  if (error) {
    return (
      <div
        role="alert"
        style={{
          display: 'flex',
          alignItems: 'flex-start',
          gap: 8,
          padding: '10px 12px',
          borderRadius: 8,
          background: 'var(--color-error-light)',
          border: '1px solid var(--color-error)',
          color: 'var(--color-error)',
          fontSize: 12.5,
          lineHeight: 1.45,
        }}
      >
        <AlertCircle size={14} style={{ flexShrink: 0, marginTop: 1 }} />
        <span>{error}</span>
      </div>
    );
  }

  return (
    <div
      role="status"
      style={{
        display: 'flex',
        alignItems: 'flex-start',
        gap: 8,
        padding: '10px 12px',
        borderRadius: 8,
        background: 'var(--color-brand-primary-10)',
        border: '1px solid var(--color-brand-primary-20)',
        color: 'var(--color-brand-primary)',
        fontSize: 12.5,
        lineHeight: 1.45,
      }}
    >
      <Info size={14} style={{ flexShrink: 0, marginTop: 1 }} />
      <span>{notice}</span>
    </div>
  );
}
