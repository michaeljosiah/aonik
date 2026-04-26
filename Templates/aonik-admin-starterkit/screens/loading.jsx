// ─── First-load splash screen ───────────────────────────────────────────────
//
// Shown the first time the application is loaded. Centers the Aonik mark with
// a "tint rise" animation (loader #16 from the Aonik loaders artifact) and a
// boot-progress strip beneath the wordmark. Uses brand tokens only.

function ScreenLoading() {
  // Boot steps cycle to give the screen life on a static artboard.
  const STEPS = [
    'Authenticating',
    'Loading workspace',
    'Connecting agents',
    'Hydrating ledger',
    'Ready',
  ];
  const [step, setStep] = React.useState(2); // mid-boot looks alive in screenshots

  React.useEffect(() => {
    const id = setInterval(() => {
      setStep(s => (s + 1) % STEPS.length);
    }, 1400);
    return () => clearInterval(id);
  }, []);

  return (
    <div style={{
      width: '100%', height: '100%',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'var(--background)',
      position: 'relative', overflow: 'hidden',
    }}>
      {/* Subtle radial wash behind the mark */}
      <div style={{
        position: 'absolute', inset: 0,
        background: 'radial-gradient(60% 50% at 50% 45%, var(--brand-primary-10) 0%, transparent 70%)',
        pointerEvents: 'none',
      }}/>

      {/* Tiny attribution strip at the top of the screen — like the source product */}
      <div style={{
        position: 'absolute', top: 24, left: '50%', transform: 'translateX(-50%)',
        display: 'flex', alignItems: 'center', gap: 8,
        fontSize: 11, color: 'var(--text-tertiary)',
        fontFamily: 'var(--font-mono)', letterSpacing: '0.06em', textTransform: 'uppercase',
      }}>
        <span style={{ width: 5, height: 5, borderRadius: 999, background: 'var(--brand-secondary)', animation: 'liveDot 1.4s ease-in-out infinite' }}/>
        Aonik · Admin
      </div>

      {/* Centered stack: animated mark, wordmark, status text, progress bar */}
      <div style={{
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 28,
        zIndex: 1,
      }}>
        {/* Animated A-mark (tint rise) */}
        <AonikLoadingMark size={88}/>

        {/* Wordmark */}
        <div style={{
          fontFamily: 'var(--font-brand)', fontWeight: 700,
          fontSize: 32, letterSpacing: '-0.015em',
          color: 'var(--text-primary)',
        }}>
          aonik
        </div>

        {/* Status line */}
        <div style={{
          minHeight: 18, fontSize: 13,
          color: 'var(--text-secondary)',
          letterSpacing: '0.01em',
          display: 'flex', alignItems: 'center', gap: 8,
        }}>
          <span className="shimmer" style={{ fontSize: 13 }}>{STEPS[step]}</span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>
            {String(Math.round((step + 1) / STEPS.length * 100)).padStart(2, '0')}%
          </span>
        </div>

        {/* Boot progress strip */}
        <div style={{
          width: 220, height: 3, background: 'var(--border-light)',
          borderRadius: 999, overflow: 'hidden', position: 'relative',
        }}>
          <div style={{
            position: 'absolute', inset: 0,
            background: 'var(--brand-primary)',
            borderRadius: 999,
            animation: 'loaderSlide 1.6s cubic-bezier(.65,0,.35,1) infinite',
            width: '40%',
          }}/>
        </div>
      </div>

      {/* Footer attribution */}
      <div style={{
        position: 'absolute', bottom: 28, left: '50%', transform: 'translateX(-50%)',
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4,
      }}>
        <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>
          Agents propose · Systems apply
        </div>
        <div style={{ fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
          v 26.4.1 · Primrose Logistics
        </div>
      </div>

      {/* Animation keyframes scoped to this screen */}
      <style>{`
        @keyframes loaderSlide {
          0%   { left: -40%; }
          100% { left: 100%; }
        }
        @keyframes liveDot {
          0%, 100% { opacity: .35; }
          50%      { opacity: 1; }
        }
        @keyframes tintRise {
          0%, 100% { height: 0%; }
          45%, 55% { height: 100%; }
        }
        @keyframes dotPulse {
          0%, 100% { transform: scale(1);    box-shadow: 0 0 0 0 rgba(232,168,56,.6); }
          50%      { transform: scale(1.12); box-shadow: 0 0 0 8px rgba(232,168,56,0); }
        }
        .shimmer {
          background: linear-gradient(90deg, var(--text-secondary) 0%, var(--text-primary) 50%, var(--text-secondary) 100%);
          background-size: 200% 100%;
          -webkit-background-clip: text;
          background-clip: text;
          -webkit-text-fill-color: transparent;
          animation: shimmerSweep 1.8s linear infinite;
        }
        @keyframes shimmerSweep {
          0%   { background-position: 200% 0; }
          100% { background-position: -200% 0; }
        }
      `}</style>
    </div>
  );
}

// The Aonik mark with a "tint rise" fill — teal fills from bottom, A stays
// legible via mix-blend. Gold dot pulses in the corner.
function AonikLoadingMark({ size = 88 }) {
  const radius = Math.round(size * 0.25);
  const dotSize = Math.max(7, Math.round(size * 0.16));
  const dotInset = Math.max(4, Math.round(size * 0.09));

  return (
    <span style={{
      position: 'relative', display: 'inline-flex',
      alignItems: 'center', justifyContent: 'center',
      width: size, height: size,
      borderRadius: radius,
      background: 'var(--surface)',
      border: '1px solid var(--border-light)',
      boxShadow: '0 8px 24px -10px rgba(5,90,96,.25), 0 0 0 6px rgba(5,90,96,.04)',
      color: 'var(--text-tertiary)',
      fontFamily: 'var(--font-brand)',
      fontWeight: 700,
      fontSize: Math.round(size * 0.58),
      letterSpacing: '-0.04em',
      lineHeight: 1,
      overflow: 'hidden',
    }}>
      {/* Rising teal fill */}
      <span style={{
        position: 'absolute', left: 0, right: 0, bottom: 0,
        background: 'var(--brand-primary)',
        animation: 'tintRise 2.2s ease-in-out infinite',
        zIndex: 0,
      }}/>
      {/* The "A" — uses mix-blend so it inverts as the fill rises */}
      <span style={{
        position: 'relative', zIndex: 1,
        marginTop: -2,
        mixBlendMode: 'difference',
        color: '#fff',
      }}>A</span>
      {/* Gold dot */}
      <span style={{
        position: 'absolute',
        top: dotInset, right: dotInset,
        width: dotSize, height: dotSize,
        borderRadius: '50%',
        background: 'var(--brand-mark-dot)',
        zIndex: 2,
        animation: 'dotPulse 1.6s ease-in-out infinite',
      }}/>
    </span>
  );
}

Object.assign(window, { ScreenLoading });
