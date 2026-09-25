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
          ? 'flex h-full flex-col bg-card'
          : 'shrink-0 border-t border-border bg-card'
      }
      style={side ? undefined : { height }}
    >
      {/* Drag handle — bottom mode only */}
      {!side && (
        <div
          onMouseDown={onMouseDown}
          className="group flex cursor-row-resize items-center justify-center border-b border-border py-1"
        >
          <GripHorizontal className="h-4 w-4 text-muted-foreground transition-colors group-hover:text-muted-foreground" />
        </div>
      )}

      {/* Header row */}
      <div className={`flex shrink-0 items-center justify-between px-6 py-2${side ? ' border-b border-border' : ''}`}>
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-muted-foreground">
            Output
          </span>
          {(metrics?.modelName || modelName) && (
            <span className="rounded bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">
              {metrics?.modelName || modelName}
            </span>
          )}
          {voiceModeEnabled && (
            <span className="rounded bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">
              Voice {voicePlaybackState === 'error' ? 'unavailable' : voicePlaybackState}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {/* Metrics inline */}
          {metrics && (
            <div className="flex items-center gap-3 text-[10px] tabular-nums text-muted-foreground">
              <span>{metrics.inputTokens} in</span>
              <span>{metrics.outputTokens} out</span>
              <span className="font-medium text-muted-foreground">
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
                        <div key={`t-${i}`} className="text-sm leading-relaxed text-foreground">
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
                  <span className="inline-flex items-center gap-2 text-xs text-muted-foreground">
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    Thinking...
                  </span>
                )}
              </div>
            ) : (
              /* Fallback: plain text rendering (no tool calls/reasoning) */
              <pre className="whitespace-pre-wrap font-sans text-sm leading-relaxed text-foreground">
                {output || (isStreaming ? '...' : '')}
              </pre>
            )}
            {streamError && (
              <div className="mt-3 rounded-[2px] border border-destructive bg-destructive/10 px-3 py-2 text-xs text-destructive">
                {streamError}
              </div>
            )}
            {voiceModeEnabled && voiceDetails && (
              <div className="mt-3 rounded-[2px] border border-border bg-muted px-3 py-3 text-xs text-muted-foreground">
                <div className="mb-2 flex items-center gap-2 text-foreground">
                  <Volume2 className="h-3.5 w-3.5" />
                  <span className="font-medium">Speech render</span>
                </div>
                <div className="space-y-1">
                  <div>Provider: <span className="font-medium text-foreground">{voiceDetails.provider ?? 'Pending'}</span></div>
                  <div>Voice: <span className="font-medium text-foreground">{voiceDetails.voiceId ?? 'Pending'}</span></div>
                  <div>AiRunId: <span className="font-mono text-foreground">{voiceDetails.aiRunId ?? 'n/a'}</span></div>
                </div>
                <pre className="mt-2 whitespace-pre-wrap font-sans text-xs leading-relaxed text-foreground">
                  {voiceDetails.speechText}
                </pre>
              </div>
            )}
            {voiceModeEnabled && voiceError && (
              <div className="mt-3 rounded-[2px] border border-destructive bg-destructive/10 px-3 py-2 text-xs text-destructive">
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
          <p className="text-xs italic text-muted-foreground">
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
      className="rounded-[2px] border border-border bg-[color-mix(in_srgb,var(--card)_92%,var(--background))] px-3 py-2 text-xs leading-relaxed text-muted-foreground"
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
    <Loader2 className="h-3 w-3 animate-spin text-info" />
  ) : toolCall.status === 'awaiting-approval' ? (
    <ShieldAlert className="h-3 w-3 text-warning" />
  ) : toolCall.status === 'awaiting-selection' ? (
    <ShieldAlert className="h-3 w-3 text-info" />
  ) : isError ? (
    <XCircle className="h-3 w-3 text-destructive" />
  ) : toolCall.result === 'approved' ? (
    <ShieldCheck className="h-3 w-3 text-success" />
  ) : toolCall.result === 'rejected' ? (
    <ShieldX className="h-3 w-3 text-muted-foreground" />
  ) : (
    <CheckCircle2 className="h-3 w-3 text-success" />
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
            ? 'border-[color-mix(in_srgb,var(--destructive)_25%,transparent)] bg-[color-mix(in_srgb,var(--destructive)_6%,transparent)]'
            : isAwaiting
              ? 'border-[color-mix(in_srgb,var(--warning)_30%,transparent)] bg-[color-mix(in_srgb,var(--warning)_6%,transparent)]'
              : 'border-border bg-card'
        }`}
      >
        <CollapsibleTrigger asChild disabled={!hasContent}>
          <button
            type="button"
            className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-accent rounded-lg transition-colors"
          >
            <Wrench className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
            <span
              className={`font-medium ${
                isActive
                  ? 'text-shimmer'
                  : isAwaiting
                    ? 'text-warning'
                    : isError
                      ? 'text-destructive'
                      : 'text-muted-foreground'
              }`}
            >
              {toolCall.toolCallName}
            </span>
            <span className="inline-flex items-center gap-1 text-muted-foreground">
              {statusIcon}
              <span className="hidden sm:inline">{statusLabel}</span>
            </span>
            {hasContent && (
              <ChevronDown
                className={`ml-auto h-3.5 w-3.5 text-muted-foreground shrink-0 transition-all duration-150
                  opacity-0 group-hover:opacity-100
                  ${effectiveOpen ? 'rotate-0' : '-rotate-90'}`}
              />
            )}
          </button>
        </CollapsibleTrigger>

        <CollapsibleContent>
          <div className="border-t border-border px-3 py-2 space-y-2">
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
              <pre className="text-muted-foreground whitespace-pre-wrap break-all">
                {tryFormatJson(toolCall.args)}
              </pre>
            )}
            {toolCall.result && (
              <div className="text-muted-foreground">
                Result: {truncate(toolCall.result, 300)}
              </div>
            )}
            {toolCall.error && (
              <div className="text-destructive">Error: {toolCall.error}</div>
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
    badgeClass: 'bg-[color-mix(in_srgb,var(--info)_15%,transparent)] text-info',
    borderClass: 'border-[color-mix(in_srgb,var(--info)_20%,transparent)]',
  },
  medium: {
    label: 'Medium risk',
    icon: ShieldAlert,
    badgeClass: 'bg-[color-mix(in_srgb,var(--warning)_15%,transparent)] text-warning',
    borderClass: 'border-[color-mix(in_srgb,var(--warning)_20%,transparent)]',
  },
  high: {
    label: 'High risk',
    icon: ShieldX,
    badgeClass: 'bg-[color-mix(in_srgb,var(--destructive)_15%,transparent)] text-destructive',
    borderClass: 'border-[color-mix(in_srgb,var(--destructive)_20%,transparent)]',
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
    <div className={`rounded-md border ${config.borderClass} bg-card p-3 space-y-2.5`}>
      {/* Severity badge + action name */}
      <div className="flex items-center gap-2">
        <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${config.badgeClass}`}>
          <SeverityIcon className="h-3 w-3" />
          {config.label}
        </span>
        <span className="text-xs font-semibold text-foreground">
          {approval.action}
        </span>
      </div>

      {/* Description */}
      {approval.description && (
        <p className="text-xs leading-relaxed text-muted-foreground">
          {approval.description}
        </p>
      )}

      {/* Action buttons */}
      <div className="flex items-center gap-2 pt-1">
        <Button
          size="sm"
          className="h-7 gap-1.5 bg-success px-3 text-xs font-medium text-white hover:bg-[color-mix(in_srgb,var(--success)_85%,black)]"
          onClick={() => onApprove?.(toolCallId)}
        >
          <Check className="h-3 w-3" />
          Approve
        </Button>
        <Button
          variant="outline"
          size="sm"
          className="h-7 gap-1.5 px-3 text-xs font-medium text-muted-foreground hover:text-destructive hover:border-destructive"
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
    <div className="rounded-md border border-[color-mix(in_srgb,var(--info)_20%,transparent)] bg-card p-3 space-y-2.5">
      {/* Question */}
      <p className="text-xs font-semibold text-foreground">
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
                  ? 'border-primary bg-[color-mix(in_srgb,var(--primary)_8%,transparent)]'
                  : 'border-border bg-card hover:bg-accent'
              }`}
            >
              {/* Radio/checkbox indicator */}
              <span className={`mt-0.5 flex h-3.5 w-3.5 shrink-0 items-center justify-center rounded-${selection.multiSelect ? 'sm' : 'full'} border ${
                isSelected
                  ? 'border-primary bg-primary'
                  : 'border-muted-foreground'
              }`}>
                {isSelected && <Check className="h-2.5 w-2.5 text-white" />}
              </span>
              <div className="min-w-0">
                <span className={`font-medium ${isSelected ? 'text-foreground' : 'text-muted-foreground'}`}>
                  {option.label}
                </span>
                {option.description && (
                  <p className="mt-0.5 text-muted-foreground">{option.description}</p>
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
    <div className="rounded-lg border border-border bg-card text-xs overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">Budget Breakdown</span>
          {period && (
            <span className="rounded bg-card px-1.5 py-0.5 text-[10px] text-muted-foreground">
              {period}
            </span>
          )}
        </div>
        <div className="text-right">
          <div className={`text-sm font-bold tabular-nums ${isOver ? 'text-destructive' : 'text-foreground'}`}>
            {fmt(totalSpent)} <span className="font-normal text-muted-foreground">/ {fmt(totalBudget)}</span>
          </div>
        </div>
      </div>

      {/* Overall progress bar */}
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
                    <span className="tabular-nums text-muted-foreground">
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
    <div className="rounded-lg border border-border bg-card text-xs overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">{title}</span>
        </div>
        <span className="text-sm font-bold tabular-nums text-foreground">{fmt(totalSpent)}</span>
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
                stroke="var(--card)"
                strokeWidth="1.5"
              />
            ))}
            {/* Center text */}
            <text x={cx} y={cy - 4} textAnchor="middle" className="fill-muted-foreground" fontSize="9">
              Total
            </text>
            <text x={cx} y={cy + 10} textAnchor="middle" className="fill-foreground font-semibold" fontSize="12">
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
              <span className="truncate text-muted-foreground flex-1">{slice.name}</span>
              <span className="tabular-nums font-medium text-foreground shrink-0">{fmt(slice.amount)}</span>
              <span className="tabular-nums text-muted-foreground shrink-0 w-10 text-right">
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
    buy: { label: 'Buy now', color: 'text-success', bg: 'bg-[color-mix(in_srgb,var(--success)_12%,transparent)]', Icon: TrendingDown },
    hold: { label: 'Hold', color: 'text-warning', bg: 'bg-[color-mix(in_srgb,var(--warning)_12%,transparent)]', Icon: ArrowUpDown },
    wait: { label: 'Wait', color: 'text-info', bg: 'bg-[color-mix(in_srgb,var(--info)_12%,transparent)]', Icon: TrendingUp },
  }[signal] ?? { label: signal, color: 'text-muted-foreground', bg: 'bg-muted', Icon: ArrowUpDown };

  return (
    <div className="rounded-lg border border-border bg-card text-xs overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <TrendingUp className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">
            {baseCurrency}/{targetCurrency} Rate
          </span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm font-bold tabular-nums text-foreground">
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
                fill="var(--primary)"
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
                stroke="var(--primary)"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
          {/* Date labels */}
          <div className="flex justify-between text-[10px] text-muted-foreground mt-1">
            {rates.length > 0 && <span>{String((rates[0] as Record<string, unknown>).date ?? '')}</span>}
            {rates.length > 1 && <span>{String((rates[rates.length - 1] as Record<string, unknown>).date ?? '')}</span>}
          </div>
        </div>
      )}

      {/* Signal reason */}
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
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted">
        <div className="flex items-center gap-2">
          <Bot className="h-4 w-4 text-primary" />
          <span className="font-semibold text-foreground">{action}</span>
        </div>
        <div className="flex items-center gap-2">
          {agent && (
            <span className="rounded bg-card px-1.5 py-0.5 text-[10px] text-muted-foreground">
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
        <p className="text-muted-foreground leading-relaxed">{description}</p>

        {/* Detail rows */}
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
                <code className="block bg-muted rounded-md px-3 py-2 text-xs font-mono overflow-x-auto whitespace-pre-wrap break-all my-2">
                  {children}
                </code>
              );
            }
            return (
              <code className="bg-muted rounded px-1 py-0.5 text-xs font-mono">
                {children}
              </code>
            );
          },
          pre: ({ children }) => <div className="my-2">{children}</div>,
          strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
          em: ({ children }) => <em>{children}</em>,
          a: ({ href, children }) => (
            <a href={href} target="_blank" rel="noopener noreferrer" className="text-primary underline hover:opacity-80">
              {children}
            </a>
          ),
          hr: () => <hr className="my-2 border-border" />,
          blockquote: ({ children }) => (
            <blockquote className="border-l-2 border-border pl-3 my-2 text-muted-foreground italic">
              {children}
            </blockquote>
          ),
          table: ({ children }) => (
            <div className="my-2 overflow-x-auto">
              <table className="text-xs border-collapse w-full">{children}</table>
            </div>
          ),
          th: ({ children }) => (
            <th className="border border-border bg-muted px-2 py-1 text-left font-medium">
              {children}
            </th>
          ),
          td: ({ children }) => (
            <td className="border border-border px-2 py-1">{children}</td>
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
    <div className="mt-4 rounded-lg border border-border bg-muted overflow-hidden">
      {/* Header */}
      <button
        type="button"
        className="flex w-full items-center justify-between px-4 py-3 text-left hover:bg-[color-mix(in_srgb,var(--muted)_90%,var(--background))] transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <Sparkles className="h-4 w-4 text-primary" />
          <span className="text-xs font-semibold text-foreground">
            AI Review
          </span>
          {result && (
            <span className="flex items-center gap-1 rounded-full bg-primary px-2 py-0.5 text-[10px] font-bold text-primary-foreground">
              <Star className="h-2.5 w-2.5" />
              {result.overallScore.toFixed(1)} / 5
            </span>
          )}
          {isReviewing && (
            <span className="flex items-center gap-1 text-[10px] text-muted-foreground">
              <Loader2 className="h-3 w-3 animate-spin" />
              Analyzing...
            </span>
          )}
        </div>
        {expanded ? (
          <ChevronUp className="h-3.5 w-3.5 text-muted-foreground" />
        ) : (
          <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
        )}
      </button>

      {expanded && (
        <div className="border-t border-border px-4 py-3 space-y-4">
          {error && (
            <div className="rounded-[2px] border border-destructive bg-destructive/10 px-3 py-2 text-xs text-destructive">
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
                  <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-success">
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    Strengths
                  </div>
                  <ul className="space-y-1 pl-5 text-xs text-muted-foreground list-disc">
                    {result.strengths.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Suggestions */}
              {result.suggestions.length > 0 && (
                <div>
                  <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-warning">
                    <Lightbulb className="h-3.5 w-3.5" />
                    Suggestions
                  </div>
                  <ul className="space-y-1 pl-5 text-xs text-muted-foreground list-disc">
                    {result.suggestions.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Prompt Improvements */}
              {result.promptImprovements.length > 0 && (
                <div>
                  <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-primary">
                    <Zap className="h-3.5 w-3.5" />
                    Prompt Improvements
                  </div>
                  <ul className="space-y-1 pl-5 text-xs text-muted-foreground list-disc">
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
            <div className="text-xs text-muted-foreground whitespace-pre-wrap">
              {rawText}
            </div>
          )}

          {/* Loading state */}
          {isReviewing && !result && !error && (
            <div className="flex items-center justify-center py-6">
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
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
      ? 'text-success'
      : metric.score >= 3
        ? 'text-warning'
        : 'text-destructive';

  const bgColor =
    metric.score >= 4
      ? 'bg-[color-mix(in_srgb,var(--success)_8%,transparent)]'
      : metric.score >= 3
        ? 'bg-[color-mix(in_srgb,var(--warning)_8%,transparent)]'
        : 'bg-[color-mix(in_srgb,var(--destructive)_8%,transparent)]';

  const MetricIcon =
    metric.name === 'Faithfulness'
      ? Target
      : metric.name === 'Answer Relevancy'
        ? Zap
        : metric.name === 'Coherence'
          ? Brain
          : CheckCircle2;

  return (
    <div className={`rounded-lg border border-border ${bgColor} px-3 py-2.5`}>
      <div className="flex items-center justify-between mb-1">
        <div className="flex items-center gap-1.5">
          <MetricIcon className={`h-3 w-3 ${scoreColor}`} />
          <span className="text-[10px] font-semibold text-foreground">
            {metric.name}
          </span>
        </div>
        <span className={`text-sm font-bold tabular-nums ${scoreColor}`}>
          {metric.score}/5
        </span>
      </div>
      <p className="text-[10px] leading-relaxed text-muted-foreground">
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
