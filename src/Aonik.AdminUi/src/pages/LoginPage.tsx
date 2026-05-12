// 1:1 port of Templates/aonik-admin-starterkit/screens/login.jsx, adapted to
// our redirect-based Auth0 flow.
//
//  Layout (Slack-style "single CTA" pattern):
//    LEFT  — brand wall: logo, tagline, single primary CTA ("Sign In to
//            Aonik") that hands off to Auth0 via login() and a footer link
//            for "Create a new workspace" (currently inert, follow-up).
//    RIGHT — live animated agent ↔ operator chat (LoginAgentChat) cycling
//            through three scenarios (Payments / Collections / PFM) that
//            illustrate the propose-apply-approve loop end-to-end.
//
//  Notes vs the template:
//    - No email / password / SSO picker on the page itself. Email selection,
//      SSO connection routing, and tenant resolution all happen *after*
//      auth via the Auth0 universal login + /host/me/tenants flow
//      (TenantResolutionGate + OrganizationPickerPage).
//    - returnTo / reason query params are still respected for session
//      expiry, missing tenant, and post-auth redirects.
//    - Uses --color-* tokens from src/index.css @theme; the template's
//      unprefixed --brand-mark-dot / --success names are not defined in
//      this app and are mapped to --color-brand-mark-dot / --color-success.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '@/auth';
import { LoadingScreen } from '@/components/layout';

const APP_VERSION = __APP_VERSION__;

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, isLoading, login, authError } = useAuth();

  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState(false);

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
    if (reason === 'tenant-missing') {
      setNotice('Your organization selection expired. Sign in again to continue.');
    }
  }, [reason]);

  useEffect(() => {
    // Don't auto-bounce if we landed here because of an expired session or
    // missing tenant context — that causes a redirect loop.
    if (reason === 'session-expired' || reason === 'tenant-missing') return;
    if (isAuthenticated && !isLoading) {
      navigate(from, { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, from, reason]);

  const initiateLogin = useCallback(async () => {
    setError(null);
    setIsLoggingIn(true);

    try {
      if (isAuthenticated && reason === 'tenant-missing') {
        // Already authenticated; just bounce back into the app and let
        // TenantResolutionGate re-resolve the tenant.
        navigate(from, { replace: true });
        return;
      }

      await login();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sign in. Please try again.');
      setIsLoggingIn(false);
    }
  }, [isAuthenticated, reason, navigate, from, login]);

  if (isLoading) {
    return <LoadingScreen phase="authenticating" />;
  }

  return (
    <div
      className="aonik-login-page"
      style={{
        width: '100%',
        minHeight: '100vh',
        position: 'relative',
        overflow: 'hidden',
        background: 'linear-gradient(135deg, #044045 0%, #055a60 50%, #066970 100%)',
        color: '#fff',
        fontFamily: 'var(--font-sans)',
      }}
    >
      {/* Decorative grid + warm halos */}
      <div
        aria-hidden
        style={{
          position: 'absolute',
          inset: 0,
          backgroundImage:
            'radial-gradient(circle at 22% 28%, rgba(232,168,56,0.14) 0%, transparent 38%),' +
            'radial-gradient(circle at 78% 72%, rgba(255,255,255,0.06) 0%, transparent 45%),' +
            'linear-gradient(rgba(255,255,255,0.025) 1px, transparent 1px),' +
            'linear-gradient(90deg, rgba(255,255,255,0.025) 1px, transparent 1px)',
          backgroundSize: 'auto, auto, 32px 32px, 32px 32px',
          pointerEvents: 'none',
        }}
      />

      <div
        className="aonik-login-grid"
        style={{
          position: 'relative',
          minHeight: '100vh',
          display: 'grid',
          gridTemplateColumns: '1fr 1fr',
          alignItems: 'center',
        }}
      >
        {/* ───────── LEFT — brand + sign-in ───────── */}
        <div
          className="aonik-login-left"
          style={{
            padding: '64px 0 64px 88px',
            display: 'flex',
            flexDirection: 'column',
            maxWidth: 580,
          }}
        >
          {/* Logo */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 56 }}>
            <span
              style={{
                position: 'relative',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: 44,
                height: 44,
                borderRadius: 11,
                background: '#fff',
                color: '#055a60',
                fontFamily: 'var(--font-brand)',
                fontWeight: 700,
                fontSize: 26,
                letterSpacing: '-0.04em',
                lineHeight: 1,
                boxShadow: '0 6px 22px -8px rgba(0,0,0,.35)',
              }}
            >
              A
              <span
                style={{
                  position: 'absolute',
                  top: 5,
                  right: 5,
                  width: 7,
                  height: 7,
                  borderRadius: '50%',
                  background: 'var(--color-brand-mark-dot)',
                }}
              />
            </span>
            <span
              style={{
                fontFamily: 'var(--font-brand)',
                fontWeight: 700,
                fontSize: 30,
                letterSpacing: '-0.015em',
                color: '#fff',
              }}
            >
              aonik
            </span>
          </div>

          {/* Tagline */}
          <h1
            style={{
              fontFamily: 'var(--font-brand)',
              fontWeight: 700,
              fontSize: 56,
              lineHeight: 1.05,
              letterSpacing: '-0.025em',
              margin: 0,
              marginBottom: 22,
              color: '#fff',
            }}
          >
            Agents propose.
            <br />
            Systems apply.
            <br />
            <span style={{ color: 'var(--color-brand-mark-dot)' }}>Everywhere you work.</span>
          </h1>

          <p
            style={{
              fontSize: 16,
              lineHeight: 1.55,
              color: 'rgba(255,255,255,0.78)',
              margin: 0,
              marginBottom: 40,
              maxWidth: 480,
            }}
          >
            AI-native financial intelligence platform powering the next generation of money
            products, from bill payments and cross-border collections to personal finance.
          </p>

          <Banners error={error} notice={notice} />

          {/* Primary CTA */}
          <button
            type="button"
            onClick={() => void initiateLogin()}
            disabled={isLoggingIn}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 10,
              width: 320,
              height: 56,
              border: 'none',
              borderRadius: 10,
              background: '#fff',
              color: '#055a60',
              fontFamily: 'var(--font-sans)',
              fontWeight: 600,
              fontSize: 17,
              letterSpacing: '-0.005em',
              cursor: isLoggingIn ? 'progress' : 'pointer',
              opacity: isLoggingIn ? 0.75 : 1,
              boxShadow:
                '0 12px 28px -10px rgba(0,0,0,0.35), inset 0 -2px 0 0 rgba(0,0,0,0.05)',
              transition: 'transform 120ms ease, box-shadow 120ms ease, opacity 120ms ease',
            }}
            onMouseEnter={(e) => {
              if (isLoggingIn) return;
              e.currentTarget.style.transform = 'translateY(-1px)';
              e.currentTarget.style.boxShadow =
                '0 16px 32px -10px rgba(0,0,0,0.4), inset 0 -2px 0 0 rgba(0,0,0,0.05)';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.transform = 'translateY(0)';
              e.currentTarget.style.boxShadow =
                '0 12px 28px -10px rgba(0,0,0,0.35), inset 0 -2px 0 0 rgba(0,0,0,0.05)';
            }}
          >
            {isLoggingIn ? 'Redirecting…' : 'Sign In to Aonik'}
          </button>

          <p
            style={{
              fontSize: 13,
              lineHeight: 1.5,
              color: 'rgba(255,255,255,0.6)',
              margin: 0,
              marginTop: 14,
              maxWidth: 320,
            }}
          >
            We'll take you to your web browser to sign in and then bring you back here.
          </p>

          {/* Footer — new workspace */}
          <div
            style={{
              marginTop: 88,
              fontSize: 14,
              color: 'rgba(255,255,255,0.75)',
            }}
          >
            Is your team new to Aonik?{' '}
            <a
              href="#"
              onClick={(e) => e.preventDefault()}
              style={{
                color: '#fff',
                fontWeight: 600,
                textDecoration: 'underline',
                textUnderlineOffset: 4,
                textDecorationColor: 'rgba(255,255,255,0.5)',
              }}
            >
              Create a new workspace
            </a>
          </div>
        </div>

        {/* ───────── RIGHT — animated agent ↔ operator chat ───────── */}
        <div
          className="aonik-login-right"
          style={{
            position: 'relative',
            minHeight: '100%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            paddingRight: 56,
          }}
        >
          <LoginAgentChat />
        </div>
      </div>

      {/* Bottom trust strip */}
      <div
        className="aonik-login-trust"
        style={{
          position: 'absolute',
          left: 88,
          right: 56,
          bottom: 28,
          display: 'flex',
          alignItems: 'center',
          gap: 24,
          fontSize: 11,
          color: 'rgba(255,255,255,0.45)',
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

      <style>{`
        @media (max-width: 1024px) {
          .aonik-login-grid { grid-template-columns: 1fr !important; }
          .aonik-login-right { display: none !important; }
          .aonik-login-left { padding: 56px 32px !important; max-width: 100% !important; align-items: flex-start; }
          .aonik-login-trust { left: 32px !important; right: 32px !important; flex-wrap: wrap; }
        }
      `}</style>
    </div>
  );
}

// ─── Dark-pane banners for error / notice ────────────────────────────────

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
          marginBottom: 20,
          maxWidth: 480,
          borderRadius: 10,
          background: 'rgba(210, 74, 44, 0.18)',
          border: '1px solid rgba(210, 74, 44, 0.55)',
          color: '#fdcdc0',
          fontSize: 13,
          lineHeight: 1.45,
        }}
      >
        <span aria-hidden style={{ marginTop: 1 }}>⚠</span>
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
        marginBottom: 20,
        maxWidth: 480,
        borderRadius: 10,
        background: 'rgba(232, 168, 56, 0.15)',
        border: '1px solid rgba(232, 168, 56, 0.45)',
        color: '#f6d99a',
        fontSize: 13,
        lineHeight: 1.45,
      }}
    >
      <span aria-hidden style={{ marginTop: 1 }}>•</span>
      <span>{notice}</span>
    </div>
  );
}

// ─── Scenario library ─────────────────────────────────────────────────────
// Three scenarios that map 1:1 to Aonik's product lines:
//   1. Payments        — bill payments (with inline Approve / Decline)
//   2. Collections     — cross-border inbound wires + FX settlement
//   3. Personal Finance — PFM intelligence (idle cash → savings sweep)
// Each scenario plays the same six-step state machine (typing → alert
// → decision → typing → execution → hold) before advancing.

type TagKey = 'proposal' | 'inbound' | 'insight' | 'alert' | 'risk' | 'executed';
type BadgeTone = 'neutral' | 'warning' | 'danger' | 'insight';

interface MessageActions {
  approve: string;
  decline: string;
  approvedAtStep?: number;
}

interface ScenarioMessage {
  id: string;
  from: 'agent' | 'operator';
  visibleAt: number;
  tag?: TagKey;
  text: string;
  actions?: MessageActions;
}

interface Scenario {
  id: string;
  team: string;
  badge: string;
  badgeTone: BadgeTone;
  hasButtons: boolean;
  operator: string;
  messages: ScenarioMessage[];
}

const SCENARIOS: Scenario[] = [
  {
    id: 'bills',
    team: 'Payments',
    badge: 'HUMAN-IN-LOOP',
    badgeTone: 'neutral',
    hasButtons: true,
    operator: 'J',
    messages: [
      {
        id: 'm1',
        from: 'agent',
        visibleAt: 1,
        tag: 'proposal',
        text: 'Vodafone Business bill · £4,820 due Friday. Pay from GBP operating account?',
        actions: { approve: 'Approve', decline: 'Decline', approvedAtStep: 2 },
      },
      // No operator reply bubble — the button click IS the reply
      {
        id: 'm3',
        from: 'agent',
        visibleAt: 4,
        tag: 'executed',
        text: 'Paid · £4,820 · ref AON-7421 · ledger #98203',
      },
    ],
  },
  {
    id: 'collections',
    team: 'Collections',
    badge: 'CROSS-BORDER',
    badgeTone: 'warning',
    hasButtons: false,
    operator: 'J',
    messages: [
      {
        id: 'm1',
        from: 'agent',
        visibleAt: 1,
        tag: 'inbound',
        text: 'Inbound wire · $24,500 from Acme Corp (US). FX to GBP at 1.262?',
      },
      { id: 'm2', from: 'operator', visibleAt: 2, text: 'Settle to GBP' },
      {
        id: 'm3',
        from: 'agent',
        visibleAt: 4,
        tag: 'executed',
        text: 'Settled · £19,415 credited · invoice INV-2041 reconciled',
      },
    ],
  },
  {
    id: 'pfm',
    team: 'Personal Finance',
    badge: 'INSIGHT',
    badgeTone: 'insight',
    hasButtons: false,
    operator: 'J',
    messages: [
      {
        id: 'm1',
        from: 'agent',
        visibleAt: 1,
        tag: 'insight',
        text: 'Idle £12,400 in current · earning 0.1% APY. Sweep to ISA at 5.1%?',
      },
      { id: 'm2', from: 'operator', visibleAt: 2, text: 'Sweep £10,000' },
      {
        id: 'm3',
        from: 'agent',
        visibleAt: 4,
        tag: 'executed',
        text: 'Swept · £10,000 → ISA · +£42/mo at 5.1% APY',
      },
    ],
  },
];

// Step durations (ms) within a single scenario.
//   0: opening agent-typing
//   1: agent alert shown (with action buttons if hasButtons)
//   2: operator decision (button → approved indicator, or new reply bubble)
//   3: agent-typing (executing)
//   4: agent confirmation shown (full thread visible)
//   5: hold full thread, then advance to next scenario
const STEP_DURATIONS: Record<'buttons' | 'textOnly', number[]> = {
  buttons: [700, 2900, 1200, 1100, 1700, 2400],
  textOnly: [600, 2300, 1100, 1100, 1500, 1900],
};

// Tag chip styles inside agent bubbles
const TAG_STYLES: Record<TagKey, { color: string; dot: string; label: string }> = {
  proposal: { color: '#7d5811', dot: '#e8a838', label: 'PROPOSAL' },
  inbound: { color: '#a85a0e', dot: '#f59f25', label: 'INBOUND WIRE' },
  insight: { color: '#1f6e7a', dot: '#3a9aa8', label: 'INSIGHT' },
  alert: { color: '#a85a0e', dot: '#f59f25', label: 'ALERT' },
  risk: { color: '#a8341a', dot: '#d24a2c', label: 'POLICY RISK' },
  executed: { color: '#2b7a31', dot: '#6abf6e', label: 'EXECUTED' },
};

// Header badge tone palette
const BADGE_TONES: Record<BadgeTone, { fill: string; border: string; color: string }> = {
  neutral: {
    fill: 'rgba(255,255,255,0.04)',
    border: 'rgba(255,255,255,0.14)',
    color: 'rgba(255,255,255,0.6)',
  },
  warning: {
    fill: 'rgba(232,168,56,0.16)',
    border: 'rgba(232,168,56,0.45)',
    color: '#f4cb7a',
  },
  danger: {
    fill: 'rgba(210,74,44,0.18)',
    border: 'rgba(210,74,44,0.50)',
    color: '#ee8d75',
  },
  insight: {
    fill: 'rgba(58,154,168,0.18)',
    border: 'rgba(58,154,168,0.55)',
    color: '#7fcfd9',
  },
};

// ─── Animated agent ↔ operator chat ──────────────────────────────────────
// Cycles through SCENARIOS; each scenario plays the same six-step state
// machine (typing → alert → decision → typing → confirmation → hold). The
// first scenario renders inline Approve / Decline buttons inside the
// agent's bubble; on the decision step the buttons collapse into a
// "✓ Approved by operator" line. Other scenarios use a quick operator
// chat reply for the decision step.
function LoginAgentChat() {
  const [scenarioIdx, setScenarioIdx] = useState(0);
  const [step, setStep] = useState(0);
  const scenario = SCENARIOS[scenarioIdx];

  useEffect(() => {
    const durations = scenario.hasButtons ? STEP_DURATIONS.buttons : STEP_DURATIONS.textOnly;
    const t = setTimeout(() => {
      if (step >= durations.length - 1) {
        setStep(0);
        setScenarioIdx((i) => (i + 1) % SCENARIOS.length);
      } else {
        setStep((s) => s + 1);
      }
    }, durations[step]);
    return () => clearTimeout(t);
  }, [step, scenarioIdx, scenario.hasButtons]);

  const visible = scenario.messages.filter((m) => step >= m.visibleAt);
  const showAgentTyping = step === 0 || step === 3;
  const badgeStyle = BADGE_TONES[scenario.badgeTone];

  return (
    <div style={{ position: 'relative', width: '100%', maxWidth: 460 }}>
      {/* Local keyframes */}
      <style>{`
        @keyframes aonikMsgIn {
          0%   { opacity: 0; transform: translateY(8px) scale(0.985); }
          100% { opacity: 1; transform: translateY(0)   scale(1); }
        }
        @keyframes aonikDot {
          0%, 80%, 100% { opacity: 0.30; transform: translateY(0); }
          40%           { opacity: 1;    transform: translateY(-3px); }
        }
        @keyframes aonikPulseRing {
          0%   { transform: scale(0.6); opacity: 0.9; }
          100% { transform: scale(2.2); opacity: 0;   }
        }
        @keyframes aonikApprovePop {
          0%   { transform: scale(0.3); opacity: 0; }
          60%  { transform: scale(1.18); opacity: 1; }
          100% { transform: scale(1);    opacity: 1; }
        }
      `}</style>

      {/* Soft warm halo behind the panel */}
      <div
        aria-hidden
        style={{
          position: 'absolute',
          inset: -36,
          background:
            'radial-gradient(circle at 28% 28%, rgba(232,168,56,0.18) 0%, transparent 55%),' +
            'radial-gradient(circle at 78% 82%, rgba(255,255,255,0.06) 0%, transparent 55%)',
          filter: 'blur(10px)',
          pointerEvents: 'none',
        }}
      />

      {/* Chat surface */}
      <div
        style={{
          position: 'relative',
          padding: '20px 22px 16px',
          borderRadius: 22,
          background:
            'linear-gradient(180deg, rgba(255,255,255,0.07) 0%, rgba(255,255,255,0.02) 100%)',
          border: '1px solid rgba(255,255,255,0.10)',
          boxShadow:
            '0 40px 80px -30px rgba(0,0,0,0.55), inset 0 1px 0 0 rgba(255,255,255,0.05)',
          backdropFilter: 'blur(10px)',
        }}
      >
        {/* ───── Header ───── */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 12,
            padding: '0 0 14px',
            borderBottom: '1px solid rgba(255,255,255,0.08)',
            marginBottom: 18,
          }}
        >
          {/* Agent mark */}
          <div
            style={{
              position: 'relative',
              width: 38,
              height: 38,
              borderRadius: 11,
              background: '#fff',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#055a60',
              fontFamily: 'var(--font-brand)',
              fontWeight: 700,
              fontSize: 22,
              letterSpacing: '-0.04em',
              lineHeight: 1,
              flexShrink: 0,
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
                background: '#e8a838',
              }}
            />
          </div>

          <div style={{ flex: 1, minWidth: 0 }}>
            <div
              key={scenario.id + '-title'}
              style={{
                fontSize: 14,
                fontWeight: 600,
                color: '#fff',
                letterSpacing: '-0.005em',
                animation: 'aonikMsgIn 280ms cubic-bezier(0.2, 0.8, 0.2, 1) both',
              }}
            >
              Aonik Agent · {scenario.team}
            </div>
            <div
              style={{
                fontSize: 10.5,
                color: 'rgba(255,255,255,0.6)',
                fontFamily: 'var(--font-mono)',
                letterSpacing: '0.05em',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                marginTop: 3,
              }}
            >
              {/* Live pulse */}
              <span
                style={{
                  position: 'relative',
                  display: 'inline-flex',
                  width: 8,
                  height: 8,
                }}
              >
                <span
                  style={{
                    position: 'absolute',
                    inset: 0,
                    borderRadius: 999,
                    background: '#6abf6e',
                    animation: 'aonikPulseRing 1.8s ease-out infinite',
                  }}
                />
                <span
                  style={{
                    position: 'absolute',
                    top: 1,
                    left: 1,
                    width: 6,
                    height: 6,
                    borderRadius: 999,
                    background: '#6abf6e',
                  }}
                />
              </span>
              LIVE · POLICY v3
            </div>
          </div>

          {/* Dynamic alert badge — colour shifts with scenario severity */}
          <div
            key={scenario.id + '-badge'}
            style={{
              fontSize: 10,
              color: badgeStyle.color,
              fontFamily: 'var(--font-mono)',
              letterSpacing: '0.06em',
              padding: '4px 9px',
              borderRadius: 999,
              border: `1px solid ${badgeStyle.border}`,
              background: badgeStyle.fill,
              fontWeight: 600,
              animation: 'aonikMsgIn 320ms cubic-bezier(0.2, 0.8, 0.2, 1) both',
            }}
          >
            {scenario.badge}
          </div>
        </div>

        {/* ───── Messages ───── */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 12,
            minHeight: 290,
          }}
        >
          {visible.map((m) => (
            <ChatBubble
              key={`${scenario.id}-${m.id}`}
              from={m.from}
              text={m.text}
              tag={m.tag}
              actions={m.actions}
              operator={scenario.operator}
              isApproved={!!m.actions && step >= (m.actions.approvedAtStep ?? Infinity)}
            />
          ))}
          {showAgentTyping && <ChatTyping key={`typing-${scenario.id}-${step}`} />}
        </div>

        {/* ───── Composer (decorative) ───── */}
        <div
          style={{
            marginTop: 14,
            paddingTop: 14,
            borderTop: '1px solid rgba(255,255,255,0.08)',
            display: 'flex',
            alignItems: 'center',
            gap: 10,
          }}
        >
          <div
            style={{
              flex: 1,
              height: 38,
              display: 'flex',
              alignItems: 'center',
              padding: '0 14px',
              borderRadius: 10,
              background: 'rgba(255,255,255,0.06)',
              border: '1px solid rgba(255,255,255,0.08)',
              fontSize: 13,
              color: 'rgba(255,255,255,0.42)',
              fontFamily: 'var(--font-sans)',
            }}
          >
            Reply to agent…
          </div>
          <button
            type="button"
            tabIndex={-1}
            aria-hidden
            style={{
              width: 38,
              height: 38,
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              borderRadius: 10,
              border: 'none',
              cursor: 'default',
              background: '#e8a838',
              color: '#3a2a05',
              fontSize: 16,
              fontWeight: 700,
              lineHeight: 1,
              boxShadow: '0 6px 14px -4px rgba(0,0,0,0.35)',
            }}
          >
            ↑
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── Single chat bubble (agent or operator) ──────────────────────────────
// Agent bubbles carry a `tag` chip (proposal / alert / risk / executed)
// and optional inline `actions`. When `actions` are present they render
// as Approve / Decline buttons; once `isApproved` flips true the row
// collapses into a "✓ Approved by operator" indicator.

interface ChatBubbleProps {
  from: 'agent' | 'operator';
  text: string;
  tag?: TagKey;
  actions?: MessageActions;
  isApproved: boolean;
  operator: string;
}

function ChatBubble({ from, text, tag, actions, isApproved, operator }: ChatBubbleProps) {
  const isAgent = from === 'agent';
  const tagStyle = tag ? TAG_STYLES[tag] : null;
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: isAgent ? 'row' : 'row-reverse',
        alignItems: 'flex-end',
        gap: 10,
        animation: 'aonikMsgIn 320ms cubic-bezier(0.2, 0.8, 0.2, 1) both',
      }}
    >
      {/* Avatar */}
      <div
        style={{
          position: 'relative',
          width: 26,
          height: 26,
          borderRadius: '50%',
          flexShrink: 0,
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontFamily: 'var(--font-brand)',
          fontWeight: 700,
          fontSize: 11,
          background: isAgent ? '#fff' : '#e8a838',
          color: isAgent ? '#055a60' : '#3a2a05',
          boxShadow: '0 2px 8px -2px rgba(0,0,0,0.45)',
        }}
      >
        {isAgent ? 'A' : operator || 'J'}
        {isAgent && (
          <span
            style={{
              position: 'absolute',
              bottom: -1,
              right: -1,
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: '#e8a838',
              border: '2px solid #055a60',
            }}
          />
        )}
      </div>

      {/* Bubble */}
      <div
        style={{
          maxWidth: 340,
          padding: actions ? '11px 14px 10px' : '10px 14px',
          borderRadius: 14,
          ...(isAgent
            ? {
                background: 'rgba(255,255,255,0.96)',
                color: '#0c2a2c',
                borderBottomLeftRadius: 4,
              }
            : {
                background: 'rgba(232,168,56,0.96)',
                color: '#3a2a05',
                borderBottomRightRadius: 4,
              }),
          fontSize: 13.5,
          lineHeight: 1.45,
          fontFamily: 'var(--font-sans)',
          boxShadow: '0 6px 18px -6px rgba(0,0,0,0.35)',
        }}
      >
        {tagStyle && (
          <div
            style={{
              fontSize: 9.5,
              fontFamily: 'var(--font-mono)',
              letterSpacing: '0.1em',
              textTransform: 'uppercase',
              marginBottom: 4,
              fontWeight: 600,
              color: tagStyle.color,
              display: 'flex',
              alignItems: 'center',
              gap: 5,
            }}
          >
            <span
              style={{
                width: 5,
                height: 5,
                borderRadius: 999,
                background: tagStyle.dot,
              }}
            />
            {tagStyle.label}
          </div>
        )}
        <div>{text}</div>

        {/* Inline action buttons — shown while a decision is pending */}
        {actions && !isApproved && (
          <div
            style={{
              marginTop: 11,
              paddingTop: 10,
              borderTop: '1px solid rgba(12,42,44,0.08)',
              display: 'flex',
              gap: 8,
            }}
          >
            <button
              type="button"
              tabIndex={-1}
              aria-hidden
              style={{
                flex: 1,
                padding: '8px 12px',
                borderRadius: 8,
                border: 'none',
                background: '#055a60',
                color: '#fff',
                fontFamily: 'var(--font-sans)',
                fontWeight: 600,
                fontSize: 12.5,
                letterSpacing: '-0.005em',
                cursor: 'default',
                boxShadow: '0 2px 6px -2px rgba(5,90,96,0.45)',
              }}
            >
              {actions.approve}
            </button>
            <button
              type="button"
              tabIndex={-1}
              aria-hidden
              style={{
                flex: 1,
                padding: '8px 12px',
                borderRadius: 8,
                border: '1px solid rgba(12,42,44,0.18)',
                background: 'transparent',
                color: '#0c2a2c',
                fontFamily: 'var(--font-sans)',
                fontWeight: 600,
                fontSize: 12.5,
                cursor: 'default',
              }}
            >
              {actions.decline}
            </button>
          </div>
        )}

        {/* Approved indicator — replaces the buttons once the decision is in */}
        {actions && isApproved && (
          <div
            style={{
              marginTop: 11,
              paddingTop: 10,
              borderTop: '1px solid rgba(12,42,44,0.08)',
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              fontSize: 11.5,
              color: '#2b7a31',
              fontWeight: 600,
              fontFamily: 'var(--font-sans)',
              letterSpacing: '-0.005em',
            }}
          >
            <span
              style={{
                width: 18,
                height: 18,
                borderRadius: '50%',
                background: '#6abf6e',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
                fontSize: 11,
                fontWeight: 800,
                animation: 'aonikApprovePop 480ms cubic-bezier(0.2, 0.8, 0.2, 1) both',
              }}
            >
              ✓
            </span>
            Approved by operator {operator || 'J'}
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Animated "agent is typing" indicator ────────────────────────────────
function ChatTyping() {
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'flex-end',
        gap: 10,
        animation: 'aonikMsgIn 260ms cubic-bezier(0.2, 0.8, 0.2, 1) both',
      }}
    >
      <div
        style={{
          position: 'relative',
          width: 26,
          height: 26,
          borderRadius: '50%',
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: '#fff',
          color: '#055a60',
          fontFamily: 'var(--font-brand)',
          fontWeight: 700,
          fontSize: 11,
          boxShadow: '0 2px 8px -2px rgba(0,0,0,0.45)',
          flexShrink: 0,
        }}
      >
        A
        <span
          style={{
            position: 'absolute',
            bottom: -1,
            right: -1,
            width: 8,
            height: 8,
            borderRadius: '50%',
            background: '#e8a838',
            border: '2px solid #055a60',
          }}
        />
      </div>
      <div
        style={{
          padding: '12px 16px',
          borderRadius: 14,
          borderBottomLeftRadius: 4,
          background: 'rgba(255,255,255,0.92)',
          display: 'inline-flex',
          alignItems: 'center',
          gap: 5,
          boxShadow: '0 6px 18px -6px rgba(0,0,0,0.35)',
        }}
      >
        {[0, 1, 2].map((i) => (
          <span
            key={i}
            style={{
              width: 6,
              height: 6,
              borderRadius: '50%',
              background: '#055a60',
              animation: `aonikDot 1.2s ease-in-out ${i * 0.18}s infinite`,
            }}
          />
        ))}
      </div>
    </div>
  );
}
