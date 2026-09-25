// First-load splash screen — adapted from
// Templates/aonik-admin-starterkit/screens/loading.jsx and driven by a real
// `phase` prop instead of a cosmetic timer.
//
// Centres the Aonik mark with a "tint rise" animation, a wordmark, a status
// line, and a boot-progress strip. Reads theme tokens only; every animation
// stops under prefers-reduced-motion.

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

/** Radial primary wash behind the mark (PageLoadingScreen uses the same). */
const LOADING_WASH =
  'radial-gradient(60% 50% at 50% 45%, color-mix(in oklab, var(--primary) 12%, transparent) 0%, transparent 70%)';

export function LoadingScreen({ phase = 'loading-workspace' }: LoadingScreenProps) {
  const phaseIndex = PHASE_ORDER.indexOf(phase);
  const percent = Math.round(((phaseIndex + 1) / PHASE_ORDER.length) * 100);
  const percentLabel = String(percent).padStart(2, '0');

  return (
    <div
      // In Electron the renderer area is `100vh - titlebar`; use the CSS var
      // (0 on web, 32px in the desktop build) so the loader fills the
      // available viewport without being clipped at the bottom.
      className="relative flex size-full min-h-[calc(100vh-var(--app-titlebar-height,0px))] items-center justify-center overflow-hidden bg-background"
    >
      {/* Subtle radial wash behind the mark */}
      <div className="pointer-events-none absolute inset-0" style={{ background: LOADING_WASH }} />

      {/* Top attribution strip */}
      <div className="absolute top-6 left-1/2 flex -translate-x-1/2 items-center gap-2 font-mono text-[11px] text-muted-foreground">
        <span
          className="aonik-loading-anim size-[5px] rounded-full bg-agent"
          style={{ animation: 'aonikLoadingLiveDot 1.4s ease-in-out infinite' }}
        />
        Aonik · Admin
      </div>

      {/* Centred stack: mark, wordmark, status, progress */}
      <div className="relative flex flex-col items-center gap-7">
        <AonikLoadingMark size={88} />

        <div className="font-brand text-[32px] leading-none font-bold tracking-tight text-foreground">
          aonik
        </div>

        <div className="flex min-h-[18px] items-center gap-2 text-[13px] text-muted-foreground">
          <span className="text-shimmer">{PHASE_LABEL[phase]}</span>
          <span className="font-mono text-[11px] tabular-nums">{percentLabel}%</span>
        </div>

        <div className="relative h-[3px] w-[220px] overflow-hidden rounded-full bg-border">
          <div
            className="aonik-loading-anim absolute inset-y-0 w-2/5 rounded-full bg-primary"
            style={{ animation: 'aonikLoadingSlide 1.6s cubic-bezier(.65,0,.35,1) infinite' }}
          />
        </div>
      </div>

      {/* Footer */}
      <div className="absolute bottom-7 left-1/2 flex -translate-x-1/2 flex-col items-center gap-1">
        <div className="text-[11px] text-muted-foreground">Agents propose · Systems apply</div>
        <div className="font-mono text-[10px] tabular-nums text-muted-foreground">v {__APP_VERSION__}</div>
      </div>

      <style>{`
        @keyframes aonikLoadingSlide {
          0%   { left: -40%; }
          100% { left: 100%; }
        }
        @keyframes aonikLoadingLiveDot {
          0%, 100% { opacity: .35; }
          50%      { opacity: 1; }
        }
      `}</style>
    </div>
  );
}

/**
 * The Aonik "A" mark with a primary tint that rises and falls behind it. The
 * letter is drawn twice: muted on the card, and in primary-foreground inside
 * the tint (clipped by the tint's height), so it reads in both themes.
 */
export function AonikLoadingMark({ size = 88 }: { size?: number }) {
  const radius = Math.round(size * 0.25);
  const dotSize = Math.max(7, Math.round(size * 0.16));
  const dotInset = Math.max(4, Math.round(size * 0.09));
  const innerSize = size - 2; // inside the 1px border
  const letterStyle = {
    fontSize: Math.round(size * 0.58),
    letterSpacing: '-0.04em',
    marginTop: -2,
  };

  return (
    <span
      className="relative inline-flex items-center justify-center overflow-hidden border bg-card font-brand leading-none font-bold text-muted-foreground"
      style={{
        width: size,
        height: size,
        borderRadius: radius,
        boxShadow:
          '0 8px 24px -10px color-mix(in oklab, var(--primary) 25%, transparent), 0 0 0 6px color-mix(in oklab, var(--primary) 4%, transparent)',
      }}
    >
      <span className="relative" style={letterStyle}>
        A
      </span>
      <span
        aria-hidden
        className="aonik-loading-anim absolute inset-x-0 bottom-0 overflow-hidden bg-primary"
        style={{ animation: 'aonikLoadingTintRise 2.2s ease-in-out infinite' }}
      >
        <span
          className="absolute inset-x-0 bottom-0 flex items-center justify-center text-primary-foreground"
          style={{ height: innerSize }}
        >
          <span style={letterStyle}>A</span>
        </span>
      </span>
      <span
        className="aonik-loading-anim absolute rounded-full"
        style={{
          top: dotInset,
          right: dotInset,
          width: dotSize,
          height: dotSize,
          background: 'var(--color-brand-mark-dot)',
          animation: 'aonikLoadingDotPulse 1.6s ease-in-out infinite',
        }}
      />
      <style>{`
        @keyframes aonikLoadingTintRise {
          0%, 100% { height: 0%; }
          45%, 55% { height: 100%; }
        }
        @keyframes aonikLoadingDotPulse {
          0%, 100% { transform: scale(1);    box-shadow: 0 0 0 0 color-mix(in oklab, var(--color-brand-mark-dot) 60%, transparent); }
          50%      { transform: scale(1.12); box-shadow: 0 0 0 8px color-mix(in oklab, var(--color-brand-mark-dot) 0%, transparent); }
        }
        @media (prefers-reduced-motion: reduce) {
          .aonik-loading-anim { animation: none !important; }
        }
      `}</style>
    </span>
  );
}
