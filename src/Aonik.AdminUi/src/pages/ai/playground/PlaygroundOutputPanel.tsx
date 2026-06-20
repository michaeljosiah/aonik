import { useCallback, useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import {
  ArrowDownToLine,
  ArrowUpDown,
  BarChart3,
  Bot,
  Brain,
  Check,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  GripHorizontal,
  Lightbulb,
  Loader2,
  ShieldAlert,
  ShieldCheck,
  ShieldX,
  Sparkles,
  Square,
  Star,
  Target,
  TrendingDown,
  TrendingUp,
  Volume2,
  Wrench,
  X,
  XCircle,
  Zap,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Collapsible,
  CollapsibleTrigger,
  CollapsibleContent,
} from '@/components/ui/collapsible';
import type { PlaygroundRunMetrics } from '@/lib/playground-client';
import type { PlaygroundOutputPart, PlaygroundToolCall } from '@/hooks/usePlaygroundChat';
import type { PlaygroundReviewResult } from '@/types/ai';
import {
  AiFollowUpSuggestionsCard,
  parseFollowUpSuggestions,
  ServerApprovalCard,
  type ServerApprovalState,
} from '@/components/ai/chatSupport';

interface PlaygroundOutputPanelProps {
  output: string;
  outputParts: PlaygroundOutputPart[];
  isStreaming: boolean;
  streamError: string | null;
  metrics: PlaygroundRunMetrics | null;
  modelName?: string | null;
  voiceModeEnabled?: boolean;
  voicePlaybackState?: 'idle' | 'loading' | 'playing' | 'error';
  voiceError?: string | null;
  voiceDetails?: {
    speechText: string;
    provider: string | null;
    voiceId: string | null;
    aiRunId: string | null;
  } | null;
  onStopVoice?: () => void;
  onAddToMessages?: () => void;
  // Review props
  isReviewing?: boolean;
  reviewResult?: PlaygroundReviewResult | null;
  reviewRawText?: string | null;
  reviewError?: string | null;
  onReview?: () => void;
  // Tool approval/selection callbacks
  onApproveToolCall?: (toolCallId: string) => void;
  onRejectToolCall?: (toolCallId: string) => void;
  onSelectToolCallOptions?: (toolCallId: string, selected: string[]) => void;
  onSelectFollowUpSuggestion?: (prompt: string) => void;
  /** Spec 032 — record a decision for a server-owned approval card (Medium/High gated mutation). */
  onDecideApproval?: (approval: ServerApprovalState, decision: 'Approve' | 'Reject') => void;
  /** When true, renders as a full-height side panel (no drag handle, fills parent). */
  side?: boolean;
}

const MIN_HEIGHT = 48;
const DEFAULT_HEIGHT = 280;
const MAX_RATIO = 0.7; // max 70% of viewport

export function PlaygroundOutputPanel({
  output,
  outputParts,
  isStreaming,
  streamError,
  metrics,
  modelName,
  voiceModeEnabled = false,
  voicePlaybackState = 'idle',
  voiceError,
  voiceDetails,
  onStopVoice,
  onAddToMessages,
  isReviewing = false,
  reviewResult,
  reviewRawText,
  reviewError,
  onReview,
  onApproveToolCall,
  onRejectToolCall,
  onSelectToolCallOptions,
  onSelectFollowUpSuggestion,
  onDecideApproval,
  side = false,
}: PlaygroundOutputPanelProps) {
  const [height, setHeight] = useState(DEFAULT_HEIGHT);
  const dragging = useRef(false);
  const startY = useRef(0);
  const startH = useRef(0);
  const panelRef = useRef<HTMLDivElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const hasOutput = output || outputParts.length > 0 || isStreaming || streamError;

  // Auto-scroll to bottom while streaming
  useEffect(() => {
    if (isStreaming && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [output, outputParts, isStreaming]);

  // Expand to default height when output first appears (bottom mode only)
  useEffect(() => {
    if (!side && hasOutput && height < DEFAULT_HEIGHT) {
      setHeight(DEFAULT_HEIGHT);
    }
  }, [hasOutput]); // eslint-disable-line react-hooks/exhaustive-deps

  const onMouseDown = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      dragging.current = true;
      startY.current = e.clientY;
      startH.current = height;
      document.body.style.cursor = 'row-resize';
      document.body.style.userSelect = 'none';
    },
    [height],
  );

  useEffect(() => {
    if (side) return;
    const onMouseMove = (e: MouseEvent) => {
      if (!dragging.current) return;
      const delta = startY.current - e.clientY;
      const maxH = window.innerHeight * MAX_RATIO;
      const next = Math.min(maxH, Math.max(MIN_HEIGHT, startH.current + delta));
      setHeight(next);
    };

    const onMouseUp = () => {
      if (!dragging.current) return;
      dragging.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
  }, [side]);

  const hasStructuredParts = outputParts.length > 0 &&
    outputParts.some((p) => p.type !== 'text');

  return (
    <div
      ref={panelRef}
      className={
        side
          ? 'flex h-full flex-col bg-[var(--color-surface)]'
          : 'shrink-0 border-t border-[var(--color-border-light)] bg-[var(--color-surface)]'
      }
      style={side ? undefined : { height }}
    >
      {/* Drag handle — bottom mode only */}
      {!side && (
        <div
          onMouseDown={onMouseDown}
          className="group flex cursor-row-resize items-center justify-center border-b border-[var(--color-border-light)] py-1"
        >
          <GripHorizontal className="h-4 w-4 text-[var(--color-text-tertiary)] transition-colors group-hover:text-[var(--color-text-secondary)]" />
        </div>
      )}

      {/* Header row */}
      <div className={`flex shrink-0 items-center justify-between px-6 py-2${side ? ' border-b border-[var(--color-border-light)]' : ''}`}>
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-[var(--color-text-secondary)]">
            Output
          </span>
          {(metrics?.modelName || modelName) && (
            <span className="rounded bg-[var(--color-surface-inset)] px-1.5 py-0.5 text-[10px] font-medium text-[var(--color-text-tertiary)]">
              {metrics?.modelName || modelName}
            </span>
          )}
          {voiceModeEnabled && (
            <span className="rounded bg-[var(--color-surface-inset)] px-1.5 py-0.5 text-[10px] font-medium text-[var(--color-text-tertiary)]">
              Voice {voicePlaybackState === 'error' ? 'unavailable' : voicePlaybackState}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {/* Metrics inline */}
          {metrics && (
            <div className="flex items-center gap-3 text-[10px] tabular-nums text-[var(--color-text-tertiary)]">
              <span>{metrics.inputTokens} in</span>
              <span>{metrics.outputTokens} out</span>
              <span className="font-medium text-[var(--color-text-secondary)]">
                {metrics.totalTokens} total
              </span>
              <span>{(metrics.latencyMs / 1000).toFixed(1)}s</span>
              {metrics.estimatedCostUsd != null && (
                <span>${metrics.estimatedCostUsd.toFixed(4)}</span>
              )}
            </div>
          )}
          {output && !isStreaming && onReview && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onReview}
              disabled={isReviewing}
              className="h-6 px-2 text-xs"
            >
              {isReviewing ? (
                <>
                  <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                  Reviewing...
                </>
              ) : (
                <>
                  <Sparkles className="mr-1 h-3 w-3" />
                  AI Review
                </>
              )}
            </Button>
          )}
          {output && !isStreaming && onAddToMessages && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onAddToMessages}
              className="h-6 px-2 text-xs"
            >
              <ArrowDownToLine className="mr-1 h-3 w-3" />
              Add to messages
            </Button>
          )}
          {voiceModeEnabled && voicePlaybackState === 'playing' && onStopVoice && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onStopVoice}
              className="h-6 px-2 text-xs"
            >
              <Square className="mr-1 h-3 w-3" />
              Stop voice
            </Button>
          )}
        </div>
      </div>

      {/* Scrollable output content */}
      <div
        ref={scrollRef}
        className={side ? 'min-h-0 flex-1 overflow-y-auto px-6 pb-4' : 'overflow-y-auto px-6 pb-4'}
        style={side ? undefined : { height: `calc(100% - 68px)` }}
      >
        {hasOutput ? (
          <>
            {/* Structured rendering: show tool calls, reasoning, and text inline */}
            {hasStructuredParts ? (
              <div className="space-y-3">
                {outputParts.map((part, i) => {
                  switch (part.type) {
                    case 'reasoning':
                      return <ReasoningBlock key={`r-${i}`} content={part.content} />;
                    case 'tool-call':
                      return (
                        <ToolCallCard
                          key={part.toolCall.toolCallId}
                          toolCall={part.toolCall}
                          onApprove={onApproveToolCall}
                          onReject={onRejectToolCall}
                          onSelectOptions={onSelectToolCallOptions}
                          onSelectFollowUpSuggestion={onSelectFollowUpSuggestion}
                        />
                      );
                    case 'text':
                      return (
                        <div key={`t-${i}`} className="text-sm leading-relaxed text-[var(--color-text-primary)]">
                          <Markdown text={part.content} />
                        </div>
                      );
                    case 'approval':
                      return (
                        <ServerApprovalCard
                          key={part.approval.id}
                          approval={part.approval}
                          onDecide={onDecideApproval}
                        />
                      );
                    default:
                      return null;
                  }
                })}
                {isStreaming && outputParts.length === 0 && (
                  <span className="inline-flex items-center gap-2 text-xs text-[var(--color-text-tertiary)]">
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    Thinking...
                  </span>
                )}
              </div>
            ) : (
              /* Fallback: plain text rendering (no tool calls/reasoning) */
              <pre className="whitespace-pre-wrap font-sans text-sm leading-relaxed text-[var(--color-text-primary)]">
                {output || (isStreaming ? '...' : '')}
              </pre>
            )}
            {streamError && (
              <div className="mt-3 rounded-[2px] border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                {streamError}
              </div>
            )}
            {voiceModeEnabled && voiceDetails && (
              <div className="mt-3 rounded-[2px] border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-3 text-xs text-[var(--color-text-secondary)]">
                <div className="mb-2 flex items-center gap-2 text-[var(--color-text-primary)]">
                  <Volume2 className="h-3.5 w-3.5" />
                  <span className="font-medium">Speech render</span>
                </div>
                <div className="space-y-1">
                  <div>Provider: <span className="font-medium text-[var(--color-text-primary)]">{voiceDetails.provider ?? 'Pending'}</span></div>
                  <div>Voice: <span className="font-medium text-[var(--color-text-primary)]">{voiceDetails.voiceId ?? 'Pending'}</span></div>
                  <div>AiRunId: <span className="font-mono text-[var(--color-text-primary)]">{voiceDetails.aiRunId ?? 'n/a'}</span></div>
                </div>
                <pre className="mt-2 whitespace-pre-wrap font-sans text-xs leading-relaxed text-[var(--color-text-primary)]">
                  {voiceDetails.speechText}
                </pre>
              </div>
            )}
            {voiceModeEnabled && voiceError && (
              <div className="mt-3 rounded-[2px] border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                <div className="font-medium">Voice playback unavailable</div>
                {voiceError}
              </div>
            )}
            {/* AI Review results */}
            {(reviewResult || reviewRawText || reviewError || isReviewing) && (
              <ReviewResultsPanel
                result={reviewResult}
                rawText={reviewRawText}
                error={reviewError}
                isReviewing={isReviewing}
              />
            )}
          </>
        ) : (
          <p className="text-xs italic text-[var(--color-text-tertiary)]">
            Run the playground to see output here.
          </p>
        )}
      </div>
    </div>
  );
}

// ─── Reasoning Block ───────────────────────────────────────────────────────────

function ReasoningBlock({ content }: { content: string }) {
  return (
    <div
      className="rounded-[2px] border border-[var(--color-border-light)] bg-[color-mix(in_srgb,var(--color-surface)_92%,var(--color-background))] px-3 py-2 text-xs leading-relaxed text-[var(--color-text-tertiary)]"
      data-component="reasoning-part"
    >
      <div className="flex items-start gap-2">
        <Brain className="h-3.5 w-3.5 mt-0.5 shrink-0 opacity-50" />
        <div className="min-w-0 break-words whitespace-pre-wrap">{content}</div>
      </div>
    </div>
  );
}

// ─── Tool Call Card ────────────────────────────────────────────────────────────

function ToolCallCard({
  toolCall,
  onApprove,
  onReject,
  onSelectOptions,
  onSelectFollowUpSuggestion,
}: {
  toolCall: PlaygroundToolCall;
  onApprove?: (toolCallId: string) => void;
  onReject?: (toolCallId: string) => void;
  onSelectOptions?: (toolCallId: string, selected: string[]) => void;
  onSelectFollowUpSuggestion?: (prompt: string) => void;
}) {
  const isActive = toolCall.status === 'streaming' || toolCall.status === 'pending';
  const isAwaiting = toolCall.status === 'awaiting-approval' || toolCall.status === 'awaiting-selection';
  const isError = toolCall.status === 'error';
  const isDisplayTool = toolCall.toolCallName.startsWith('display_');

  const [open, setOpen] = useState(isActive || isAwaiting);
  const prevActiveRef = useRef(isActive || isAwaiting);

  // Auto-collapse when transitioning from active/awaiting -> done
  useEffect(() => {
    const wasActive = prevActiveRef.current;
    const nowActive = isActive || isAwaiting;
    if (wasActive && !nowActive) {
      setOpen(false);
    }
    prevActiveRef.current = nowActive;
  }, [isActive, isAwaiting]);

  // Force open when awaiting user input
  const effectiveOpen = isActive || isAwaiting ? true : open;

  const statusIcon = isActive ? (
    <Loader2 className="h-3 w-3 animate-spin text-[var(--color-info)]" />
  ) : toolCall.status === 'awaiting-approval' ? (
    <ShieldAlert className="h-3 w-3 text-[var(--color-warning)]" />
  ) : toolCall.status === 'awaiting-selection' ? (
    <ShieldAlert className="h-3 w-3 text-[var(--color-info)]" />
  ) : isError ? (
    <XCircle className="h-3 w-3 text-[var(--color-danger)]" />
  ) : toolCall.result === 'approved' ? (
    <ShieldCheck className="h-3 w-3 text-[var(--color-success)]" />
  ) : toolCall.result === 'rejected' ? (
    <ShieldX className="h-3 w-3 text-[var(--color-text-tertiary)]" />
  ) : (
    <CheckCircle2 className="h-3 w-3 text-[var(--color-success)]" />
  );

  const statusLabel = isActive
    ? toolCall.status === 'pending'
      ? 'Awaiting execution...'
      : 'Streaming...'
    : toolCall.status === 'awaiting-approval'
      ? 'Awaiting approval'
      : toolCall.status === 'awaiting-selection'
        ? 'Awaiting selection'
        : isError
          ? 'Failed'
          : toolCall.result === 'approved'
            ? 'Approved'
            : toolCall.result === 'rejected'
              ? 'Rejected'
              : 'Completed';

  const hasContent = !!(toolCall.args || toolCall.result || toolCall.error || isAwaiting);

  // Display tools: render visual card directly (no collapsible wrapper)
  if (isDisplayTool && toolCall.status === 'completed' && toolCall.args) {
    const parsedArgs = tryParseJson(toolCall.args);
    if (parsedArgs) {
      return (
        <DisplayToolVisual
          toolName={toolCall.toolCallName}
          args={parsedArgs}
          onSelectFollowUpSuggestion={onSelectFollowUpSuggestion}
        />
      );
    }
  }

  return (
    <Collapsible open={effectiveOpen} onOpenChange={setOpen}>
      <div
        className={`group rounded-lg border text-xs transition-colors ${
          isError
            ? 'border-[color-mix(in_srgb,var(--color-danger)_25%,transparent)] bg-[color-mix(in_srgb,var(--color-danger)_6%,transparent)]'
            : isAwaiting
              ? 'border-[color-mix(in_srgb,var(--color-warning)_30%,transparent)] bg-[color-mix(in_srgb,var(--color-warning)_6%,transparent)]'
              : 'border-[var(--color-border-light)] bg-[var(--color-surface)]'
        }`}
      >
        <CollapsibleTrigger asChild disabled={!hasContent}>
          <button
            type="button"
            className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-[var(--color-background)] rounded-lg transition-colors"
          >
            <Wrench className="h-3.5 w-3.5 text-[var(--color-text-tertiary)] shrink-0" />
            <span
              className={`font-medium ${
                isActive
                  ? 'text-shimmer'
                  : isAwaiting
                    ? 'text-[var(--color-warning)]'
                    : isError
                      ? 'text-[var(--color-danger)]'
                      : 'text-[var(--color-text-secondary)]'
              }`}
            >
              {toolCall.toolCallName}
            </span>
            <span className="inline-flex items-center gap-1 text-[var(--color-text-tertiary)]">
              {statusIcon}
              <span className="hidden sm:inline">{statusLabel}</span>
            </span>
            {hasContent && (
              <ChevronDown
                className={`ml-auto h-3.5 w-3.5 text-[var(--color-text-tertiary)] shrink-0 transition-all duration-150
                  opacity-0 group-hover:opacity-100
                  ${effectiveOpen ? 'rotate-0' : '-rotate-90'}`}
              />
            )}
          </button>
        </CollapsibleTrigger>

        <CollapsibleContent>
          <div className="border-t border-[var(--color-border-light)] px-3 py-2 space-y-2">
            {/* Approval interaction */}
            {toolCall.status === 'awaiting-approval' && toolCall.approval && (
              <ApprovalInteraction
                toolCallId={toolCall.toolCallId}
                approval={toolCall.approval}
                onApprove={onApprove}
                onReject={onReject}
              />
            )}

            {/* Option selection interaction */}
            {toolCall.status === 'awaiting-selection' && toolCall.optionSelection && (
              <OptionSelectionInteraction
                toolCallId={toolCall.toolCallId}
                selection={toolCall.optionSelection}
                onSelect={onSelectOptions}
              />
            )}

            {/* Args (show for non-interactive states, or below interactive UI) */}
            {toolCall.args && !isAwaiting && (
              <pre className="text-[var(--color-text-tertiary)] whitespace-pre-wrap break-all">
                {tryFormatJson(toolCall.args)}
              </pre>
            )}
            {toolCall.result && (
              <div className="text-[var(--color-text-tertiary)]">
                Result: {truncate(toolCall.result, 300)}
              </div>
            )}
            {toolCall.error && (
              <div className="text-[var(--color-danger)]">Error: {toolCall.error}</div>
            )}
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}

// ─── Approval Interaction ─────────────────────────────────────────────────────

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

function ApprovalInteraction({
  toolCallId,
  approval,
  onApprove,
  onReject,
}: {
  toolCallId: string;
  approval: { action: string; description: string; severity: 'low' | 'medium' | 'high' };
  onApprove?: (toolCallId: string) => void;
  onReject?: (toolCallId: string) => void;
}) {
  const config = severityConfig[approval.severity] ?? severityConfig.medium;
  const SeverityIcon = config.icon;

  return (
    <div className={`rounded-md border ${config.borderClass} bg-[var(--color-surface)] p-3 space-y-2.5`}>
      {/* Severity badge + action name */}
      <div className="flex items-center gap-2">
        <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${config.badgeClass}`}>
          <SeverityIcon className="h-3 w-3" />
          {config.label}
        </span>
        <span className="text-xs font-semibold text-[var(--color-text-primary)]">
          {approval.action}
        </span>
      </div>

      {/* Description */}
      {approval.description && (
        <p className="text-xs leading-relaxed text-[var(--color-text-secondary)]">
          {approval.description}
        </p>
      )}

      {/* Action buttons */}
      <div className="flex items-center gap-2 pt-1">
        <Button
          size="sm"
          className="h-7 gap-1.5 bg-[var(--color-success)] px-3 text-xs font-medium text-white hover:bg-[color-mix(in_srgb,var(--color-success)_85%,black)]"
          onClick={() => onApprove?.(toolCallId)}
        >
          <Check className="h-3 w-3" />
          Approve
        </Button>
        <Button
          variant="outline"
          size="sm"
          className="h-7 gap-1.5 px-3 text-xs font-medium text-[var(--color-text-secondary)] hover:text-[var(--color-danger)] hover:border-[var(--color-danger)]"
          onClick={() => onReject?.(toolCallId)}
        >
          <X className="h-3 w-3" />
          Reject
        </Button>
      </div>
    </div>
  );
}

// ─── Option Selection Interaction ─────────────────────────────────────────────

function OptionSelectionInteraction({
  toolCallId,
  selection,
  onSelect,
}: {
  toolCallId: string;
  selection: {
    question: string;
    options: Array<{ label: string; description?: string }>;
    multiSelect: boolean;
  };
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
        // Single-select: replace
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
      {/* Question */}
      <p className="text-xs font-semibold text-[var(--color-text-primary)]">
        {selection.question}
      </p>

      {/* Options list */}
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
              {/* Radio/checkbox indicator */}
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

      {/* Confirm button */}
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

// ─── Display Tool Visual Renderers ────────────────────────────────────────────

function DisplayToolVisual({
  toolName,
  args,
  onSelectFollowUpSuggestion,
}: {
  toolName: string;
  args: Record<string, unknown>;
  onSelectFollowUpSuggestion?: (prompt: string) => void;
}) {
  switch (toolName) {
    case 'display_follow_up_suggestions': {
      const suggestions = parseFollowUpSuggestions(args);
      return suggestions
        ? <AiFollowUpSuggestionsCard suggestions={suggestions} onSelect={onSelectFollowUpSuggestion} />
        : null;
    }
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
      {/* Header */}
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

      {/* Overall progress bar */}
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

      {/* Category rows */}
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

// ─── Pie chart colors (accessible, distinct) ─────────────────────────────────

const PIE_COLORS = [
  '#3b82f6', // blue
  '#10b981', // emerald
  '#f59e0b', // amber
  '#ef4444', // red
  '#8b5cf6', // violet
  '#ec4899', // pink
  '#06b6d4', // cyan
  '#f97316', // orange
  '#14b8a6', // teal
  '#6366f1', // indigo
];

function SpendingPieChartVisual({ args }: { args: Record<string, unknown> }) {
  const title = String(args.title ?? 'Spending by Category');
  const currency = String(args.currency ?? 'USD');
  const totalSpent = Number(args.totalSpent) || 0;
  const categories = Array.isArray(args.categories) ? args.categories : [];

  const fmt = (n: number) => {
    const sym = currency === 'GBP' ? '£' : currency === 'EUR' ? '€' : currency === 'NGN' ? '₦' : '$';
    return `${sym}${n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  // Build slices with computed percentages
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

  // SVG pie chart math
  const size = 140;
  const cx = size / 2;
  const cy = size / 2;
  const r = 54;
  const ir = 34; // inner radius for donut

  let cumulativeAngle = -90; // start at 12 o'clock

  const paths = slices.map((slice) => {
    const angle = (slice.percentage / 100) * 360;
    const startAngle = cumulativeAngle;
    const endAngle = cumulativeAngle + angle;
    cumulativeAngle = endAngle;

    // Edge case: full circle
    if (angle >= 359.99) {
      return {
        ...slice,
        d: `M${cx},${cy - r} A${r},${r} 0 1,1 ${cx - 0.01},${cy - r} Z M${cx},${cy - ir} A${ir},${ir} 0 1,0 ${cx - 0.01},${cy - ir} Z`,
      };
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

    return {
      ...slice,
      d: `M${x1},${y1} A${r},${r} 0 ${largeArc},1 ${x2},${y2} L${ix1},${iy1} A${ir},${ir} 0 ${largeArc},0 ${ix2},${iy2} Z`,
    };
  });

  return (
    <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] text-xs overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-[var(--color-brand-primary)]" />
          <span className="font-semibold text-[var(--color-text-primary)]">{title}</span>
        </div>
        <span className="text-sm font-bold tabular-nums text-[var(--color-text-primary)]">{fmt(totalSpent)}</span>
      </div>

      {/* Chart + Legend */}
      <div className="flex items-start gap-6 px-4 py-4">
        {/* Donut chart */}
        <div className="shrink-0">
          <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
            {paths.map((slice, i) => (
              <path
                key={i}
                d={slice.d}
                fill={slice.color}
                stroke="var(--color-surface)"
                strokeWidth="1.5"
              />
            ))}
            {/* Center text */}
            <text x={cx} y={cy - 4} textAnchor="middle" className="fill-[var(--color-text-tertiary)]" fontSize="9">
              Total
            </text>
            <text x={cx} y={cy + 10} textAnchor="middle" className="fill-[var(--color-text-primary)] font-semibold" fontSize="12">
              {fmt(totalSpent)}
            </text>
          </svg>
        </div>

        {/* Legend */}
        <div className="flex-1 space-y-2 min-w-0 pt-1">
          {slices.map((slice, i) => (
            <div key={i} className="flex items-center gap-2">
              <span
                className="h-2.5 w-2.5 shrink-0 rounded-sm"
                style={{ backgroundColor: slice.color }}
              />
              <span className="truncate text-[var(--color-text-secondary)] flex-1">{slice.name}</span>
              <span className="tabular-nums font-medium text-[var(--color-text-primary)] shrink-0">{fmt(slice.amount)}</span>
              <span className="tabular-nums text-[var(--color-text-tertiary)] shrink-0 w-10 text-right">
                {slice.percentage.toFixed(0)}%
              </span>
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
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
        <div className="flex items-center gap-2">
          <TrendingUp className="h-4 w-4 text-[var(--color-brand-primary)]" />
          <span className="font-semibold text-[var(--color-text-primary)]">
            {baseCurrency}/{targetCurrency} Rate
          </span>
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

      {/* Sparkline chart */}
      {rates.length > 1 && (
        <div className="px-4 pt-3 pb-1">
          <div className="relative h-16 w-full">
            <svg viewBox={`0 0 ${(rates.length - 1) * 40} 60`} className="h-full w-full" preserveAspectRatio="none">
              {/* Area fill */}
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
              {/* Line */}
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
          {/* Date labels */}
          <div className="flex justify-between text-[10px] text-[var(--color-text-tertiary)] mt-1">
            {rates.length > 0 && <span>{String((rates[0] as Record<string, unknown>).date ?? '')}</span>}
            {rates.length > 1 && <span>{String((rates[rates.length - 1] as Record<string, unknown>).date ?? '')}</span>}
          </div>
        </div>
      )}

      {/* Signal reason */}
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
      {/* Header */}
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

      {/* Description */}
      <div className="px-4 py-3 space-y-3">
        <p className="text-[var(--color-text-secondary)] leading-relaxed">{description}</p>

        {/* Detail rows */}
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

// ─── Markdown Renderer ─────────────────────────────────────────────────────────

function Markdown({ text }: { text: string }) {
  return (
    <div className="chat-markdown">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          h1: ({ children }) => <h3 className="text-sm font-bold mt-3 first:mt-0 mb-1">{children}</h3>,
          h2: ({ children }) => <h4 className="text-sm font-semibold mt-2 first:mt-0 mb-1">{children}</h4>,
          h3: ({ children }) => <h5 className="text-sm font-medium mt-2 first:mt-0 mb-0.5">{children}</h5>,
          p: ({ children }) => <p className="mb-2 last:mb-0 leading-relaxed">{children}</p>,
          ul: ({ children }) => <ul className="mb-2 last:mb-0 pl-4 list-disc space-y-1">{children}</ul>,
          ol: ({ children }) => <ol className="mb-2 last:mb-0 pl-4 list-decimal space-y-1">{children}</ol>,
          li: ({ children }) => <li className="leading-relaxed">{children}</li>,
          code: ({ children, className }) => {
            if (className) {
              return (
                <code className="block bg-[var(--color-surface-inset)] rounded-md px-3 py-2 text-xs font-mono overflow-x-auto whitespace-pre-wrap break-all my-2">
                  {children}
                </code>
              );
            }
            return (
              <code className="bg-[var(--color-surface-inset)] rounded px-1 py-0.5 text-xs font-mono">
                {children}
              </code>
            );
          },
          pre: ({ children }) => <div className="my-2">{children}</div>,
          strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
          em: ({ children }) => <em>{children}</em>,
          a: ({ href, children }) => (
            <a href={href} target="_blank" rel="noopener noreferrer" className="text-[var(--color-brand-primary)] underline hover:opacity-80">
              {children}
            </a>
          ),
          hr: () => <hr className="my-2 border-[var(--color-border-light)]" />,
          blockquote: ({ children }) => (
            <blockquote className="border-l-2 border-[var(--color-border-light)] pl-3 my-2 text-[var(--color-text-secondary)] italic">
              {children}
            </blockquote>
          ),
          table: ({ children }) => (
            <div className="my-2 overflow-x-auto">
              <table className="text-xs border-collapse w-full">{children}</table>
            </div>
          ),
          th: ({ children }) => (
            <th className="border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-2 py-1 text-left font-medium">
              {children}
            </th>
          ),
          td: ({ children }) => (
            <td className="border border-[var(--color-border-light)] px-2 py-1">{children}</td>
          ),
        }}
      >
        {text}
      </ReactMarkdown>
    </div>
  );
}

// ─── Review Results Panel ─────────────────────────────────────────────────────

function ReviewResultsPanel({
  result,
  rawText,
  error,
  isReviewing,
}: {
  result?: PlaygroundReviewResult | null;
  rawText?: string | null;
  error?: string | null;
  isReviewing: boolean;
}) {
  const [expanded, setExpanded] = useState(true);

  return (
    <div className="mt-4 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] overflow-hidden">
      {/* Header */}
      <button
        type="button"
        className="flex w-full items-center justify-between px-4 py-3 text-left hover:bg-[color-mix(in_srgb,var(--color-surface-inset)_90%,var(--color-background))] transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <Sparkles className="h-4 w-4 text-[var(--color-brand-primary)]" />
          <span className="text-xs font-semibold text-[var(--color-text-primary)]">
            AI Review
          </span>
          {result && (
            <span className="flex items-center gap-1 rounded-full bg-[var(--color-brand-primary)] px-2 py-0.5 text-[10px] font-bold text-white">
              <Star className="h-2.5 w-2.5" />
              {result.overallScore.toFixed(1)} / 5
            </span>
          )}
          {isReviewing && (
            <span className="flex items-center gap-1 text-[10px] text-[var(--color-text-tertiary)]">
              <Loader2 className="h-3 w-3 animate-spin" />
              Analyzing...
            </span>
          )}
        </div>
        {expanded ? (
          <ChevronUp className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
        ) : (
          <ChevronDown className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
        )}
      </button>

      {expanded && (
        <div className="border-t border-[var(--color-border-light)] px-4 py-3 space-y-4">
          {error && (
            <div className="rounded-[2px] border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
              {error}
            </div>
          )}

          {result && (
            <>
              {/* Metrics grid */}
              <div className="grid grid-cols-2 gap-3">
                {result.metrics.map((metric) => (
                  <MetricCard key={metric.name} metric={metric} />
                ))}
              </div>

              {/* Strengths */}
              {result.strengths.length > 0 && (
                <div>
                  <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-[var(--color-success)]">
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    Strengths
                  </div>
                  <ul className="space-y-1 pl-5 text-xs text-[var(--color-text-secondary)] list-disc">
                    {result.strengths.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Suggestions */}
              {result.suggestions.length > 0 && (
                <div>
                  <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-[var(--color-warning)]">
                    <Lightbulb className="h-3.5 w-3.5" />
                    Suggestions
                  </div>
                  <ul className="space-y-1 pl-5 text-xs text-[var(--color-text-secondary)] list-disc">
                    {result.suggestions.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Prompt Improvements */}
              {result.promptImprovements.length > 0 && (
                <div>
                  <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-[var(--color-brand-primary)]">
                    <Zap className="h-3.5 w-3.5" />
                    Prompt Improvements
                  </div>
                  <ul className="space-y-1 pl-5 text-xs text-[var(--color-text-secondary)] list-disc">
                    {result.promptImprovements.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}
            </>
          )}

          {/* Fallback: show raw text if structured parsing failed */}
          {!result && rawText && !isReviewing && (
            <div className="text-xs text-[var(--color-text-secondary)] whitespace-pre-wrap">
              {rawText}
            </div>
          )}

          {/* Loading state */}
          {isReviewing && !result && !error && (
            <div className="flex items-center justify-center py-6">
              <div className="flex items-center gap-2 text-xs text-[var(--color-text-tertiary)]">
                <Loader2 className="h-4 w-4 animate-spin" />
                Evaluating response quality...
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function MetricCard({ metric }: { metric: PlaygroundReviewResult['metrics'][0] }) {
  const scoreColor =
    metric.score >= 4
      ? 'text-[var(--color-success)]'
      : metric.score >= 3
        ? 'text-[var(--color-warning)]'
        : 'text-[var(--color-error)]';

  const bgColor =
    metric.score >= 4
      ? 'bg-[color-mix(in_srgb,var(--color-success)_8%,transparent)]'
      : metric.score >= 3
        ? 'bg-[color-mix(in_srgb,var(--color-warning)_8%,transparent)]'
        : 'bg-[color-mix(in_srgb,var(--color-error)_8%,transparent)]';

  const MetricIcon =
    metric.name === 'Faithfulness'
      ? Target
      : metric.name === 'Answer Relevancy'
        ? Zap
        : metric.name === 'Coherence'
          ? Brain
          : CheckCircle2;

  return (
    <div className={`rounded-lg border border-[var(--color-border-light)] ${bgColor} px-3 py-2.5`}>
      <div className="flex items-center justify-between mb-1">
        <div className="flex items-center gap-1.5">
          <MetricIcon className={`h-3 w-3 ${scoreColor}`} />
          <span className="text-[10px] font-semibold text-[var(--color-text-primary)]">
            {metric.name}
          </span>
        </div>
        <span className={`text-sm font-bold tabular-nums ${scoreColor}`}>
          {metric.score}/5
        </span>
      </div>
      <p className="text-[10px] leading-relaxed text-[var(--color-text-tertiary)]">
        {metric.explanation}
      </p>
    </div>
  );
}

// ─── Helpers ───────────────────────────────────────────────────────────────────

function truncate(str: string, max: number): string {
  return str.length > max ? str.slice(0, max) + '...' : str;
}

function tryFormatJson(str: string): string {
  try {
    return JSON.stringify(JSON.parse(str), null, 2);
  } catch {
    return str;
  }
}

function tryParseJson(str: string): Record<string, unknown> | null {
  try {
    const parsed = JSON.parse(str);
    return typeof parsed === 'object' && parsed !== null ? parsed : null;
  } catch {
    return null;
  }
}
