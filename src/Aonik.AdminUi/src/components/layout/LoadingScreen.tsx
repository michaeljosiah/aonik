// First-load splash screen — 1:1 port of
// Templates/aonik-admin-starterkit/screens/loading.jsx, adapted to be driven
// by a real `phase` prop instead of a cosmetic timer.
//
// Centres the Aonik mark with a "tint rise" animation, a wordmark, a status
// line, and a boot-progress strip. Reads brand tokens only.

export type LoadingPhase =
  | 'authenticating'
  | 'loading-workspace'
  | 'connecting-agents'
  | 'hydrating-ledger'
  | 'ready';

interface LoadingScreenProps {
  phase?: LoadingPhase;
}

const PHASE_ORDER: LoadingPhase[] = [
  'authenticating',
  'loading-workspace',
  'connecting-agents',
  'hydrating-ledger',
  'ready',
];

const PHASE_LABEL: Record<LoadingPhase, string> = {
  'authenticating': 'Authenticating',
  'loading-workspace': 'Loading workspace',
  'connecting-agents': 'Connecting agents',
  'hydrating-ledger': 'Hydrating ledger',
  'ready': 'Ready',
};

export function LoadingScreen({ phase = 'loading-workspace' }: LoadingScreenProps) {
  const phaseIndex = PHASE_ORDER.indexOf(phase);
  const percent = Math.round(((phaseIndex + 1) / PHASE_ORDER.length) * 100);
  const percentLabel = String(percent).padStart(2, '0');

  return (
    <div
      style={{
        width: '100%',
        height: '100%',
        // In Electron the renderer area is `100vh - titlebar`; use the CSS
        // var (0 on web, 32px in the desktop build) so the loader fills the
        // available viewport without being clipped at the bottom.
        minHeight: 'calc(100vh - var(--app-titlebar-height, 0px))',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'var(--color-background)',
        position: 'relative',
        overflow: 'hidden',
      }}
    >
      {/* Subtle radial wash behind the mark */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          background:
            'radial-gradient(60% 50% at 50% 45%, var(--color-brand-primary-10) 0%, transparent 70%)',
          pointerEvents: 'none',
        }}
      />

      {/* Top attribution strip */}
      <div
        style={{
          position: 'absolute',
          top: 24,
          left: '50%',
          transform: 'translateX(-50%)',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          fontSize: 11,
          color: 'var(--color-text-tertiary)',
          fontFamily: 'var(--font-mono)',
          letterSpacing: '0.06em',
          textTransform: 'uppercase',
        }}
      >
        <span
          style={{
            width: 5,
            height: 5,
            borderRadius: 999,
            background: 'var(--color-brand-secondary)',
            animation: 'aonikLoadingLiveDot 1.4s ease-in-out infinite',
          }}
        />
        Aonik · Admin
      </div>

      {/* Centred stack: mark, wordmark, status, progress */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 28,
          zIndex: 1,
        }}
      >
        <AonikLoadingMark size={88} />

        <div
          style={{
            fontFamily: 'var(--font-brand)',
            fontWeight: 700,
            fontSize: 32,
            letterSpacing: '-0.015em',
            color: 'var(--color-text-primary)',
            lineHeight: 1,
          }}
        >
          aonik
        </div>

        <div
          style={{
            minHeight: 18,
            fontSize: 13,
            color: 'var(--color-text-secondary)',
            letterSpacing: '0.01em',
            display: 'flex',
            alignItems: 'center',
            gap: 8,
          }}
        >
          <span className="text-shimmer" style={{ fontSize: 13 }}>
            {PHASE_LABEL[phase]}
          </span>
          <span
            style={{
              fontFamily: 'var(--font-mono)',
              fontVariantNumeric: 'tabular-nums',
              fontSize: 11,
              color: 'var(--color-text-tertiary)',
            }}
          >
            {percentLabel}%
          </span>
        </div>

        <div
          style={{
            width: 220,
            height: 3,
            background: 'var(--color-border-light)',
            borderRadius: 999,
            overflow: 'hidden',
            position: 'relative',
          }}
        >
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              background: 'var(--color-brand-primary)',
              borderRadius: 999,
              animation: 'aonikLoadingSlide 1.6s cubic-bezier(.65,0,.35,1) infinite',
              width: '40%',
            }}
          />
        </div>
      </div>

      {/* Footer */}
      <div
        style={{
          position: 'absolute',
          bottom: 28,
          left: '50%',
          transform: 'translateX(-50%)',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 4,
        }}
      >
        <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)' }}>
          Agents propose · Systems apply
        </div>
        <div
          style={{
            fontSize: 10,
            color: 'var(--color-text-tertiary)',
            fontFamily: 'var(--font-mono)',
            fontVariantNumeric: 'tabular-nums',
          }}
        >
          v {__APP_VERSION__}
        </div>
      </div>

      {/* Reduced-motion handling: .text-shimmer (used on the status line)
          self-disables in index.css. The remaining keyframes are subtle and
          do not translate; we leave them on for a clearer "still booting"
          signal even when motion is reduced. */}
      <style>{`
        @keyframes aonikLoadingSlide {
          0%   { left: -40%; }
          100% { left: 100%; }
        }
        @keyframes aonikLoadingLiveDot {
          0%, 100% { opacity: .35; }
          50%      { opacity: 1; }
        }
        @keyframes aonikLoadingTintRise {
          0%, 100% { height: 0%; }
          45%, 55% { height: 100%; }
        }
        @keyframes aonikLoadingDotPulse {
          0%, 100% { transform: scale(1);    box-shadow: 0 0 0 0 rgba(232,168,56,.6); }
          50%      { transform: scale(1.12); box-shadow: 0 0 0 8px rgba(232,168,56,0); }
        }
      `}</style>
    </div>
  );
}

function AonikLoadingMark({ size = 88 }: { size?: number }) {
  const radius = Math.round(size * 0.25);
  const dotSize = Math.max(7, Math.round(size * 0.16));
  const dotInset = Math.max(4, Math.round(size * 0.09));

  return (
    <span
      style={{
        position: 'relative',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: size,
        height: size,
        borderRadius: radius,
        background: 'var(--color-surface)',
        border: '1px solid var(--color-border-light)',
        boxShadow:
          '0 8px 24px -10px rgba(5,90,96,.25), 0 0 0 6px rgba(5,90,96,.04)',
        color: 'var(--color-text-tertiary)',
        fontFamily: 'var(--font-brand)',
        fontWeight: 700,
        fontSize: Math.round(size * 0.58),
        letterSpacing: '-0.04em',
        lineHeight: 1,
        overflow: 'hidden',
      }}
    >
      <span
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          background: 'var(--color-brand-primary)',
          animation: 'aonikLoadingTintRise 2.2s ease-in-out infinite',
          zIndex: 0,
        }}
      />
      <span
        style={{
          position: 'relative',
          zIndex: 1,
          marginTop: -2,
          mixBlendMode: 'difference',
          color: '#fff',
        }}
      >
        A
      </span>
      <span
        style={{
          position: 'absolute',
          top: dotInset,
          right: dotInset,
          width: dotSize,
          height: dotSize,
          borderRadius: '50%',
          background: 'var(--color-brand-mark-dot)',
          zIndex: 2,
          animation: 'aonikLoadingDotPulse 1.6s ease-in-out infinite',
        }}
      />
    </span>
  );
}
