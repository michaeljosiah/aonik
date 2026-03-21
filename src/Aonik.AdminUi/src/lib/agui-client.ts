/**
 * AG-UI Protocol Client — Full Implementation
 *
 * Implements the complete AG-UI (Agent-User Interaction) protocol using native
 * `fetch` with streaming body reader. No external dependencies needed.
 *
 * Supports all 27 event types, frontend-defined tools with client-side
 * execution, and the automatic re-run loop for tool call results.
 *
 * Protocol: POST with JSON body → SSE response with AG-UI events.
 * Reference: https://docs.ag-ui.com/concepts/events
 */

import { apiConfig } from '@/auth';
import { getSelectedTenant } from '@/lib/tenantContext';

// ─── Event Types ──────────────────────────────────────────────────────────────

/**
 * All AG-UI event type discriminators.
 * @see https://docs.ag-ui.com/sdk/js/core/events
 */
export type EventType =
  // Lifecycle
  | 'RUN_STARTED'
  | 'RUN_FINISHED'
  | 'RUN_ERROR'
  | 'STEP_STARTED'
  | 'STEP_FINISHED'
  // Text messages
  | 'TEXT_MESSAGE_START'
  | 'TEXT_MESSAGE_CONTENT'
  | 'TEXT_MESSAGE_END'
  | 'TEXT_MESSAGE_CHUNK'
  // Tool calls
  | 'TOOL_CALL_START'
  | 'TOOL_CALL_ARGS'
  | 'TOOL_CALL_END'
  | 'TOOL_CALL_RESULT'
  | 'TOOL_CALL_CHUNK'
  // State management
  | 'STATE_SNAPSHOT'
  | 'STATE_DELTA'
  | 'MESSAGES_SNAPSHOT'
  // Activity
  | 'ACTIVITY_SNAPSHOT'
  | 'ACTIVITY_DELTA'
  // Reasoning
  | 'REASONING_START'
  | 'REASONING_MESSAGE_START'
  | 'REASONING_MESSAGE_CONTENT'
  | 'REASONING_MESSAGE_END'
  | 'REASONING_MESSAGE_CHUNK'
  | 'REASONING_END'
  | 'REASONING_ENCRYPTED_VALUE'
  // Special
  | 'RAW'
  | 'CUSTOM';

// ─── Base Event ───────────────────────────────────────────────────────────────

export interface BaseEvent {
  type: EventType;
  timestamp?: number;
  rawEvent?: unknown;
}

// ─── Lifecycle Events ─────────────────────────────────────────────────────────

export interface RunStartedEvent extends BaseEvent {
  type: 'RUN_STARTED';
  threadId: string;
  runId: string;
  parentRunId?: string;
  input?: RunAgentInput;
}

export interface RunFinishedEvent extends BaseEvent {
  type: 'RUN_FINISHED';
  threadId: string;
  runId: string;
  result?: unknown;
}

export interface RunErrorEvent extends BaseEvent {
  type: 'RUN_ERROR';
  message: string;
  code?: string;
}

export interface StepStartedEvent extends BaseEvent {
  type: 'STEP_STARTED';
  stepName: string;
}

export interface StepFinishedEvent extends BaseEvent {
  type: 'STEP_FINISHED';
  stepName: string;
}

// ─── Text Message Events ──────────────────────────────────────────────────────

export interface TextMessageStartEvent extends BaseEvent {
  type: 'TEXT_MESSAGE_START';
  messageId: string;
  role: string;
}

export interface TextMessageContentEvent extends BaseEvent {
  type: 'TEXT_MESSAGE_CONTENT';
  messageId: string;
  delta: string;
}

export interface TextMessageEndEvent extends BaseEvent {
  type: 'TEXT_MESSAGE_END';
  messageId: string;
}

export interface TextMessageChunkEvent extends BaseEvent {
  type: 'TEXT_MESSAGE_CHUNK';
  messageId?: string;
  role?: string;
  delta?: string;
}

// ─── Tool Call Events ─────────────────────────────────────────────────────────

export interface ToolCallStartEvent extends BaseEvent {
  type: 'TOOL_CALL_START';
  toolCallId: string;
  toolCallName: string;
  parentMessageId?: string;
}

export interface ToolCallArgsEvent extends BaseEvent {
  type: 'TOOL_CALL_ARGS';
  toolCallId: string;
  delta: string;
}

export interface ToolCallEndEvent extends BaseEvent {
  type: 'TOOL_CALL_END';
  toolCallId: string;
}

export interface ToolCallResultEvent extends BaseEvent {
  type: 'TOOL_CALL_RESULT';
  messageId: string;
  toolCallId: string;
  content: string;
  role?: 'tool';
}

export interface ToolCallChunkEvent extends BaseEvent {
  type: 'TOOL_CALL_CHUNK';
  toolCallId?: string;
  toolCallName?: string;
  parentMessageId?: string;
  delta?: string;
}

// ─── State Management Events ──────────────────────────────────────────────────

export interface StateSnapshotEvent extends BaseEvent {
  type: 'STATE_SNAPSHOT';
  snapshot: unknown;
}

export interface StateDeltaEvent extends BaseEvent {
  type: 'STATE_DELTA';
  delta: unknown[];
}

export interface MessagesSnapshotEvent extends BaseEvent {
  type: 'MESSAGES_SNAPSHOT';
  messages: Message[];
}

// ─── Activity Events ──────────────────────────────────────────────────────────

export interface ActivitySnapshotEvent extends BaseEvent {
  type: 'ACTIVITY_SNAPSHOT';
  messageId: string;
  activityType: string;
  content: Record<string, unknown>;
  replace?: boolean;
}

export interface ActivityDeltaEvent extends BaseEvent {
  type: 'ACTIVITY_DELTA';
  messageId: string;
  activityType: string;
  patch: unknown[];
}

// ─── Reasoning Events ─────────────────────────────────────────────────────────

export interface ReasoningStartEvent extends BaseEvent {
  type: 'REASONING_START';
  messageId: string;
}

export interface ReasoningMessageStartEvent extends BaseEvent {
  type: 'REASONING_MESSAGE_START';
  messageId: string;
  role: string;
}

export interface ReasoningMessageContentEvent extends BaseEvent {
  type: 'REASONING_MESSAGE_CONTENT';
  messageId: string;
  delta: string;
}

export interface ReasoningMessageEndEvent extends BaseEvent {
  type: 'REASONING_MESSAGE_END';
  messageId: string;
}

export interface ReasoningMessageChunkEvent extends BaseEvent {
  type: 'REASONING_MESSAGE_CHUNK';
  messageId?: string;
  delta?: string;
}

export interface ReasoningEndEvent extends BaseEvent {
  type: 'REASONING_END';
  messageId: string;
}

export interface ReasoningEncryptedValueEvent extends BaseEvent {
  type: 'REASONING_ENCRYPTED_VALUE';
  subtype: 'tool-call' | 'message';
  entityId: string;
  encryptedValue: string;
}

// ─── Special Events ───────────────────────────────────────────────────────────

export interface RawEvent extends BaseEvent {
  type: 'RAW';
  event: unknown;
  source?: string;
}

export interface CustomEvent extends BaseEvent {
  type: 'CUSTOM';
  name: string;
  value: unknown;
}

// ─── Event Union ──────────────────────────────────────────────────────────────

export type AguiEvent =
  | RunStartedEvent
  | RunFinishedEvent
  | RunErrorEvent
  | StepStartedEvent
  | StepFinishedEvent
  | TextMessageStartEvent
  | TextMessageContentEvent
  | TextMessageEndEvent
  | TextMessageChunkEvent
  | ToolCallStartEvent
  | ToolCallArgsEvent
  | ToolCallEndEvent
  | ToolCallResultEvent
  | ToolCallChunkEvent
  | StateSnapshotEvent
  | StateDeltaEvent
  | MessagesSnapshotEvent
  | ActivitySnapshotEvent
  | ActivityDeltaEvent
  | ReasoningStartEvent
  | ReasoningMessageStartEvent
  | ReasoningMessageContentEvent
  | ReasoningMessageEndEvent
  | ReasoningMessageChunkEvent
  | ReasoningEndEvent
  | ReasoningEncryptedValueEvent
  | RawEvent
  | CustomEvent;

// ─── Message Types ────────────────────────────────────────────────────────────

export type Role =
  | 'developer'
  | 'system'
  | 'assistant'
  | 'user'
  | 'tool'
  | 'activity'
  | 'reasoning';

export interface ToolCall {
  id: string;
  type: 'function';
  function: {
    name: string;
    arguments: string;
  };
  encryptedValue?: string;
}

export interface UserMessage {
  id: string;
  role: 'user';
  content: string;
  name?: string;
}

export interface AssistantMessage {
  id: string;
  role: 'assistant';
  content?: string;
  name?: string;
  toolCalls?: ToolCall[];
  encryptedContent?: string;
}

export interface SystemMessage {
  id: string;
  role: 'system';
  content: string;
  name?: string;
}

export interface DeveloperMessage {
  id: string;
  role: 'developer';
  content: string;
  name?: string;
}

export interface ToolMessage {
  id: string;
  role: 'tool';
  content: string;
  toolCallId: string;
  error?: string;
  encryptedValue?: string;
}

export interface ActivityMessage {
  id: string;
  role: 'activity';
  activityType: string;
  content: Record<string, unknown>;
}

export interface ReasoningMessage {
  id: string;
  role: 'reasoning';
  content: string;
  encryptedValue?: string;
}

export type Message =
  | UserMessage
  | AssistantMessage
  | SystemMessage
  | DeveloperMessage
  | ToolMessage
  | ActivityMessage
  | ReasoningMessage;

// ─── Tool Definition ──────────────────────────────────────────────────────────

/** Tool definition sent to the agent in RunAgentInput. */
export interface Tool {
  name: string;
  description: string;
  parameters: unknown; // JSON Schema
}

// ─── Context ──────────────────────────────────────────────────────────────────

export interface Context {
  description: string;
  value: string;
}

// ─── Run Agent Input ──────────────────────────────────────────────────────────

/** Full AG-UI RunAgentInput as defined by the protocol. */
export interface RunAgentInput {
  threadId: string;
  runId: string;
  parentRunId?: string;
  state?: unknown;
  messages: Message[];
  tools?: Tool[];
  context?: Context[];
  forwardedProps?: unknown;
}

// ─── Frontend Tool Handler ────────────────────────────────────────────────────

/** Context passed to frontend tool handlers alongside the parsed arguments. */
export interface FrontendToolContext {
  /** The unique tool call ID assigned by the agent. */
  toolCallId: string;
  /** The tool name being called. */
  toolCallName: string;
}

/**
 * A frontend tool handler receives the parsed tool arguments and an optional
 * context (containing toolCallId) and returns a result string. If it throws,
 * the error message is sent back as the tool result with an error flag.
 */
export type FrontendToolHandler = (
  args: Record<string, unknown>,
  context: FrontendToolContext,
) => Promise<string> | string;

/**
 * Registry mapping tool names to their frontend handler + definition.
 */
export interface FrontendToolRegistration {
  tool: Tool;
  handler: FrontendToolHandler;
}

// ─── Stream Callbacks ─────────────────────────────────────────────────────────

export interface AguiStreamCallbacks {
  // Lifecycle
  onRunStarted?: (event: RunStartedEvent) => void;
  onRunFinished?: (event: RunFinishedEvent) => void;
  onRunError?: (event: RunErrorEvent) => void;
  onStepStarted?: (event: StepStartedEvent) => void;
  onStepFinished?: (event: StepFinishedEvent) => void;
  // Text messages
  onTextMessageStart?: (event: TextMessageStartEvent) => void;
  onTextMessageContent?: (event: TextMessageContentEvent) => void;
  onTextMessageEnd?: (event: TextMessageEndEvent) => void;
  // Tool calls
  onToolCallStart?: (event: ToolCallStartEvent) => void;
  onToolCallArgs?: (event: ToolCallArgsEvent) => void;
  onToolCallEnd?: (event: ToolCallEndEvent) => void;
  onToolCallResult?: (event: ToolCallResultEvent) => void;
  // State management
  onStateSnapshot?: (event: StateSnapshotEvent) => void;
  onStateDelta?: (event: StateDeltaEvent) => void;
  onMessagesSnapshot?: (event: MessagesSnapshotEvent) => void;
  // Activity
  onActivitySnapshot?: (event: ActivitySnapshotEvent) => void;
  onActivityDelta?: (event: ActivityDeltaEvent) => void;
  // Reasoning
  onReasoningStart?: (event: ReasoningStartEvent) => void;
  onReasoningMessageStart?: (event: ReasoningMessageStartEvent) => void;
  onReasoningMessageContent?: (event: ReasoningMessageContentEvent) => void;
  onReasoningMessageEnd?: (event: ReasoningMessageEndEvent) => void;
  onReasoningEnd?: (event: ReasoningEndEvent) => void;
  onReasoningEncryptedValue?: (event: ReasoningEncryptedValueEvent) => void;
  // Special
  onRawEvent?: (event: RawEvent) => void;
  onCustomEvent?: (event: CustomEvent) => void;
  // Catch-all
  onEvent?: (event: AguiEvent) => void;
}

// ─── Core Streaming Function ──────────────────────────────────────────────────

export interface StreamAguiOptions {
  /** AG-UI run input (messages, tools, thread context, etc.) */
  input: RunAgentInput;
  /** Callbacks for individual event types */
  callbacks: AguiStreamCallbacks;
  /** Function to retrieve the current auth token */
  getAccessToken: () => Promise<string | null>;
  /** Optional AbortSignal to cancel the stream */
  signal?: AbortSignal;
  /**
   * Frontend tool registrations. When the agent calls a tool that exists in
   * this map, the client executes the handler locally and automatically
   * re-runs the agent with the tool result appended to messages.
   */
  frontendTools?: Map<string, FrontendToolRegistration>;
  /**
   * Maximum number of automatic re-runs for client-side tool execution.
   * Prevents infinite loops. Defaults to 10.
   */
  maxToolReruns?: number;
}

/**
 * Streams an AG-UI request to the backend and invokes callbacks for each event.
 *
 * When the agent calls a frontend-defined tool:
 * 1. The tool call events (START → ARGS → END) are emitted via callbacks
 * 2. The client accumulates the tool call arguments
 * 3. After RUN_FINISHED, the client executes the frontend tool handler
 * 4. The client appends the tool result as a ToolMessage to the conversation
 * 5. The client automatically re-runs the agent with the updated messages
 *
 * This loop continues until the agent finishes without pending frontend tool
 * calls, or the maxToolReruns limit is reached.
 */
export async function streamAguiChat(options: StreamAguiOptions): Promise<void> {
  const {
    input,
    callbacks,
    getAccessToken,
    signal,
    frontendTools,
    maxToolReruns = 10,
  } = options;

  let currentInput = input;
  let rerunCount = 0;

  // Re-run loop: after executing frontend tools, re-invoke the agent
  while (true) {
    if (signal?.aborted) break;

    // Track tool calls during this run for client-side execution
    const pendingToolCalls = new Map<
      string,
      { name: string; argFragments: string[]; parentMessageId?: string }
    >();
    // Track which tool calls got server-side results (TOOL_CALL_RESULT)
    const serverResolvedToolCalls = new Set<string>();

    await executeStream(currentInput, callbacks, getAccessToken, signal, {
      onToolCallStartInternal: (event) => {
        pendingToolCalls.set(event.toolCallId, {
          name: event.toolCallName,
          argFragments: [],
          parentMessageId: event.parentMessageId,
        });
      },
      onToolCallArgsInternal: (event) => {
        const tc = pendingToolCalls.get(event.toolCallId);
        if (tc) tc.argFragments.push(event.delta);
      },
      onToolCallResultInternal: (event) => {
        serverResolvedToolCalls.add(event.toolCallId);
      },
    });

    // Determine which tool calls need client-side execution
    const frontendPendingCalls: Array<{
      toolCallId: string;
      name: string;
      args: string;
      parentMessageId?: string;
    }> = [];

    if (frontendTools && frontendTools.size > 0) {
      for (const [toolCallId, tc] of pendingToolCalls) {
        if (serverResolvedToolCalls.has(toolCallId)) continue;
        if (frontendTools.has(tc.name)) {
          frontendPendingCalls.push({
            toolCallId,
            name: tc.name,
            args: tc.argFragments.join(''),
            parentMessageId: tc.parentMessageId,
          });
        }
      }
    }

    // If no frontend tool calls need execution, we're done
    if (frontendPendingCalls.length === 0) break;

    // Guard against infinite re-run loops
    rerunCount++;
    if (rerunCount > maxToolReruns) {
      console.warn(
        `AG-UI client: reached max tool re-runs (${maxToolReruns}), stopping.`
      );
      break;
    }

    // Execute frontend tools and collect results
    const toolResultMessages: ToolMessage[] = [];
    for (const call of frontendPendingCalls) {
      const registration = frontendTools!.get(call.name)!;
      let result: string;
      let error: string | undefined;

      try {
        const parsedArgs = call.args ? JSON.parse(call.args) : {};
        result = await registration.handler(parsedArgs, {
          toolCallId: call.toolCallId,
          toolCallName: call.name,
        });
      } catch (err) {
        error = err instanceof Error ? err.message : String(err);
        result = error;
      }

      const toolMsg: ToolMessage = {
        id: `tool-result-${call.toolCallId}`,
        role: 'tool',
        content: result,
        toolCallId: call.toolCallId,
        ...(error ? { error } : {}),
      };
      toolResultMessages.push(toolMsg);
    }

    // Build the assistant message with tool calls for conversation history
    const assistantToolCalls: ToolCall[] = frontendPendingCalls.map((call) => ({
      id: call.toolCallId,
      type: 'function' as const,
      function: {
        name: call.name,
        arguments: call.args,
      },
    }));

    // Reconstruct messages: existing + assistant message with tool calls + tool results
    const updatedMessages: Message[] = [
      ...currentInput.messages,
      {
        id: `assistant-tc-${Date.now()}`,
        role: 'assistant',
        toolCalls: assistantToolCalls,
      } satisfies AssistantMessage,
      ...toolResultMessages,
    ];

    // Prepare re-run input
    currentInput = {
      ...currentInput,
      messages: updatedMessages,
      runId: generateId(),
      parentRunId: currentInput.runId,
    };
  }
}

// ─── Internal Stream Execution ────────────────────────────────────────────────

interface InternalHooks {
  onToolCallStartInternal: (event: ToolCallStartEvent) => void;
  onToolCallArgsInternal: (event: ToolCallArgsEvent) => void;
  onToolCallResultInternal: (event: ToolCallResultEvent) => void;
}

/**
 * Executes a single POST → SSE stream against the AG-UI endpoint.
 * Parses the SSE format and dispatches events to both public callbacks
 * and internal hooks (for tool call tracking).
 */
async function executeStream(
  input: RunAgentInput,
  callbacks: AguiStreamCallbacks,
  getAccessToken: () => Promise<string | null>,
  signal: AbortSignal | undefined,
  hooks: InternalHooks
): Promise<void> {
  const token = await getAccessToken();
  const selectedTenant = getSelectedTenant();

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    Accept: 'text/event-stream',
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  if (selectedTenant?.tenantId) {
    headers['X-Tenant-Id'] = selectedTenant.tenantId;
  }

  const response = await fetch(`${apiConfig.baseUrl}/ai/agui`, {
    method: 'POST',
    headers,
    body: JSON.stringify(input),
    signal,
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => 'Unknown error');
    throw new Error(`AG-UI request failed: ${response.status} ${errorText}`);
  }

  if (!response.body) {
    throw new Error('No response body (SSE streaming not supported)');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  // State for expanding chunk convenience events
  let currentTextChunkMessageId: string | undefined;
  let currentToolChunkId: string | undefined;
  let currentReasoningChunkMessageId: string | undefined;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // SSE format: "data: {json}\n\n"
      const lines = buffer.split('\n');
      buffer = '';

      for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        if (line.startsWith('data: ')) {
          const jsonStr = line.slice(6);

          // Check for terminating empty line
          if (i + 1 < lines.length && lines[i + 1] === '') {
            try {
              const event = JSON.parse(jsonStr) as AguiEvent;
              // Expand chunk convenience events before dispatching
              const expanded = expandChunkEvents(event, {
                currentTextChunkMessageId,
                currentToolChunkId,
                currentReasoningChunkMessageId,
              });

              for (const e of expanded.events) {
                dispatchEvent(e, callbacks, hooks);
              }

              currentTextChunkMessageId = expanded.currentTextChunkMessageId;
              currentToolChunkId = expanded.currentToolChunkId;
              currentReasoningChunkMessageId = expanded.currentReasoningChunkMessageId;
            } catch {
              console.warn('Failed to parse AG-UI event:', jsonStr);
            }
            i++; // Skip the empty line
          } else if (i === lines.length - 1) {
            buffer = line + '\n';
          }
        } else if (line === '' || line.startsWith(':')) {
          // Empty line (event separator) or SSE comment — skip
        } else if (line.length > 0) {
          buffer = line;
          for (let j = i + 1; j < lines.length; j++) {
            buffer += '\n' + lines[j];
          }
          break;
        }
      }
    }

    // Close any open chunk sequences at stream end
    const closingEvents: AguiEvent[] = [];
    if (currentTextChunkMessageId) {
      closingEvents.push({
        type: 'TEXT_MESSAGE_END',
        messageId: currentTextChunkMessageId,
      });
    }
    if (currentToolChunkId) {
      closingEvents.push({
        type: 'TOOL_CALL_END',
        toolCallId: currentToolChunkId,
      });
    }
    if (currentReasoningChunkMessageId) {
      closingEvents.push({
        type: 'REASONING_MESSAGE_END',
        messageId: currentReasoningChunkMessageId,
      });
    }
    for (const e of closingEvents) {
      dispatchEvent(e, callbacks, hooks);
    }
  } finally {
    reader.releaseLock();
  }
}

// ─── Chunk Event Expansion ────────────────────────────────────────────────────

interface ChunkState {
  currentTextChunkMessageId: string | undefined;
  currentToolChunkId: string | undefined;
  currentReasoningChunkMessageId: string | undefined;
}

interface ChunkResult extends ChunkState {
  events: AguiEvent[];
}

/**
 * Expands convenience chunk events (TEXT_MESSAGE_CHUNK, TOOL_CALL_CHUNK,
 * REASONING_MESSAGE_CHUNK) into their standard start/content/end triads.
 */
function expandChunkEvents(event: AguiEvent, state: ChunkState): ChunkResult {
  const events: AguiEvent[] = [];
  let { currentTextChunkMessageId, currentToolChunkId, currentReasoningChunkMessageId } = state;

  switch (event.type) {
    case 'TEXT_MESSAGE_CHUNK': {
      const chunk = event as TextMessageChunkEvent;
      const msgId = chunk.messageId ?? currentTextChunkMessageId;
      if (!msgId) {
        // Invalid chunk — no messageId and no active chunk
        events.push(event);
        break;
      }

      // Close previous chunk if messageId changed
      if (currentTextChunkMessageId && currentTextChunkMessageId !== msgId) {
        events.push({ type: 'TEXT_MESSAGE_END', messageId: currentTextChunkMessageId });
        currentTextChunkMessageId = undefined;
      }

      // Start new chunk if needed
      if (!currentTextChunkMessageId || currentTextChunkMessageId !== msgId) {
        events.push({
          type: 'TEXT_MESSAGE_START',
          messageId: msgId,
          role: chunk.role ?? 'assistant',
        });
        currentTextChunkMessageId = msgId;
      }

      // Emit content if delta is present
      if (chunk.delta) {
        events.push({ type: 'TEXT_MESSAGE_CONTENT', messageId: msgId, delta: chunk.delta });
      }
      break;
    }

    case 'TOOL_CALL_CHUNK': {
      const chunk = event as ToolCallChunkEvent;
      const tcId = chunk.toolCallId ?? currentToolChunkId;
      if (!tcId) {
        events.push(event);
        break;
      }

      if (currentToolChunkId && currentToolChunkId !== tcId) {
        events.push({ type: 'TOOL_CALL_END', toolCallId: currentToolChunkId });
        currentToolChunkId = undefined;
      }

      if (!currentToolChunkId || currentToolChunkId !== tcId) {
        events.push({
          type: 'TOOL_CALL_START',
          toolCallId: tcId,
          toolCallName: chunk.toolCallName ?? '',
          parentMessageId: chunk.parentMessageId,
        });
        currentToolChunkId = tcId;
      }

      if (chunk.delta) {
        events.push({ type: 'TOOL_CALL_ARGS', toolCallId: tcId, delta: chunk.delta });
      }
      break;
    }

    case 'REASONING_MESSAGE_CHUNK': {
      const chunk = event as ReasoningMessageChunkEvent;
      const msgId = chunk.messageId ?? currentReasoningChunkMessageId;
      if (!msgId) {
        events.push(event);
        break;
      }

      if (currentReasoningChunkMessageId && currentReasoningChunkMessageId !== msgId) {
        events.push({ type: 'REASONING_MESSAGE_END', messageId: currentReasoningChunkMessageId });
        currentReasoningChunkMessageId = undefined;
      }

      if (!currentReasoningChunkMessageId || currentReasoningChunkMessageId !== msgId) {
        events.push({
          type: 'REASONING_MESSAGE_START',
          messageId: msgId,
          role: 'assistant',
        });
        currentReasoningChunkMessageId = msgId;
      }

      if (chunk.delta) {
        events.push({ type: 'REASONING_MESSAGE_CONTENT', messageId: msgId, delta: chunk.delta });
      } else {
        // Empty delta closes the message
        events.push({ type: 'REASONING_MESSAGE_END', messageId: msgId });
        currentReasoningChunkMessageId = undefined;
      }
      break;
    }

    default:
      // Non-chunk events close any open text/tool chunks if they're not content events
      if (event.type !== 'TEXT_MESSAGE_CONTENT' && currentTextChunkMessageId) {
        // Only auto-close if this isn't part of the same text message
        if (event.type !== 'TEXT_MESSAGE_END') {
          // Let it pass through naturally
        }
      }
      events.push(event);
      break;
  }

  return {
    events,
    currentTextChunkMessageId,
    currentToolChunkId,
    currentReasoningChunkMessageId,
  };
}

// ─── Event Dispatch ───────────────────────────────────────────────────────────

function dispatchEvent(
  event: AguiEvent,
  callbacks: AguiStreamCallbacks,
  hooks: InternalHooks
): void {
  // Always fire the catch-all
  callbacks.onEvent?.(event);

  switch (event.type) {
    // Lifecycle
    case 'RUN_STARTED':
      callbacks.onRunStarted?.(event as RunStartedEvent);
      break;
    case 'RUN_FINISHED':
      callbacks.onRunFinished?.(event as RunFinishedEvent);
      break;
    case 'RUN_ERROR':
      callbacks.onRunError?.(event as RunErrorEvent);
      break;
    case 'STEP_STARTED':
      callbacks.onStepStarted?.(event as StepStartedEvent);
      break;
    case 'STEP_FINISHED':
      callbacks.onStepFinished?.(event as StepFinishedEvent);
      break;

    // Text messages
    case 'TEXT_MESSAGE_START':
      callbacks.onTextMessageStart?.(event as TextMessageStartEvent);
      break;
    case 'TEXT_MESSAGE_CONTENT':
      callbacks.onTextMessageContent?.(event as TextMessageContentEvent);
      break;
    case 'TEXT_MESSAGE_END':
      callbacks.onTextMessageEnd?.(event as TextMessageEndEvent);
      break;

    // Tool calls
    case 'TOOL_CALL_START':
      hooks.onToolCallStartInternal(event as ToolCallStartEvent);
      callbacks.onToolCallStart?.(event as ToolCallStartEvent);
      break;
    case 'TOOL_CALL_ARGS':
      hooks.onToolCallArgsInternal(event as ToolCallArgsEvent);
      callbacks.onToolCallArgs?.(event as ToolCallArgsEvent);
      break;
    case 'TOOL_CALL_END':
      callbacks.onToolCallEnd?.(event as ToolCallEndEvent);
      break;
    case 'TOOL_CALL_RESULT':
      hooks.onToolCallResultInternal(event as ToolCallResultEvent);
      callbacks.onToolCallResult?.(event as ToolCallResultEvent);
      break;

    // State management
    case 'STATE_SNAPSHOT':
      callbacks.onStateSnapshot?.(event as StateSnapshotEvent);
      break;
    case 'STATE_DELTA':
      callbacks.onStateDelta?.(event as StateDeltaEvent);
      break;
    case 'MESSAGES_SNAPSHOT':
      callbacks.onMessagesSnapshot?.(event as MessagesSnapshotEvent);
      break;

    // Activity
    case 'ACTIVITY_SNAPSHOT':
      callbacks.onActivitySnapshot?.(event as ActivitySnapshotEvent);
      break;
    case 'ACTIVITY_DELTA':
      callbacks.onActivityDelta?.(event as ActivityDeltaEvent);
      break;

    // Reasoning
    case 'REASONING_START':
      callbacks.onReasoningStart?.(event as ReasoningStartEvent);
      break;
    case 'REASONING_MESSAGE_START':
      callbacks.onReasoningMessageStart?.(event as ReasoningMessageStartEvent);
      break;
    case 'REASONING_MESSAGE_CONTENT':
      callbacks.onReasoningMessageContent?.(event as ReasoningMessageContentEvent);
      break;
    case 'REASONING_MESSAGE_END':
      callbacks.onReasoningMessageEnd?.(event as ReasoningMessageEndEvent);
      break;
    case 'REASONING_END':
      callbacks.onReasoningEnd?.(event as ReasoningEndEvent);
      break;
    case 'REASONING_ENCRYPTED_VALUE':
      callbacks.onReasoningEncryptedValue?.(event as ReasoningEncryptedValueEvent);
      break;

    // Special
    case 'RAW':
      callbacks.onRawEvent?.(event as RawEvent);
      break;
    case 'CUSTOM':
      callbacks.onCustomEvent?.(event as CustomEvent);
      break;
  }
}

// ─── Utilities ────────────────────────────────────────────────────────────────

/** Generate a simple unique ID. */
export function generateId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 9)}`;
}
