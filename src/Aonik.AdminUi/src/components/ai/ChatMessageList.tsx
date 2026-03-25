import { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import {
  Loader2,
  Wrench,
  CheckCircle2,
  XCircle,
  Brain,
  Activity,
  ChevronRight,
  ChevronDown,
  ShieldAlert,
  ShieldCheck,
  ShieldX,
} from 'lucide-react';

import {
  Message,
  MessageContent,
  MessageAvatar,
} from '@/components/ai-elements';
import { Collapsible, CollapsibleTrigger, CollapsibleContent } from '@/components/ui/collapsible';
import type { ChatMessage, ChatToolCall, PendingApproval } from '@/hooks/useAguiChat';

interface ChatMessageListProps {
  messages: ChatMessage[];
  isStreaming: boolean;
  pendingApprovals?: PendingApproval[];
  onApproveAction?: (toolCallId: string) => void;
  onRejectAction?: (toolCallId: string, reason?: string) => void;
}

/**
 * Renders a list of ChatMessage objects with support for all AG-UI message types:
 * user, assistant (with tool calls), tool results, steps, reasoning, and activity.
 *
 * Inspired by OpenCode's part-based rendering:
 * - 24px gap between top-level messages
 * - 12px gap between parts within an assistant message
 * - Tool calls are collapsible (collapsed once completed)
 * - Shimmer animation on streaming tool call names
 * - Reasoning is inline with muted styling
 * - Hover-reveal chevron on collapsible tool calls
 * - Assistant text is rendered as markdown via react-markdown
 * - Tool-result messages are suppressed (info already in tool call card)
 */
export function ChatMessageList({
  messages,
  isStreaming,
  pendingApprovals,
  onApproveAction,
  onRejectAction,
}: ChatMessageListProps) {
  return (
    <div className="flex flex-col gap-4">
      {messages.map((m) => {
        switch (m.type) {
          case 'user':
            return (
              <Message from="user" key={m.id}>
                <MessageContent from="user">{m.content}</MessageContent>
              </Message>
            );

          case 'assistant':
            return (
              <div key={m.id} className="flex items-start">
                <MessageAvatar />
                <Message from="assistant">
                  {/* Text content — rendered as markdown */}
                  {m.content ? (
                    <MessageContent from="assistant">
                      <Markdown text={m.content} />
                    </MessageContent>
                  ) : isStreaming && !m.toolCalls?.length ? (
                    <MessageContent from="assistant">
                      <span className="inline-flex items-center gap-2 text-[var(--color-text-tertiary)]">
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Thinking...
                      </span>
                    </MessageContent>
                  ) : null}

                  {/* Tool calls — 8px gap between them */}
                  {m.toolCalls && m.toolCalls.length > 0 && (
                    <div className="mt-2 flex flex-col gap-2 w-full max-w-full">
                      {m.toolCalls.map((tc) => {
                        if (tc.toolCallName === 'confirmAction') {
                          const approval = pendingApprovals?.find(
                            (a) => a.toolCallId === tc.toolCallId,
                          );
                          return (
                            <ApprovalCard
                              key={tc.toolCallId}
                              toolCall={tc}
                              approval={approval}
                              onApprove={onApproveAction}
                              onReject={onRejectAction}
                            />
                          );
                        }
                        return <ToolCallCard key={tc.toolCallId} toolCall={tc} />;
                      })}
                    </div>
                  )}
                </Message>
              </div>
            );

          // Tool results are already shown inside the collapsible tool call card,
          // so we suppress the separate tool-result block to avoid visual clutter.
          case 'tool-result':
            return null;

          case 'step':
            return (
              <div
                key={m.id}
                className="flex items-center gap-2 px-2 py-1 text-xs text-[var(--color-text-tertiary)]"
              >
                {m.status === 'started' ? (
                  <Loader2 className="h-3 w-3 animate-spin" />
                ) : (
                  <CheckCircle2 className="h-3 w-3 text-[var(--color-success)]" />
                )}
                <ChevronRight className="h-3 w-3" />
                <span>{m.stepName}</span>
              </div>
            );

          case 'reasoning':
            return (
              <div
                key={m.id}
                className="ml-10 rounded-[2px] border border-[var(--color-border-light)] bg-[color-mix(in_srgb,var(--color-surface)_92%,var(--color-background))] px-3 py-2 text-xs leading-relaxed text-[var(--color-text-tertiary)]"
                data-component="reasoning-part"
              >
                <div className="flex items-start gap-2">
                  <Brain className="h-3.5 w-3.5 mt-0.5 shrink-0 opacity-50" />
                  <div className="min-w-0 break-words">{m.content}</div>
                </div>
              </div>
            );

          case 'activity':
            return (
              <Message from="system" key={m.id}>
                <div className="flex items-start gap-2 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 py-2 text-xs">
                  <Activity className="h-3.5 w-3.5 mt-0.5 text-[var(--color-brand-primary)] shrink-0" />
                  <div className="min-w-0">
                    <div className="font-medium text-[var(--color-text-secondary)]">
                      {m.activityType}
                    </div>
                    <pre className="mt-0.5 text-[var(--color-text-tertiary)] whitespace-pre-wrap break-all">
                      {JSON.stringify(m.content, null, 2)}
                    </pre>
                  </div>
                </div>
              </Message>
            );

          default:
            return null;
        }
      })}
    </div>
  );
}

// ─── Tool Call Card (Collapsible) ────────────────────────────────────────────

function ToolCallCard({ toolCall }: { toolCall: ChatToolCall }) {
  const isActive = toolCall.status === 'streaming' || toolCall.status === 'pending' || toolCall.status === 'executing';
  const isError = toolCall.status === 'error';

  const [open, setOpen] = useState(isActive);
  const prevActiveRef = useRef(isActive);

  // Auto-collapse when transitioning from active → done/error
  useEffect(() => {
    if (prevActiveRef.current && !isActive) {
      setOpen(false);
    }
    prevActiveRef.current = isActive;
  }, [isActive]);

  // Active tools are always forced open
  const effectiveOpen = isActive ? true : open;

  const statusIcon = {
    streaming: <Loader2 className="h-3 w-3 animate-spin text-[var(--color-info)]" />,
    pending: <Loader2 className="h-3 w-3 animate-spin text-[var(--color-warning)]" />,
    executing: <Loader2 className="h-3 w-3 animate-spin text-[var(--color-violet)]" />,
    completed: <CheckCircle2 className="h-3 w-3 text-[var(--color-success)]" />,
    error: <XCircle className="h-3 w-3 text-[var(--color-danger)]" />,
    'awaiting-approval': <ShieldAlert className="h-3 w-3 text-[var(--color-warning)]" />,
  }[toolCall.status];

  const statusLabel = {
    streaming: 'Streaming...',
    pending: 'Awaiting execution...',
    executing: 'Executing...',
    completed: 'Completed',
    error: 'Failed',
    'awaiting-approval': 'Awaiting approval',
  }[toolCall.status];

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
        {/* Trigger row */}
        <CollapsibleTrigger asChild disabled={!hasContent}>
          <button
            type="button"
            className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-[var(--color-background)] rounded-lg transition-colors"
          >
            <Wrench className="h-3.5 w-3.5 text-[var(--color-text-tertiary)] shrink-0" />

            {/* Tool name — shimmer when active */}
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

            {/* Status badge */}
            <span className="inline-flex items-center gap-1 text-[var(--color-text-tertiary)]">
              {statusIcon}
              <span className="hidden sm:inline">{statusLabel}</span>
            </span>

            {/* Expand chevron — hover-reveal */}
            {hasContent && (
              <ChevronDown
                className={`ml-auto h-3.5 w-3.5 text-[var(--color-text-tertiary)] shrink-0 transition-all duration-150
                  opacity-0 group-hover:opacity-100
                  ${effectiveOpen ? 'rotate-0' : '-rotate-90'}`}
              />
            )}
          </button>
        </CollapsibleTrigger>

        {/* Expandable content */}
        <CollapsibleContent>
          <div className="border-t border-[var(--color-border-light)] px-3 py-2 space-y-1">
            {toolCall.args && (
              <pre className="text-[var(--color-text-tertiary)] whitespace-pre-wrap break-all">
                {tryFormatJson(toolCall.args)}
              </pre>
            )}
            {toolCall.result && (
              <div className="text-[var(--color-text-tertiary)]">
                Result: {truncate(toolCall.result, 200)}
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

// ─── Markdown renderer ───────────────────────────────────────────────────────

/**
 * Renders markdown text as formatted HTML using react-markdown + remark-gfm.
 * Tailored for chat-sized messages: compact spacing, inline-friendly.
 */
function Markdown({ text }: { text: string }) {
  return (
    <div className="chat-markdown">
    <ReactMarkdown
      remarkPlugins={[remarkGfm]}
      components={{
        // Headings — compact, no huge top margin in chat
        h1: ({ children }) => <h3 className="text-sm font-bold mt-3 first:mt-0 mb-1">{children}</h3>,
        h2: ({ children }) => <h4 className="text-sm font-semibold mt-2 first:mt-0 mb-1">{children}</h4>,
        h3: ({ children }) => <h5 className="text-sm font-medium mt-2 first:mt-0 mb-0.5">{children}</h5>,
        // Paragraphs
        p: ({ children }) => <p className="mb-2 last:mb-0 leading-relaxed">{children}</p>,
        // Lists
        ul: ({ children }) => <ul className="mb-2 last:mb-0 pl-4 list-disc space-y-1">{children}</ul>,
        ol: ({ children }) => <ol className="mb-2 last:mb-0 pl-4 list-decimal space-y-1">{children}</ol>,
        li: ({ children }) => <li className="leading-relaxed">{children}</li>,
        // Inline code
        code: ({ children, className }) => {
          // Block code has a className like "language-xxx"
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
        // Strong / emphasis
        strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
        em: ({ children }) => <em>{children}</em>,
        // Links
        a: ({ href, children }) => (
          <a href={href} target="_blank" rel="noopener noreferrer" className="text-[var(--color-brand-primary)] underline hover:opacity-80">
            {children}
          </a>
        ),
        // Horizontal rule
        hr: () => <hr className="my-2 border-[var(--color-border-light)]" />,
        // Blockquote
        blockquote: ({ children }) => (
          <blockquote className="border-l-2 border-[var(--color-border-light)] pl-3 my-2 text-[var(--color-text-secondary)] italic">
            {children}
          </blockquote>
        ),
        // Table
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

// ─── Helpers ──────────────────────────────────────────────────────────────────

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

// ─── Approval Card ────────────────────────────────────────────────────────────

const severityConfig = {
  low: {
    badge: 'bg-[var(--color-info-10)] text-[var(--color-info)] border-[color-mix(in_srgb,var(--color-info)_25%,transparent)]',
    border: 'border-[color-mix(in_srgb,var(--color-info)_25%,transparent)]',
    icon: <ShieldAlert className="h-4 w-4 text-[var(--color-info)]" />,
    label: 'Low Risk',
  },
  medium: {
    badge: 'bg-[var(--color-warning-10)] text-[var(--color-warning)] border-[color-mix(in_srgb,var(--color-warning)_25%,transparent)]',
    border: 'border-[color-mix(in_srgb,var(--color-warning)_25%,transparent)]',
    icon: <ShieldAlert className="h-4 w-4 text-[var(--color-warning)]" />,
    label: 'Medium Risk',
  },
  high: {
    badge: 'bg-[var(--color-danger-10)] text-[var(--color-danger)] border-[color-mix(in_srgb,var(--color-danger)_25%,transparent)]',
    border: 'border-[color-mix(in_srgb,var(--color-danger)_25%,transparent)]',
    icon: <ShieldAlert className="h-4 w-4 text-[var(--color-danger)]" />,
    label: 'High Risk',
  },
} as const;

interface ApprovalCardProps {
  toolCall: ChatToolCall;
  approval?: PendingApproval;
  onApprove?: (toolCallId: string) => void;
  onReject?: (toolCallId: string, reason?: string) => void;
}

function ApprovalCard({ toolCall, approval, onApprove, onReject }: ApprovalCardProps) {
  const isAwaitingApproval = toolCall.status === 'awaiting-approval';
  const isCompleted = toolCall.status === 'completed';
  const wasApproved = isCompleted && toolCall.result === 'approved';

  let action = approval?.action ?? '';
  let description = approval?.description ?? '';
  let severity: 'low' | 'medium' | 'high' = approval?.severity ?? 'medium';

  if (!action && toolCall.args) {
    try {
      const parsed = JSON.parse(toolCall.args);
      action = parsed.action ?? '';
      description = parsed.description ?? '';
      if (['low', 'medium', 'high'].includes(parsed.severity)) {
        severity = parsed.severity;
      }
    } catch {
      // ignore parse errors
    }
  }

  const config = severityConfig[severity];

  // Completed state
  if (isCompleted) {
    return (
      <div
        className={`flex items-start gap-3 rounded-lg border ${
          wasApproved ? 'border-[color-mix(in_srgb,var(--color-success)_25%,transparent)] bg-[color-mix(in_srgb,var(--color-success)_8%,transparent)]' : 'border-[color-mix(in_srgb,var(--color-danger)_25%,transparent)] bg-[color-mix(in_srgb,var(--color-danger)_8%,transparent)]'
        } px-4 py-3 text-sm`}
      >
        {wasApproved ? (
          <ShieldCheck className="h-5 w-5 text-[var(--color-success)] mt-0.5 shrink-0" />
        ) : (
          <ShieldX className="h-5 w-5 text-[var(--color-danger)] mt-0.5 shrink-0" />
        )}
        <div className="min-w-0 flex-1">
          <div className="font-medium text-[var(--color-text-primary)]">
            {action || 'Action'} — {wasApproved ? 'Approved' : 'Rejected'}
          </div>
          {description && (
            <div className="mt-0.5 text-xs text-[var(--color-text-secondary)]">
              {description}
            </div>
          )}
        </div>
      </div>
    );
  }

  // Streaming or pending state
  if (toolCall.status === 'streaming' || toolCall.status === 'pending') {
    return (
      <div
        className={`flex items-start gap-3 rounded-lg border ${config.border} bg-[var(--color-surface)] px-4 py-3 text-sm`}
      >
        <Loader2 className="h-4 w-4 animate-spin text-[var(--color-text-tertiary)] mt-0.5 shrink-0" />
        <div className="text-shimmer font-medium">Preparing approval request...</div>
      </div>
    );
  }

  // Awaiting approval — interactive card
  return (
    <div className={`rounded-lg border-2 ${config.border} bg-[var(--color-surface)] overflow-hidden`}>
      {/* Header */}
      <div className="flex items-center gap-2 px-4 py-2.5 bg-[var(--color-surface-inset)] border-b border-[var(--color-border-light)]">
        {config.icon}
        <span className="font-semibold text-sm text-[var(--color-text-primary)]">
          Approval Required
        </span>
        <span
          className={`ml-auto inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-semibold ${config.badge}`}
        >
          {config.label}
        </span>
      </div>

      {/* Body */}
      <div className="px-4 py-3">
        <div className="font-medium text-sm text-[var(--color-text-primary)]">
          {action || 'Confirm Action'}
        </div>
        {description && (
          <div className="mt-1 text-xs text-[var(--color-text-secondary)] leading-relaxed">
            {description}
          </div>
        )}
      </div>

      {/* Actions */}
      {isAwaitingApproval && onApprove && onReject && (
        <div className="flex items-center gap-2 px-4 py-2.5 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
          <button
            type="button"
            onClick={() => onApprove(toolCall.toolCallId)}
            className="inline-flex items-center gap-1.5 rounded-md bg-[var(--color-success)] px-3 py-1.5 text-xs font-medium text-white shadow-sm hover:brightness-110 transition-colors"
          >
            <CheckCircle2 className="h-3.5 w-3.5" />
            Approve
          </button>
          <button
            type="button"
            onClick={() => onReject(toolCall.toolCallId)}
            className="inline-flex items-center gap-1.5 rounded-md bg-[var(--color-surface)] border border-[color-mix(in_srgb,var(--color-danger)_40%,transparent)] px-3 py-1.5 text-xs font-medium text-[var(--color-danger)] shadow-sm hover:bg-[color-mix(in_srgb,var(--color-danger)_6%,transparent)] transition-colors"
          >
            <XCircle className="h-3.5 w-3.5" />
            Reject
          </button>
        </div>
      )}
    </div>
  );
}
