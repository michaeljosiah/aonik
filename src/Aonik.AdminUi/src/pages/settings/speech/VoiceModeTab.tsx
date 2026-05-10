import { useEffect, useMemo, useState } from 'react';
import {
  Layers,
  Loader2,
  Mic,
  MicOff,
  Radio,
  Save,
} from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { speechProviderLibraryService } from '@/services/speechProviderLibraryService';
import { voiceModeSettingsService } from '@/services/speechActiveSettingsService';
import { voiceRecipeLibraryService } from '@/services/voiceRecipeLibraryService';
import type { SpeechProvider } from '@/types/speechLibrary';
import type { VoiceRecipe } from '@/types/voiceRecipes';

import {
  PageHeader,
  Pill,
  RecipeFlow,
  buildRecipeSteps,
} from './_primitives';

import type { TabId } from '../SettingsSpeechPage';

interface VoiceModeTabProps {
  onJump?: (tab: TabId) => void;
  /** Notify the parent shell that the persisted settings changed (refreshes the rail footer). */
  onSettingsChanged?: () => void;
}

/**
 * Voice Mode tab. Reads/writes the per-tenant <c>VoiceModeSettings</c> row (spec 024 Phase C):
 * picking an active recipe + the on/off switch persist immediately on Save.
 *
 * <para>
 * Phase C.2 (now live) rewires the WSS pipeline factory to read this row directly: the
 * endpoint resolves the active recipe + its STT/TTS provider rows from the speech library and
 * hands the factory a fully-typed runtime spec. The legacy <c>/settings/voice</c> page sticks
 * around for credential management — the "Open legacy page" button is a deliberate pointer.
 * </para>
 */
export function VoiceModeTab({ onJump, onSettingsChanged }: VoiceModeTabProps) {
  const [recipes, setRecipes] = useState<VoiceRecipe[]>([]);
  const [providers, setProviders] = useState<SpeechProvider[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Local edit state — initialised from VoiceModeSettings on first load. Edits stay local until
  // Save; refresh / cancel restores the persisted values.
  const [enabled, setEnabled] = useState(true);
  const [activeRecipeId, setActiveRecipeId] = useState<string | null>(null);
  // Snapshot of the persisted state — used to detect dirty changes for the Save button.
  const [persistedEnabled, setPersistedEnabled] = useState(true);
  const [persistedActiveRecipeId, setPersistedActiveRecipeId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const [recipeList, providerList, settings] = await Promise.all([
          voiceRecipeLibraryService.list({ includeDisabled: false }),
          speechProviderLibraryService.list({ includeDisabled: true }),
          voiceModeSettingsService.get(),
        ]);
        if (cancelled) return;
        setRecipes(recipeList);
        setProviders(providerList);
        setEnabled(settings.enabled);
        setActiveRecipeId(settings.activeRecipeId);
        setPersistedEnabled(settings.enabled);
        setPersistedActiveRecipeId(settings.activeRecipeId);
      } catch (err) {
        if (cancelled) return;
        // eslint-disable-next-line no-console
        console.error(err);
        setError('Failed to load voice mode settings.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  const providerLookup = useMemo(() => {
    const m = new Map<string, SpeechProvider>();
    for (const p of providers) m.set(p.id, p);
    return m;
  }, [providers]);

  const active = useMemo(
    () => recipes.find((r) => r.id === activeRecipeId) ?? null,
    [recipes, activeRecipeId],
  );

  const others = useMemo(
    () => recipes.filter((r) => r.id !== activeRecipeId),
    [recipes, activeRecipeId],
  );

  const isDirty =
    enabled !== persistedEnabled || activeRecipeId !== persistedActiveRecipeId;

  const handleSave = async () => {
    setSaving(true);
    try {
      const saved = await voiceModeSettingsService.update({
        activeRecipeId,
        enabled,
      });
      setEnabled(saved.enabled);
      setActiveRecipeId(saved.activeRecipeId);
      setPersistedEnabled(saved.enabled);
      setPersistedActiveRecipeId(saved.activeRecipeId);
      onSettingsChanged?.();
      toast.success('Voice mode settings saved.');
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'Failed to save voice mode settings.';
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center p-12 text-[var(--color-text-secondary)]">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading voice mode…
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

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Speech & Voice"
        title="Voice mode"
        subtitle="The live spoken conversation experience. One recipe is active at a time."
        actions={
          <Button size="sm" onClick={() => void handleSave()} disabled={saving || !isDirty}>
            {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Save className="h-3.5 w-3.5" />}
            Save changes
          </Button>
        }
      />

      {/* Phase D callout — the WSS pipeline reads the active recipe + provider rows
          (with their encrypted API keys) from the speech library. No separate legacy
          page in the loop. */}
      <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2 text-xs text-[var(--color-text-secondary)]">
        <span className="font-semibold text-[var(--color-text-primary)]">Live</span>{' '}
        — the WebSocket voice pipeline reads the active recipe (and the recipe's TTS
        provider with its encrypted API key) directly from your library. Pick a recipe
        below; configure providers + credentials in the Providers tab.
      </div>

      {/* Hero status */}
      <HeroStatus
        enabled={enabled}
        onToggle={() => setEnabled((v) => !v)}
        activeName={active?.displayName ?? null}
      />

      {/* 2-column body */}
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
        {/* Left column */}
        <div className="space-y-6">
          <Section
            title="Active recipe"
            description="Voice Mode plays through this recipe. Switch by selecting another from the list below."
            action={
              <Button variant="ghost" size="sm" onClick={() => onJump?.('recipes')}>
                <Layers className="h-3.5 w-3.5" /> Manage recipes
              </Button>
            }
          >
            {active ? (
              <div className="rounded-xl border-2 border-[var(--color-brand-primary)] bg-[var(--color-surface)] p-4">
                <div className="mb-3 flex items-start justify-between gap-3">
                  <div>
                    <div className="text-sm font-semibold text-[var(--color-text-primary)]">
                      {active.displayName}
                    </div>
                    {active.description && (
                      <p className="mt-0.5 text-xs text-[var(--color-text-secondary)]">
                        {active.description}
                      </p>
                    )}
                  </div>
                  <Pill tone="success" dot>
                    Active
                  </Pill>
                </div>
                <RecipeFlow
                  steps={buildRecipeSteps(active, providerLookup)}
                  kind={active.kind === 'Composite' ? 'composite' : 'chained'}
                />
              </div>
            ) : (
              <div className="rounded-xl border border-dashed border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-6 text-center text-sm text-[var(--color-text-secondary)]">
                No recipe selected. Switch from the list below.
              </div>
            )}

            {others.length > 0 && (
              <div className="mt-4">
                <div className="mb-2 text-[10.5px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-tertiary)]">
                  Switch to
                </div>
                <div className="flex flex-col gap-2">
                  {others.map((r) => (
                    <SwitchRow
                      key={r.id}
                      recipe={r}
                      onActivate={() => setActiveRecipeId(r.id)}
                    />
                  ))}
                </div>
              </div>
            )}
          </Section>

          {/* Providers in use */}
          {active && (
            <Section
              title="Providers used by this recipe"
              description="Changing any of these providers affects every recipe that references them."
            >
              <div className="grid gap-2.5 md:grid-cols-2">
                {buildRecipeSteps(active, providerLookup)
                  .filter((s) => s.label !== 'Listen' && s.label !== 'Agent')
                  .map((s, i) => (
                    <div
                      key={`${s.label}-${i}`}
                      className="flex items-center gap-2.5 rounded-[10px] bg-[var(--color-surface-inset)] p-3"
                    >
                      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]">
                        <s.icon className="h-3.5 w-3.5 text-[var(--color-brand-primary)]" />
                      </span>
                      <div className="min-w-0">
                        <div className="text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                          {s.label}
                        </div>
                        <div className="mt-0.5 truncate text-[12.5px] font-medium text-[var(--color-text-primary)]">
                          {s.detail}
                        </div>
                      </div>
                    </div>
                  ))}
              </div>
            </Section>
          )}
        </div>

        {/* Right rail */}
        <div className="space-y-3 xl:sticky xl:top-6 xl:self-start">
          <LiveTestCard enabled={enabled} />
          <UsageCard />
          <HelperCard onJump={onJump} />
        </div>
      </div>
    </div>
  );
}

// ─── Hero status ────────────────────────────────────────────────────────

function HeroStatus({
  enabled,
  onToggle,
  activeName,
}: {
  enabled: boolean;
  onToggle: () => void;
  activeName: string | null;
}) {
  return (
    <div
      className={cn(
        'flex flex-wrap items-center justify-between gap-6 rounded-2xl p-6',
        enabled
          ? 'bg-[linear-gradient(135deg,var(--color-brand-primary),#044045)] text-white'
          : 'border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] text-[var(--color-text-primary)]',
      )}
    >
      <div className="flex items-center gap-4">
        <div
          className={cn(
            'grid h-14 w-14 shrink-0 place-items-center rounded-2xl border',
            enabled
              ? 'border-white/25 bg-white/20'
              : 'border-[var(--color-border-light)] bg-[var(--color-surface)]',
          )}
        >
          {enabled ? <Mic className="h-6 w-6" /> : <MicOff className="h-6 w-6 text-[var(--color-text-tertiary)]" />}
        </div>
        <div>
          <div className={cn(
            'text-[10.5px] font-semibold uppercase tracking-[0.08em]',
            enabled ? 'opacity-85' : 'text-[var(--color-text-tertiary)]',
          )}>
            {enabled ? 'Voice Mode is on' : 'Voice Mode is off'}
          </div>
          <div className="mt-0.5 text-xl font-semibold">
            {enabled ? (activeName ?? 'No recipe selected') : 'No recipe running'}
          </div>
          <div className={cn('mt-1 text-xs', enabled ? 'opacity-80' : 'text-[var(--color-text-secondary)]')}>
            {enabled
              ? 'Operators can talk to agents in real time using the active recipe below.'
              : 'Spoken conversations are disabled across the workspace.'}
          </div>
        </div>
      </div>

      {/* Custom toggle (matches starter kit) */}
      <button
        type="button"
        onClick={onToggle}
        className={cn(
          'relative h-8 w-14 shrink-0 rounded-full transition-colors',
          enabled ? 'bg-white/30' : 'bg-[var(--color-border)]',
        )}
        aria-pressed={enabled}
      >
        <span
          className={cn(
            'absolute top-[3px] h-[26px] w-[26px] rounded-full bg-white shadow-sm transition-all',
            enabled ? 'left-[27px]' : 'left-[3px]',
          )}
        />
      </button>
    </div>
  );
}

// ─── Switch-to row ───────────────────────────────────────────────────────

function SwitchRow({ recipe, onActivate }: { recipe: VoiceRecipe; onActivate: () => void }) {
  const Icon = recipe.kind === 'Composite' ? Radio : Layers;
  const stepCount = recipe.kind === 'Composite' ? 1 : 4;
  return (
    <div className="flex items-center justify-between gap-3 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3">
      <div className="flex min-w-0 items-center gap-2.5">
        <span className="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-[var(--color-surface-inset)]">
          <Icon className="h-3.5 w-3.5 text-[var(--color-text-secondary)]" />
        </span>
        <div className="min-w-0">
          <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">
            {recipe.displayName}
          </div>
          <div className="mt-0.5 text-[11px] text-[var(--color-text-tertiary)]">
            {recipe.kind === 'Composite' ? 'Realtime' : `Chained · ${stepCount} steps`} ·{' '}
            {recipe.isBuiltIn ? 'Built-in' : 'Custom'}
          </div>
        </div>
      </div>
      <Button variant="ghost" size="sm" onClick={onActivate}>
        Activate
      </Button>
    </div>
  );
}

// ─── Right-rail cards ────────────────────────────────────────────────────

function LiveTestCard(_props: { enabled: boolean }) {
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">Live test</div>
      <p className="mt-1 mb-3 text-xs leading-relaxed text-[var(--color-text-secondary)]">
        Run a short conversation through the active recipe. Records an AiRun for audit and shows
        latency at each step.
      </p>
      <Button className="w-full justify-center" disabled size="sm" title="Inline voice test ships in a follow-up phase.">
        <Mic className="h-3.5 w-3.5" /> Start voice test
      </Button>

      {/* Mic preview waveform */}
      <div className="mt-3 flex items-center gap-3 rounded-[10px] border border-dashed border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3.5">
        <div className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-[var(--color-brand-primary-10)]">
          <Mic className="h-4 w-4 text-[var(--color-brand-primary)]" />
        </div>
        <svg viewBox="0 0 200 32" className="h-8 w-full" aria-hidden="true">
          {Array.from({ length: 60 }).map((_, i) => {
            const h = 4 + Math.abs(Math.sin(i * 0.7) * 14);
            return (
              <rect
                key={i}
                x={i * 3.3}
                y={(32 - h) / 2}
                width="2"
                height={h}
                rx="1"
                fill="var(--color-brand-primary)"
                opacity={0.6}
              />
            );
          })}
        </svg>
      </div>

      <div className="mt-2 flex justify-between font-mono text-[11px] text-[var(--color-text-tertiary)]">
        <span>Latency budget · 800ms</span>
        <span>Indicative · P50</span>
      </div>
    </div>
  );
}

function UsageCard() {
  const rows = [
    { label: 'Conversations', value: '—', pct: null as number | null },
    { label: 'Avg duration', value: '—', pct: null },
    { label: 'STT minutes', value: '— / 2,000', pct: null },
    { label: 'TTS characters', value: '— / 500k', pct: null },
  ];
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="mb-3 text-[13px] font-semibold text-[var(--color-text-primary)]">
        Last 24 hours
      </div>
      <div className="space-y-2.5">
        {rows.map((row) => (
          <div key={row.label}>
            <div className="mb-1 flex justify-between text-xs">
              <span className="text-[var(--color-text-secondary)]">{row.label}</span>
              <span className="font-mono text-[11.5px] text-[var(--color-text-primary)]">
                {row.value}
              </span>
            </div>
            {row.pct != null && (
              <div className="h-1 overflow-hidden rounded-full bg-[var(--color-surface-inset)]">
                <div
                  className="h-full bg-[var(--color-brand-primary)]"
                  style={{ width: `${row.pct}%` }}
                />
              </div>
            )}
          </div>
        ))}
      </div>
      <p className="mt-3 text-[10.5px] text-[var(--color-text-tertiary)]">
        Live usage metrics ship with Phase C observability.
      </p>
    </div>
  );
}

function HelperCard({ onJump }: { onJump?: (tab: TabId) => void }) {
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">
        Voice Mode vs Chat Speech
      </div>
      <p className="mt-1 text-xs leading-relaxed text-[var(--color-text-secondary)]">
        Voice Mode is the live spoken conversation.{' '}
        <span className="font-semibold text-[var(--color-text-primary)]">Chat Speech</span> is
        optional voice-over for written replies — they share providers but configure independently.
      </p>
      <Button
        variant="outline"
        size="sm"
        className="mt-3 w-full justify-center"
        onClick={() => onJump?.('chat-speech')}
      >
        Open Chat Speech
      </Button>
    </div>
  );
}

// ─── Section primitive ──────────────────────────────────────────────────

function Section({
  title,
  description,
  action,
  children,
}: {
  title: string;
  description?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h3>
          {description && (
            <p className="mt-0.5 text-xs text-[var(--color-text-secondary)]">{description}</p>
          )}
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}
