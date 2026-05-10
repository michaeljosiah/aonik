import type { ReactNode } from 'react';
import { ArrowRight, Mic, Radio, Speaker, Sparkles, type LucideIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

import type { SpeechProvider, SpeechProviderType } from '@/types/speechLibrary';
import type { ChainedRecipeBody, CompositeRecipeBody, VoiceRecipe } from '@/types/voiceRecipes';

/**
 * Shared visual primitives for the Speech & Voice settings page (spec 024).
 * Mirrors the layout vocabulary in `Templates/aonik-admin-starterkit/screens/speech.jsx`:
 *   - PageHeader (eyebrow / title / subtitle / actions)
 *   - Pill (dot-prefixed status chip)
 *   - StatTile (label + big mono number / total)
 *   - RecipeFlow (Listen → Transcribe → Agent → Speak boxes connected by arrows)
 *
 * Kept in one file so the four tabs can import from a single barrel without a
 * web of tiny components in the directory listing.
 */

// ─── PageHeader ──────────────────────────────────────────────────────────

interface PageHeaderProps {
  eyebrow?: string;
  title: string;
  subtitle?: string;
  actions?: ReactNode;
}

export function PageHeader({ eyebrow, title, subtitle, actions }: PageHeaderProps) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-6">
      <div className="min-w-0">
        {eyebrow && (
          <p className="text-[10.5px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
            {eyebrow}
          </p>
        )}
        <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-[var(--color-text-primary)]">
          {title}
        </h1>
        {subtitle && (
          <p className="mt-1 max-w-3xl text-sm text-[var(--color-text-secondary)]">{subtitle}</p>
        )}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}

// ─── Pill ────────────────────────────────────────────────────────────────

type PillTone = 'default' | 'success' | 'warning' | 'tint' | 'pending' | 'danger';

interface PillProps {
  tone?: PillTone;
  dot?: boolean;
  children: ReactNode;
}

/**
 * Compact status chip. Wraps the shared `Badge` with the starter-kit tone vocabulary
 * (success / warning / tint / pending) and an optional leading dot.
 */
export function Pill({ tone = 'default', dot, children }: PillProps) {
  const variant = TONE_TO_VARIANT[tone];
  const dotColor = DOT_COLORS[tone];
  return (
    <Badge variant={variant} className="gap-1.5 px-2 py-0.5 text-[10.5px] font-medium">
      {dot && <span className="inline-block h-1.5 w-1.5 rounded-full" style={{ background: dotColor }} />}
      {children}
    </Badge>
  );
}

const TONE_TO_VARIANT: Record<PillTone, 'default' | 'success' | 'warning' | 'outline' | 'secondary' | 'error'> = {
  default: 'outline',
  success: 'success',
  warning: 'warning',
  tint: 'secondary',
  pending: 'secondary',
  danger: 'error',
};

const DOT_COLORS: Record<PillTone, string> = {
  default: 'var(--color-text-tertiary)',
  success: '#16a34a',
  warning: '#d97706',
  tint: 'var(--color-brand-primary)',
  pending: 'var(--color-brand-secondary)',
  danger: 'var(--color-error, #dc2626)',
};

// ─── StatTile (KPI strip) ────────────────────────────────────────────────

interface StatTileProps {
  label: string;
  value: number | string;
  total?: number | string;
  icon: LucideIcon;
}

export function StatTile({ label, value, total, icon: Icon }: StatTileProps) {
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="flex items-center justify-between">
        <span className="text-xs text-[var(--color-text-secondary)]">{label}</span>
        <span className="grid h-7 w-7 place-items-center rounded-md bg-[var(--color-brand-primary-10)]">
          <Icon className="h-3.5 w-3.5 text-[var(--color-brand-primary)]" />
        </span>
      </div>
      <div className="mt-2 flex items-baseline gap-1.5 font-mono text-2xl font-semibold text-[var(--color-text-primary)]">
        {value}
        {total != null && (
          <span className="text-sm font-normal text-[var(--color-text-tertiary)]">/ {total}</span>
        )}
      </div>
    </div>
  );
}

// ─── RecipeFlow (visual chain renderer) ──────────────────────────────────

export interface RecipeStep {
  label: string;
  detail: string;
  icon: LucideIcon;
}

interface RecipeFlowProps {
  steps: RecipeStep[];
  /** When kind === 'composite', the single step takes full width and centres. */
  kind: 'chained' | 'composite';
}

export function RecipeFlow({ steps, kind }: RecipeFlowProps) {
  return (
    <div className="flex items-center gap-2 overflow-x-auto rounded-[10px] bg-[var(--color-surface-inset)] p-4">
      {steps.map((step, i) => (
        <div key={`${step.label}-${i}`} className="flex items-center gap-2">
          <div
            className={cn(
              'flex shrink-0 items-center gap-2.5 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 py-2.5',
              kind === 'composite' ? 'min-w-[220px]' : 'min-w-[150px]',
            )}
          >
            <span className="grid h-7 w-7 place-items-center rounded-md bg-[var(--color-brand-primary-10)]">
              <step.icon className="h-3.5 w-3.5 text-[var(--color-brand-primary)]" />
            </span>
            <div className="min-w-0">
              <div className="text-[11.5px] font-semibold text-[var(--color-text-primary)]">{step.label}</div>
              <div className="truncate text-[10.5px] text-[var(--color-text-tertiary)]">{step.detail}</div>
            </div>
          </div>
          {i < steps.length - 1 && (
            <ArrowRight className="h-3.5 w-3.5 shrink-0 text-[var(--color-text-tertiary)]" />
          )}
        </div>
      ))}
    </div>
  );
}

// ─── Recipe step builder (resolves provider ids → display names) ─────────

/**
 * Build the visual flow steps for a recipe, given the provider lookup map.
 * Chained recipes always render four boxes (Listen / Transcribe / Agent / Speak);
 * composite recipes render one (Listen & Respond).
 */
export function buildRecipeSteps(
  recipe: VoiceRecipe,
  providers: Map<string, SpeechProvider>,
): RecipeStep[] {
  if (recipe.kind === 'Chained' && recipe.chained) {
    return chainedSteps(recipe.chained, providers);
  }
  if (recipe.kind === 'Composite' && recipe.composite) {
    return compositeSteps(recipe.composite, providers);
  }
  return [];
}

function chainedSteps(body: ChainedRecipeBody, providers: Map<string, SpeechProvider>): RecipeStep[] {
  const stt = providers.get(body.sttProviderId);
  const tts = providers.get(body.ttsProviderId);
  const agentDetail = body.pinnedAgentId
    ? `Pinned · ${body.pinnedAgentId}`
    : 'Orchestrator (uses client request)';
  return [
    { label: 'Listen', detail: 'Browser mic · push-to-talk', icon: Mic },
    {
      label: 'Transcribe',
      detail: stt ? `${stt.displayName} · ${stt.vendor}` : body.sttProviderId,
      icon: Mic,
    },
    { label: 'Agent', detail: agentDetail, icon: Sparkles },
    {
      label: 'Speak',
      detail: tts ? `${tts.displayName} · ${tts.vendor}` : body.ttsProviderId,
      icon: Speaker,
    },
  ];
}

function compositeSteps(
  body: CompositeRecipeBody,
  providers: Map<string, SpeechProvider>,
): RecipeStep[] {
  const composite = providers.get(body.compositeProviderId);
  return [
    {
      label: 'Listen & Respond',
      detail: composite
        ? `${composite.displayName} · ${composite.vendor}`
        : body.compositeProviderId,
      icon: Radio,
    },
  ];
}

// ─── Type helpers ────────────────────────────────────────────────────────

export const TYPE_LABEL: Record<SpeechProviderType, string> = {
  Stt: 'Speech-to-Text',
  Tts: 'Text-to-Speech',
  Composite: 'Realtime Voice',
};

export const TYPE_TONE: Record<SpeechProviderType, PillTone> = {
  Stt: 'tint',
  Tts: 'success',
  Composite: 'pending',
};
