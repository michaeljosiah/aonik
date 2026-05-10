import { useEffect, useMemo, useState } from 'react';
import {
  Check,
  ExternalLink,
  HelpCircle,
  Loader2,
  Play,
  RefreshCw,
  Save,
  Speaker,
  Upload,
  Volume2,
  VolumeX,
} from 'lucide-react';
import { Link as RouterLink } from 'react-router-dom';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { speechProviderLibraryService } from '@/services/speechProviderLibraryService';
import type { SpeechProvider } from '@/types/speechLibrary';

import { PageHeader, Pill } from './_primitives';

import type { TabId } from '../SettingsSpeechPage';

interface ChatSpeechTabProps {
  onJump?: (tab: TabId) => void;
}

interface VoicePickerEntry {
  providerId: string;
  providerName: string;
  vendor: string;
  voiceLabel: string;
  detail: string;
  isCloned: boolean;
}

/**
 * Chat Speech tab. Voice picker is built from the real TTS providers in the library so
 * admins see live entries (OpenAI / alloy, ElevenLabs / Aria, …). Persistence ships in
 * Phase C; in the meantime, the legacy `/settings/text-to-speech` page is the source of
 * truth and is linked at the top of the tab.
 */
export function ChatSpeechTab({ onJump }: ChatSpeechTabProps) {
  const [providers, setProviders] = useState<SpeechProvider[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Local-only mock state (Phase C wires real persistence).
  const [enabled, setEnabled] = useState(true);
  const [autoPlay, setAutoPlay] = useState(false);
  const [showSpeakButton, setShowSpeakButton] = useState(true);
  const [rate, setRate] = useState(1.0);
  const [selectedVoice, setSelectedVoice] = useState<string | null>(null);
  const [previewText, setPreviewText] = useState(
    'Three invoices are awaiting your review, and April fuel spending is trending twelve percent above plan.',
  );

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const list = await speechProviderLibraryService.list({ includeDisabled: false });
        if (cancelled) return;
        setProviders(list);
      } catch (err) {
        if (cancelled) return;
        // eslint-disable-next-line no-console
        console.error(err);
        setError('Failed to load TTS providers.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  const voices = useMemo(() => buildVoiceEntries(providers), [providers]);

  // Default the selected voice to the first one once loaded.
  useEffect(() => {
    if (!selectedVoice && voices.length > 0) setSelectedVoice(voices[0].providerId);
  }, [voices, selectedVoice]);

  const handleSave = () => {
    toast.info(
      'Chat speech persistence ships with Phase C. Use /settings/text-to-speech for live changes today.',
    );
  };

  const handlePreview = () => {
    toast.info('Preview synthesis lands with Phase C. Use the legacy TTS page for live previews.');
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center p-12 text-[var(--color-text-secondary)]">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading chat speech preview…
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
        title="Chat speech"
        subtitle="Speak written chat replies aloud. Independent of Voice Mode."
        actions={
          <>
            <Button variant="outline" size="sm" asChild>
              <RouterLink to="/settings/text-to-speech">
                <ExternalLink className="h-3.5 w-3.5" /> Open legacy page
              </RouterLink>
            </Button>
            <Button size="sm" onClick={handleSave}>
              <Save className="h-3.5 w-3.5" /> Save changes
            </Button>
          </>
        }
      />

      {/* Helper banner explaining the difference from Voice Mode */}
      <div className="flex items-center gap-3 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-3">
        <HelpCircle className="h-3.5 w-3.5 shrink-0 text-[var(--color-text-secondary)]" />
        <div className="flex-1 text-xs leading-relaxed text-[var(--color-text-secondary)]">
          <span className="font-semibold text-[var(--color-text-primary)]">Chat Speech</span> reads
          chat replies aloud.{' '}
          <span className="font-semibold text-[var(--color-text-primary)]">Voice Mode</span> is live
          spoken conversation. They share providers but configure independently.
        </div>
        <Button variant="ghost" size="sm" onClick={() => onJump?.('voice-mode')}>
          Open Voice Mode
        </Button>
      </div>

      {/* Preview-only callout */}
      <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2 text-xs text-[var(--color-text-secondary)]">
        <span className="font-semibold text-[var(--color-text-primary)]">Preview · Phase C</span>{' '}
        — voice picker / playback toggles below don't persist yet. Configure live changes at
        <RouterLink
          to="/settings/text-to-speech"
          className="ml-1 underline decoration-dotted underline-offset-2 hover:text-[var(--color-brand-primary)]"
        >
          /settings/text-to-speech
        </RouterLink>
        .
      </div>

      {/* Hero status */}
      <HeroStatus enabled={enabled} onToggle={() => setEnabled((v) => !v)} />

      {/* 2-column body */}
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
        {/* Left column */}
        <div className="space-y-6">
          <Section
            title="Voice"
            description="The voice used to read chat replies. Switching voice has no effect on Voice Mode."
            action={
              <Button variant="ghost" size="sm" disabled>
                <RefreshCw className="h-3.5 w-3.5" /> Refresh voices
              </Button>
            }
          >
            {voices.length === 0 ? (
              <Card>
                <CardContent className="p-6 text-sm text-[var(--color-text-secondary)]">
                  No TTS providers configured yet. Add one in the{' '}
                  <button
                    type="button"
                    className="underline decoration-dotted underline-offset-2"
                    onClick={() => onJump?.('providers')}
                  >
                    Providers
                  </button>{' '}
                  tab to see voice options here.
                </CardContent>
              </Card>
            ) : (
              <div className="grid gap-2.5 md:grid-cols-2">
                {voices.map((v) => (
                  <VoiceCard
                    key={v.providerId}
                    voice={v}
                    selected={v.providerId === selectedVoice}
                    onSelect={() => setSelectedVoice(v.providerId)}
                  />
                ))}
              </div>
            )}
          </Section>

          <Section
            title="Playback"
            description="How chat replies are spoken when Chat Speech is on."
          >
            <div className="space-y-2.5">
              <ToggleRow
                label="Auto-play replies"
                code="ChatSpeech.AutoPlay"
                help="Speak each reply automatically as it arrives. Operators can mute per-thread."
                value={autoPlay}
                onChange={setAutoPlay}
              />
              <ToggleRow
                label="Show speak button"
                code="ChatSpeech.ShowSpeakButton"
                help="Adds a speaker icon next to each chat reply."
                value={showSpeakButton}
                onChange={setShowSpeakButton}
              />
              <RangeRow
                label="Speed"
                code="ChatSpeech.Rate"
                help="1.0 is natural pace. 1.2 is slightly brisk."
                value={rate}
                onChange={setRate}
              />
            </div>
          </Section>
        </div>

        {/* Right rail */}
        <div className="space-y-3 xl:sticky xl:top-6 xl:self-start">
          <PreviewCard
            value={previewText}
            onChange={setPreviewText}
            onPlay={handlePreview}
            disabled={!enabled || !selectedVoice}
          />
          <UsageCard />
          <CloneCard />
        </div>
      </div>
    </div>
  );
}

// ─── Hero status ────────────────────────────────────────────────────────

function HeroStatus({ enabled, onToggle }: { enabled: boolean; onToggle: () => void }) {
  return (
    <div className="flex items-center justify-between gap-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="flex items-center gap-3.5">
        <div
          className={cn(
            'grid h-11 w-11 shrink-0 place-items-center rounded-[10px] border border-[var(--color-border-light)]',
            enabled ? 'bg-[var(--color-brand-primary-10)]' : 'bg-[var(--color-surface-inset)]',
          )}
        >
          {enabled ? (
            <Speaker className="h-5 w-5 text-[var(--color-brand-primary)]" />
          ) : (
            <VolumeX className="h-5 w-5 text-[var(--color-text-tertiary)]" />
          )}
        </div>
        <div>
          <div className="text-sm font-semibold text-[var(--color-text-primary)]">
            Chat Speech is {enabled ? 'on' : 'off'}
          </div>
          <div className="mt-0.5 text-xs text-[var(--color-text-secondary)]">
            {enabled
              ? 'Operators can play chat replies aloud from any chat surface.'
              : 'Spoken playback is disabled. Operators see text replies only.'}
          </div>
        </div>
      </div>
      <Switch checked={enabled} onCheckedChange={onToggle} aria-label="Toggle chat speech" />
    </div>
  );
}

// ─── Voice picker card ──────────────────────────────────────────────────

function VoiceCard({
  voice,
  selected,
  onSelect,
}: {
  voice: VoicePickerEntry;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={cn(
        'flex items-center gap-3 rounded-[10px] p-3 text-left transition-colors',
        selected
          ? 'border-2 border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-10)]'
          : 'border border-[var(--color-border-light)] bg-[var(--color-surface)] hover:border-[var(--color-brand-primary)]/40',
      )}
    >
      <span
        className={cn(
          'grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[var(--color-border-light)]',
          selected ? 'bg-[var(--color-brand-primary-10)]' : 'bg-[var(--color-surface-inset)]',
        )}
      >
        {selected ? (
          <Check className="h-3.5 w-3.5 text-[var(--color-brand-primary)]" />
        ) : (
          <Speaker className="h-3.5 w-3.5 text-[var(--color-text-secondary)]" />
        )}
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5 text-[13px] font-semibold text-[var(--color-text-primary)]">
          <span className="truncate">{voice.voiceLabel}</span>
          {voice.isCloned && <Pill tone="success">cloned</Pill>}
        </div>
        <div className="mt-0.5 truncate text-[11px] text-[var(--color-text-secondary)]">
          {voice.vendor} · {voice.detail}
        </div>
      </div>
      <span
        role="button"
        aria-label={`Preview ${voice.voiceLabel}`}
        className="grid h-7 w-7 shrink-0 place-items-center rounded-md text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]"
      >
        <Play className="h-3.5 w-3.5" />
      </span>
    </button>
  );
}

// ─── Right-rail cards ───────────────────────────────────────────────────

function PreviewCard({
  value,
  onChange,
  onPlay,
  disabled,
}: {
  value: string;
  onChange: (next: string) => void;
  onPlay: () => void;
  disabled: boolean;
}) {
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">Preview voice</div>
      <p className="mt-1 mb-3 text-xs leading-relaxed text-[var(--color-text-secondary)]">
        Synthesises a sample using the selected voice. Records an AiRun for audit.
      </p>
      <Textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        rows={4}
        className="mb-3 text-[13px] leading-relaxed"
      />
      <Button
        size="sm"
        className="w-full justify-center"
        disabled={disabled}
        onClick={onPlay}
      >
        <Volume2 className="h-3.5 w-3.5" /> Synthesize &amp; play
      </Button>

      {/* Static waveform placeholder */}
      <div className="mt-3 rounded-[10px] bg-[var(--color-surface-inset)] p-3.5">
        <div className="mb-2 flex items-center justify-between">
          <Button variant="ghost" size="sm" className="h-7 w-7 p-0" disabled>
            <Volume2 className="h-3.5 w-3.5" />
          </Button>
          <span className="font-mono text-[11px] text-[var(--color-text-tertiary)]">0:00 / 0:09</span>
        </div>
        <svg viewBox="0 0 200 32" className="h-8 w-full" aria-hidden="true">
          {Array.from({ length: 60 }).map((_, i) => {
            const h = 6 + Math.abs(Math.sin(i * 0.6) * 12) + (i % 3) * 2;
            return (
              <rect
                key={i}
                x={i * 3.3}
                y={(32 - h) / 2}
                width="2"
                height={h}
                rx="1"
                fill="var(--color-brand-primary)"
                opacity={i < 24 ? 1 : 0.35}
              />
            );
          })}
        </svg>
      </div>
      <div className="mt-2 flex justify-between font-mono text-[11px] text-[var(--color-text-tertiary)]">
        <span>AiRunId · pending</span>
        <span>312ms · 14kb</span>
      </div>
    </div>
  );
}

function UsageCard() {
  const rows = [
    { label: 'Characters spoken', value: '— / 500,000', pct: null as number | null },
    { label: 'Cost', value: '— / $40 limit', pct: null },
    { label: 'Replies played', value: '—', pct: null },
  ];
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="mb-3 text-[13px] font-semibold text-[var(--color-text-primary)]">
        Usage · this month
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
          </div>
        ))}
      </div>
      <p className="mt-3 text-[10.5px] text-[var(--color-text-tertiary)]">
        Live usage metrics ship with Phase C observability.
      </p>
    </div>
  );
}

function CloneCard() {
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">
        Need a custom voice?
      </div>
      <p className="mt-1 text-xs leading-relaxed text-[var(--color-text-secondary)]">
        Upload a 30-second clean sample to clone a voice. Cloned voices appear with a green badge in
        the picker.
      </p>
      <Button asChild variant="outline" size="sm" className="mt-3 w-full justify-center">
        <RouterLink to="/settings/text-to-speech">
          <Upload className="h-3.5 w-3.5" /> Upload sample
        </RouterLink>
      </Button>
    </div>
  );
}

// ─── Section + form rows ────────────────────────────────────────────────

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

function ToggleRow({
  label,
  code,
  help,
  value,
  onChange,
}: {
  label: string;
  code: string;
  help: string;
  value: boolean;
  onChange: (next: boolean) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] px-4 py-3">
      <div className="min-w-0">
        <div className="text-[13px] font-medium text-[var(--color-text-primary)]">{label}</div>
        <div className="mt-0.5 text-[11px] text-[var(--color-text-secondary)]">{help}</div>
        <div className="mt-1 font-mono text-[10.5px] text-[var(--color-text-tertiary)]">{code}</div>
      </div>
      <Switch checked={value} onCheckedChange={onChange} aria-label={label} />
    </div>
  );
}

function RangeRow({
  label,
  code,
  help,
  value,
  onChange,
}: {
  label: string;
  code: string;
  help: string;
  value: number;
  onChange: (next: number) => void;
}) {
  return (
    <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] px-4 py-3">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="text-[13px] font-medium text-[var(--color-text-primary)]">{label}</div>
          <div className="mt-0.5 text-[11px] text-[var(--color-text-secondary)]">{help}</div>
          <div className="mt-1 font-mono text-[10.5px] text-[var(--color-text-tertiary)]">{code}</div>
        </div>
        <span className="font-mono text-[12.5px] text-[var(--color-text-primary)]">{value.toFixed(1)}x</span>
      </div>
      <input
        type="range"
        min={0.5}
        max={2}
        step={0.1}
        value={value}
        onChange={(e) => onChange(Number.parseFloat(e.target.value))}
        className="mt-3 w-full accent-[var(--color-brand-primary)]"
      />
    </div>
  );
}

// ─── Voice picker entry derivation ──────────────────────────────────────

function buildVoiceEntries(providers: SpeechProvider[]): VoicePickerEntry[] {
  const tts = providers.filter((p) => p.type === 'Tts' && p.status === 'Active');
  return tts.map((p) => {
    const config = p.config;
    let voiceLabel = p.displayName;
    let detail = '';
    let isCloned = false;

    if (config.kind === 'openai-tts') {
      voiceLabel = config.voiceId || p.displayName;
      detail = config.modelId ?? 'tts-1';
    } else if (config.kind === 'azure-tts') {
      voiceLabel = config.voiceId || p.displayName;
      detail = config.region;
    } else if (config.kind === 'elevenlabs-tts') {
      // ElevenLabs voiceIds are uuids, so prefer the provider display name.
      voiceLabel = p.displayName;
      detail = config.modelId ?? 'eleven_multilingual_v2';
    } else if (config.kind === 'mistral-tts') {
      voiceLabel = config.voiceId || p.displayName;
      detail = config.modelId ?? 'voxtral-tts';
      isCloned = !p.isBuiltIn;
    }

    return {
      providerId: p.id,
      providerName: p.displayName,
      vendor: p.vendor,
      voiceLabel,
      detail,
      isCloned,
    };
  });
}
