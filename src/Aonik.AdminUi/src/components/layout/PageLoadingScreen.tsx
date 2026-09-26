// Page-level loading screen — uses the same Aonik mark and animations
// as the startup LoadingScreen, but displayed inline on a page with
// customizable loading text (e.g., "Loading Dashboard...", "Loading Observability...").
//
// Perfect for full-page load states that need visual consistency with the
// app startup experience. Theme tokens only; motion stops under
// prefers-reduced-motion (handled by AonikLoadingMark and .text-shimmer).

import { AonikLoadingMark } from './LoadingScreen';

interface PageLoadingScreenProps {
  /** Custom loading message, e.g. "Loading Dashboard", "Loading Observability" */
  message?: string;
}

export function PageLoadingScreen({ message = 'Loading' }: PageLoadingScreenProps) {
  return (
    <div className="relative flex size-full min-h-full items-center justify-center overflow-hidden bg-background">
      {/* Subtle radial wash behind the mark */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            'radial-gradient(60% 50% at 50% 45%, color-mix(in oklab, var(--primary) 12%, transparent) 0%, transparent 70%)',
        }}
      />

      {/* Centred stack: mark + loading text */}
      <div className="relative flex flex-col items-center gap-6">
        <AonikLoadingMark size={72} />

        <div className="flex items-center gap-2 text-[13px] text-muted-foreground">
          <span className="text-shimmer">{message}</span>
        </div>
      </div>
    </div>
  );
}
