import { Loader2, Wrench, CheckCircle2, XCircle, Brain, Activity, ChevronRight, ShieldAlert, ShieldCheck, ShieldX } from 'lucide-react';

import {
  Message,
  MessageContent,
} from '@/components/ai-elements';
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
 */
export function ChatMessageList({ messages, isStreaming, pendingApprovals, onApproveAction, onRejectAction }: ChatMessageListProps) {
  return (
    <>
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
              <Message from="assistant" key={m.id}>
                <MessageContent from="assistant">
                  {m.content || (
                    isStreaming && !m.toolCalls?.length ? (
                      <span className="inline-flex items-center gap-2 text-[var(--color-text-tertiary)]">
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Thinking...
                      </span>
                    ) : null
                  )}
                </MessageContent>
                {m.toolCalls && m.toolCalls.length > 0 && (
                  <div className="mt-1 space-y-1 w-full max-w-full">
                    {m.toolCalls.map((tc) => {
                      // Render ApprovalCard for confirmAction tool calls
                      if (tc.toolCallName === 'confirmAction') {
                        const approval = pendingApprovals?.find((a) => a.toolCallId === tc.toolCallId);
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
            );

          case 'tool-result':
            return (
              <Message from="system" key={m.id}>
                <div className="flex items-start gap-2 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 py-2 text-xs">
                  <Wrench className="h-3.5 w-3.5 mt-0.5 text-[var(--color-text-tertiary)] shrink-0" />
                  <div className="min-w-0">
                    <div className="font-medium text-[var(--color-text-secondary)]">
                      Tool result: {m.toolCallName}
                    </div>
                    <div className="mt-0.5 text-[var(--color-text-tertiary)] break-all">
                      {m.error ? (
                        <span className="text-red-500">Error: {m.error}</span>
                      ) : (
                        truncate(m.content, 200)
                      )}
                    </div>
                  </div>
                </div>
              </Message>
            );

          case 'step':
            return (
              <div
                key={m.id}
                className="flex items-center gap-2 px-2 py-1 text-xs text-[var(--color-text-tertiary)]"
              >
                {m.status === 'started' ? (
                  <Loader2 className="h-3 w-3 animate-spin" />
                ) : (
                  <CheckCircle2 className="h-3 w-3 text-green-500" />
                )}
                <ChevronRight className="h-3 w-3" />
                <span>{m.stepName}</span>
              </div>
            );

          case 'reasoning':
            return (
              <Message from="system" key={m.id}>
                <div className="flex items-start gap-2 rounded-lg border border-dashed border-[var(--color-border)] bg-[var(--color-surface-inset)] px-3 py-2 text-xs italic text-[var(--color-text-tertiary)]">
                  <Brain className="h-3.5 w-3.5 mt-0.5 shrink-0" />
                  <div className="min-w-0 break-words">{m.content}</div>
                </div>
              </Message>
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
    </>
  );
}

// ─── Tool Call Card ───────────────────────────────────────────────────────────

function ToolCallCard({ toolCall }: { toolCall: ChatToolCall }) {
  const statusIcon = {
    streaming: <Loader2 className="h-3 w-3 animate-spin text-blue-500" />,
    pending: <Loader2 className="h-3 w-3 animate-spin text-amber-500" />,
    executing: <Loader2 className="h-3 w-3 animate-spin text-purple-500" />,
    completed: <CheckCircle2 className="h-3 w-3 text-green-500" />,
    error: <XCircle className="h-3 w-3 text-red-500" />,
  }[toolCall.status];

  const statusLabel = {
    streaming: 'Streaming args...',
    pending: 'Awaiting execution...',
    executing: 'Executing...',
    completed: 'Completed',
    error: 'Failed',
  }[toolCall.status];

  return (
    <div className="flex items-start gap-2 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 py-2 text-xs">
      <Wrench className="h-3.5 w-3.5 mt-0.5 text-[var(--color-text-tertiary)] shrink-0" />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="font-medium text-[var(--color-text-secondary)]">
            {toolCall.toolCallName}
          </span>
          <span className="inline-flex items-center gap-1 text-[var(--color-text-tertiary)]">
            {statusIcon}
            {statusLabel}
          </span>
        </div>
        {toolCall.args && (
          <pre className="mt-1 text-[var(--color-text-tertiary)] whitespace-pre-wrap break-all">
            {tryFormatJson(toolCall.args)}
          </pre>
        )}
        {toolCall.result && (
          <div className="mt-1 text-[var(--color-text-tertiary)]">
            Result: {truncate(toolCall.result, 200)}
          </div>
        )}
        {toolCall.error && (
          <div className="mt-1 text-red-500">
            Error: {toolCall.error}
          </div>
        )}
      </div>
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
    badge: 'bg-blue-100 text-blue-700 border-blue-200',
    border: 'border-blue-200',
    icon: <ShieldAlert className="h-4 w-4 text-blue-500" />,
    label: 'Low Risk',
  },
  medium: {
    badge: 'bg-amber-100 text-amber-700 border-amber-200',
    border: 'border-amber-200',
    icon: <ShieldAlert className="h-4 w-4 text-amber-500" />,
    label: 'Medium Risk',
  },
  high: {
    badge: 'bg-red-100 text-red-700 border-red-200',
    border: 'border-red-200',
    icon: <ShieldAlert className="h-4 w-4 text-red-500" />,
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
  const wasRejected = isCompleted && toolCall.result?.startsWith('rejected');

  // Parse action/description from args or approval state
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

  // Completed state — show result
  if (isCompleted) {
    return (
      <div className={`flex items-start gap-3 rounded-lg border ${wasApproved ? 'border-green-200 bg-green-50' : 'border-red-200 bg-red-50'} px-4 py-3 text-sm`}>
        {wasApproved ? (
          <ShieldCheck className="h-5 w-5 text-green-600 mt-0.5 shrink-0" />
        ) : (
          <ShieldX className="h-5 w-5 text-red-600 mt-0.5 shrink-0" />
        )}
        <div className="min-w-0 flex-1">
          <div className="font-medium text-[var(--color-text-primary)]">
            {action || 'Action'} — {wasApproved ? 'Approved' : 'Rejected'}
          </div>
          {description && (
            <div className="mt-0.5 text-xs text-[var(--color-text-secondary)]">{description}</div>
          )}
        </div>
      </div>
    );
  }

  // Streaming or pending state — show loading
  if (toolCall.status === 'streaming' || toolCall.status === 'pending') {
    return (
      <div className={`flex items-start gap-3 rounded-lg border ${config.border} bg-[var(--color-surface)] px-4 py-3 text-sm`}>
        <Loader2 className="h-4 w-4 animate-spin text-[var(--color-text-tertiary)] mt-0.5 shrink-0" />
        <div className="text-[var(--color-text-secondary)]">Preparing approval request...</div>
      </div>
    );
  }

  // Awaiting approval — show the interactive card
  return (
    <div className={`rounded-lg border-2 ${config.border} bg-[var(--color-surface)] overflow-hidden`}>
      {/* Header */}
      <div className="flex items-center gap-2 px-4 py-2.5 bg-[var(--color-surface-inset)] border-b border-[var(--color-border-light)]">
        {config.icon}
        <span className="font-semibold text-sm text-[var(--color-text-primary)]">
          Approval Required
        </span>
        <span className={`ml-auto inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-semibold ${config.badge}`}>
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
            className="inline-flex items-center gap-1.5 rounded-md bg-green-600 px-3 py-1.5 text-xs font-medium text-white shadow-sm hover:bg-green-700 transition-colors"
          >
            <CheckCircle2 className="h-3.5 w-3.5" />
            Approve
          </button>
          <button
            type="button"
            onClick={() => onReject(toolCall.toolCallId)}
            className="inline-flex items-center gap-1.5 rounded-md bg-[var(--color-surface)] border border-red-300 px-3 py-1.5 text-xs font-medium text-red-600 shadow-sm hover:bg-red-50 transition-colors"
          >
            <XCircle className="h-3.5 w-3.5" />
            Reject
          </button>
        </div>
      )}
    </div>
  );
}
