import { useState } from 'react';
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
} from 'lucide-react';

import {
  Message,
  MessageContent,
  MessageAvatar,
} from '@/components/ai-elements';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Collapsible, CollapsibleTrigger, CollapsibleContent } from '@/components/ui/collapsible';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  AiDisplayToolCard,
  AiFollowUpSuggestionsCard,
  AiOptionSelectionCard,
  approvalSeverityConfig,
  ApprovalResolvedAlert,
  parseFollowUpSuggestions,
  ServerApprovalCard,
  tryParseJsonRecord,
} from '@/components/ai/chatSupport';
import type { ChatMessage, ChatToolCall, PendingApproval, ServerApprovalChatMessage } from '@/hooks/useAguiChat';

interface ChatMessageListProps {
  messages: ChatMessage[];
  isStreaming: boolean;
  pendingApprovals?: PendingApproval[];
  onApproveAction?: (toolCallId: string) => void;
  onRejectAction?: (toolCallId: string, reason?: string) => void;
  onDecideApproval?: (approval: ServerApprovalChatMessage, decision: 'Approve' | 'Reject') => void;
  onSelectToolCallOptions?: (toolCallId: string, selected: string[]) => void;
  onSelectFollowUpSuggestion?: (prompt: string) => void;
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
  onDecideApproval,
  onSelectToolCallOptions,
  onSelectFollowUpSuggestion,
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
                      <span className="inline-flex items-center gap-2 text-muted-foreground">
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Thinking...
                      </span>
                    </MessageContent>
                  ) : null}

                  {/* Tool calls — 8px gap between them */}
                  {m.toolCalls && m.toolCalls.length > 0 && (
                    <div className="mt-2 flex flex-col gap-2 w-full max-w-full">
                      {m.toolCalls.map((tc) => {
                        const parsedArgs = tc.args ? tryParseJsonRecord(tc.args) : null;

                        if (tc.toolCallName.startsWith('display_') && tc.status === 'completed' && parsedArgs) {
                          if (tc.toolCallName === 'display_follow_up_suggestions') {
                            const suggestions = parseFollowUpSuggestions(parsedArgs);
                            if (suggestions) {
                              return (
                                <AiFollowUpSuggestionsCard
                                  key={tc.toolCallId}
                                  suggestions={suggestions}
                                  onSelect={onSelectFollowUpSuggestion}
                                />
                              );
                            }
                          }

                          return (
                            <AiDisplayToolCard
                              key={tc.toolCallId}
                              toolName={tc.toolCallName}
                              args={parsedArgs}
                            />
                          );
                        }

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

                        if (tc.toolCallName === 'display_option_selector' && tc.status === 'awaiting-selection' && tc.optionSelection) {
                          return (
                            <AiOptionSelectionCard
                              key={tc.toolCallId}
                              toolCallId={tc.toolCallId}
                              selection={tc.optionSelection}
                              onSelect={onSelectToolCallOptions}
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

          // Spec 032 — server-owned approval card for a gated Medium/High mutation.
          // The shared card decides via the durable approvalRequestId; we close over the
          // concrete message so the typed onDecideApproval callback gets the right value.
          case 'approval':
            return (
              <ServerApprovalCard
                key={m.id}
                approval={m}
                onDecide={onDecideApproval ? (_, decision) => onDecideApproval(m, decision) : undefined}
              />
            );

          case 'step':
            return (
              <div
                key={m.id}
                className="flex items-center gap-2 px-2 py-1 text-xs text-muted-foreground"
              >
                {m.status === 'started' ? (
                  <Loader2 className="h-3 w-3 animate-spin" />
                ) : (
                  <CheckCircle2 className="h-3 w-3 text-success" />
                )}
                <ChevronRight className="h-3 w-3" />
                <span>{m.stepName}</span>
              </div>
            );

          case 'reasoning':
            return (
              <div
                key={m.id}
                className="ml-10 rounded-lg border border-border bg-muted/40 px-3 py-2 text-xs leading-relaxed text-muted-foreground"
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
                <div className="flex items-start gap-2 rounded-lg border border-border bg-card px-3 py-2 text-xs">
                  <Activity className="h-3.5 w-3.5 mt-0.5 text-primary shrink-0" />
                  <div className="min-w-0">
                    <div className="font-medium text-muted-foreground">
                      {m.activityType}
                    </div>
                    <pre className="mt-0.5 text-muted-foreground whitespace-pre-wrap break-all">
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
  const isActive =
    toolCall.status === 'streaming'
    || toolCall.status === 'pending'
    || toolCall.status === 'executing'
    || toolCall.status === 'awaiting-selection';
  const isError = toolCall.status === 'error';

  const [open, setOpen] = useState(() => isActive);

  // Active tools are always forced open
  const effectiveOpen = isActive ? true : open;

  const statusIcon = {
    streaming: <Loader2 className="h-3 w-3 animate-spin text-info" />,
    pending: <Loader2 className="h-3 w-3 animate-spin text-warning" />,
    executing: <Loader2 className="h-3 w-3 animate-spin text-[var(--chart-4)]" />,
    completed: <CheckCircle2 className="h-3 w-3 text-success" />,
    error: <XCircle className="h-3 w-3 text-destructive" />,
    'awaiting-approval': <ShieldAlert className="h-3 w-3 text-warning" />,
    'awaiting-selection': <ChevronRight className="h-3 w-3 text-info" />,
  }[toolCall.status];

  const statusLabel = {
    streaming: 'Streaming...',
    pending: 'Awaiting execution...',
    executing: 'Executing...',
    completed: 'Completed',
    error: 'Failed',
    'awaiting-approval': 'Awaiting approval',
    'awaiting-selection': 'Awaiting selection',
  }[toolCall.status];

  const hasContent = !!(toolCall.args || toolCall.result || toolCall.error);

  return (
    <Collapsible open={effectiveOpen} onOpenChange={setOpen}>
      <div
        className={`group rounded-lg border text-xs transition-colors ${
          isError
            ? 'border-destructive/25 bg-destructive/5'
            : 'border-border bg-card'
        }`}
      >
        {/* Trigger row */}
        <CollapsibleTrigger asChild disabled={!hasContent}>
          <button
            type="button"
            className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-accent rounded-lg transition-colors"
          >
            <Wrench className="h-3.5 w-3.5 text-muted-foreground shrink-0" />

            {/* Tool name — shimmer when active */}
            <span
              className={`font-medium ${
                isActive
                  ? 'text-shimmer'
                  : isError
                    ? 'text-destructive'
                    : 'text-muted-foreground'
              }`}
            >
              {toolCall.toolCallName}
            </span>

            {/* Status badge */}
            <span className="inline-flex items-center gap-1 text-muted-foreground">
              {statusIcon}
              <span className="hidden sm:inline">{statusLabel}</span>
            </span>

            {/* Expand chevron — hover-reveal */}
            {hasContent && (
              <ChevronDown
                className={`ml-auto h-3.5 w-3.5 text-muted-foreground shrink-0 transition-all duration-150
                  opacity-0 group-hover:opacity-100
                  ${effectiveOpen ? 'rotate-0' : '-rotate-90'}`}
              />
            )}
          </button>
        </CollapsibleTrigger>

        {/* Expandable content */}
        <CollapsibleContent>
          <div className="border-t border-border px-3 py-2 space-y-1">
            {toolCall.args && (
              <pre className="text-muted-foreground whitespace-pre-wrap break-all">
                {tryFormatJson(toolCall.args)}
              </pre>
            )}
            {toolCall.result && (
              <div className="text-muted-foreground">
                Result: {truncate(toolCall.result, 200)}
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
        // Strong / emphasis
        strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
        em: ({ children }) => <em>{children}</em>,
        // Links
        a: ({ href, children }) => (
          <a href={href} target="_blank" rel="noopener noreferrer" className="text-primary underline hover:opacity-80">
            {children}
          </a>
        ),
        // Horizontal rule
        hr: () => <hr className="my-2 border-border" />,
        // Blockquote
        blockquote: ({ children }) => (
          <blockquote className="border-l-2 border-border pl-3 my-2 text-muted-foreground italic">
            {children}
          </blockquote>
        ),
        // Table
        table: ({ children }) => (
          <div className="my-2 overflow-hidden rounded-md border">
            <Table className="text-xs">{children}</Table>
          </div>
        ),
        thead: ({ children }) => <TableHeader className="bg-muted">{children}</TableHeader>,
        tbody: ({ children }) => <TableBody>{children}</TableBody>,
        tr: ({ children }) => <TableRow>{children}</TableRow>,
        th: ({ children }) => (
          <TableHead className="h-8 whitespace-normal">{children}</TableHead>
        ),
        td: ({ children }) => (
          <TableCell className="whitespace-normal">{children}</TableCell>
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

  const config = approvalSeverityConfig[severity];

  // Completed state
  if (isCompleted) {
    return (
      <ApprovalResolvedAlert
        approved={wasApproved}
        title={action || 'Action'}
        detail={description || undefined}
      />
    );
  }

  // Streaming or pending state
  if (toolCall.status === 'streaming' || toolCall.status === 'pending') {
    return (
      <div
        className={`flex items-start gap-3 rounded-lg border ${config.border} bg-card px-4 py-3 text-sm`}
      >
        <Loader2 className="h-4 w-4 animate-spin text-muted-foreground mt-0.5 shrink-0" />
        <div className="text-shimmer font-medium">Preparing approval request...</div>
      </div>
    );
  }

  // Awaiting approval — interactive card
  return (
    <div className={`rounded-lg border-2 ${config.border} bg-card overflow-hidden`}>
      {/* Header */}
      <div className="flex items-center gap-2 px-4 py-2.5 bg-muted border-b border-border">
        {config.icon}
        <span className="font-semibold text-sm text-foreground">
          Approval required
        </span>
        <Badge variant={config.badge} className="ml-auto">
          {config.label}
        </Badge>
      </div>

      {/* Body */}
      <div className="px-4 py-3">
        <div className="font-medium text-sm text-foreground">
          {action || 'Confirm action'}
        </div>
        {description && (
          <div className="mt-1 text-xs text-muted-foreground leading-relaxed">
            {description}
          </div>
        )}
      </div>

      {/* Actions */}
      {isAwaitingApproval && onApprove && onReject && (
        <div className="flex items-center gap-2 px-4 py-2.5 border-t border-border bg-muted">
          <Button type="button" size="sm" variant="agent" onClick={() => onApprove(toolCall.toolCallId)}>
            <CheckCircle2 />
            Approve
          </Button>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => onReject(toolCall.toolCallId)}
            className="text-destructive hover:text-destructive"
          >
            <XCircle />
            Reject
          </Button>
        </div>
      )}
    </div>
  );
}
