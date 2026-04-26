import { useRef, useState, type ReactNode } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Info,
  Loader2,
  Sparkles,
  Square,
  Volume2,
} from 'lucide-react';

import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import {
  observabilityService,
  type ObservabilityPanelKind,
} from '@/services/observabilityService';
import { textToSpeechSettingsService } from '@/services/textToSpeechSettingsService';

export type PanelCalloutLevel = 'good' | 'warning' | 'critical' | 'info';

export interface PanelCallout {
  level: PanelCalloutLevel;
  message: ReactNode;
}

export interface PanelInfoPopoverProps {
  title: string;
  description: ReactNode;
  callouts?: PanelCallout[];
  /** If set, an "Explain my data" button appears that sends the metrics to an LLM. */
  panelKind?: ObservabilityPanelKind;
  /** Snapshot of panel data sent to the LLM. Called at click time so values are fresh. */
  getMetrics?: () => unknown;
  triggerLabel?: string;
}

type PlaybackState = 'idle' | 'loading' | 'playing' | 'error';

export function PanelInfoPopover({
  title,
  description,
  callouts,
  panelKind,
  getMetrics,
  triggerLabel,
}: PanelInfoPopoverProps) {
  const [summary, setSummary] = useState<string | null>(null);
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [playback, setPlayback] = useState<PlaybackState>('idle');
  const [error, setError] = useState<string | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioUrlRef = useRef<string | null>(null);

  const stopAudio = () => {
    if (audioRef.current) {
      try {
        audioRef.current.pause();
      } catch {
        /* ignore */
      }
      audioRef.current.onended = null;
      audioRef.current.onerror = null;
      audioRef.current = null;
    }
    if (audioUrlRef.current) {
      URL.revokeObjectURL(audioUrlRef.current);
      audioUrlRef.current = null;
    }
    setPlayback('idle');
  };

  const explain = async () => {
    if (!panelKind || !getMetrics) return;
    setLoadingSummary(true);
    setError(null);
    setSummary(null);
    stopAudio();
    try {
      const res = await observabilityService.explainPanel(panelKind, getMetrics());
      setSummary(res.summary);
    } catch (e) {
      setError(resolveErrorMessage(e) ?? 'Could not generate summary.');
    } finally {
      setLoadingSummary(false);
    }
  };

  const speak = async () => {
    if (!summary) return;
    stopAudio();
    setPlayback('loading');
    setError(null);
    try {
      const res = await textToSpeechSettingsService.synthesize({
        speechText: summary,
        locale: 'en-US',
      });
      const url = URL.createObjectURL(res.audioBlob);
      const audio = new Audio(url);
      audio.onended = () => {
        if (audioUrlRef.current === url) {
          URL.revokeObjectURL(url);
          audioUrlRef.current = null;
        }
        audioRef.current = null;
        setPlayback('idle');
      };
      audio.onerror = () => {
        if (audioUrlRef.current === url) {
          URL.revokeObjectURL(url);
          audioUrlRef.current = null;
        }
        audioRef.current = null;
        setPlayback('error');
        setError('Audio playback failed.');
      };
      audioRef.current = audio;
      audioUrlRef.current = url;
      await audio.play();
      setPlayback('playing');
    } catch (e) {
      setPlayback('error');
      setError(resolveErrorMessage(e) ?? 'Could not synthesize speech.');
    }
  };

  const canExplain = Boolean(panelKind && getMetrics);

  return (
    <Popover
      onOpenChange={(open) => {
        if (!open) stopAudio();
      }}
    >
      <PopoverTrigger asChild>
        {triggerLabel ? (
          <button
            type="button"
            aria-label={triggerLabel}
            className="inline-flex h-8 items-center gap-1.5 rounded-md border border-[var(--color-border-light)] px-3 text-xs font-medium text-[var(--color-text-primary)] transition-colors hover:bg-black/5 dark:hover:bg-white/5"
          >
            <Sparkles className="h-3.5 w-3.5" />
            {triggerLabel}
          </button>
        ) : (
          <button
            type="button"
            aria-label={`About ${title}`}
            className="inline-flex h-5 w-5 items-center justify-center rounded-full text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] transition-colors"
          >
            <Info className="w-3.5 h-3.5" />
          </button>
        )}
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-[28rem] max-h-[36rem] overflow-y-auto"
      >
        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h3>
          <div className="text-xs leading-relaxed text-[var(--color-text-secondary)] space-y-2 [&_strong]:text-[var(--color-text-primary)] [&_strong]:font-semibold [&_ul]:list-disc [&_ul]:pl-4 [&_ul]:space-y-1">
            {description}
          </div>
          {callouts && callouts.length > 0 && (
            <div className="pt-2 border-t border-[var(--color-border-light)] space-y-1.5">
              <p className="text-[10px] uppercase tracking-wide font-medium text-[var(--color-text-tertiary)]">
                What your data shows
              </p>
              {callouts.map((c, i) => (
                <Callout key={i} level={c.level}>
                  {c.message}
                </Callout>
              ))}
            </div>
          )}
          {canExplain && (
            <div className="pt-2 border-t border-[var(--color-border-light)] space-y-2">
              <div className="flex items-center justify-between gap-2">
                <p className="text-[10px] uppercase tracking-wide font-medium text-[var(--color-text-tertiary)]">
                  Explain my data
                </p>
                {!summary && (
                  <button
                    type="button"
                    onClick={() => void explain()}
                    disabled={loadingSummary}
                    className="inline-flex items-center gap-1 rounded-sm border border-[var(--color-border-light)] px-2 py-1 text-[11px] font-medium text-[var(--color-text-primary)] hover:bg-black/5 dark:hover:bg-white/5 disabled:opacity-60 transition-colors"
                  >
                    {loadingSummary ? (
                      <>
                        <Loader2 className="w-3 h-3 animate-spin" />
                        Thinking...
                      </>
                    ) : (
                      <>
                        <Sparkles className="w-3 h-3" />
                        Ask AI
                      </>
                    )}
                  </button>
                )}
              </div>
              {summary && (
                <div className="space-y-2">
                  <p className="text-xs leading-relaxed text-[var(--color-text-secondary)]">
                    {summary}
                  </p>
                  <div className="flex items-center gap-2">
                    {playback === 'playing' ? (
                      <button
                        type="button"
                        onClick={stopAudio}
                        aria-label="Stop spoken summary"
                        className="inline-flex h-6 items-center gap-1 rounded-sm border border-[var(--color-border-light)] px-2 text-[11px] font-medium text-[var(--color-text-primary)] hover:bg-black/5 dark:hover:bg-white/5 transition-colors"
                      >
                        <Square className="w-3 h-3 fill-current" />
                        Stop
                      </button>
                    ) : (
                      <button
                        type="button"
                        onClick={() => void speak()}
                        disabled={playback === 'loading'}
                        aria-label="Play spoken summary"
                        className="inline-flex h-6 items-center gap-1 rounded-sm border border-[var(--color-border-light)] px-2 text-[11px] font-medium text-[var(--color-text-primary)] hover:bg-black/5 dark:hover:bg-white/5 disabled:opacity-60 transition-colors"
                      >
                        {playback === 'loading' ? (
                          <Loader2 className="w-3 h-3 animate-spin" />
                        ) : (
                          <Volume2 className="w-3 h-3" />
                        )}
                        Listen
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => void explain()}
                      disabled={loadingSummary}
                      className="text-[11px] text-[var(--color-text-tertiary)] underline underline-offset-2 hover:text-[var(--color-text-primary)] disabled:opacity-60"
                    >
                      Regenerate
                    </button>
                  </div>
                </div>
              )}
              {error && (
                <p className="text-[11px] text-red-600 dark:text-red-400">{error}</p>
              )}
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

function resolveErrorMessage(e: unknown): string | null {
  if (e && typeof e === 'object') {
    const asObj = e as { userMessage?: unknown; message?: unknown };
    if (typeof asObj.userMessage === 'string') return asObj.userMessage;
    if (typeof asObj.message === 'string') return asObj.message;
  }
  return null;
}

function Callout({ level, children }: { level: PanelCalloutLevel; children: ReactNode }) {
  const Icon = level === 'good' ? CheckCircle2 : level === 'info' ? Info : AlertTriangle;
  const iconColor =
    level === 'good'
      ? 'text-emerald-500'
      : level === 'critical'
        ? 'text-red-500'
        : level === 'warning'
          ? 'text-amber-500'
          : 'text-[var(--color-brand-primary)]';
  return (
    <div className="flex items-start gap-1.5 text-[11px] text-[var(--color-text-secondary)] [&_strong]:text-[var(--color-text-primary)] [&_strong]:font-semibold">
      <Icon className={cn('w-3 h-3 mt-0.5 shrink-0', iconColor)} />
      <span>{children}</span>
    </div>
  );
}
