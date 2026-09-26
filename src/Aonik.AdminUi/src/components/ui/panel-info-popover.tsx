import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Info,
  Loader2,
  Sparkles,
  Square,
  Volume2,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
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
  /** Enables persistent auto-play of generated insight audio for this panel. */
  voiceModeStorageKey?: string;
}

type PlaybackState = 'idle' | 'loading' | 'playing' | 'error';

export function PanelInfoPopover({
  title,
  description,
  callouts,
  panelKind,
  getMetrics,
  triggerLabel,
  voiceModeStorageKey,
}: PanelInfoPopoverProps) {
  const [summary, setSummary] = useState<string | null>(null);
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [playback, setPlayback] = useState<PlaybackState>('idle');
  const [error, setError] = useState<string | null>(null);
  const [voiceModeEnabled, setVoiceModeEnabled] = useState(() => {
    if (typeof window === 'undefined' || !voiceModeStorageKey) return false;

    try {
      return window.localStorage.getItem(voiceModeStorageKey) === 'true';
    } catch {
      return false;
    }
  });
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioUrlRef = useRef<string | null>(null);
  const autoSpokenSummaryRef = useRef<string | null>(null);

  useEffect(() => {
    if (typeof window === 'undefined' || !voiceModeStorageKey) return;

    try {
      window.localStorage.setItem(voiceModeStorageKey, voiceModeEnabled ? 'true' : 'false');
    } catch {
      /* localStorage disabled - non-fatal */
    }
  }, [voiceModeEnabled, voiceModeStorageKey]);

  const stopAudio = useCallback(() => {
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
  }, []);

  const explain = async () => {
    if (!panelKind || !getMetrics) return;
    setLoadingSummary(true);
    setError(null);
    setSummary(null);
    autoSpokenSummaryRef.current = null;
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

  const speak = useCallback(async () => {
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
  }, [summary, stopAudio]);

  useEffect(() => {
    if (!voiceModeEnabled || !summary || loadingSummary) return;
    if (autoSpokenSummaryRef.current === summary) return;

    autoSpokenSummaryRef.current = summary;
    void speak();
  }, [loadingSummary, speak, summary, voiceModeEnabled]);

  const canExplain = Boolean(panelKind && getMetrics);

  return (
    <Popover
      onOpenChange={(open) => {
        if (!open) stopAudio();
      }}
    >
      <PopoverTrigger asChild>
        {triggerLabel ? (
          <Button variant="outline" size="sm" aria-label={triggerLabel}>
            <Sparkles />
            {triggerLabel}
          </Button>
        ) : (
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label={`About ${title}`}
            className="size-6 rounded-full text-muted-foreground hover:text-foreground"
          >
            <Info className="size-3.5" />
          </Button>
        )}
      </PopoverTrigger>
      <PopoverContent align="start" className="max-h-[36rem] w-[28rem] max-w-[calc(100vw-2rem)] overflow-y-auto">
        <div className="flex flex-col gap-3">
          <h3 className="text-sm font-semibold">{title}</h3>
          <div className="space-y-2 text-xs leading-relaxed text-muted-foreground [&_strong]:font-semibold [&_strong]:text-foreground [&_ul]:list-disc [&_ul]:space-y-1 [&_ul]:pl-4">
            {description}
          </div>
          {callouts && callouts.length > 0 && (
            <div className="flex flex-col gap-1.5 border-t pt-3">
              <p className="text-xs font-medium">What your data shows</p>
              {callouts.map((c, i) => (
                <Callout key={i} level={c.level}>
                  {c.message}
                </Callout>
              ))}
            </div>
          )}
          {canExplain && (
            <div className="flex flex-col gap-2 border-t pt-3">
              <div className="flex items-center justify-between gap-2">
                <p className="text-xs font-medium">Explain my data</p>
                <div className="flex items-center gap-2">
                  {voiceModeStorageKey ? (
                    <label className="inline-flex items-center gap-2 text-xs text-muted-foreground">
                      <Volume2 className="size-3.5" />
                      Voice mode
                      <Switch
                        checked={voiceModeEnabled}
                        onCheckedChange={(checked) => {
                          setVoiceModeEnabled(checked);
                          if (!checked) stopAudio();
                        }}
                        aria-label="Voice mode"
                      />
                    </label>
                  ) : null}
                  {!summary && (
                    <Button variant="outline" size="sm" onClick={() => void explain()} disabled={loadingSummary}>
                      {loadingSummary ? <Loader2 className="animate-spin" /> : <Sparkles />}
                      Ask AI
                    </Button>
                  )}
                </div>
              </div>
              {summary && (
                <div className="flex flex-col gap-2">
                  <p className="text-xs leading-relaxed text-muted-foreground">{summary}</p>
                  <div className="flex items-center gap-2">
                    {playback === 'playing' ? (
                      <Button variant="outline" size="sm" onClick={stopAudio} aria-label="Stop spoken summary">
                        <Square className="fill-current" />
                        Stop
                      </Button>
                    ) : (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => void speak()}
                        disabled={playback === 'loading'}
                        aria-label="Play spoken summary"
                      >
                        {playback === 'loading' ? <Loader2 className="animate-spin" /> : <Volume2 />}
                        Listen
                      </Button>
                    )}
                    <Button variant="link" size="sm" onClick={() => void explain()} disabled={loadingSummary} className="px-1 text-muted-foreground">
                      Regenerate
                    </Button>
                  </div>
                </div>
              )}
              {error && <p className="text-xs text-destructive">{error}</p>}
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
      ? 'text-success'
      : level === 'critical'
        ? 'text-destructive'
        : level === 'warning'
          ? 'text-warning'
          : 'text-info';
  return (
    <div className="flex items-start gap-2 text-xs text-muted-foreground [&_strong]:font-semibold [&_strong]:text-foreground">
      <Icon className={cn('mt-0.5 size-3.5 shrink-0', iconColor)} />
      <span>{children}</span>
    </div>
  );
}
