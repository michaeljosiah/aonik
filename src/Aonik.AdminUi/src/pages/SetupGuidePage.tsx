import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ReactMarkdown, { type Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { ArrowLeft, ExternalLink, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type { SetupGuideDefinition, SetupGuideManifest } from '@/services/setupGuideService';
import { getSetupGuideManifest, getSetupGuideMarkdown } from '@/services/setupGuideService';

interface GuideState {
  guide: SetupGuideDefinition | null;
  manifest: SetupGuideManifest | null;
  markdown: string;
  loading: boolean;
  error: string | null;
}

const initialState: GuideState = {
  guide: null,
  manifest: null,
  markdown: '',
  loading: true,
  error: null,
};

export function SetupGuidePage() {
  const { slug } = useParams();
  const navigate = useNavigate();
  const [state, setState] = useState<GuideState>(initialState);
  const [initialLoad, setInitialLoad] = useState(true);

  useEffect(() => {
    let active = true;

    const loadGuide = async () => {
      if (!slug) return;
      setState((prev) => ({ ...prev, loading: true, error: null }));

      try {
        const [manifest, markdown] = await Promise.all([
          getSetupGuideManifest(),
          getSetupGuideMarkdown(slug),
        ]);

        if (!active) return;
        const guide = manifest.guides.find((item) => item.slug === slug) ?? null;
        setState({ guide, manifest, markdown, loading: false, error: null });
      } catch (err) {
        if (!active) return;
        setState({ guide: null, manifest: null, markdown: '', loading: false, error: 'Unable to load guide content.' });
      } finally {
        if (active) setInitialLoad(false);
      }
    };

    loadGuide();

    return () => {
      active = false;
    };
  }, [slug]);

  const assetBasePath = slug ? `/content/setup-guides/${slug}/` : '/content/setup-guides/';

  const resolveAssetUrl = useCallback((value?: string) => {
    if (!value) return value;
    if (value.startsWith('http://') || value.startsWith('https://') || value.startsWith('/')) {
      return value;
    }
    return `${assetBasePath}${value}`;
  }, [assetBasePath]);

  const markdownComponents: Components = useMemo(() => ({
    h1: (props) => (
      <h1 className="text-2xl font-semibold text-[var(--color-text-primary)] mt-8 first:mt-0">
        {props.children}
      </h1>
    ),
    h2: (props) => (
      <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mt-6">
        {props.children}
      </h2>
    ),
    h3: (props) => (
      <h3 className="text-lg font-semibold text-[var(--color-text-primary)] mt-5">
        {props.children}
      </h3>
    ),
    p: (props) => (
      <p className="text-sm leading-relaxed text-[var(--color-text-secondary)] mt-3">
        {props.children}
      </p>
    ),
    ul: (props) => (
      <ul className="mt-3 space-y-2 text-sm text-[var(--color-text-secondary)] list-disc pl-5">
        {props.children}
      </ul>
    ),
    ol: (props) => (
      <ol className="mt-3 space-y-2 text-sm text-[var(--color-text-secondary)] list-decimal pl-5">
        {props.children}
      </ol>
    ),
    li: (props) => <li className="leading-relaxed">{props.children}</li>,
    a: (props) => (
      <a
        {...props}
        href={resolveAssetUrl(props.href)}
        className="text-[var(--color-brand-primary)] underline underline-offset-4"
        target={props.href?.startsWith('http') ? '_blank' : undefined}
        rel={props.href?.startsWith('http') ? 'noreferrer' : undefined}
      >
        {props.children}
      </a>
    ),
    img: (props) => (
      <img
        {...props}
        src={resolveAssetUrl(props.src)}
        alt={props.alt ?? ''}
        className="mt-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]"
      />
    ),
    blockquote: (props) => (
      <blockquote className="mt-4 border-l-4 border-[var(--color-border)] pl-4 text-sm text-[var(--color-text-secondary)]">
        {props.children}
      </blockquote>
    ),
    code: (props) => (
      <code className="rounded bg-[var(--color-surface-inset)] px-1.5 py-0.5 text-xs text-[var(--color-text-primary)]">
        {props.children}
      </code>
    ),
    pre: (props) => (
      <pre className="mt-4 overflow-x-auto rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4 text-xs text-[var(--color-text-primary)]">
        {props.children}
      </pre>
    ),
  }), [resolveAssetUrl]);

  const guideTitle = state.guide?.title ?? slug ?? 'Guide';
  const guideDescription = state.guide?.description;

  const categoryCounts = useMemo(() => {
    if (!state.manifest) return [] as { name: string; count: number }[];
    const tally = state.manifest.guides.reduce<Record<string, number>>((acc, guide) => {
      acc[guide.category] = (acc[guide.category] ?? 0) + 1;
      return acc;
    }, {});
    return Object.entries(tally)
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => b.count - a.count);
  }, [state.manifest]);

  const recentGuides = useMemo(() => {
    if (!state.manifest) return [] as SetupGuideDefinition[];
    return state.manifest.guides.slice().sort((a, b) => a.order - b.order).slice(0, 4);
  }, [state.manifest]);

  const guideCover = state.guide?.cover;
  const guideAccent = state.guide?.accent ?? 'from-slate-200/70 to-slate-100';
  const guideCoverUrl = guideCover
    ? guideCover.startsWith('http') || guideCover.startsWith('/')
      ? guideCover
      : `/content/setup-guides/${state.guide?.slug ?? ''}/${guideCover}`
    : undefined;

  if (initialLoad) {
    return <PageLoadingScreen message="Loading guide" />;
  }

  return (
    <div className="flex-1 overflow-auto bg-[var(--color-surface-inset)]">
      <div className="mx-auto w-full max-w-[1240px] px-8 py-8">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="space-y-1">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--color-text-tertiary)]">Guide</p>
            <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">{guideTitle}</h1>
          </div>
          <Button variant="ghost" size="sm" onClick={() => navigate('/setup-guides')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back to guides
          </Button>
        </div>

        <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
          <div className="space-y-6">
            <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm overflow-hidden">
              <div
                className={guideCoverUrl ? 'h-64 bg-cover bg-center' : `h-64 bg-gradient-to-br ${guideAccent}`}
                style={guideCoverUrl ? { backgroundImage: `url(${guideCoverUrl})` } : undefined}
              />
              <div className="px-6 py-5 border-b border-[var(--color-border-light)]">
                <div className="flex flex-wrap items-center justify-between gap-4">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-3 text-xs text-[var(--color-text-tertiary)]">
                      <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-1 font-semibold uppercase tracking-[0.2em]">
                        {state.guide?.category ?? 'Guide'}
                      </span>
                      <span>{state.guide?.title ? '5 mins read' : 'Guide'}</span>
                    </div>
                    {guideDescription && (
                      <p className="text-sm text-[var(--color-text-secondary)] leading-relaxed max-w-[42rem]">{guideDescription}</p>
                    )}
                  </div>
                  {slug && (
                    <a
                      href={`/content/setup-guides/${slug}/index.md`}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex items-center text-xs font-semibold text-[var(--color-brand-primary)] hover:underline"
                    >
                      View raw markdown
                      <ExternalLink className="ml-1.5 h-3.5 w-3.5" />
                    </a>
                  )}
                </div>
              </div>
              <div className="px-6 py-6">
                {state.loading ? (
                  <div className="flex items-center gap-3 text-sm text-[var(--color-text-secondary)]">
                    <div className="h-5 w-5 border-2 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
                    Loading guide...
                  </div>
                ) : state.error ? (
                  <p className="text-sm text-[var(--color-error)]">{state.error}</p>
                ) : (
                  <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownComponents}>
                    {state.markdown}
                  </ReactMarkdown>
                )}
              </div>
            </div>
          </div>

          <aside className="space-y-6">
            <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-4 shadow-sm">
              <p className="text-sm font-semibold text-[var(--color-text-primary)]">Search guides</p>
              <div className="mt-3 flex items-center gap-2 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2 text-sm text-[var(--color-text-tertiary)]">
                <Search className="h-4 w-4" />
                <span>Search</span>
              </div>
            </div>

            <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-4 shadow-sm">
              <p className="text-sm font-semibold text-[var(--color-text-primary)]">Categories</p>
              <div className="mt-3 space-y-2 text-sm text-[var(--color-text-secondary)]">
                {categoryCounts.map((category) => (
                  <div key={category.name} className="flex items-center justify-between">
                    <span>{category.name}</span>
                    <span className="text-[var(--color-text-tertiary)]">{category.count}</span>
                  </div>
                ))}
              </div>
            </div>

            <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-4 shadow-sm">
              <p className="text-sm font-semibold text-[var(--color-text-primary)]">Recent guides</p>
              <div className="mt-4 space-y-3">
                {recentGuides.map((guide) => (
                  <button
                    key={guide.id}
                    type="button"
                    onClick={() => navigate(`/setup-guides/${guide.slug}`)}
                    className="flex w-full items-start gap-3 text-left"
                  >
                    <div
                      className={
                        guide.cover
                          ? 'h-12 w-12 rounded-lg bg-cover bg-center'
                          : `h-12 w-12 rounded-lg bg-gradient-to-br ${guide.accent ?? 'from-slate-200/70 to-slate-100'}`
                      }
                      style={
                        guide.cover
                          ? {
                            backgroundImage: `url(${guide.cover.startsWith('http') || guide.cover.startsWith('/')
                              ? guide.cover
                              : `/content/setup-guides/${guide.slug}/${guide.cover}`})`,
                          }
                          : undefined
                      }
                    />
                    <div>
                      <p className="text-xs font-semibold text-[var(--color-text-primary)] leading-snug">{guide.title}</p>
                      <p className="text-xs text-[var(--color-text-tertiary)]">{guide.category}</p>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </aside>
        </div>
      </div>
    </div>
  );
}
