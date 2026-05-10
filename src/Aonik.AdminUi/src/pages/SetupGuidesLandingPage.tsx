import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type { SetupGuideDefinition, SetupGuideManifest } from '@/services/setupGuideService';
import { getSetupGuideManifest } from '@/services/setupGuideService';

interface LandingState {
  manifest: SetupGuideManifest | null;
  loading: boolean;
  error: string | null;
}

const initialState: LandingState = {
  manifest: null,
  loading: true,
  error: null,
};

export function SetupGuidesLandingPage() {
  const navigate = useNavigate();
  const [state, setState] = useState<LandingState>(initialState);
  const [initialLoad, setInitialLoad] = useState(true);

  useEffect(() => {
    let active = true;

    const loadManifest = async () => {
      setState((prev) => ({ ...prev, loading: true, error: null }));
      try {
        const manifest = await getSetupGuideManifest();
        if (!active) return;
        setState({ manifest, loading: false, error: null });
      } catch {
        if (!active) return;
        setState({ manifest: null, loading: false, error: 'Unable to load guides.' });
      } finally {
        if (active) setInitialLoad(false);
      }
    };

    loadManifest();

    return () => {
      active = false;
    };
  }, []);

  const sortedGuides = useMemo(() => {
    if (!state.manifest) return [] as SetupGuideDefinition[];
    return state.manifest.guides.slice().sort((a, b) => a.order - b.order);
  }, [state.manifest]);

  const featuredGuide = sortedGuides[0];
  const listGuides = sortedGuides.slice(1, 5);

  if (initialLoad) {
    return <PageLoadingScreen message="Loading guides" />;
  }

  const resolveCover = (guide?: SetupGuideDefinition) => {
    if (!guide?.cover) return undefined;
    if (guide.cover.startsWith('http') || guide.cover.startsWith('/')) return guide.cover;
    return `/content/setup-guides/${guide.slug}/${guide.cover}`;
  };

  return (
    <div className="flex-1 overflow-auto bg-[var(--color-surface-inset)]">
      <div className="mx-auto w-full max-w-[1680px] px-12 py-10">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--color-text-tertiary)]">Guides Home</p>
            <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">Setup Guides</h1>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm">Filter</Button>
            <Button size="sm">Create</Button>
          </div>
        </div>

        <div className="mt-6 rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-6 shadow-sm">
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">Latest Guides & Updates</h2>

          {state.loading ? (
            <div className="mt-6 flex items-center gap-3 text-sm text-[var(--color-text-secondary)]">
              <div className="h-5 w-5 border-2 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
              Loading guides...
            </div>
          ) : state.error ? (
            <p className="mt-4 text-sm text-[var(--color-error)]">{state.error}</p>
          ) : (
            <div className="mt-6 grid grid-cols-1 gap-8 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)]">
              <div className="space-y-4">
                {featuredGuide && (
                  <button
                    type="button"
                    onClick={() => navigate(`/setup-guides/${featuredGuide.slug}`)}
                    className="w-full text-left"
                  >
                    <div className="overflow-hidden rounded-2xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
                      <div
                        className={
                          resolveCover(featuredGuide)
                            ? 'h-56 bg-cover bg-center'
                            : `h-56 bg-gradient-to-br ${featuredGuide.accent ?? 'from-emerald-500/20 to-cyan-500/20'}`
                        }
                        style={resolveCover(featuredGuide) ? { backgroundImage: `url(${resolveCover(featuredGuide)})` } : undefined}
                      />
                      <div className="px-5 py-4">
                        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--color-text-tertiary)]">
                          {featuredGuide.category}
                        </p>
                        <h3 className="mt-2 text-base font-semibold text-[var(--color-text-primary)]">
                          {featuredGuide.title}
                        </h3>
                        <p className="mt-2 text-sm text-[var(--color-text-secondary)] leading-relaxed">
                          {featuredGuide.description}
                        </p>
                      </div>
                    </div>
                  </button>
                )}
              </div>

              <div className="space-y-5">
                {listGuides.map((guide) => (
                  <button
                    key={guide.id}
                    type="button"
                    onClick={() => navigate(`/setup-guides/${guide.slug}`)}
                    className="flex w-full items-start gap-4 text-left"
                  >
                    <div className="flex-1">
                      <h3 className="text-sm font-semibold text-[var(--color-text-primary)] leading-snug">
                        {guide.title}
                      </h3>
                      <p className="mt-2 text-sm text-[var(--color-text-secondary)] leading-relaxed">
                        {guide.description}
                      </p>
                      <div className="mt-3 inline-flex items-center gap-2 text-xs text-[var(--color-text-tertiary)]">
                        <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-1 font-semibold uppercase tracking-[0.2em]">
                          {guide.category}
                        </span>
                        <span>Guide</span>
                      </div>
                    </div>
                    <ArrowRight className="mt-1 h-4 w-4 text-[var(--color-text-tertiary)]" />
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
