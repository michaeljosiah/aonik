import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useAuth } from '@/auth';
import { api } from '@/lib/api';
import {
  type ActivityMessage,
  type ActivitySnapshotEvent,
  type AguiStreamCallbacks,
  type AssistantMessage,
  type CustomEvent,
  type FrontendToolHandler,
  type FrontendToolRegistration,
  generateId,
  type Message,
  type MessagesSnapshotEvent,
  type ReasoningMessage,
  type ReasoningMessageContentEvent,
  streamAguiChat,
  type StepFinishedEvent,
  type StepStartedEvent,
  type Tool,
  type ToolCall,
  type ToolCallArgsEvent,
  type ToolCallEndEvent,
  type ToolCallResultEvent,
  type ToolCallStartEvent,
  type ToolMessage,
  type UserMessage,
} from '@/lib/agui-client';
import type { ThreadDetail, ThreadMessageDto } from '@/hooks/useThreads';
import {
  type FollowUpSuggestionsState,
  type OptionSelectionState,
  type SharedToolStatus,
  type SpeechChunkPayload,
  type SpeechRenderPayload,
  parseFollowUpSuggestions,
  tryParseJsonRecord,
  useAiChatFrontendTools,
  useAiChatVoicePlayback,
  type VoiceRenderDetails,
} from '@/components/ai/chatSupport';

export type ChatToolStatus = SharedToolStatus;

export interface ChatToolCall {
  toolCallId: string;
  toolCallName: string;
  args: string;
  status: ChatToolStatus;
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

export interface ConfirmActionArgs {
  action: string;
  description: string;
  severity?: 'low' | 'medium' | 'high';
}

export interface PendingApproval {
  toolCallId: string;
  action: string;
  description: string;
  severity: 'low' | 'medium' | 'high';
  resolve: (result: string) => void;
}

export interface ChatStep {
  stepName: string;
  status: 'started' | 'finished';
}

/**
 * A server-owned tool-approval card (Spec 032). Unlike the legacy `confirmAction`
 * frontend-tool flow, this is driven by the backend approval gate: a gated Medium
 * tool emits `tool.approval.required` (carrying an `approvalRequestId`) and a High
 * money tool emits `tool.approval.queued` (carrying a `proposalId`). The user's
 * decision is routed to the server — `POST /ai/tool-approvals/{id}/decide` for
 * Medium, the same `/ai/proposals/{id}/approve|dismiss` path the queue uses for
 * High — not resolved client-side.
 */
export interface ServerApprovalChatMessage {
  type: 'approval';
  id: string;
  kind: 'medium' | 'high';
  /** Set for Medium — the durable ToolApprovalRequest to decide. */
  approvalRequestId?: string;
  /** Set for High — the durable Proposal to approve/dismiss. */
  proposalId?: string;
  toolCallId?: string;
  tool: string;
  /** Risk tier label as emitted by the server ("Medium" / "High"). */
  tier: string;
  actionKind: string;
  status: 'pending' | 'deciding' | 'approved' | 'rejected' | 'error';
  message?: string;
}

export type ChatMessage =
  | { type: 'user'; id: string; content: string }
  | { type: 'assistant'; id: string; content: string; toolCalls?: ChatToolCall[] }
  | { type: 'tool-result'; id: string; toolCallId: string; toolCallName: string; content: string; error?: string }
  | { type: 'step'; id: string; stepName: string; status: 'started' | 'finished' }
  | { type: 'reasoning'; id: string; content: string }
  | { type: 'activity'; id: string; activityType: string; content: Record<string, unknown> }
  | ServerApprovalChatMessage;

export type ChatRunState = 'idle' | 'streaming' | 'awaiting-approval' | 'awaiting-selection';

export function resolveChatRunState(messages: ChatMessage[], isStreaming: boolean): ChatRunState {
  if (
    messages.some(
      (message) =>
        message.type === 'assistant'
        && message.toolCalls?.some((toolCall) => toolCall.status === 'awaiting-approval'),
    )
  ) {
    return 'awaiting-approval';
  }

  if (
    messages.some(
      (message) =>
        message.type === 'assistant'
        && message.toolCalls?.some((toolCall) => toolCall.status === 'awaiting-selection'),
    )
  ) {
    return 'awaiting-selection';
  }

  return isStreaming ? 'streaming' : 'idle';
}

export interface FrontendToolConfig {
  name: string;
  description: string;
  parameters: unknown;
  handler: FrontendToolHandler;
}

export interface UseAguiChatOptions {
  agentId?: string;
  enablePersonalFinanceFeatures?: boolean;
}

type StateUpdater<T> = T | ((prev: T) => T);

export interface UseAguiChatReturn {
  messages: ChatMessage[];
  draft: string;
  setDraft: (value: string) => void;
  isStreaming: boolean;
  streamError: string | null;
  activeSteps: ChatStep[];
  handleSend: () => Promise<void>;
  sendMessage: (text: string) => Promise<void>;
  stopStreaming: () => void;
  resetChat: () => void;
  registerTool: (config: FrontendToolConfig) => void;
  unregisterTool: (name: string) => void;
  pendingApprovals: PendingApproval[];
  approveAction: (toolCallId: string) => void;
  rejectAction: (toolCallId: string, reason?: string) => void;
  decideServerApproval: (approval: ServerApprovalChatMessage, decision: 'Approve' | 'Reject') => Promise<void>;
  selectToolCallOptions: (toolCallId: string, selected: string[]) => void;
  threadId: string | null;
  loadThread: (thread: ThreadDetail) => void;
  voiceModeAvailable: boolean;
  voiceModeEnabled: boolean;
  setVoiceModeEnabled: (enabled: boolean) => void;
  voicePlaybackState: 'idle' | 'loading' | 'playing' | 'error';
  voiceError: string | null;
  voiceDetails: VoiceRenderDetails | null;
  stopVoicePreview: () => void;
}

export function useAguiChat(agentIdOrOptions?: string | UseAguiChatOptions): UseAguiChatReturn {
  const { agentId, enablePersonalFinanceFeatures = false } = resolveHookOptions(agentIdOrOptions);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [streamError, setStreamError] = useState<string | null>(null);
  const [activeSteps, setActiveSteps] = useState<ChatStep[]>([]);
  const [pendingApprovals, setPendingApprovals] = useState<PendingApproval[]>([]);
  const [manualFrontendTools, setManualFrontendTools] = useState<Map<string, FrontendToolRegistration>>(new Map());
  const [voiceModeEnabled, setVoiceModeEnabled] = useState(false);
  const [speechRender, setSpeechRender] = useState<SpeechRenderPayload | null>(null);
  const [speechChunks, setSpeechChunks] = useState<SpeechChunkPayload[]>([]);

  const abortControllerRef = useRef<AbortController | null>(null);
  const threadIdRef = useRef<string | null>(null);
  const pendingApprovalResolversRef = useRef<Map<string, (result: string) => void>>(new Map());
  const pendingSelectionResolversRef = useRef<Map<string, (result: string) => void>>(new Map());
  // Spec 032 — a Medium approval card can be approved while the original run is still streaming (the
  // gate emits the card before RUN_FINISHED). sendInternal drops messages mid-run, so we stash the
  // retry prompt here and flush it the moment streaming ends — otherwise the approval is recorded but
  // the gated tool never reruns to consume it. isStreamingRef gives the flush the live stream state.
  const pendingApprovalRetryRef = useRef<string | null>(null);
  const isStreamingRef = useRef(isStreaming);
  isStreamingRef.current = isStreaming;
  const { getAccessToken } = useAuth();

  const updateToolCall = useCallback((toolCallId: string, updater: (toolCall: ChatToolCall) => ChatToolCall) => {
    setMessages((prev) =>
      prev.map((message) => {
        if (message.type !== 'assistant' || !message.toolCalls) {
          return message;
        }

        let changed = false;
        const toolCalls = message.toolCalls.map((toolCall) => {
          if (toolCall.toolCallId !== toolCallId) {
            return toolCall;
          }

          changed = true;
          return updater(toolCall);
        });

        return changed ? { ...message, toolCalls } : message;
      }),
    );
  }, []);

  const resolvePendingInteractions = useCallback((result: string) => {
    for (const [, resolver] of pendingApprovalResolversRef.current) {
      resolver(result);
    }
    pendingApprovalResolversRef.current.clear();

    for (const [, resolver] of pendingSelectionResolversRef.current) {
      resolver(result);
    }
    pendingSelectionResolversRef.current.clear();
  }, []);

  const confirmAction = useCallback(
    (toolCallId: string, args: Required<ConfirmActionArgs>) => {
      return new Promise<string>((resolve) => {
        pendingApprovalResolversRef.current.set(toolCallId, resolve);

        setPendingApprovals((prev) => [
          ...prev.filter((approval) => approval.toolCallId !== toolCallId),
          {
            toolCallId,
            action: args.action,
            description: args.description,
            severity: args.severity,
            resolve,
          },
        ]);

        updateToolCall(toolCallId, (toolCall) => ({
          ...toolCall,
          status: 'awaiting-approval',
          approval: {
            action: args.action,
            description: args.description,
            severity: args.severity,
          },
        }));
      });
    },
    [updateToolCall],
  );

  const selectOptions = useCallback(
    (toolCallId: string, args: OptionSelectionState) => {
      return new Promise<string>((resolve) => {
        pendingSelectionResolversRef.current.set(toolCallId, resolve);
        updateToolCall(toolCallId, (toolCall) => ({
          ...toolCall,
          status: 'awaiting-selection',
          optionSelection: args,
        }));
      });
    },
    [updateToolCall],
  );

  const builtInFrontendTools = useAiChatFrontendTools({
    enabled: true,
    confirmAction,
    selectOptions,
    // Spec 032: every mutating domain agent is now behind the server-side approval gate — Finance
    // and PersonalFinance classify their mutating tools, and the read-only FLG / Platform / Pf*
    // agents route through IToolApprovalGate too — and the gate surfaces its own ServerApprovalCard.
    // The legacy confirmAction frontend tool is therefore redundant, so we do not declare it to the
    // model (which otherwise emitted a duplicate confirmAction card alongside the server card). The
    // confirmAction rendering path is kept in ChatMessageList only to display historical threads.
    includeConfirmAction: false,
    includeDisplayTools: enablePersonalFinanceFeatures,
    includeOptionSelector: enablePersonalFinanceFeatures,
    includeNavigation: enablePersonalFinanceFeatures,
  });

  const frontendTools = useMemo(() => {
    const registrations = new Map<string, FrontendToolRegistration>(builtInFrontendTools);
    for (const [name, registration] of manualFrontendTools) {
      registrations.set(name, registration);
    }

    return registrations;
  }, [builtInFrontendTools, manualFrontendTools]);

  const {
    playbackState: voicePlaybackState,
    voiceError,
    voiceDetails,
    stopVoicePreview,
  } = useAiChatVoicePlayback({
    enabled: voiceModeEnabled,
    isStreaming,
    speechRender,
    speechChunks,
  });

  const registerTool = useCallback((config: FrontendToolConfig) => {
    setManualFrontendTools((prev) => {
      const next = new Map(prev);
      next.set(config.name, {
        tool: {
          name: config.name,
          description: config.description,
          parameters: config.parameters,
        },
        handler: config.handler,
      });
      return next;
    });
  }, []);

  const unregisterTool = useCallback((name: string) => {
    setManualFrontendTools((prev) => {
      if (!prev.has(name)) {
        return prev;
      }

      const next = new Map(prev);
      next.delete(name);
      return next;
    });
  }, []);

  const approveAction = useCallback(
    (toolCallId: string) => {
      const resolver = pendingApprovalResolversRef.current.get(toolCallId);
      if (resolver) {
        resolver('approved');
        pendingApprovalResolversRef.current.delete(toolCallId);
      }

      setPendingApprovals((prev) => prev.filter((approval) => approval.toolCallId !== toolCallId));
      updateToolCall(toolCallId, (toolCall) => ({
        ...toolCall,
        status: 'completed',
        result: 'approved',
      }));
    },
    [updateToolCall],
  );

  const rejectAction = useCallback(
    (toolCallId: string, reason?: string) => {
      const result = reason ? `rejected: ${reason}` : 'rejected';
      const resolver = pendingApprovalResolversRef.current.get(toolCallId);
      if (resolver) {
        resolver(result);
        pendingApprovalResolversRef.current.delete(toolCallId);
      }

      setPendingApprovals((prev) => prev.filter((approval) => approval.toolCallId !== toolCallId));
      updateToolCall(toolCallId, (toolCall) => ({
        ...toolCall,
        status: 'completed',
        result,
      }));
    },
    [updateToolCall],
  );

  const selectToolCallOptions = useCallback(
    (toolCallId: string, selected: string[]) => {
      const result = selected.length <= 1 ? (selected[0] ?? '') : JSON.stringify(selected);
      const resolver = pendingSelectionResolversRef.current.get(toolCallId);
      if (resolver) {
        resolver(result);
        pendingSelectionResolversRef.current.delete(toolCallId);
      }

      updateToolCall(toolCallId, (toolCall) => ({
        ...toolCall,
        status: 'completed',
        result: selected.join(', '),
      }));
    },
    [updateToolCall],
  );

  const resetChat = useCallback(() => {
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
    threadIdRef.current = null;
    resolvePendingInteractions('rejected: chat reset');
    stopVoicePreview();
    setMessages([]);
    setDraft('');
    setIsStreaming(false);
    setStreamError(null);
    setActiveSteps([]);
    setPendingApprovals([]);
    setSpeechRender(null);
    setSpeechChunks([]);
    setVoiceModeEnabled(false);
  }, [resolvePendingInteractions, stopVoicePreview]);

  const stopStreaming = useCallback(() => {
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
    setIsStreaming(false);
  }, []);

  const loadThread = useCallback(
    (thread: ThreadDetail) => {
      abortControllerRef.current?.abort();
      abortControllerRef.current = null;
      resolvePendingInteractions('rejected: thread changed');

      threadIdRef.current = thread.id;
      setMessages(
        thread.messages
          .sort((left, right) => left.sortOrder - right.sortOrder)
          .map(threadMessageToChatMessage)
          .filter((message): message is ChatMessage => message !== null),
      );
      setDraft('');
      setIsStreaming(false);
      setStreamError(null);
      setActiveSteps([]);
      setPendingApprovals([]);
      setSpeechRender(null);
      setSpeechChunks([]);
    },
    [resolvePendingInteractions],
  );

  const sendInternal = useCallback(
    async (text: string) => {
      if (!text || isStreaming) {
        return;
      }

      setStreamError(null);
      setActiveSteps([]);
      setSpeechRender(null);
      setSpeechChunks([]);

      const userMessageId = generateId();
      const placeholderAssistantId = generateId();

      setMessages((prev) => [...prev, { type: 'user', id: userMessageId, content: text }]);
      setIsStreaming(true);

      const aguiMessages = buildAguiMessages(messages, {
        role: 'user',
        content: text,
        id: userMessageId,
      });

      const tools: Tool[] = Array.from(frontendTools.values()).map((registration): Tool => registration.tool);
      const abortController = new AbortController();
      abortControllerRef.current = abortController;

      let currentAssistantId = placeholderAssistantId;
      const streamingToolCalls = new Map<string, { name: string; args: string; assistantMessageId: string }>();

      const ensureAssistantMessage = (assistantId: string) => {
        setMessages((prev) => {
          const exists = prev.some((message) => message.type === 'assistant' && message.id === assistantId);
          if (exists) {
            return prev;
          }

          return [...prev, { type: 'assistant', id: assistantId, content: '' }];
        });
      };

      const adoptAssistantMessageId = (nextAssistantId: string) => {
        if (!nextAssistantId || nextAssistantId === currentAssistantId) {
          return;
        }

        setMessages((prev) => {
          if (prev.some((message) => message.type === 'assistant' && message.id === nextAssistantId)) {
            return prev;
          }

          let replaced = false;
          const updated = prev.map((message) => {
            if (
              !replaced
              && message.type === 'assistant'
              && message.id === currentAssistantId
              && message.content.length === 0
              && !(message.toolCalls?.length)
            ) {
              replaced = true;
              return { ...message, id: nextAssistantId };
            }

            return message;
          });

          return replaced ? updated : [...updated, { type: 'assistant', id: nextAssistantId, content: '' }];
        });

        currentAssistantId = nextAssistantId;
      };

      try {
        setMessages((prev) => [...prev, { type: 'assistant', id: currentAssistantId, content: '' }]);

        const callbacks: AguiStreamCallbacks = {
          onRunStarted: (event) => {
            threadIdRef.current = event.threadId;
          },

          onTextMessageStart: (event) => {
            adoptAssistantMessageId(event.messageId);
          },

          onTextMessageContent: (event) => {
            setMessages((prev) =>
              prev.map((message) =>
                message.id === currentAssistantId && message.type === 'assistant'
                  ? { ...message, content: message.content + event.delta }
                  : message,
              ),
            );
          },

          onToolCallStart: (event: ToolCallStartEvent) => {
            const assistantMessageId = event.parentMessageId || currentAssistantId;
            adoptAssistantMessageId(assistantMessageId);
            ensureAssistantMessage(assistantMessageId);

            streamingToolCalls.set(event.toolCallId, {
              name: event.toolCallName,
              args: '',
              assistantMessageId,
            });

            setMessages((prev) =>
              prev.map((message) => {
                if (message.type !== 'assistant' || message.id !== assistantMessageId) {
                  return message;
                }

                const toolCall = hydrateToolCallMetadata({
                  toolCallId: event.toolCallId,
                  toolCallName: event.toolCallName,
                  args: '',
                  status: 'streaming',
                });

                return {
                  ...message,
                  toolCalls: [...(message.toolCalls ?? []), toolCall],
                };
              }),
            );
          },

          onToolCallArgs: (event: ToolCallArgsEvent) => {
            const toolCall = streamingToolCalls.get(event.toolCallId);
            if (toolCall) {
              toolCall.args += event.delta;
            }

            updateToolCall(event.toolCallId, (current) =>
              hydrateToolCallMetadata({
                ...current,
                args: current.args + event.delta,
              }),
            );
          },

          onToolCallEnd: (event: ToolCallEndEvent) => {
            updateToolCall(event.toolCallId, (toolCall) => ({
              ...toolCall,
              status: 'pending',
            }));
          },

          onToolCallResult: (event: ToolCallResultEvent) => {
            const toolCall = streamingToolCalls.get(event.toolCallId);

            setMessages((prev) => [
              ...prev,
              {
                type: 'tool-result',
                id: event.messageId ?? generateId(),
                toolCallId: event.toolCallId,
                toolCallName: toolCall?.name ?? 'tool',
                content: event.content,
              },
            ]);

            if (toolCall?.name === 'confirmAction') {
              setPendingApprovals((prev) => prev.filter((approval) => approval.toolCallId !== event.toolCallId));
            }

            updateToolCall(event.toolCallId, (current) => ({
              ...current,
              status: 'completed',
              result: event.content,
            }));
          },

          onStepStarted: (event: StepStartedEvent) => {
            const step = { stepName: event.stepName, status: 'started' as const };
            setActiveSteps((prev) => [...prev, step]);
            setMessages((prev) => [...prev, { type: 'step', id: generateId(), ...step }]);
          },

          onStepFinished: (event: StepFinishedEvent) => {
            setActiveSteps((prev) =>
              prev.map((step) =>
                step.stepName === event.stepName ? { ...step, status: 'finished' } : step,
              ),
            );
            setMessages((prev) =>
              prev.map((message) =>
                message.type === 'step' && message.stepName === event.stepName && message.status === 'started'
                  ? { ...message, status: 'finished' }
                  : message,
              ),
            );
          },

          onReasoningMessageContent: (event: ReasoningMessageContentEvent) => {
            setMessages((prev) => {
              const existing = prev.find((message) => message.type === 'reasoning' && message.id === event.messageId);
              if (existing?.type === 'reasoning') {
                return prev.map((message) =>
                  message.id === event.messageId && message.type === 'reasoning'
                    ? { ...message, content: message.content + event.delta }
                    : message,
                );
              }

              return [...prev, { type: 'reasoning', id: event.messageId, content: event.delta }];
            });
          },

          onActivitySnapshot: (event: ActivitySnapshotEvent) => {
            setMessages((prev) => {
              const existing = prev.find((message) => message.id === event.messageId);
              if (!existing) {
                return [
                  ...prev,
                  {
                    type: 'activity',
                    id: event.messageId,
                    activityType: event.activityType,
                    content: event.content,
                  },
                ];
              }

              if (event.replace === false) {
                return prev;
              }

              return prev.map((message) =>
                message.id === event.messageId
                  ? {
                      type: 'activity',
                      id: event.messageId,
                      activityType: event.activityType,
                      content: event.content,
                    }
                  : message,
              );
            });
          },

          onMessagesSnapshot: (event: MessagesSnapshotEvent) => {
            const snapshotMessages = event.messages
              .map(aguiMessageToChatMessage)
              .filter((message): message is ChatMessage => message !== null);
            setMessages(snapshotMessages);
          },

          onCustomEvent: (event: CustomEvent) => {
            handleCustomEvent(event, setSpeechRender, setSpeechChunks, setMessages);
          },

          onRunError: (event) => {
            setStreamError(event.message || 'An error occurred');
            setMessages((prev) =>
              prev.map((message) =>
                message.id === currentAssistantId && message.type === 'assistant'
                  ? {
                      ...message,
                      content: message.content || `Error: ${event.message || 'Something went wrong'}`,
                    }
                  : message,
              ),
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
            agentId: agentId || undefined,
          },
          callbacks,
          getAccessToken,
          signal: abortController.signal,
          frontendTools: frontendTools.size > 0 ? frontendTools : undefined,
        });

        setMessages((prev) =>
          prev.map((message) =>
            message.id === currentAssistantId && message.type === 'assistant' && !message.content && !message.toolCalls?.length
              ? { ...message, content: 'No response received from the agent.' }
              : message,
          ),
        );
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        const errorMessage = error instanceof Error ? error.message : 'An unexpected error occurred';
        setStreamError(errorMessage);

        setMessages((prev) => {
          const hasAssistant = prev.some((message) => message.id === currentAssistantId);
          if (hasAssistant) {
            return prev.map((message) =>
              message.id === currentAssistantId && message.type === 'assistant'
                ? { ...message, content: message.content || `Error: ${errorMessage}` }
                : message,
            );
          }

          return [
            ...prev,
            {
              type: 'assistant',
              id: currentAssistantId,
              content: `Error: ${errorMessage}`,
            },
          ];
        });
      } finally {
        setIsStreaming(false);
        abortControllerRef.current = null;
      }
    },
    [agentId, frontendTools, getAccessToken, isStreaming, messages, updateToolCall],
  );

  const handleSend = useCallback(async () => {
    const text = draft.trim();
    if (!text) {
      return;
    }

    setDraft('');
    await sendInternal(text);
  }, [draft, sendInternal]);

  const sendMessage = useCallback(
    async (messageText: string) => {
      const text = messageText.trim();
      if (!text) {
        return;
      }

      setDraft('');
      await sendInternal(text);
    },
    [sendInternal],
  );

  // Sends a queued Medium approval retry — but only when idle, since sendInternal drops messages
  // while a run is in flight. Called both directly (on approve) and from the effect below when the
  // active stream ends, so the retry fires regardless of whether the card was approved mid-stream.
  const flushPendingApprovalRetry = useCallback(() => {
    if (pendingApprovalRetryRef.current && !isStreamingRef.current) {
      const text = pendingApprovalRetryRef.current;
      pendingApprovalRetryRef.current = null;
      void sendInternal(text);
    }
  }, [sendInternal]);

  useEffect(() => {
    if (!isStreaming) {
      flushPendingApprovalRetry();
    }
  }, [isStreaming, flushPendingApprovalRetry]);

  // Spec 032 — record a decision for a server-owned approval card. Medium routes
  // through the tool-approvals decide endpoint and, on approval, nudges the agent
  // to re-invoke the gated tool (the gate consumes the approval, bound by
  // args-hash, and runs it once). High routes through the same proposal path the
  // approvals queue uses, so an in-session approval and a queue approval take the
  // identical authorization + dispatch path. The server is the decision authority;
  // this only presents and collects.
  const decideServerApproval = useCallback(
    async (approval: ServerApprovalChatMessage, decision: 'Approve' | 'Reject') => {
      const setApprovalStatus = (
        status: ServerApprovalChatMessage['status'],
        statusMessage?: string,
      ) => {
        setMessages((prev) =>
          prev.map((m) =>
            m.type === 'approval' && m.id === approval.id
              ? { ...m, status, message: statusMessage ?? m.message }
              : m,
          ),
        );
      };

      // Both tiers decide through the same server authority — POST /ai/tool-approvals/{id}/decide —
      // which validates identity / tenant / expiry / single-use and resolves the ToolApprovalRequest.
      // For High it internally routes the linked proposal through the policy-checked approve/dismiss
      // path (executing or cancelling the money) and flips the request Approved/Rejected in lock-step.
      // Hitting the bare /ai/proposals endpoints instead would skip those checks and leave the
      // correlated request stuck Pending — so we never do that from the card.
      if (!approval.approvalRequestId) {
        setApprovalStatus('error', 'This approval is missing its request reference.');
        return;
      }

      setApprovalStatus('deciding');

      try {
        const result = await api.post<{ message?: string }>(
          `/ai/tool-approvals/${encodeURIComponent(approval.approvalRequestId)}/decide`,
          { decision },
        );

        if (decision === 'Approve') {
          setApprovalStatus('approved', result?.message ?? `${approval.actionKind} was approved.`);
          // Medium runs inline when the agent re-invokes the gated tool, so nudge it to proceed (the
          // gate consumes the args-hash-bound approval and runs the tool once). High already executed
          // synchronously inside the decide call — the money has moved — so no retry is needed.
          if (approval.kind === 'medium') {
            // Queue the retry and flush it now if idle, or as soon as the active stream ends — the
            // card can be approved while the original run is still streaming, in which case a direct
            // sendInternal would be dropped by its isStreaming guard, leaving the approval unconsumed.
            pendingApprovalRetryRef.current =
              `I approved "${approval.actionKind}". Please go ahead and complete that action now.`;
            flushPendingApprovalRetry();
          }
        } else {
          setApprovalStatus('rejected', result?.message ?? `${approval.actionKind} was rejected. Nothing was changed.`);
        }
      } catch (error) {
        const message =
          (error as { userMessage?: string })?.userMessage
          ?? (error instanceof Error ? error.message : 'The decision could not be recorded.');
        setApprovalStatus('error', message);
      }
    },
    [flushPendingApprovalRetry],
  );

  return {
    messages,
    draft,
    setDraft,
    isStreaming,
    streamError,
    activeSteps,
    handleSend,
    sendMessage,
    stopStreaming,
    resetChat,
    registerTool,
    unregisterTool,
    pendingApprovals,
    approveAction,
    rejectAction,
    decideServerApproval,
    selectToolCallOptions,
    threadId: threadIdRef.current,
    loadThread,
    voiceModeAvailable: true,
    voiceModeEnabled,
    setVoiceModeEnabled,
    voicePlaybackState,
    voiceError,
    voiceDetails,
    stopVoicePreview,
  };
}

function resolveHookOptions(agentIdOrOptions?: string | UseAguiChatOptions): UseAguiChatOptions {
  if (typeof agentIdOrOptions === 'string') {
    return {
      agentId: agentIdOrOptions,
      enablePersonalFinanceFeatures: agentIdOrOptions === 'personal-finance-agent',
    };
  }

  return {
    agentId: agentIdOrOptions?.agentId,
    enablePersonalFinanceFeatures:
      agentIdOrOptions?.enablePersonalFinanceFeatures ?? agentIdOrOptions?.agentId === 'personal-finance-agent',
  };
}

function parseApproval(args: string): ChatToolCall['approval'] | undefined {
  const parsed = tryParseJsonRecord(args);
  if (!parsed) {
    return undefined;
  }

  const action = typeof parsed.action === 'string' && parsed.action.trim().length > 0 ? parsed.action : undefined;
  const description = typeof parsed.description === 'string' ? parsed.description : '';
  const severity = ['low', 'medium', 'high'].includes(String(parsed.severity))
    ? (parsed.severity as 'low' | 'medium' | 'high')
    : 'medium';

  if (!action) {
    return undefined;
  }

  return {
    action,
    description,
    severity,
  };
}

function parseOptionSelection(args: string): OptionSelectionState | undefined {
  const parsed = tryParseJsonRecord(args);
  if (!parsed) {
    return undefined;
  }

  const question = typeof parsed.question === 'string' && parsed.question.trim().length > 0
    ? parsed.question
    : undefined;
  const options = Array.isArray(parsed.options)
    ? parsed.options
        .filter((item): item is Record<string, unknown> => typeof item === 'object' && item !== null)
        .map((item) => ({
          label: typeof item.label === 'string' ? item.label : '',
          description: typeof item.description === 'string' ? item.description : undefined,
        }))
        .filter((item) => item.label.length > 0)
    : [];

  if (!question || options.length === 0) {
    return undefined;
  }

  return {
    question,
    options,
    multiSelect: parsed.multiSelect === true,
  };
}

function hydrateToolCallMetadata(toolCall: ChatToolCall): ChatToolCall {
  if (toolCall.toolCallName === 'confirmAction') {
    return {
      ...toolCall,
      approval: toolCall.approval ?? parseApproval(toolCall.args),
    };
  }

  if (toolCall.toolCallName === 'display_option_selector') {
    return {
      ...toolCall,
      optionSelection: toolCall.optionSelection ?? parseOptionSelection(toolCall.args),
    };
  }

  if (toolCall.toolCallName === 'display_follow_up_suggestions') {
    return {
      ...toolCall,
      // parseFollowUpSuggestions returns null on parse failure but the
      // ChatToolCall field is typed `FollowUpSuggestionsState | undefined`,
      // so coerce null → undefined to keep TS strict mode happy.
      followUpSuggestions: toolCall.followUpSuggestions ?? parseFollowUpSuggestions(tryParseJsonRecord(toolCall.args) ?? {}) ?? undefined,
    };
  }

  return toolCall;
}

function parseStoredToolCalls(toolCallsJson?: string): ChatToolCall[] | undefined {
  if (!toolCallsJson) {
    return undefined;
  }

  try {
    const parsed = JSON.parse(toolCallsJson);
    if (!Array.isArray(parsed)) {
      return undefined;
    }

    const toolCalls = parsed
      .filter((item): item is Record<string, unknown> => typeof item === 'object' && item !== null)
      .map((item) => {
        const toolCallId = typeof item.id === 'string' ? item.id : generateId();
        const functionCall = typeof item.function === 'object' && item.function !== null
          ? (item.function as Record<string, unknown>)
          : null;
        const toolCallName = typeof functionCall?.name === 'string' ? functionCall.name : 'tool';
        const args = typeof functionCall?.arguments === 'string' ? functionCall.arguments : '';

        return hydrateToolCallMetadata({
          toolCallId,
          toolCallName,
          args,
          status: 'completed',
        });
      });

    return toolCalls.length > 0 ? toolCalls : undefined;
  } catch {
    return undefined;
  }
}

function threadMessageToChatMessage(message: ThreadMessageDto): ChatMessage | null {
  switch (message.role) {
    case 'user':
      return {
        type: 'user',
        id: message.id,
        content: message.content,
      };
    case 'assistant':
      return {
        type: 'assistant',
        id: message.id,
        content: message.content,
        toolCalls: parseStoredToolCalls(message.toolCallsJson),
      };
    default:
      return null;
  }
}

function handleCustomEvent(
  event: CustomEvent,
  setSpeechRender: (value: StateUpdater<SpeechRenderPayload | null>) => void,
  setSpeechChunks: (value: StateUpdater<SpeechChunkPayload[]>) => void,
  setMessages: (value: StateUpdater<ChatMessage[]>) => void,
) {
  const value = typeof event.value === 'object' && event.value !== null
    ? (event.value as Record<string, unknown>)
    : null;
  const messageId = typeof value?.messageId === 'string' ? value.messageId : '';

  // Spec 032 — the backend approval gate surfaces a gated-but-not-executed mutation
  // as a CUSTOM event carrying the durable id the user's decision routes to. Render
  // it as an interactive approval card appended to the conversation.
  if (event.name === 'tool.approval.required' || event.name === 'tool.approval.queued') {
    if (!value) {
      return;
    }

    const kind: ServerApprovalChatMessage['kind'] = event.name === 'tool.approval.queued' ? 'high' : 'medium';
    const approvalRequestId = typeof value.approvalRequestId === 'string' ? value.approvalRequestId : undefined;
    const proposalId = typeof value.proposalId === 'string' ? value.proposalId : undefined;

    // Both tiers decide via the approvalRequestId (/ai/tool-approvals/{id}/decide). High also carries
    // the proposalId for reference, but the request id is the actionable one — without it there is
    // nothing the user could safely act on, so skip silently (the agent's prose already says it's pending).
    if (!approvalRequestId) {
      return;
    }

    const tool = typeof value.tool === 'string' ? value.tool : '';
    const actionKind = typeof value.actionKind === 'string' && value.actionKind.trim().length > 0
      ? value.actionKind
      : tool || 'this action';
    const tier = typeof value.tier === 'string' ? value.tier : kind === 'high' ? 'High' : 'Medium';
    const toolCallId = typeof value.toolCallId === 'string' ? value.toolCallId : undefined;
    const id = `approval-${approvalRequestId ?? proposalId}`;

    setMessages((prev) => {
      if (prev.some((message) => message.id === id)) {
        return prev;
      }

      return [
        ...prev,
        {
          type: 'approval',
          id,
          kind,
          approvalRequestId,
          proposalId,
          toolCallId,
          tool,
          tier,
          actionKind,
          status: 'pending',
        },
      ];
    });
    return;
  }

  if (event.name === 'speech.chunk') {
    const speechText = typeof value?.speechText === 'string' ? value.speechText : '';
    if (!messageId || !speechText) {
      return;
    }

    const chunkIndex = typeof value?.chunkIndex === 'number' ? value.chunkIndex : 0;
    setSpeechChunks((prev) => [
      ...prev,
      {
        messageId,
        chunkIndex,
        speechText,
        isFinal: value?.isFinal === true,
      },
    ]);
    return;
  }

  if (event.name === 'speech.render') {
    if (!messageId) {
      return;
    }

    setSpeechRender({
      messageId,
      speechText: typeof value?.speechText === 'string' ? value.speechText : '',
      requiresVisualAttention: value?.requiresVisualAttention === true,
      requiresApproval: value?.requiresApproval === true,
    });
  }
}

function buildAguiMessages(
  chatMessages: ChatMessage[],
  newUserMessage: { role: 'user'; content: string; id: string },
): Message[] {
  const result: Message[] = [];

  for (const message of chatMessages) {
    switch (message.type) {
      case 'user':
        result.push({ id: message.id, role: 'user', content: message.content } satisfies UserMessage);
        break;

      case 'assistant': {
        const assistantMessage: AssistantMessage = {
          id: message.id,
          role: 'assistant',
          content: message.content || undefined,
        };

        if (message.toolCalls && message.toolCalls.length > 0) {
          assistantMessage.toolCalls = message.toolCalls.map(
            (toolCall): ToolCall => ({
              id: toolCall.toolCallId,
              type: 'function',
              function: {
                name: toolCall.toolCallName,
                arguments: toolCall.args,
              },
            }),
          );
        }

        result.push(assistantMessage);
        break;
      }

      case 'tool-result':
        result.push({
          id: message.id,
          role: 'tool',
          content: message.content,
          toolCallId: message.toolCallId,
          ...(message.error ? { error: message.error } : {}),
        } satisfies ToolMessage);
        break;

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

function aguiMessageToChatMessage(message: Message): ChatMessage | null {
  switch (message.role) {
    case 'user':
      return {
        type: 'user',
        id: message.id,
        content: typeof message.content === 'string' ? message.content : '',
      };
    case 'assistant':
      return {
        type: 'assistant',
        id: message.id,
        content: message.content ?? '',
        toolCalls: message.toolCalls?.map((toolCall) =>
          hydrateToolCallMetadata({
            toolCallId: toolCall.id,
            toolCallName: toolCall.function.name,
            args: toolCall.function.arguments,
            status: 'completed',
          }),
        ),
      };
    case 'tool':
      return {
        type: 'tool-result',
        id: message.id,
        toolCallId: message.toolCallId,
        toolCallName: 'tool',
        content: message.content,
        error: message.error,
      };
    case 'reasoning': {
      const reasoningMessage = message as ReasoningMessage;
      return {
        type: 'reasoning',
        id: message.id,
        content: reasoningMessage.content,
      };
    }
    case 'activity': {
      const activityMessage = message as ActivityMessage;
      return {
        type: 'activity',
        id: message.id,
        activityType: activityMessage.activityType,
        content: activityMessage.content,
      };
    }
    default:
      return null;
  }
}
