import { useCallback, useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import {
  ArrowDownToLine,
  Brain,
  CheckCircle2,
  ChevronDown,
  GripHorizontal,
  Loader2,
  Square,
  Volume2,
  Wrench,
  XCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Collapsible,
  CollapsibleTrigger,
  CollapsibleContent,
} from '@/components/ui/collapsible';
import type { PlaygroundRunMetrics } from '@/lib/playground-client';
import type { PlaygroundOutputPart, PlaygroundToolCall } from '@/hooks/usePlaygroundChat';

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

  // Expand to default height when output first appears
  useEffect(() => {
    if (hasOutput && height < DEFAULT_HEIGHT) {
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
  }, []);

  const hasStructuredParts = outputParts.length > 0 &&
    outputParts.some((p) => p.type !== 'text');

  return (
    <div
      ref={panelRef}
      className="shrink-0 border-t border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{ height }}
    >
      {/* Drag handle */}
      <div
        onMouseDown={onMouseDown}
        className="group flex cursor-row-resize items-center justify-center border-b border-[var(--color-border-light)] py-1"
      >
        <GripHorizontal className="h-4 w-4 text-[var(--color-text-tertiary)] transition-colors group-hover:text-[var(--color-text-secondary)]" />
      </div>

      {/* Header row */}
      <div className="flex items-center justify-between px-6 py-2">
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
        className="overflow-y-auto px-6 pb-4"
        style={{ height: `calc(100% - 68px)` }}
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
                      return <ToolCallCard key={part.toolCall.toolCallId} toolCall={part.toolCall} />;
                    case 'text':
                      return (
                        <div key={`t-${i}`} className="text-sm leading-relaxed text-[var(--color-text-primary)]">
                          <Markdown text={part.content} />
                        </div>
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

function ToolCallCard({ toolCall }: { toolCall: PlaygroundToolCall }) {
  const isActive = toolCall.status === 'streaming';
  const isError = toolCall.status === 'error';

  const [open, setOpen] = useState(isActive);
  const prevActiveRef = useRef(isActive);

  // Auto-collapse when transitioning from active -> done
  useEffect(() => {
    if (prevActiveRef.current && !isActive) {
      setOpen(false);
    }
    prevActiveRef.current = isActive;
  }, [isActive]);

  const effectiveOpen = isActive ? true : open;

  const statusIcon = isActive ? (
    <Loader2 className="h-3 w-3 animate-spin text-[var(--color-info)]" />
  ) : isError ? (
    <XCircle className="h-3 w-3 text-[var(--color-danger)]" />
  ) : (
    <CheckCircle2 className="h-3 w-3 text-[var(--color-success)]" />
  );

  const statusLabel = isActive
    ? 'Executing...'
    : isError
      ? 'Failed'
      : 'Completed';

  const hasContent = !!(toolCall.args || toolCall.result || toolCall.error);

  return (
    <Collapsible open={effectiveOpen} onOpenChange={setOpen}>
      <div
        className={`group rounded-lg border text-xs transition-colors ${
          isError
            ? 'border-[color-mix(in_srgb,var(--color-danger)_25%,transparent)] bg-[color-mix(in_srgb,var(--color-danger)_6%,transparent)]'
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
          <div className="border-t border-[var(--color-border-light)] px-3 py-2 space-y-1">
            {toolCall.args && (
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
