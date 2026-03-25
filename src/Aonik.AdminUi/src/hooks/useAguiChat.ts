import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from '@/auth';
import {
  streamAguiChat,
  generateId,
  type Message,
  type UserMessage,
  type AssistantMessage,
  type ToolMessage,
  type ToolCall,
  type Tool,
  type FrontendToolHandler,
  type FrontendToolContext,
  type FrontendToolRegistration,
  type AguiStreamCallbacks,
  type ToolCallStartEvent,
  type ToolCallArgsEvent,
  type ToolCallEndEvent,
  type ToolCallResultEvent,
  type StepStartedEvent,
  type StepFinishedEvent,
  type ReasoningMessageContentEvent,
  type ActivitySnapshotEvent,
  type MessagesSnapshotEvent,
} from '@/lib/agui-client';
import type { ThreadDetail } from '@/hooks/useThreads';

// ─── Chat Message Types ───────────────────────────────────────────────────────

/** Visual representation of a tool call in the UI. */
export interface ChatToolCall {
  toolCallId: string;
  toolCallName: string;
  args: string;
  /** 'streaming' while args are being received, 'pending' waiting for execution,
   *  'executing' during frontend execution, 'awaiting-approval' for confirmAction,
   *  'completed' with result, 'error' on failure */
  status: 'streaming' | 'pending' | 'executing' | 'awaiting-approval' | 'completed' | 'error';
  result?: string;
  error?: string;
}

/** Parameters for the confirmAction frontend tool. */
export interface ConfirmActionArgs {
  action: string;
  description: string;
  severity?: 'low' | 'medium' | 'high';
}

/** A pending approval waiting for user interaction. */
export interface PendingApproval {
  toolCallId: string;
  action: string;
  description: string;
  severity: 'low' | 'medium' | 'high';
  resolve: (result: string) => void;
}

/** Visual representation of a step indicator in the UI. */
export interface ChatStep {
  stepName: string;
  status: 'started' | 'finished';
}

/**
 * Rich chat message model that supports all AG-UI message types.
 * The `type` discriminator controls rendering in the UI.
 */
export type ChatMessage =
  | { type: 'user'; id: string; content: string }
  | { type: 'assistant'; id: string; content: string; toolCalls?: ChatToolCall[] }
  | { type: 'tool-result'; id: string; toolCallId: string; toolCallName: string; content: string; error?: string }
  | { type: 'step'; id: string; stepName: string; status: 'started' | 'finished' }
  | { type: 'reasoning'; id: string; content: string }
  | { type: 'activity'; id: string; activityType: string; content: Record<string, unknown> };

// ─── Frontend Tool Registration ───────────────────────────────────────────────

export interface FrontendToolConfig {
  name: string;
  description: string;
  parameters: unknown;
  handler: FrontendToolHandler;
}

// ─── Hook Return Type ─────────────────────────────────────────────────────────

export interface UseAguiChatReturn {
  messages: ChatMessage[];
  draft: string;
  setDraft: (value: string) => void;
  isStreaming: boolean;
  streamError: string | null;
  activeSteps: ChatStep[];
  handleSend: () => Promise<void>;
  stopStreaming: () => void;
  resetChat: () => void;
  /** Register a frontend tool the agent can call. */
  registerTool: (config: FrontendToolConfig) => void;
  /** Unregister a frontend tool. */
  unregisterTool: (name: string) => void;
  /** Pending approvals waiting for user interaction (confirmAction tool). */
  pendingApprovals: PendingApproval[];
  /** Approve a pending confirmAction tool call. */
  approveAction: (toolCallId: string) => void;
  /** Reject a pending confirmAction tool call. */
  rejectAction: (toolCallId: string, reason?: string) => void;
  /** The current thread ID (set after first message or thread load). */
  threadId: string | null;
  /** Load a persisted thread's messages into the chat view. */
  loadThread: (thread: ThreadDetail) => void;
}

// ─── Hook Implementation ──────────────────────────────────────────────────────

export function useAguiChat(): UseAguiChatReturn {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [streamError, setStreamError] = useState<string | null>(null);
  const [activeSteps, setActiveSteps] = useState<ChatStep[]>([]);
  const [pendingApprovals, setPendingApprovals] = useState<PendingApproval[]>([]);
  const abortControllerRef = useRef<AbortController | null>(null);
  const threadIdRef = useRef<string | null>(null);
  const { getAccessToken } = useAuth();

  // Frontend tool registry
  const frontendToolsRef = useRef<Map<string, FrontendToolRegistration>>(new Map());

  // Ref for pending approval resolve callbacks (keyed by toolCallId)
  const pendingApprovalResolversRef = useRef<Map<string, (result: string) => void>>(new Map());

  const registerTool = useCallback((config: FrontendToolConfig) => {
    frontendToolsRef.current.set(config.name, {
      tool: {
        name: config.name,
        description: config.description,
        parameters: config.parameters,
      },
      handler: config.handler,
    });
  }, []);

  const unregisterTool = useCallback((name: string) => {
    frontendToolsRef.current.delete(name);
  }, []);

  // ─── confirmAction Tool ───────────────────────────────────────────────────

  /**
   * Auto-register the confirmAction frontend tool. The handler returns a
   * Promise that resolves only when the user clicks Approve or Reject in the UI.
   */
  useEffect(() => {
    const toolName = 'confirmAction';

    const handler: FrontendToolHandler = (args: Record<string, unknown>, context: FrontendToolContext) => {
      return new Promise<string>((resolve) => {
        const action = (args.action as string) ?? 'Unknown action';
        const description = (args.description as string) ?? '';
        const severity = (['low', 'medium', 'high'].includes(args.severity as string)
          ? args.severity
          : 'medium') as 'low' | 'medium' | 'high';

        const { toolCallId } = context;

        const approval: PendingApproval = {
          toolCallId,
          action,
          description,
          severity,
          resolve,
        };

        // Store the resolver so approve/reject can trigger it
        pendingApprovalResolversRef.current.set(toolCallId, resolve);

        setPendingApprovals((prev) => [...prev, approval]);

        // Update the matching tool call in messages to 'awaiting-approval'
        setMessages((prev) =>
          prev.map((m) => {
            if (m.type === 'assistant' && m.toolCalls) {
              const toolCalls = m.toolCalls.map((tc) =>
                tc.toolCallId === toolCallId
                  ? { ...tc, status: 'awaiting-approval' as const }
                  : tc
              );
              return { ...m, toolCalls };
            }
            return m;
          })
        );
      });
    };

    frontendToolsRef.current.set(toolName, {
      tool: {
        name: toolName,
        description:
          'Request user approval before executing a mutating action. The user will see an approval card with Approve/Reject buttons. Use this for any action that creates, modifies, or deletes data.',
        parameters: {
          type: 'object',
          properties: {
            action: {
              type: 'string',
              description: 'Short name of the action (e.g., "Create Invoice", "Cancel Payment")',
            },
            description: {
              type: 'string',
              description: 'Detailed description of what will happen if approved',
            },
            severity: {
              type: 'string',
              enum: ['low', 'medium', 'high'],
              description: 'Risk level of the action. Defaults to medium.',
            },
          },
          required: ['action', 'description'],
        },
      },
      handler,
    });

    return () => {
      frontendToolsRef.current.delete(toolName);
    };
  }, []);

  const approveAction = useCallback((toolCallId: string) => {
    const resolver = pendingApprovalResolversRef.current.get(toolCallId);
    if (resolver) {
      resolver('approved');
      pendingApprovalResolversRef.current.delete(toolCallId);
    }

    // Remove from pending approvals state
    setPendingApprovals((prev) => prev.filter((a) => a.toolCallId !== toolCallId));

    // Update the tool call status in messages
    setMessages((prev) =>
      prev.map((m) => {
        if (m.type === 'assistant' && m.toolCalls) {
          const toolCalls = m.toolCalls.map((tc) =>
            tc.toolCallId === toolCallId && tc.status === 'awaiting-approval'
              ? { ...tc, status: 'completed' as const, result: 'approved' }
              : tc
          );
          return { ...m, toolCalls };
        }
        return m;
      })
    );
  }, []);

  const rejectAction = useCallback((toolCallId: string, reason?: string) => {
    const result = reason ? `rejected: ${reason}` : 'rejected';
    const resolver = pendingApprovalResolversRef.current.get(toolCallId);
    if (resolver) {
      resolver(result);
      pendingApprovalResolversRef.current.delete(toolCallId);
    }

    // Remove from pending approvals state
    setPendingApprovals((prev) => prev.filter((a) => a.toolCallId !== toolCallId));

    // Update the tool call status in messages
    setMessages((prev) =>
      prev.map((m) => {
        if (m.type === 'assistant' && m.toolCalls) {
          const toolCalls = m.toolCalls.map((tc) =>
            tc.toolCallId === toolCallId && tc.status === 'awaiting-approval'
              ? { ...tc, status: 'completed' as const, result }
              : tc
          );
          return { ...m, toolCalls };
        }
        return m;
      })
    );
  }, []);

  const resetChat = useCallback(() => {
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
    threadIdRef.current = null;
    // Reject any pending approvals so the re-run loop doesn't hang
    for (const [, resolver] of pendingApprovalResolversRef.current) {
      resolver('rejected: chat reset');
    }
    pendingApprovalResolversRef.current.clear();
    setMessages([]);
    setDraft('');
    setIsStreaming(false);
    setStreamError(null);
    setActiveSteps([]);
    setPendingApprovals([]);
  }, []);

  const stopStreaming = useCallback(() => {
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
    setIsStreaming(false);
  }, []);

  /** Load a persisted thread's messages into the chat view. */
  const loadThread = useCallback((thread: ThreadDetail) => {
    // Abort any in-flight stream
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;

    // Reject any pending approvals
    for (const [, resolver] of pendingApprovalResolversRef.current) {
      resolver('rejected: thread changed');
    }
    pendingApprovalResolversRef.current.clear();

    // Set the thread ID so subsequent messages continue this thread
    threadIdRef.current = thread.id;

    // Convert persisted messages to ChatMessage[]
    const chatMessages: ChatMessage[] = thread.messages.map((msg) => {
      if (msg.role === 'user') {
        return { type: 'user' as const, id: msg.id, content: msg.content };
      }
      return { type: 'assistant' as const, id: msg.id, content: msg.content };
    });

    setMessages(chatMessages);
    setDraft('');
    setIsStreaming(false);
    setStreamError(null);
    setActiveSteps([]);
    setPendingApprovals([]);
  }, []);

  const handleSend = useCallback(async () => {
    const text = draft.trim();
    if (!text || isStreaming) return;

    setStreamError(null);
    setActiveSteps([]);

    const userMessageId = generateId();
    const assistantMessageId = generateId();

    // Add user message to UI
    setMessages((prev) => [
      ...prev,
      { type: 'user', id: userMessageId, content: text },
    ]);
    setDraft('');
    setIsStreaming(true);

    // Build AG-UI message history from current chat messages
    const aguiMessages = buildAguiMessages(messages, {
      role: 'user',
      content: text,
      id: userMessageId,
    });

    // Collect tool definitions from frontend registry
    const tools: Tool[] = Array.from(frontendToolsRef.current.values()).map((r) => r.tool);

    const abortController = new AbortController();
    abortControllerRef.current = abortController;

    // Track current streaming state
    let currentAssistantId = assistantMessageId;

    // Track tool calls being streamed in the current assistant message
    const streamingToolCalls = new Map<string, { name: string; args: string }>();

    try {
      // Add placeholder assistant message
      setMessages((prev) => [
        ...prev,
        { type: 'assistant', id: currentAssistantId, content: '' },
      ]);

      const callbacks: AguiStreamCallbacks = {
        onRunStarted: (event) => {
          threadIdRef.current = event.threadId;
        },

        onTextMessageStart: (event) => {
          // If we get a new message ID that differs from our placeholder, update
          if (event.messageId !== currentAssistantId) {
            currentAssistantId = event.messageId;
            setMessages((prev) => [
              ...prev,
              { type: 'assistant', id: currentAssistantId, content: '' },
            ]);
          }
        },

        onTextMessageContent: (event) => {
          setMessages((prev) =>
            prev.map((m) =>
              m.id === currentAssistantId && m.type === 'assistant'
                ? { ...m, content: m.content + event.delta }
                : m
            )
          );
        },

        onTextMessageEnd: () => {
          // Text message streaming complete
        },

        // Tool call lifecycle
        onToolCallStart: (event: ToolCallStartEvent) => {
          streamingToolCalls.set(event.toolCallId, { name: event.toolCallName, args: '' });
          setMessages((prev) =>
            prev.map((m) => {
              if (m.id === currentAssistantId && m.type === 'assistant') {
                const toolCalls: ChatToolCall[] = [
                  ...(m.toolCalls ?? []),
                  {
                    toolCallId: event.toolCallId,
                    toolCallName: event.toolCallName,
                    args: '',
                    status: 'streaming',
                  },
                ];
                return { ...m, toolCalls };
              }
              return m;
            })
          );
        },

        onToolCallArgs: (event: ToolCallArgsEvent) => {
          const tc = streamingToolCalls.get(event.toolCallId);
          if (tc) tc.args += event.delta;

          setMessages((prev) =>
            prev.map((m) => {
              if (m.id === currentAssistantId && m.type === 'assistant' && m.toolCalls) {
                const toolCalls = m.toolCalls.map((tc) =>
                  tc.toolCallId === event.toolCallId
                    ? { ...tc, args: tc.args + event.delta }
                    : tc
                );
                return { ...m, toolCalls };
              }
              return m;
            })
          );
        },

        onToolCallEnd: (event: ToolCallEndEvent) => {
          setMessages((prev) =>
            prev.map((m) => {
              if (m.id === currentAssistantId && m.type === 'assistant' && m.toolCalls) {
                const toolCalls = m.toolCalls.map((tc) =>
                  tc.toolCallId === event.toolCallId
                    ? { ...tc, status: 'pending' as const }
                    : tc
                );
                return { ...m, toolCalls };
              }
              return m;
            })
          );
        },

        onToolCallResult: (event: ToolCallResultEvent) => {
          // Server-side tool result — add as a separate message
          const toolCall = streamingToolCalls.get(event.toolCallId);
          setMessages((prev) => [
            ...prev,
            {
              type: 'tool-result',
              id: event.messageId ?? generateId(),
              toolCallId: event.toolCallId,
              toolCallName: toolCall?.name ?? 'unknown',
              content: event.content,
            },
          ]);

          // Mark the tool call as completed in the assistant message
          setMessages((prev) =>
            prev.map((m) => {
              if (m.type === 'assistant' && m.toolCalls) {
                const toolCalls = m.toolCalls.map((tc) =>
                  tc.toolCallId === event.toolCallId
                    ? { ...tc, status: 'completed' as const, result: event.content }
                    : tc
                );
                return { ...m, toolCalls };
              }
              return m;
            })
          );
        },

        // Step lifecycle
        onStepStarted: (event: StepStartedEvent) => {
          const step: ChatStep = { stepName: event.stepName, status: 'started' };
          setActiveSteps((prev) => [...prev, step]);
          setMessages((prev) => [
            ...prev,
            { type: 'step', id: generateId(), stepName: event.stepName, status: 'started' },
          ]);
        },

        onStepFinished: (event: StepFinishedEvent) => {
          setActiveSteps((prev) =>
            prev.map((s) =>
              s.stepName === event.stepName ? { ...s, status: 'finished' } : s
            )
          );
          setMessages((prev) =>
            prev.map((m) =>
              m.type === 'step' && m.stepName === event.stepName && m.status === 'started'
                ? { ...m, status: 'finished' }
                : m
            )
          );
        },

        // Reasoning
        onReasoningMessageContent: (event: ReasoningMessageContentEvent) => {
          setMessages((prev) => {
            const existing = prev.find(
              (m) => m.type === 'reasoning' && m.id === event.messageId
            );
            if (existing && existing.type === 'reasoning') {
              return prev.map((m) =>
                m.id === event.messageId && m.type === 'reasoning'
                  ? { ...m, content: m.content + event.delta }
                  : m
              );
            }
            return [
              ...prev,
              { type: 'reasoning', id: event.messageId, content: event.delta },
            ];
          });
        },

        // Activity
        onActivitySnapshot: (event: ActivitySnapshotEvent) => {
          setMessages((prev) => {
            const existing = prev.find((m) => m.id === event.messageId);
            if (existing) {
              if (event.replace === false) return prev;
              return prev.map((m) =>
                m.id === event.messageId
                  ? { type: 'activity', id: event.messageId, activityType: event.activityType, content: event.content }
                  : m
              );
            }
            return [
              ...prev,
              { type: 'activity', id: event.messageId, activityType: event.activityType, content: event.content },
            ];
          });
        },

        // Messages snapshot — replace entire message history
        onMessagesSnapshot: (event: MessagesSnapshotEvent) => {
          const chatMessages = event.messages.map(aguiMessageToChatMessage).filter(Boolean) as ChatMessage[];
          setMessages(chatMessages);
        },

        onRunError: (event) => {
          setStreamError(event.message || 'An error occurred');
          setMessages((prev) =>
            prev.map((m) =>
              m.id === currentAssistantId && m.type === 'assistant'
                ? {
                    ...m,
                    content:
                      m.content ||
                      `Error: ${event.message || 'Something went wrong'}`,
                  }
                : m
            )
          );
        },
      };

      const threadId = threadIdRef.current ?? generateId();
      const runId = generateId();

      await streamAguiChat({
        input: {
          threadId,
          runId,
          messages: aguiMessages,
          tools: tools.length > 0 ? tools : undefined,
        },
        callbacks,
        getAccessToken,
        signal: abortController.signal,
        frontendTools: frontendToolsRef.current.size > 0 ? frontendToolsRef.current : undefined,
      });

      // If the assistant message ended up empty, show a fallback
      setMessages((prev) =>
        prev.map((m) =>
          m.id === currentAssistantId && m.type === 'assistant' && !m.content && !m.toolCalls?.length
            ? { ...m, content: 'No response received from the agent.' }
            : m
        )
      );
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        return;
      }

      const errorMessage =
        err instanceof Error ? err.message : 'An unexpected error occurred';
      setStreamError(errorMessage);

      setMessages((prev) => {
        const hasAssistant = prev.some((m) => m.id === currentAssistantId);
        if (hasAssistant) {
          return prev.map((m) =>
            m.id === currentAssistantId && m.type === 'assistant'
              ? { ...m, content: m.content || `Error: ${errorMessage}` }
              : m
          );
        }
        return [
          ...prev,
          {
            type: 'assistant' as const,
            id: currentAssistantId,
            content: `Error: ${errorMessage}`,
          },
        ];
      });
    } finally {
      setIsStreaming(false);
      abortControllerRef.current = null;
    }
  }, [draft, isStreaming, messages, getAccessToken]);

  return {
    messages,
    draft,
    setDraft,
    isStreaming,
    streamError,
    activeSteps,
    handleSend,
    stopStreaming,
    resetChat,
    registerTool,
    unregisterTool,
    pendingApprovals,
    approveAction,
    rejectAction,
    threadId: threadIdRef.current,
    loadThread,
  };
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Converts the chat UI messages into AG-UI protocol messages for the backend.
 * Includes full message history with tool calls and tool results.
 */
function buildAguiMessages(
  chatMessages: ChatMessage[],
  newUserMessage: { role: 'user'; content: string; id: string }
): Message[] {
  const result: Message[] = [];

  for (const m of chatMessages) {
    switch (m.type) {
      case 'user':
        result.push({ id: m.id, role: 'user', content: m.content } satisfies UserMessage);
        break;

      case 'assistant': {
        const assistantMsg: AssistantMessage = {
          id: m.id,
          role: 'assistant',
          content: m.content || undefined,
        };
        if (m.toolCalls && m.toolCalls.length > 0) {
          assistantMsg.toolCalls = m.toolCalls.map(
            (tc): ToolCall => ({
              id: tc.toolCallId,
              type: 'function',
              function: {
                name: tc.toolCallName,
                arguments: tc.args,
              },
            })
          );
        }
        result.push(assistantMsg);
        break;
      }

      case 'tool-result':
        result.push({
          id: m.id,
          role: 'tool',
          content: m.content,
          toolCallId: m.toolCallId,
          ...(m.error ? { error: m.error } : {}),
        } satisfies ToolMessage);
        break;

      // Steps, reasoning, activity are not sent back to the agent
      default:
        break;
    }
  }

  result.push({
    id: newUserMessage.id,
    role: 'user',
    content: newUserMessage.content,
  } satisfies UserMessage);

  return result;
}

/**
 * Converts an AG-UI protocol message (from MESSAGES_SNAPSHOT) into a ChatMessage.
 */
function aguiMessageToChatMessage(msg: Message): ChatMessage | null {
  switch (msg.role) {
    case 'user':
      return {
        type: 'user',
        id: msg.id,
        content: typeof msg.content === 'string' ? msg.content : '',
      };
    case 'assistant':
      return {
        type: 'assistant',
        id: msg.id,
        content: msg.content ?? '',
        toolCalls: msg.toolCalls?.map((tc) => ({
          toolCallId: tc.id,
          toolCallName: tc.function.name,
          args: tc.function.arguments,
          status: 'completed' as const,
        })),
      };
    case 'tool':
      return {
        type: 'tool-result',
        id: msg.id,
        toolCallId: msg.toolCallId,
        toolCallName: 'tool',
        content: msg.content,
        error: msg.error,
      };
    case 'reasoning':
      return {
        type: 'reasoning',
        id: msg.id,
        content: msg.content,
      };
    case 'activity':
      return {
        type: 'activity',
        id: msg.id,
        activityType: msg.activityType,
        content: msg.content,
      };
    default:
      return null;
  }
}
