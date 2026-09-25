/* eslint-disable react-refresh/only-export-components */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ArrowUpDown,
  BarChart3,
  Bot,
  Check,
  CheckCircle2,
  Loader2,
  ShieldAlert,
  ShieldCheck,
  ShieldX,
  TrendingDown,
  TrendingUp,
  XCircle,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
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

export interface FollowUpSuggestionsState {
  prompt?: string;
  suggestions: Array<{ label: string; prompt: string; description?: string }>;
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
  followUpSuggestions?: FollowUpSuggestionsState;
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

// ─── Server Approval Card (Spec 032) ──────────────────────────────────────────
// A backend-gated mutation awaiting the user's decision, shared by every agent
// surface (AG-UI chat, admin playground). Unlike the legacy `confirmAction`
// frontend-tool flow, the decision is routed to the server — the durable
// ToolApprovalRequest (Medium) or Proposal (High) is the authority; this card
// only presents and collects. Driven by the `tool.approval.required` /
// `tool.approval.queued` CUSTOM events the gate emits, decided via
// `POST /ai/tool-approvals/{id}/decide`.

/** Lifecycle of a server-owned approval card from the user's point of view. */
export type ServerApprovalStatus = 'pending' | 'deciding' | 'approved' | 'rejected' | 'error';

/**
 * Presentation state for a server-owned tool-approval card. Carried as a chat
 * message (AG-UI) or an output part (playground); the same shape feeds the same
 * {@link ServerApprovalCard} on both surfaces so the two stay pixel-identical.
 */
export interface ServerApprovalState {
  id: string;
  /** Medium (in-session confirm) vs High (durable money proposal). */
  kind: 'medium' | 'high';
  /** Set for both tiers — the durable ToolApprovalRequest the decision routes to. */
  approvalRequestId?: string;
  /** Set for High — the durable Proposal that executes on approval. Reference only. */
  proposalId?: string;
  toolCallId?: string;
  tool: string;
  /** Risk-tier label as emitted by the server ("Medium" / "High"). */
  tier: string;
  actionKind: string;
  status: ServerApprovalStatus;
  message?: string;
}

/**
 * Risk-tier presentation shared by the approval cards (client `confirmAction`
 * and the Spec 032 server-owned card): badge variant, card border, icon, label.
 */
export const approvalSeverityConfig = {
  low: {
    badge: 'info',
    border: 'border-info/25',
    icon: <ShieldAlert className="h-4 w-4 text-info" />,
    label: 'Low risk',
  },
  medium: {
    badge: 'warning',
    border: 'border-warning/25',
    icon: <ShieldAlert className="h-4 w-4 text-warning" />,
    label: 'Medium risk',
  },
  high: {
    badge: 'destructive',
    border: 'border-destructive/25',
    icon: <ShieldAlert className="h-4 w-4 text-destructive" />,
    label: 'High risk',
  },
} as const;

/** Resolved approval summary: an Alert in the success or destructive tone. */
export function ApprovalResolvedAlert({
  approved,
  title,
  detail,
}: {
  approved: boolean;
  title: string;
  detail?: string;
}) {
  return (
    <Alert variant={approved ? 'success' : 'destructive'}>
      {approved ? <ShieldCheck /> : <ShieldX />}
      <AlertTitle className="line-clamp-none">
        {title} — {approved ? 'Approved' : 'Rejected'}
      </AlertTitle>
      {detail && <AlertDescription className="text-xs">{detail}</AlertDescription>}
    </Alert>
  );
}

export function ServerApprovalCard({
  approval,
  onDecide,
}: {
  approval: ServerApprovalState;
  onDecide?: (approval: ServerApprovalState, decision: 'Approve' | 'Reject') => void;
}) {
  const severity: 'low' | 'medium' | 'high' =
    approval.tier.toLowerCase() === 'high'
      ? 'high'
      : approval.tier.toLowerCase() === 'low'
        ? 'low'
        : 'medium';
  const config = approvalSeverityConfig[severity];

  // Resolved states — a compact summary line.
  if (approval.status === 'approved' || approval.status === 'rejected') {
    return (
      <ApprovalResolvedAlert
        approved={approval.status === 'approved'}
        title={approval.actionKind}
        detail={approval.message}
      />
    );
  }

  const isDeciding = approval.status === 'deciding';
  const canDecide = !!onDecide && (approval.status === 'pending' || approval.status === 'error');

  return (
    <div className={`rounded-lg border-2 ${config.border} bg-card overflow-hidden`}>
      {/* Header */}
      <div className="flex items-center gap-2 px-4 py-2.5 bg-muted border-b border-border">
        {config.icon}
        <span className="font-semibold text-sm text-foreground">
          {approval.kind === 'high' ? 'Approval required — money movement' : 'Approval required'}
        </span>
        <Badge variant={config.badge} className="ml-auto">
          {config.label}
        </Badge>
      </div>

      {/* Body */}
      <div className="px-4 py-3">
        <div className="font-medium text-sm text-foreground">{approval.actionKind}</div>
        <div className="mt-1 text-xs text-muted-foreground leading-relaxed">
          {approval.kind === 'high'
            ? 'This action moves money and runs only after you approve it. It is queued as a durable proposal.'
            : 'This action needs your explicit approval before it runs.'}
        </div>
        {approval.status === 'error' && approval.message && (
          <div className="mt-2 text-xs text-destructive">{approval.message}</div>
        )}
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2 px-4 py-2.5 border-t border-border bg-muted">
        {isDeciding ? (
          <span className="inline-flex items-center gap-2 text-xs text-muted-foreground">
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
            Recording your decision…
          </span>
        ) : (
          <>
            <Button
              type="button"
              size="sm"
              variant="agent"
              disabled={!canDecide}
              onClick={() => onDecide?.(approval, 'Approve')}
            >
              <CheckCircle2 />
              Approve
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={!canDecide}
              onClick={() => onDecide?.(approval, 'Reject')}
              className="text-destructive hover:text-destructive"
            >
              <XCircle />
              Reject
            </Button>
          </>
        )}
      </div>
    </div>
  );
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

export function parseFollowUpSuggestions(
  args: Record<string, unknown>,
): FollowUpSuggestionsState | null {
  const suggestions = Array.isArray(args.suggestions)
    ? args.suggestions
        .filter((item): item is Record<string, unknown> => typeof item === 'object' && item !== null)
        .map((item) => ({
          label: pickString(item.label) ?? '',
          prompt: pickString(item.prompt) ?? '',
          description: pickString(item.description) ?? undefined,
        }))
        .filter((item) => item.label.length > 0 && item.prompt.length > 0)
    : [];

  if (suggestions.length === 0) {
    return null;
  }

  return {
    prompt: pickString(args.prompt) ?? undefined,
    suggestions,
  };
}

export function AiFollowUpSuggestionsCard({
  suggestions,
  onSelect,
}: {
  suggestions: FollowUpSuggestionsState;
  onSelect?: (prompt: string) => void;
}) {
  return (
    <div className="rounded-lg border border-border bg-card px-4 py-3 text-sm">
      {suggestions.prompt && (
        <div className="mb-3 text-xs font-semibold text-foreground">
          {suggestions.prompt}
        </div>
      )}
      <div className="flex flex-wrap gap-2">
        {suggestions.suggestions.map((item) => (
          <Button
            key={`${item.label}-${item.prompt}`}
            type="button"
            variant="outline"
            size="sm"
            onClick={() => onSelect?.(item.prompt)}
            className="h-auto rounded-full border-primary/20 bg-primary/10 py-1.5 text-xs text-primary shadow-none hover:bg-primary/15 hover:text-primary dark:border-primary/20 dark:bg-primary/10 dark:hover:bg-primary/15"
          >
            {item.label}
          </Button>
        ))}
      </div>
    </div>
  );
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
    <div className="rounded-lg border border-info/20 bg-card p-3 space-y-2.5">
      <p className="text-xs font-semibold text-foreground">
        {selection.question}
      </p>

      {selection.multiSelect ? (
        <div className="space-y-1">
          {selection.options.map((option) => {
            const isSelected = selected.has(option.label);
            return (
              <label key={option.label} className={optionRowClass(isSelected)}>
                <Checkbox
                  checked={isSelected}
                  onCheckedChange={() => toggleOption(option.label)}
                  className="mt-0.5"
                />
                <OptionText option={option} isSelected={isSelected} />
              </label>
            );
          })}
        </div>
      ) : (
        <RadioGroup
          value={Array.from(selected)[0] ?? ''}
          onValueChange={(value) => toggleOption(value)}
          className="gap-1"
        >
          {selection.options.map((option) => {
            const isSelected = selected.has(option.label);
            return (
              <label key={option.label} className={optionRowClass(isSelected)}>
                <RadioGroupItem value={option.label} className="mt-0.5" />
                <OptionText option={option} isSelected={isSelected} />
              </label>
            );
          })}
        </RadioGroup>
      )}

      <div className="flex items-center gap-2 pt-1">
        <Button
          size="sm"
          onClick={handleConfirm}
          disabled={selected.size === 0}
        >
          <Check />
          Confirm{selected.size > 0 ? ` (${selected.size})` : ''}
        </Button>
      </div>
    </div>
  );
}

function optionRowClass(isSelected: boolean): string {
  return `flex w-full cursor-pointer items-start gap-2 rounded-md border px-3 py-2 text-left text-xs transition-colors ${
    isSelected ? 'border-primary bg-primary/10' : 'border-border bg-card hover:bg-accent'
  }`;
}

function OptionText({
  option,
  isSelected,
}: {
  option: OptionSelectionState['options'][number];
  isSelected: boolean;
}) {
  return (
    <div className="min-w-0">
      <span className={`font-medium ${isSelected ? 'text-foreground' : 'text-muted-foreground'}`}>
        {option.label}
      </span>
      {option.description && (
        <p className="mt-0.5 text-muted-foreground">{option.description}</p>
      )}
    </div>
  );
}

const severityConfig = {
  low: {
    label: 'Low risk',
    icon: ShieldCheck,
    badge: 'info',
    borderClass: 'border-info/20',
  },
  medium: {
    label: 'Medium risk',
    icon: ShieldAlert,
    badge: 'warning',
    borderClass: 'border-warning/20',
  },
  high: {
    label: 'High risk',
    icon: ShieldX,
    badge: 'destructive',
    borderClass: 'border-destructive/20',
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
    <div className="rounded-lg border border-border bg-card text-xs overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">Budget breakdown</span>
          {period && (
            <Badge variant="outline" className="bg-card font-normal text-muted-foreground">
              {period}
            </Badge>
          )}
        </div>
        <div className="text-right">
          <div className={`font-mono text-sm font-bold tabular-nums ${isOver ? 'text-destructive' : 'text-foreground'}`}>
            {fmt(totalSpent)} <span className="font-normal text-muted-foreground">/ {fmt(totalBudget)}</span>
          </div>
        </div>
      </div>

      <div className="px-4 pt-3 pb-1">
        <div className="h-2 w-full rounded-full bg-muted overflow-hidden">
          <div
            className={`h-full rounded-full transition-all ${isOver ? 'bg-destructive' : 'bg-primary'}`}
            style={{ width: `${spentPct}%` }}
          />
        </div>
        <div className="mt-1 flex justify-between text-[10px] text-muted-foreground">
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
                ? 'bg-destructive'
                : status === 'under'
                  ? 'bg-success'
                  : 'bg-primary';
            const statusLabel =
              status === 'over' ? 'Over' : status === 'under' ? 'Under' : 'On track';
            const statusColor =
              status === 'over'
                ? 'text-destructive'
                : status === 'under'
                  ? 'text-success'
                  : 'text-muted-foreground';

            return (
              <div key={`${name}-${i}`}>
                <div className="flex items-center justify-between mb-1">
                  <span className="font-medium text-foreground">{name}</span>
                  <div className="flex items-center gap-2">
                    <span className="font-mono tabular-nums text-muted-foreground">
                      {fmt(spent)} / {fmt(budgeted)}
                    </span>
                    <span className={`text-[10px] font-medium ${statusColor}`}>{statusLabel}</span>
                  </div>
                </div>
                <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
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

// Categorical series: the five chart tokens, then lighter tints of the same
// five so up to ten slices stay distinguishable and flip with the theme.
const PIE_COLORS = [
  'var(--chart-1)',
  'var(--chart-2)',
  'var(--chart-3)',
  'var(--chart-4)',
  'var(--chart-5)',
  'color-mix(in oklab, var(--chart-1) 55%, var(--card))',
  'color-mix(in oklab, var(--chart-2) 55%, var(--card))',
  'color-mix(in oklab, var(--chart-3) 55%, var(--card))',
  'color-mix(in oklab, var(--chart-4) 55%, var(--card))',
  'color-mix(in oklab, var(--chart-5) 55%, var(--card))',
];

function SpendingPieChartVisual({ args }: { args: Record<string, unknown> }) {
  const title = String(args.title ?? 'Spending by category');
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
    <div className="rounded-lg border border-border bg-card text-xs overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">{title}</span>
        </div>
        <span className="font-mono text-sm font-bold tabular-nums text-foreground">{fmt(totalSpent)}</span>
      </div>

      <div className="flex items-start gap-6 px-4 py-4">
        <div className="shrink-0">
          <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
            {paths.map((slice, i) => (
              <path key={i} d={slice.d} fill={slice.color} stroke="var(--card)" strokeWidth="1.5" />
            ))}
            <text x={cx} y={cy - 4} textAnchor="middle" className="fill-muted-foreground" fontSize="9">
              Total
            </text>
            <text x={cx} y={cy + 10} textAnchor="middle" className="fill-foreground font-semibold" fontSize="12">
              {fmt(totalSpent)}
            </text>
          </svg>
        </div>

        <div className="flex-1 space-y-2 min-w-0 pt-1">
          {slices.map((slice, i) => (
            <div key={i} className="flex items-center gap-2">
              <span className="h-2.5 w-2.5 shrink-0 rounded-sm" style={{ backgroundColor: slice.color }} />
              <span className="truncate text-muted-foreground flex-1">{slice.name}</span>
              <span className="font-mono tabular-nums font-medium text-foreground shrink-0">{fmt(slice.amount)}</span>
              <span className="font-mono tabular-nums text-muted-foreground shrink-0 w-10 text-right">{slice.percentage.toFixed(0)}%</span>
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
    buy: { label: 'Buy now', badge: 'success' as const, Icon: TrendingDown },
    hold: { label: 'Hold', badge: 'warning' as const, Icon: ArrowUpDown },
    wait: { label: 'Wait', badge: 'info' as const, Icon: TrendingUp },
  }[signal] ?? { label: signal, badge: 'secondary' as const, Icon: ArrowUpDown };

  return (
    <div className="rounded-lg border border-border bg-card text-xs overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <TrendingUp className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">{baseCurrency}/{targetCurrency} rate</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="font-mono text-sm font-bold tabular-nums text-foreground">
            {latestRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}
          </span>
          <Badge variant={signalConfig.badge}>
            <signalConfig.Icon />
            {signalConfig.label}
          </Badge>
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
                fill="var(--primary)"
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
                stroke="var(--primary)"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
          <div className="flex justify-between text-[10px] text-muted-foreground mt-1">
            {rates.length > 0 && <span>{String((rates[0] as Record<string, unknown>).date ?? '')}</span>}
            {rates.length > 1 && <span>{String((rates[rates.length - 1] as Record<string, unknown>).date ?? '')}</span>}
          </div>
        </div>
      )}

      {signalReason && (
        <div className="px-4 pb-3 pt-1">
          <p className="text-muted-foreground leading-relaxed">{signalReason}</p>
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
    <div className={`rounded-lg border ${config.borderClass} bg-card text-xs overflow-hidden`}>
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <Bot className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">{action}</span>
        </div>
        <div className="flex items-center gap-2">
          {agent && (
            <Badge variant="outline" className="bg-card font-normal text-muted-foreground">
              {agent}
            </Badge>
          )}
          <Badge variant={config.badge}>
            <SeverityIcon />
            {config.label}
          </Badge>
        </div>
      </div>

      <div className="px-4 py-3 space-y-3">
        <p className="text-muted-foreground leading-relaxed">{description}</p>

        {details.length > 0 && (
          <div className="rounded-md border border-border bg-muted divide-y divide-border">
            {details.map((d: Record<string, unknown>, i: number) => (
              <div key={i} className="flex items-center justify-between px-3 py-2">
                <span className="text-muted-foreground">{String(d.label ?? '')}</span>
                <span className="font-medium text-foreground">{String(d.value ?? '')}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
