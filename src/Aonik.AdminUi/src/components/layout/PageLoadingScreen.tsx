// Page-level loading screen — uses the same Aonik mark and animations
// as the startup LoadingScreen, but displayed inline on a page with
// customizable loading text (e.g., "Loading Dashboard...", "Loading Observability...").
//
// Perfect for full-page load states that need visual consistency with the
// app startup experience.

interface PageLoadingScreenProps {
  /** Custom loading message, e.g. "Loading Dashboard", "Loading Observability" */
  message?: string;
}

export function PageLoadingScreen({ message = 'Loading' }: PageLoadingScreenProps) {
  return (
    <div
      style={{
        width: '100%',
        height: '100%',
        minHeight: '100%',
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

      {/* Centred stack: mark + loading text */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 24,
          zIndex: 1,
        }}
      >
        <AonikLoadingMark size={72} />

        <div
          style={{
            fontSize: 13,
            color: 'var(--color-text-secondary)',
            letterSpacing: '0.01em',
            display: 'flex',
            alignItems: 'center',
            gap: 8,
          }}
        >
          <span className="text-shimmer">{message}</span>
        </div>
      </div>

      {/* Keyframe animations — must match LoadingScreen */}
      <style>{`
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

function AonikLoadingMark({ size = 72 }: { size?: number }) {
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
