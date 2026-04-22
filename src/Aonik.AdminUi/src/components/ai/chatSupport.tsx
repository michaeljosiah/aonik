/* eslint-disable react-refresh/only-export-components */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BarChart3,
  Bot,
  Check,
  ShieldAlert,
  ShieldCheck,
  ShieldX,
  TrendingDown,
  TrendingUp,
  ArrowUpDown,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { textToSpeechSettingsService } from '@/services/textToSpeechSettingsService';
import type { PlaygroundFrontendToolRegistration } from '@/lib/playground-client';
import {
  createPlaygroundFrontendTools,
} from '@/pages/ai/playground/frontendTools';

export interface VoiceRenderDetails {
  speechText: string;
  provider: string | null;
  voiceId: string | null;
  aiRunId: string | null;
}

export interface SpeechRenderPayload {
  messageId: string;
  speechText: string;
  requiresVisualAttention: boolean;
  requiresApproval: boolean;
}

export interface SpeechChunkPayload {
  messageId: string;
  chunkIndex: number;
  speechText: string;
  isFinal: boolean;
}

export interface OptionSelectionState {
  question: string;
  options: Array<{ label: string; description?: string }>;
  multiSelect: boolean;
}

export interface NavigateToScreenArgs {
  screen: string;
  params?: Record<string, unknown>;
}

export type SharedToolStatus =
  | 'streaming'
  | 'pending'
  | 'executing'
  | 'awaiting-approval'
  | 'awaiting-selection'
  | 'completed'
  | 'error';

export interface SharedToolCallViewModel {
  toolCallId: string;
  toolCallName: string;
  args: string;
  status: SharedToolStatus;
  result?: string;
  error?: string;
  approval?: {
    action: string;
    description: string;
    severity: 'low' | 'medium' | 'high';
  };
  optionSelection?: OptionSelectionState;
}

function pickString(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : null;
}

function parseNavigationArgs(args: Record<string, unknown>): NavigateToScreenArgs | null {
  const screen =
    pickString(args.screen)
    ?? pickString(args.screenId)
    ?? pickString(args.destination)
    ?? pickString(args.route);

  if (!screen) {
    return null;
  }

  const params =
    typeof args.params === 'object' && args.params !== null
      ? (args.params as Record<string, unknown>)
      : undefined;

  return { screen, params };
}

function resolveAdminChatRoute(navigation: NavigateToScreenArgs): string | null {
  const userId = pickString(navigation.params?.userId) ?? pickString(navigation.params?.partyId);

  switch (navigation.screen) {
    case 'spending-accounts-upload-statement':
      return userId ? `/customers/${userId}` : '/customers';
    case 'spending-transaction-detail':
      return null;
    default:
      return null;
  }
}

function resolveVoiceErrorMessage(error: unknown): string {
  const userMessage =
    typeof error === 'object' && error && 'userMessage' in error && typeof (error as { userMessage?: unknown }).userMessage === 'string'
      ? (error as { userMessage: string }).userMessage
      : error instanceof Error
        ? error.message
        : null;

  if (userMessage && userMessage !== 'The service is unavailable right now. Please try again shortly.') {
    return userMessage;
  }

  return 'Voice synthesis is unavailable right now. Check the provider quota or credentials and try again.';
}

function getBrowserSpeechSynthesis(): SpeechSynthesis | null {
  if (typeof window === 'undefined') {
    return null;
  }

  const synthesis = window.speechSynthesis;
  return synthesis ?? null;
}

function playWithBrowserSpeech(
  speechText: string,
  locale: string,
  onStart: () => void,
  onEnd: () => void,
  onError: (message: string) => void,
): () => void {
  const synthesis = getBrowserSpeechSynthesis();
  if (!synthesis) {
    throw new Error('Browser speech synthesis is unavailable.');
  }

  synthesis.cancel();

  const utterance = new SpeechSynthesisUtterance(speechText);
  utterance.lang = locale;

  utterance.onstart = () => {
    onStart();
  };

  utterance.onend = () => {
    onEnd();
  };

  utterance.onerror = (event) => {
    onError(event.error || 'Browser speech synthesis failed.');
  };

  try {
    synthesis.speak(utterance);
  } catch (error) {
    throw error instanceof Error ? error : new Error('Browser speech synthesis failed.');
  }

  return () => {
    utterance.onstart = null;
    utterance.onend = null;
    utterance.onerror = null;
    synthesis.cancel();
  };
}

export function useAiChatVoicePlayback(options: {
  enabled: boolean;
  isStreaming: boolean;
  speechRender: SpeechRenderPayload | null;
  speechChunks: SpeechChunkPayload[];
}) {
  const { enabled, isStreaming, speechRender, speechChunks } = options;
  const [playbackState, setPlaybackState] = useState<'idle' | 'loading' | 'playing' | 'error'>('idle');
  const [voiceError, setVoiceError] = useState<string | null>(null);
  const [voiceDetails, setVoiceDetails] = useState<VoiceRenderDetails | null>(null);
  const [playedChunkCount, setPlayedChunkCount] = useState(0);

  const previewAudioRef = useRef<HTMLAudioElement | null>(null);
  const previewAudioUrlRef = useRef<string | null>(null);
  const browserSpeechCancelRef = useRef<(() => void) | null>(null);
  const voiceModeEnabledRef = useRef<boolean>(enabled);
  const chunkPlaybackBusyRef = useRef<boolean>(false);
  const guidancePlayedRef = useRef<boolean>(false);
  const synthesizedChunksRef = useRef<
    Map<number, Promise<Awaited<ReturnType<typeof textToSpeechSettingsService.synthesize>>>>
  >(new Map());

  const stopVoicePreview = useCallback(() => {
    browserSpeechCancelRef.current?.();
    browserSpeechCancelRef.current = null;

    const synthesis = getBrowserSpeechSynthesis();
    synthesis?.cancel();

    previewAudioRef.current?.pause();
    previewAudioRef.current?.removeAttribute('src');
    previewAudioRef.current = null;

    if (previewAudioUrlRef.current) {
      URL.revokeObjectURL(previewAudioUrlRef.current);
      previewAudioUrlRef.current = null;
    }

    chunkPlaybackBusyRef.current = false;
    setPlaybackState('idle');
  }, []);

  useEffect(() => {
    voiceModeEnabledRef.current = enabled;
    if (!enabled) {
      stopVoicePreview();
    }
  }, [enabled, stopVoicePreview]);

  useEffect(() => {
    return () => {
      stopVoicePreview();
    };
  }, [stopVoicePreview]);

  useEffect(() => {
    if (isStreaming) {
      setPlayedChunkCount(0);
      guidancePlayedRef.current = false;
      synthesizedChunksRef.current.clear();
    }
  }, [isStreaming]);

  useEffect(() => {
    if (!enabled) return;

    speechChunks.forEach((chunk, index) => {
      if (synthesizedChunksRef.current.has(index)) return;
      const text = chunk.speechText?.trim();
      if (!text) return;

      const promise = textToSpeechSettingsService.synthesize({
        speechText: text,
        locale: 'en-US',
        threadId: `agui-${chunk.messageId || Date.now()}`,
        messageId: chunk.messageId || `chunk-${index}`,
      });
      promise.catch(() => {});
      synthesizedChunksRef.current.set(index, promise);
    });
  }, [enabled, speechChunks]);

  useEffect(() => {
    if (!enabled) return;
    if (chunkPlaybackBusyRef.current) return;

    const nextChunk = speechChunks[playedChunkCount];
    const guidanceText = speechRender?.speechText?.trim() ?? '';
    const shouldPlayGuidance =
      !isStreaming
      && !guidancePlayedRef.current
      && !!speechRender
      && guidanceText.length > 0
      && playedChunkCount >= speechChunks.length;

    if (!nextChunk && !shouldPlayGuidance) return;

    const messageId = nextChunk?.messageId ?? speechRender?.messageId ?? '';
    const speechText = nextChunk?.speechText ?? guidanceText;
    if (!speechText) {
      if (nextChunk) setPlayedChunkCount((n) => n + 1);
      if (shouldPlayGuidance) guidancePlayedRef.current = true;
      return;
    }

    chunkPlaybackBusyRef.current = true;

    const advance = () => {
      chunkPlaybackBusyRef.current = false;
      if (nextChunk) {
        setPlayedChunkCount((n) => n + 1);
      } else {
        guidancePlayedRef.current = true;
      }
      if (voiceModeEnabledRef.current) {
        setPlaybackState('idle');
      }
    };

    const playChunk = async () => {
      setPlaybackState('loading');
      setVoiceError(null);
      setVoiceDetails({
        speechText,
        provider: null,
        voiceId: null,
        aiRunId: null,
      });

      try {
        const locale = 'en-US';

        try {
          let synthesisPromise = nextChunk
            ? synthesizedChunksRef.current.get(playedChunkCount)
            : undefined;
          if (!synthesisPromise) {
            synthesisPromise = textToSpeechSettingsService.synthesize({
              speechText,
              locale,
              threadId: `agui-${messageId || Date.now()}`,
              messageId: messageId || `chunk-${Date.now()}`,
            });
            if (nextChunk) {
              synthesisPromise.catch(() => {});
              synthesizedChunksRef.current.set(playedChunkCount, synthesisPromise);
            }
          }

          const response = await synthesisPromise;

          if (!voiceModeEnabledRef.current) {
            chunkPlaybackBusyRef.current = false;
            return;
          }

          const audioUrl = URL.createObjectURL(response.audioBlob);
          const audio = new Audio(audioUrl);
          previewAudioRef.current = audio;
          previewAudioUrlRef.current = audioUrl;

          audio.onended = () => {
            if (previewAudioUrlRef.current === audioUrl) {
              URL.revokeObjectURL(audioUrl);
              previewAudioUrlRef.current = null;
            }
            previewAudioRef.current = null;
            advance();
          };

          audio.onerror = () => {
            if (previewAudioUrlRef.current === audioUrl) {
              URL.revokeObjectURL(audioUrl);
              previewAudioUrlRef.current = null;
            }
            previewAudioRef.current = null;
            if (voiceModeEnabledRef.current) {
              setPlaybackState('error');
              setVoiceError('Voice playback failed.');
            }
            chunkPlaybackBusyRef.current = false;
          };

          setVoiceDetails({
            speechText,
            provider: response.provider,
            voiceId: response.voiceId,
            aiRunId: response.aiRunId,
          });

          await audio.play();
          if (voiceModeEnabledRef.current) {
            setPlaybackState('playing');
          }
        } catch (primaryError) {
          const fallbackSynthesis = getBrowserSpeechSynthesis();
          if (!fallbackSynthesis) throw primaryError;

          if (!voiceModeEnabledRef.current) {
            chunkPlaybackBusyRef.current = false;
            return;
          }

          const cancelBrowserSpeech = playWithBrowserSpeech(
            speechText,
            locale,
            () => {
              if (voiceModeEnabledRef.current) setPlaybackState('playing');
            },
            () => {
              browserSpeechCancelRef.current = null;
              advance();
            },
            (message) => {
              browserSpeechCancelRef.current = null;
              if (voiceModeEnabledRef.current) {
                setPlaybackState('error');
                setVoiceError(message);
              }
              chunkPlaybackBusyRef.current = false;
            },
          );

          browserSpeechCancelRef.current = cancelBrowserSpeech;
          setVoiceDetails({
            speechText,
            provider: 'Browser',
            voiceId: locale,
            aiRunId: null,
          });
        }
      } catch (error: unknown) {
        if (voiceModeEnabledRef.current) {
          setPlaybackState('error');
          setVoiceError(resolveVoiceErrorMessage(error));
        }
        chunkPlaybackBusyRef.current = false;
      }
    };

    void playChunk();
  }, [enabled, speechChunks, speechRender, isStreaming, playedChunkCount]);

  return {
    playbackState,
    voiceError,
    voiceDetails,
    stopVoicePreview,
  };
}

export function useAiChatFrontendTools(options: {
  enabled: boolean;
  confirmAction: (toolCallId: string, args: { action: string; description: string; severity: 'low' | 'medium' | 'high' }) => Promise<string>;
  selectOptions: (toolCallId: string, args: OptionSelectionState) => Promise<string>;
  includeConfirmAction?: boolean;
  includeDisplayTools?: boolean;
  includeOptionSelector?: boolean;
  includeNavigation?: boolean;
}) {
  const {
    enabled,
    confirmAction,
    selectOptions,
    includeConfirmAction = true,
    includeDisplayTools = true,
    includeOptionSelector = true,
    includeNavigation = false,
  } = options;
  const navigate = useNavigate();

  return useMemo<Map<string, PlaygroundFrontendToolRegistration>>(() => {
    if (!enabled) {
      return new Map();
    }

    const registrations = createPlaygroundFrontendTools({
      confirmAction,
      selectOptions,
      includeConfirmAction,
      includeDisplayTools,
      includeOptionSelector,
    });

    if (includeNavigation) {
      registrations.set('navigate_to_screen', {
        tool: {
          name: 'navigate_to_screen',
          description:
            'Navigate the Admin UI to a relevant screen so the user can continue a guided workflow such as statement upload or transaction review.',
          parameters: {
            type: 'object',
            properties: {
              screen: {
                type: 'string',
                description: 'Logical screen identifier (for example spending-accounts-upload-statement).',
              },
              params: {
                type: 'object',
                description: 'Optional route parameters such as transactionId or userId.',
                additionalProperties: true,
              },
            },
            required: ['screen'],
          },
        },
        handler: async (args) => {
          const navigation = parseNavigationArgs(args);
          if (!navigation) {
            return 'Navigation request ignored.';
          }

          const route = resolveAdminChatRoute(navigation);
          if (route) {
            navigate(route);
            return `Opened ${navigation.screen}.`;
          }

          return `${navigation.screen} is not available in Admin UI yet.`;
        },
      });
    }

    return registrations;
  }, [
    confirmAction,
    enabled,
    includeConfirmAction,
    includeDisplayTools,
    includeNavigation,
    includeOptionSelector,
    navigate,
    selectOptions,
  ]);
}

export function tryParseJsonRecord(value: string): Record<string, unknown> | null {
  try {
    const parsed = JSON.parse(value);
    return typeof parsed === 'object' && parsed !== null ? parsed : null;
  } catch {
    return null;
  }
}

export function AiDisplayToolCard({ toolName, args }: { toolName: string; args: Record<string, unknown> }) {
  switch (toolName) {
    case 'display_budget_breakdown':
      return <BudgetBreakdownVisual args={args} />;
    case 'display_fx_rate_chart':
      return <FxRateChartVisual args={args} />;
    case 'display_spending_pie_chart':
      return <SpendingPieChartVisual args={args} />;
    case 'display_autopilot_proposal':
      return <AutopilotProposalVisual args={args} />;
    default:
      return null;
  }
}

export function AiOptionSelectionCard({
  toolCallId,
  selection,
  onSelect,
}: {
  toolCallId: string;
  selection: OptionSelectionState;
  onSelect?: (toolCallId: string, selected: string[]) => void;
}) {
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const toggleOption = (label: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (selection.multiSelect) {
        if (next.has(label)) next.delete(label);
        else next.add(label);
      } else {
        next.clear();
        next.add(label);
      }
      return next;
    });
  };

  const handleConfirm = () => {
    if (selected.size > 0 && onSelect) {
      onSelect(toolCallId, Array.from(selected));
    }
  };

  return (
    <div className="rounded-md border border-[color-mix(in_srgb,var(--color-info)_20%,transparent)] bg-[var(--color-surface)] p-3 space-y-2.5">
      <p className="text-xs font-semibold text-[var(--color-text-primary)]">
        {selection.question}
      </p>

      <div className="space-y-1">
        {selection.options.map((option) => {
          const isSelected = selected.has(option.label);
          return (
            <button
              key={option.label}
              type="button"
              onClick={() => toggleOption(option.label)}
              className={`flex w-full items-start gap-2 rounded-md border px-3 py-2 text-left text-xs transition-colors ${
                isSelected
                  ? 'border-[var(--color-brand-primary)] bg-[color-mix(in_srgb,var(--color-brand-primary)_8%,transparent)]'
                  : 'border-[var(--color-border-light)] bg-[var(--color-surface)] hover:bg-[var(--color-background)]'
              }`}
            >
              <span className={`mt-0.5 flex h-3.5 w-3.5 shrink-0 items-center justify-center rounded-${selection.multiSelect ? 'sm' : 'full'} border ${
                isSelected
                  ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)]'
                  : 'border-[var(--color-text-tertiary)]'
              }`}>
                {isSelected && <Check className="h-2.5 w-2.5 text-white" />}
              </span>
              <div className="min-w-0">
                <span className={`font-medium ${isSelected ? 'text-[var(--color-text-primary)]' : 'text-[var(--color-text-secondary)]'}`}>
                  {option.label}
                </span>
                {option.description && (
                  <p className="mt-0.5 text-[var(--color-text-tertiary)]">{option.description}</p>
                )}
              </div>
            </button>
          );
        })}
      </div>

      <div className="flex items-center gap-2 pt-1">
        <Button
          size="sm"
          className="h-7 gap-1.5 px-3 text-xs font-medium"
          onClick={handleConfirm}
          disabled={selected.size === 0}
        >
          <Check className="h-3 w-3" />
          Confirm{selected.size > 0 ? ` (${selected.size})` : ''}
        </Button>
      </div>
    </div>
  );
}

const severityConfig = {
  low: {
    label: 'Low risk',
    icon: ShieldCheck,
    badgeClass: 'bg-[color-mix(in_srgb,var(--color-info)_15%,transparent)] text-[var(--color-info)]',
    borderClass: 'border-[color-mix(in_srgb,var(--color-info)_20%,transparent)]',
  },
  medium: {
    label: 'Medium risk',
    icon: ShieldAlert,
    badgeClass: 'bg-[color-mix(in_srgb,var(--color-warning)_15%,transparent)] text-[var(--color-warning)]',
    borderClass: 'border-[color-mix(in_srgb,var(--color-warning)_20%,transparent)]',
  },
  high: {
    label: 'High risk',
    icon: ShieldX,
    badgeClass: 'bg-[color-mix(in_srgb,var(--color-danger)_15%,transparent)] text-[var(--color-danger)]',
    borderClass: 'border-[color-mix(in_srgb,var(--color-danger)_20%,transparent)]',
  },
} as const;

function BudgetBreakdownVisual({ args }: { args: Record<string, unknown> }) {
  const period = String(args.period ?? '');
  const totalBudget = Number(args.totalBudget) || 0;
  const totalSpent = Number(args.totalSpent) || 0;
  const currency = String(args.currency ?? 'USD');
  const categories = Array.isArray(args.categories) ? args.categories : [];
  const spentPct = totalBudget > 0 ? Math.min((totalSpent / totalBudget) * 100, 100) : 0;
  const isOver = totalSpent > totalBudget;

  const fmt = (n: number) => {
    const sym = currency === 'GBP' ? '£' : currency === 'EUR' ? '€' : currency === 'NGN' ? '₦' : '$';
    return `${sym}${n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  return (
    <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] text-xs overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-[var(--color-brand-primary)]" />
          <span className="font-semibold text-[var(--color-text-primary)]">Budget Breakdown</span>
          {period && (
            <span className="rounded bg-[var(--color-surface)] px-1.5 py-0.5 text-[10px] text-[var(--color-text-tertiary)]">
              {period}
            </span>
          )}
        </div>
        <div className="text-right">
          <div className={`text-sm font-bold tabular-nums ${isOver ? 'text-[var(--color-danger)]' : 'text-[var(--color-text-primary)]'}`}>
            {fmt(totalSpent)} <span className="font-normal text-[var(--color-text-tertiary)]">/ {fmt(totalBudget)}</span>
          </div>
        </div>
      </div>

      <div className="px-4 pt-3 pb-1">
        <div className="h-2 w-full rounded-full bg-[var(--color-surface-inset)] overflow-hidden">
          <div
            className={`h-full rounded-full transition-all ${isOver ? 'bg-[var(--color-danger)]' : 'bg-[var(--color-brand-primary)]'}`}
            style={{ width: `${spentPct}%` }}
          />
        </div>
        <div className="mt-1 flex justify-between text-[10px] text-[var(--color-text-tertiary)]">
          <span>{spentPct.toFixed(0)}% used</span>
          <span>{fmt(Math.max(totalBudget - totalSpent, 0))} remaining</span>
        </div>
      </div>

      {categories.length > 0 && (
        <div className="px-4 pb-3 pt-2 space-y-2">
          {categories.map((cat: Record<string, unknown>, i: number) => {
            const name = String(cat.name ?? '');
            const budgeted = Number(cat.budgeted) || 0;
            const spent = Number(cat.spent) || 0;
            const status = String(cat.status ?? 'on_track');
            const catPct = budgeted > 0 ? Math.min((spent / budgeted) * 100, 100) : 0;
            const barColor =
              status === 'over'
                ? 'bg-[var(--color-danger)]'
                : status === 'under'
                  ? 'bg-[var(--color-success)]'
                  : 'bg-[var(--color-brand-primary)]';
            const statusLabel =
              status === 'over' ? 'Over' : status === 'under' ? 'Under' : 'On track';
            const statusColor =
              status === 'over'
                ? 'text-[var(--color-danger)]'
                : status === 'under'
                  ? 'text-[var(--color-success)]'
                  : 'text-[var(--color-text-tertiary)]';

            return (
              <div key={`${name}-${i}`}>
                <div className="flex items-center justify-between mb-1">
                  <span className="font-medium text-[var(--color-text-primary)]">{name}</span>
                  <div className="flex items-center gap-2">
                    <span className="tabular-nums text-[var(--color-text-secondary)]">
                      {fmt(spent)} / {fmt(budgeted)}
                    </span>
                    <span className={`text-[10px] font-medium ${statusColor}`}>{statusLabel}</span>
                  </div>
                </div>
                <div className="h-1.5 w-full rounded-full bg-[var(--color-surface-inset)] overflow-hidden">
                  <div
                    className={`h-full rounded-full transition-all ${barColor}`}
                    style={{ width: `${catPct}%` }}
                  />
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

const PIE_COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#06b6d4', '#f97316', '#14b8a6', '#6366f1'];

function SpendingPieChartVisual({ args }: { args: Record<string, unknown> }) {
  const title = String(args.title ?? 'Spending by Category');
  const currency = String(args.currency ?? 'USD');
  const totalSpent = Number(args.totalSpent) || 0;
  const categories = Array.isArray(args.categories) ? args.categories : [];

  const fmt = (n: number) => {
    const sym = currency === 'GBP' ? '£' : currency === 'EUR' ? '€' : currency === 'NGN' ? '₦' : '$';
    return `${sym}${n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const slices = categories
    .filter((c): c is Record<string, unknown> => typeof c === 'object' && c !== null)
    .map((c, i) => {
      const amount = Number(c.amount) || 0;
      const pct = totalSpent > 0 ? (amount / totalSpent) * 100 : 0;
      return {
        name: String(c.name ?? 'Other'),
        amount,
        percentage: Number(c.percentage) || pct,
        color: PIE_COLORS[i % PIE_COLORS.length],
      };
    })
    .sort((a, b) => b.amount - a.amount);

  const size = 140;
  const cx = size / 2;
  const cy = size / 2;
  const r = 54;
  const ir = 34;

  const pathAccumulator = slices.reduce<{
    currentAngle: number;
    paths: Array<(typeof slices)[number] & { d: string }>;
  }>((accumulator, slice) => {
    const startAngle = accumulator.currentAngle;
    const angle = (slice.percentage / 100) * 360;
    const endAngle = startAngle + angle;

    if (angle >= 359.99) {
      accumulator.paths.push({
        ...slice,
        d: `M${cx},${cy - r} A${r},${r} 0 1,1 ${cx - 0.01},${cy - r} Z M${cx},${cy - ir} A${ir},${ir} 0 1,0 ${cx - 0.01},${cy - ir} Z`,
      });
      accumulator.currentAngle = endAngle;
      return accumulator;
    }

    const startRad = (startAngle * Math.PI) / 180;
    const endRad = (endAngle * Math.PI) / 180;
    const largeArc = angle > 180 ? 1 : 0;

    const x1 = cx + r * Math.cos(startRad);
    const y1 = cy + r * Math.sin(startRad);
    const x2 = cx + r * Math.cos(endRad);
    const y2 = cy + r * Math.sin(endRad);
    const ix1 = cx + ir * Math.cos(endRad);
    const iy1 = cy + ir * Math.sin(endRad);
    const ix2 = cx + ir * Math.cos(startRad);
    const iy2 = cy + ir * Math.sin(startRad);

    accumulator.paths.push({
      ...slice,
      d: `M${x1},${y1} A${r},${r} 0 ${largeArc},1 ${x2},${y2} L${ix1},${iy1} A${ir},${ir} 0 ${largeArc},0 ${ix2},${iy2} Z`,
    });
    accumulator.currentAngle = endAngle;
    return accumulator;
  }, {
    currentAngle: -90,
    paths: [],
  });

  const paths = pathAccumulator.paths;

  return (
    <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] text-xs overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-[var(--color-brand-primary)]" />
          <span className="font-semibold text-[var(--color-text-primary)]">{title}</span>
        </div>
        <span className="text-sm font-bold tabular-nums text-[var(--color-text-primary)]">{fmt(totalSpent)}</span>
      </div>

      <div className="flex items-start gap-6 px-4 py-4">
        <div className="shrink-0">
          <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
            {paths.map((slice, i) => (
              <path key={i} d={slice.d} fill={slice.color} stroke="var(--color-surface)" strokeWidth="1.5" />
            ))}
            <text x={cx} y={cy - 4} textAnchor="middle" className="fill-[var(--color-text-tertiary)]" fontSize="9">
              Total
            </text>
            <text x={cx} y={cy + 10} textAnchor="middle" className="fill-[var(--color-text-primary)] font-semibold" fontSize="12">
              {fmt(totalSpent)}
            </text>
          </svg>
        </div>

        <div className="flex-1 space-y-2 min-w-0 pt-1">
          {slices.map((slice, i) => (
            <div key={i} className="flex items-center gap-2">
              <span className="h-2.5 w-2.5 shrink-0 rounded-sm" style={{ backgroundColor: slice.color }} />
              <span className="truncate text-[var(--color-text-secondary)] flex-1">{slice.name}</span>
              <span className="tabular-nums font-medium text-[var(--color-text-primary)] shrink-0">{fmt(slice.amount)}</span>
              <span className="tabular-nums text-[var(--color-text-tertiary)] shrink-0 w-10 text-right">{slice.percentage.toFixed(0)}%</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function FxRateChartVisual({ args }: { args: Record<string, unknown> }) {
  const baseCurrency = String(args.baseCurrency ?? '');
  const targetCurrency = String(args.targetCurrency ?? '');
  const rates = Array.isArray(args.rates) ? args.rates : [];
  const signal = String(args.signal ?? '');
  const signalReason = String(args.signalReason ?? '');

  const rateValues = rates
    .map((r: Record<string, unknown>) => Number(r.rate))
    .filter((v) => Number.isFinite(v));
  const minRate = rateValues.length > 0 ? Math.min(...rateValues) : 0;
  const maxRate = rateValues.length > 0 ? Math.max(...rateValues) : 0;
  const range = maxRate - minRate || 1;
  const latestRate = rateValues.length > 0 ? rateValues[rateValues.length - 1] : 0;

  const signalConfig = {
    buy: { label: 'Buy now', color: 'text-[var(--color-success)]', bg: 'bg-[color-mix(in_srgb,var(--color-success)_12%,transparent)]', Icon: TrendingDown },
    hold: { label: 'Hold', color: 'text-[var(--color-warning)]', bg: 'bg-[color-mix(in_srgb,var(--color-warning)_12%,transparent)]', Icon: ArrowUpDown },
    wait: { label: 'Wait', color: 'text-[var(--color-info)]', bg: 'bg-[color-mix(in_srgb,var(--color-info)_12%,transparent)]', Icon: TrendingUp },
  }[signal] ?? { label: signal, color: 'text-[var(--color-text-secondary)]', bg: 'bg-[var(--color-surface-inset)]', Icon: ArrowUpDown };

  return (
    <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] text-xs overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
        <div className="flex items-center gap-2">
          <TrendingUp className="h-4 w-4 text-[var(--color-brand-primary)]" />
          <span className="font-semibold text-[var(--color-text-primary)]">{baseCurrency}/{targetCurrency} Rate</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm font-bold tabular-nums text-[var(--color-text-primary)]">
            {latestRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}
          </span>
          <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${signalConfig.color} ${signalConfig.bg}`}>
            <signalConfig.Icon className="h-3 w-3" />
            {signalConfig.label}
          </span>
        </div>
      </div>

      {rates.length > 1 && (
        <div className="px-4 pt-3 pb-1">
          <div className="relative h-16 w-full">
            <svg viewBox={`0 0 ${(rates.length - 1) * 40} 60`} className="h-full w-full" preserveAspectRatio="none">
              <path
                d={
                  rates
                    .map((r: Record<string, unknown>, i: number) => {
                      const x = i * 40;
                      const y = 56 - ((Number(r.rate) - minRate) / range) * 52;
                      return `${i === 0 ? 'M' : 'L'}${x},${y}`;
                    })
                    .join(' ') + ` L${(rates.length - 1) * 40},58 L0,58 Z`
                }
                fill="var(--color-brand-primary)"
                opacity="0.08"
              />
              <path
                d={rates
                  .map((r: Record<string, unknown>, i: number) => {
                    const x = i * 40;
                    const y = 56 - ((Number(r.rate) - minRate) / range) * 52;
                    return `${i === 0 ? 'M' : 'L'}${x},${y}`;
                  })
                  .join(' ')}
                fill="none"
                stroke="var(--color-brand-primary)"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
          <div className="flex justify-between text-[10px] text-[var(--color-text-tertiary)] mt-1">
            {rates.length > 0 && <span>{String((rates[0] as Record<string, unknown>).date ?? '')}</span>}
            {rates.length > 1 && <span>{String((rates[rates.length - 1] as Record<string, unknown>).date ?? '')}</span>}
          </div>
        </div>
      )}

      {signalReason && (
        <div className="px-4 pb-3 pt-1">
          <p className="text-[var(--color-text-secondary)] leading-relaxed">{signalReason}</p>
        </div>
      )}
    </div>
  );
}

function AutopilotProposalVisual({ args }: { args: Record<string, unknown> }) {
  const agent = String(args.agent ?? '');
  const action = String(args.action ?? '');
  const description = String(args.description ?? '');
  const details = Array.isArray(args.details) ? args.details : [];
  const severity = String(args.severity ?? 'medium') as 'low' | 'medium' | 'high';
  const config = severityConfig[severity] ?? severityConfig.medium;
  const SeverityIcon = config.icon;

  return (
    <div className={`rounded-lg border ${config.borderClass} bg-[var(--color-surface)] text-xs overflow-hidden`}>
      <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
        <div className="flex items-center gap-2">
          <Bot className="h-4 w-4 text-[var(--color-brand-primary)]" />
          <span className="font-semibold text-[var(--color-text-primary)]">{action}</span>
        </div>
        <div className="flex items-center gap-2">
          {agent && (
            <span className="rounded bg-[var(--color-surface)] px-1.5 py-0.5 text-[10px] text-[var(--color-text-tertiary)]">
              {agent}
            </span>
          )}
          <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${config.badgeClass}`}>
            <SeverityIcon className="h-3 w-3" />
            {config.label}
          </span>
        </div>
      </div>

      <div className="px-4 py-3 space-y-3">
        <p className="text-[var(--color-text-secondary)] leading-relaxed">{description}</p>

        {details.length > 0 && (
          <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] divide-y divide-[var(--color-border-light)]">
            {details.map((d: Record<string, unknown>, i: number) => (
              <div key={i} className="flex items-center justify-between px-3 py-2">
                <span className="text-[var(--color-text-tertiary)]">{String(d.label ?? '')}</span>
                <span className="font-medium text-[var(--color-text-primary)]">{String(d.value ?? '')}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
