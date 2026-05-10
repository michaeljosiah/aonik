import { Edit3, Lock, Mic, Plug, Speaker, TestTube2, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { SpeechProvider, SpeechProviderType } from '@/types/speechLibrary';

import { Pill, TYPE_LABEL, TYPE_TONE } from './_primitives';

interface ProviderCardProps {
  provider: SpeechProvider;
  /** How many recipes reference this provider (0 = unused). */
  usageCount: number;
  onEdit: () => void;
  onTest: () => void;
  onDisable: () => void;
}

/**
 * Larger 2-column-grid provider card. Shows icon tile + display name + vendor +
 * status pill + type/builtin/used-by chips + latency / voices stat row +
 * Test / Configure / In-use indicator. Layout mirrors the starter kit
 * `ProviderCard` in `Templates/aonik-admin-starterkit/screens/speech.jsx`.
 */
export function ProviderCard({
  provider,
  usageCount,
  onEdit,
  onTest,
  onDisable,
}: ProviderCardProps) {
  const stats = providerStats(provider);
  const active = provider.status === 'Active';

  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5">
      <div className="flex items-start gap-4">
        <TypeIcon type={provider.type} active={active} />

        <div className="min-w-0 flex-1">
          {/* Title row */}
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-[var(--color-text-primary)]">
                {provider.displayName}
              </div>
              <div className="mt-0.5 truncate text-[11.5px] text-[var(--color-text-secondary)]">
                {provider.vendor}
              </div>
            </div>
            {active ? (
              <Pill tone="success" dot>
                Active
              </Pill>
            ) : (
              <Pill tone="default">{provider.status === 'Disabled' ? 'Disabled' : 'Deleted'}</Pill>
            )}
          </div>

          {/* Chip row */}
          <div className="mt-2.5 flex flex-wrap gap-1.5">
            <Pill tone={TYPE_TONE[provider.type]}>{TYPE_LABEL[provider.type]}</Pill>
            {usageCount > 0 && (
              <Pill tone="tint">
                Used by {usageCount} {usageCount === 1 ? 'recipe' : 'recipes'}
              </Pill>
            )}
          </div>

          {/* Stat row */}
          <div className="mt-3 grid grid-cols-2 gap-3 border-t border-[var(--color-border-light)] pt-2.5">
            <Stat label="Latency" value={stats.latency} />
            <Stat label={provider.type === 'Stt' ? 'Languages' : 'Voices'} value={stats.choices} />
          </div>

          {/* Action row */}
          <div className="mt-3 flex items-center gap-2">
            {provider.type !== 'Composite' && (
              <Button variant="ghost" size="sm" onClick={onTest} disabled={!active}>
                <TestTube2 className="h-3.5 w-3.5" /> Test
              </Button>
            )}
            <Button variant="ghost" size="sm" onClick={onEdit}>
              <Edit3 className="h-3.5 w-3.5" /> Configure
            </Button>
            {active && (
              <Button variant="ghost" size="sm" onClick={onDisable}>
                <Trash2 className="h-3.5 w-3.5" /> Disable
              </Button>
            )}
            {usageCount > 0 && (
              <span className="ml-auto inline-flex items-center gap-1 text-[10.5px] text-[var(--color-text-tertiary)]">
                <Lock className="h-3 w-3" /> In use
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Type-coloured square icon ───────────────────────────────────────────

function TypeIcon({ type, active }: { type: SpeechProviderType; active: boolean }) {
  const Icon = type === 'Stt' ? Mic : type === 'Tts' ? Speaker : Plug;
  return (
    <div
      className={cn(
        'grid h-10 w-10 shrink-0 place-items-center rounded-[10px] border border-[var(--color-border-light)]',
        active ? 'bg-[var(--color-brand-primary-10)]' : 'bg-[var(--color-surface-inset)]',
      )}
    >
      <Icon
        className={cn(
          'h-5 w-5',
          active ? 'text-[var(--color-brand-primary)]' : 'text-[var(--color-text-tertiary)]',
        )}
      />
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {label}
      </div>
      <div className="mt-0.5 font-mono text-[12.5px] text-[var(--color-text-primary)]">{value}</div>
    </div>
  );
}

// ─── Heuristic stat lookup ───────────────────────────────────────────────
// We don't track latency / voice counts dynamically. These are typical numbers
// per vendor / model — useful to scan but not literal. Marked with "~" so admins
// know they're nominal.

interface ProviderStatNumbers {
  latency: string;
  choices: string;
}

function providerStats(provider: SpeechProvider): ProviderStatNumbers {
  switch (provider.config.kind) {
    case 'openai-whisper':
      return { latency: '~410ms', choices: '99' };
    case 'azure-stt':
      return { latency: '~320ms', choices: '85' };
    case 'openai-tts':
      return { latency: '~290ms', choices: '6' };
    case 'azure-tts':
      return { latency: '~340ms', choices: '100+' };
    case 'elevenlabs-tts':
      return { latency: '~290ms', choices: '29+' };
    case 'mistral-tts':
      return { latency: '~350ms', choices: '12' };
    case 'openai-realtime':
      return { latency: '<300ms', choices: '6' };
    case 'azure-voice-live':
      return { latency: '<400ms', choices: '6' };
    default: {
      const _exhaustive: never = provider.config;
      return _exhaustive;
    }
  }
}
