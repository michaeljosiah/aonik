import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, Loader2, Mic, Plug, Plus, RefreshCw, Speaker } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Sheet, SheetContent } from '@/components/ui/sheet';
import { Switch } from '@/components/ui/switch';
import { cn } from '@/lib/utils';
import {
  speechProviderLibraryService,
  speechVendorsCatalogService,
} from '@/services/speechProviderLibraryService';
import { voiceRecipeLibraryService } from '@/services/voiceRecipeLibraryService';
import type {
  SpeechProvider,
  SpeechProviderType,
  SpeechVendorDescriptor,
} from '@/types/speechLibrary';

import { PageHeader, StatTile } from './_primitives';
import { ProviderCard } from './ProviderCard';
import { ProviderEditPanel } from './ProviderEditPanel';

type Filter = 'All' | SpeechProviderType;

const FILTERS: { id: Filter; label: string }[] = [
  { id: 'All', label: 'All' },
  { id: 'Stt', label: 'Speech-to-Text' },
  { id: 'Tts', label: 'Text-to-Speech' },
  { id: 'Composite', label: 'Realtime Voice' },
];

/**
 * Providers tab — KPI summary strip + filter pills + 2-column grid of rich
 * provider cards. Built-in archetypes are read-only (clone to edit); tenant rows
 * support edit + disable. Layout matches `Templates/aonik-admin-starterkit/screens/speech.jsx`.
 */
export function ProvidersTab() {
  const [providers, setProviders] = useState<SpeechProvider[]>([]);
  const [vendors, setVendors] = useState<SpeechVendorDescriptor[]>([]);
  const [usageMap, setUsageMap] = useState<Map<string, number>>(new Map());
  const [filter, setFilter] = useState<Filter>('All');
  const [includeDisabled, setIncludeDisabled] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Edit panel state. Either `editing` is set (existing provider — edit or clone) or
  // `creating` is true (fresh row — defaultType seeds the initial form).
  const [editing, setEditing] = useState<SpeechProvider | null>(null);
  const [creating, setCreating] = useState<{ defaultType: SpeechProviderType } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      // Pull recipes alongside providers so we can compute usage counts in one round-trip and
      // avoid an N+1 to /usage. Built-in recipes count toward usage too.
      const [list, catalog, recipes] = await Promise.all([
        speechProviderLibraryService.list({ includeDisabled }),
        speechVendorsCatalogService.get(),
        voiceRecipeLibraryService.list({ includeDisabled: false }),
      ]);
      setProviders(list);
      setVendors(catalog.vendors);

      const counts = new Map<string, number>();
      for (const r of recipes) {
        const refs = collectProviderRefs(r);
        for (const id of refs) counts.set(id, (counts.get(id) ?? 0) + 1);
      }
      setUsageMap(counts);
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error(err);
      setError('Failed to load speech library.');
    } finally {
      setLoading(false);
    }
  }, [includeDisabled]);

  useEffect(() => {
    void load();
  }, [load]);

  const visible = useMemo(() => {
    if (filter === 'All') return providers;
    return providers.filter((p) => p.type === filter);
  }, [filter, providers]);

  const stats = useMemo(() => {
    const isActive = (p: SpeechProvider) => p.status === 'Active';
    return {
      total: providers.length,
      active: providers.filter(isActive).length,
      stt: providers.filter((p) => p.type === 'Stt' && isActive(p)).length,
      tts: providers.filter((p) => p.type === 'Tts' && isActive(p)).length,
      composite: providers.filter((p) => p.type === 'Composite' && isActive(p)).length,
      sttTotal: providers.filter((p) => p.type === 'Stt').length,
      ttsTotal: providers.filter((p) => p.type === 'Tts').length,
      compositeTotal: providers.filter((p) => p.type === 'Composite').length,
    };
  }, [providers]);

  const handleDisable = async (provider: SpeechProvider) => {
    try {
      await speechProviderLibraryService.setStatus(provider.id, { status: 'Disabled' });
      toast.success(`${provider.displayName} disabled.`);
      void load();
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'Failed to disable provider.';
      toast.error(message);
    }
  };

  const handleSaved = (saved: SpeechProvider) => {
    setEditing(null);
    setCreating(null);
    void load();
    void saved; // we just refresh from the server
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center p-12 text-[var(--color-text-secondary)]">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading speech library…
      </div>
    );
  }
  if (error) {
    return (
      <Card>
        <CardContent className="p-6 text-[var(--color-error)]">{error}</CardContent>
      </Card>
    );
  }

  // The edit panel renders inside a slide-out Sheet (matches the starter kit "Add bank
  // account" pattern in `Templates/aonik-admin-starterkit/screens/forms.jsx`). The list
  // stays visible behind it so the user keeps context.
  const sheetOpen = editing !== null || creating !== null;
  const closeSheet = () => {
    setEditing(null);
    setCreating(null);
  };

  const tenantHasNoProviders = providers.length === 0;

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Speech & Voice"
        title="Providers"
        subtitle="Speech services available to this workspace. Activate a provider here before referencing it from a recipe."
        actions={
          <>
            <Button variant="outline" size="sm" onClick={() => void load()}>
              <RefreshCw className="h-3.5 w-3.5" /> Refresh
            </Button>
            <Button
              size="sm"
              onClick={() => setCreating({ defaultType: filter !== 'All' ? filter : 'Tts' })}
            >
              <Plus className="h-3.5 w-3.5" /> Add provider
            </Button>
          </>
        }
      />

      {tenantHasNoProviders ? (
        <FirstProviderHero
          onAdd={(t) => setCreating({ defaultType: t })}
        />
      ) : (
        <>
          {/* KPI strip */}
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            <StatTile label="Active providers" value={stats.active} total={stats.total} icon={CheckCircle2} />
            <StatTile label="Speech-to-Text" value={stats.stt} total={stats.sttTotal} icon={Mic} />
            <StatTile label="Text-to-Speech" value={stats.tts} total={stats.ttsTotal} icon={Speaker} />
            <StatTile label="Realtime Voice" value={stats.composite} total={stats.compositeTotal} icon={Plug} />
          </div>

          {/* Filter pills + show-disabled toggle */}
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap gap-1.5">
              {FILTERS.map((f) => {
                const count =
                  f.id === 'All'
                    ? providers.length
                    : providers.filter((p) => p.type === f.id).length;
                const active = filter === f.id;
                return (
                  <button
                    key={f.id}
                    type="button"
                    onClick={() => setFilter(f.id)}
                    className={cn(
                      'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs transition-colors',
                      active
                        ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)] text-white'
                        : 'border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] hover:border-[var(--color-brand-primary)]',
                    )}
                  >
                    {f.label}
                    <span
                      className={cn(
                        'font-mono text-[11px]',
                        active ? 'text-white/85' : 'text-[var(--color-text-tertiary)]',
                      )}
                    >
                      {count}
                    </span>
                  </button>
                );
              })}
            </div>

            <div className="flex items-center gap-2">
              <Switch
                id="include-disabled"
                checked={includeDisabled}
                onCheckedChange={setIncludeDisabled}
              />
              <label htmlFor="include-disabled" className="text-xs text-[var(--color-text-secondary)]">
                Show disabled
              </label>
            </div>
          </div>

          {/* 2-column rich grid */}
          {visible.length === 0 ? (
            <FilterEmptyState
              onAdd={() => setCreating({ defaultType: filter !== 'All' ? (filter as SpeechProviderType) : 'Tts' })}
            />
          ) : (
            <div className="grid gap-3 lg:grid-cols-2">
              {visible.map((p) => (
                <ProviderCard
                  key={p.id}
                  provider={p}
                  usageCount={usageMap.get(p.id) ?? 0}
                  onEdit={() => setEditing(p)}
                  onTest={() => setEditing(p)}
                  onDisable={() => void handleDisable(p)}
                />
              ))}
            </div>
          )}
        </>
      )}

      {/* Slide-out edit panel */}
      <Sheet open={sheetOpen} onOpenChange={(open) => !open && closeSheet()}>
        <SheetContent size="md" className="sm:max-w-none">
          {sheetOpen && (
            <ProviderEditPanel
              initial={editing}
              defaultType={editing?.type ?? creating?.defaultType ?? 'Tts'}
              vendors={vendors}
              onSaved={handleSaved}
              onCancel={closeSheet}
            />
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
}

/**
 * Hero shown the very first time a tenant opens the Providers tab — no providers exist yet,
 * so the page leads with a single, opinionated CTA: pick a type, click Add. Each tile seeds
 * the Add panel with the chosen type so the user lands in the right form on the first try.
 */
function FirstProviderHero({ onAdd }: { onAdd: (type: SpeechProviderType) => void }) {
  return (
    <div className="rounded-2xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-12 text-center">
      <div className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-2xl bg-[var(--color-brand-primary-10)]">
        <Plug className="h-6 w-6 text-[var(--color-brand-primary)]" />
      </div>
      <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
        Add your first provider
      </h2>
      <p className="mx-auto mt-1 max-w-md text-sm text-[var(--color-text-secondary)]">
        Providers are vendor instances (an OpenAI Whisper config, an ElevenLabs voice, an Azure
        Voice Live region…). Compose them into recipes that drive Voice mode and Chat speech.
      </p>

      <div className="mx-auto mt-6 grid max-w-2xl grid-cols-1 gap-3 sm:grid-cols-3">
        <HeroChoice
          icon={<Mic className="h-5 w-5" />}
          title="Speech-to-Text"
          description="Whisper, Azure, etc."
          onClick={() => onAdd('Stt')}
        />
        <HeroChoice
          icon={<Speaker className="h-5 w-5" />}
          title="Text-to-Speech"
          description="OpenAI, ElevenLabs, Mistral, Azure"
          onClick={() => onAdd('Tts')}
        />
        <HeroChoice
          icon={<Plug className="h-5 w-5" />}
          title="Realtime Voice"
          description="OpenAI Realtime, Azure Voice Live"
          onClick={() => onAdd('Composite')}
        />
      </div>
    </div>
  );
}

function HeroChoice({
  icon,
  title,
  description,
  onClick,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="group flex flex-col items-center gap-2 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-5 text-center transition-colors hover:border-[var(--color-brand-primary)] hover:bg-[var(--color-brand-primary-10)]"
    >
      <span className="grid h-10 w-10 place-items-center rounded-lg bg-[var(--color-surface)] text-[var(--color-brand-primary)] transition-colors group-hover:bg-[var(--color-brand-primary)] group-hover:text-white">
        {icon}
      </span>
      <span className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</span>
      <span className="text-[11.5px] text-[var(--color-text-tertiary)]">{description}</span>
    </button>
  );
}

/** Shown when a filter (e.g. "Realtime Voice") matches zero rows but other types exist. */
function FilterEmptyState({ onAdd }: { onAdd: () => void }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 p-12 text-center">
        <p className="text-sm text-[var(--color-text-secondary)]">
          No providers in this category yet.
        </p>
        <Button onClick={onAdd}>
          <Plus className="h-4 w-4" />
          Add provider
        </Button>
      </CardContent>
    </Card>
  );
}

// ─── Helpers ─────────────────────────────────────────────────────────────

import type { VoiceRecipe } from '@/types/voiceRecipes';

function collectProviderRefs(recipe: VoiceRecipe): string[] {
  const refs: string[] = [];
  if (recipe.chained) {
    refs.push(recipe.chained.sttProviderId, recipe.chained.ttsProviderId);
  }
  if (recipe.composite) {
    refs.push(recipe.composite.compositeProviderId);
  }
  return refs;
}
