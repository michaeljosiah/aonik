/**
 * AI Playground Streaming Client
 *
 * Simplified SSE client for the playground endpoint. Unlike the full AG-UI
 * client, this skips the frontend tool re-run loop (tools execute server-side
 * only in the playground) and extracts metrics from the RUN_FINISHED event.
 */

import { apiConfig } from '@/auth';
import { getSelectedTenant } from '@/lib/tenantContext';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface PlaygroundRunRequest {
  agentName?: string;
  systemPrompt?: string;
  modelId?: string;
  userBriefJson?: string;
  /**
   * When set, the backend overrides the request-scoped ICurrentUserContext.UserId
   * to this value for the duration of the run — so personal-finance sub-agents
   * and other services target this user's data instead of the calling admin's.
   * Populated by the User Brief picker when a real customer is selected.
   */
  impersonateUserId?: string;
  enabledToolNames?: string[];
  messages: PlaygroundMessage[];
  temperature?: number;
  maxTokens?: number;
  /** Client-side tool definitions to send to the agent so it knows they're available. */
  toolDefinitions?: PlaygroundToolDefinition[];
  /** AI Task ID to test. When set, the backend resolves the task's prompt templates. */
  aiTaskId?: string;
  /** Key-value pairs to substitute into the AI Task's {{variable}} template placeholders. */
  promptVariables?: Record<string, string>;
}

export interface PlaygroundToolDefinition {
  name: string;
  description: string;
  parameters: unknown;
}

/** Context passed to frontend tool handlers. */
export interface PlaygroundFrontendToolContext {
  toolCallId: string;
  toolCallName: string;
}

/** A frontend tool handler that executes client-side and returns a result string. */
export type PlaygroundFrontendToolHandler = (
  args: Record<string, unknown>,
  context: PlaygroundFrontendToolContext,
) => Promise<string> | string;

/** Registration entry for a client-side tool in the playground. */
export interface PlaygroundFrontendToolRegistration {
  tool: PlaygroundToolDefinition;
  handler: PlaygroundFrontendToolHandler;
}

export interface PlaygroundMessage {
  role: 'user' | 'assistant' | 'system' | 'tool';
  content: string;
  id?: string;
  toolCallId?: string;
  toolCalls?: Array<{
    id: string;
    type?: 'function';
    function: {
      name: string;
      arguments: string;
    };
  }>;
}

export interface PlaygroundRunMetrics {
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  latencyMs: number;
  estimatedCostUsd?: number;
  modelName?: string;
  modelId?: string;
}

export interface PlaygroundStreamCallbacks {
  onRunStarted?: (runId: string) => void;
  onRerun?: () => void;
  onTextDelta?: (delta: string) => void;
  onSpeechChunk?: (payload: {
    messageId: string;
    chunkIndex: number;
    speechText: string;
    isFinal: boolean;
  }) => void;
  onSpeechRender?: (payload: {
    messageId: string;
    speechText: string;
    requiresVisualAttention: boolean;
    requiresApproval: boolean;
  }) => void;
  onToolCallStart?: (toolCallId: string, toolName: string) => void;
  onToolCallArgs?: (toolCallId: string, argsDelta: string) => void;
  onToolCallEnd?: (toolCallId: string) => void;
  /** @deprecated Use onToolCallStart/Args/End for structured tracking */
  onToolCall?: (toolCallId: string, toolName: string, args?: string) => void;
  onToolResult?: (toolCallId: string, content: string) => void;
  onReasoningDelta?: (delta: string) => void;
  onReasoningEnd?: () => void;
  onRunFinished?: (metrics: PlaygroundRunMetrics) => void;
  onRunError?: (message: string, code?: string) => void;
}

export interface StreamPlaygroundOptions {
  request: PlaygroundRunRequest;
  callbacks: PlaygroundStreamCallbacks;
  getAccessToken: () => Promise<string | null>;
  signal?: AbortSignal;
  /**
   * Frontend tool registrations. When the agent calls a tool in this map,
   * the client executes the handler locally and re-runs the agent with
   * the tool result appended. Tool definitions are automatically included
   * in the request payload.
   */
  frontendTools?: Map<string, PlaygroundFrontendToolRegistration>;
  /** Maximum number of re-runs for frontend tool execution. Defaults to 10. */
  maxToolReruns?: number;
}

// ─── Review Types ───────────────────────────────────────────────────────────

export interface PlaygroundReviewRequest {
  systemPrompt?: string;
  userBriefJson?: string;
  messages?: PlaygroundMessage[];
  assistantResponse: string;
  toolCalls?: Array<{
    toolName: string;
    arguments: string;
    result?: string;
  }>;
  modelId?: string;
}

export interface PlaygroundReviewCallbacks {
  onReviewStarted?: (reviewId: string) => void;
  onReviewDelta?: (delta: string) => void;
  onReviewFinished?: (parsed: unknown | null, rawText: string | null) => void;
  onReviewError?: (message: string) => void;
}

// ─── Review Streaming Function ──────────────────────────────────────────────

export async function streamPlaygroundReview(options: {
  request: PlaygroundReviewRequest;
  callbacks: PlaygroundReviewCallbacks;
  getAccessToken: () => Promise<string | null>;
  signal?: AbortSignal;
}): Promise<void> {
  const { request, callbacks, getAccessToken, signal } = options;

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

  const response = await fetch(`${apiConfig.baseUrl}/ai/playground/review`, {
    method: 'POST',
    headers,
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => 'Unknown error');
    throw new Error(`Review request failed: ${response.status} ${errorText}`);
  }

  if (!response.body) {
    throw new Error('No response body (SSE streaming not supported)');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let currentData: string | null = null;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const parts = buffer.split('\n');
      buffer = parts.pop() ?? '';

      for (const line of parts) {
        if (line.startsWith('data: ')) {
          const payload = line.slice(6);
          currentData = currentData === null ? payload : currentData + '\n' + payload;
        } else if (line === '') {
          if (currentData !== null) {
            try {
              const event = JSON.parse(currentData) as Record<string, unknown>;
              dispatchReviewEvent(event, callbacks);
            } catch {
              console.warn('Failed to parse review event:', currentData);
            }
            currentData = null;
          }
        }
      }
    }

    // Process remaining buffer
    if (buffer.startsWith('data: ')) {
      currentData = currentData === null ? buffer.slice(6) : currentData + '\n' + buffer.slice(6);
    }
    if (currentData !== null) {
      try {
        const event = JSON.parse(currentData) as Record<string, unknown>;
        dispatchReviewEvent(event, callbacks);
      } catch {
        console.warn('Failed to parse final review event:', currentData);
      }
    }
  } finally {
    reader.releaseLock();
  }
}

function dispatchReviewEvent(
  event: Record<string, unknown>,
  callbacks: PlaygroundReviewCallbacks,
): void {
  switch (event.type) {
    case 'REVIEW_STARTED':
      callbacks.onReviewStarted?.(event.reviewId as string);
      break;

    case 'REVIEW_CONTENT':
      callbacks.onReviewDelta?.(event.delta as string);
      break;

    case 'REVIEW_FINISHED':
      callbacks.onReviewFinished?.(
        event.parsed ?? null,
        (event.rawText as string) ?? null,
      );
      break;

    case 'REVIEW_ERROR':
      callbacks.onReviewError?.(event.message as string);
      break;
  }
}

// ─── Core Streaming Function ─────────────────────────────────────────────────

export async function streamPlaygroundRun(
  options: StreamPlaygroundOptions,
): Promise<void> {
  const {
    request,
    callbacks,
    getAccessToken,
    signal,
    frontendTools,
    maxToolReruns = 10,
  } = options;

  // Merge frontend tool definitions into the request
  const effectiveRequest = { ...request };
  if (frontendTools && frontendTools.size > 0) {
    const clientToolDefs = Array.from(frontendTools.values()).map((r) => r.tool);
    effectiveRequest.toolDefinitions = [
      ...(request.toolDefinitions ?? []),
      ...clientToolDefs,
    ];
  }

  let currentRequest = effectiveRequest;
  let rerunCount = 0;
  let shouldResetOutputForRerun = false;

  // Re-run loop: after executing frontend tools, re-invoke the agent
  while (true) {
    if (signal?.aborted) break;

    if (shouldResetOutputForRerun) {
      callbacks.onRerun?.();
      shouldResetOutputForRerun = false;
    }

    // Track tool calls during this run for client-side execution
    const pendingToolCalls = new Map<
      string,
      { name: string; argFragments: string[] }
    >();
    const serverResolvedToolCalls = new Set<string>();

    await executePlaygroundStream(
      currentRequest,
      callbacks,
      getAccessToken,
      signal,
      {
        onToolCallStartInternal: (toolCallId, toolName) => {
          pendingToolCalls.set(toolCallId, { name: toolName, argFragments: [] });
        },
        onToolCallArgsInternal: (toolCallId, delta) => {
          const tc = pendingToolCalls.get(toolCallId);
          if (tc) tc.argFragments.push(delta);
        },
        onToolCallResultInternal: (toolCallId) => {
          serverResolvedToolCalls.add(toolCallId);
        },
      },
    );

    // Determine which tool calls need client-side execution
    const frontendPendingCalls: Array<{
      toolCallId: string;
      name: string;
      args: string;
    }> = [];

    if (frontendTools && frontendTools.size > 0) {
      for (const [toolCallId, tc] of pendingToolCalls) {
        if (serverResolvedToolCalls.has(toolCallId)) continue;
        if (frontendTools.has(tc.name)) {
          frontendPendingCalls.push({
            toolCallId,
            name: tc.name,
            args: tc.argFragments.join(''),
          });
        }
      }
    }

    // If no frontend tool calls need execution, we're done
    if (frontendPendingCalls.length === 0) break;

    rerunCount++;
    if (rerunCount > maxToolReruns) {
      console.warn(`Playground client: reached max tool re-runs (${maxToolReruns}), stopping.`);
      break;
    }

    // Execute frontend tools and append results as messages for the re-run
    const toolResultMessages: PlaygroundMessage[] = [];
    const assistantToolCalls: NonNullable<PlaygroundMessage['toolCalls']> = [];
    for (const call of frontendPendingCalls) {
      const registration = frontendTools!.get(call.name)!;
      let result: string;

       assistantToolCalls.push({
        id: call.toolCallId,
        type: 'function',
        function: {
          name: call.name,
          arguments: call.args,
        },
      });

      try {
        const parsedArgs = call.args ? JSON.parse(call.args) : {};
        result = await registration.handler(parsedArgs, {
          toolCallId: call.toolCallId,
          toolCallName: call.name,
        });
      } catch (err) {
        result = err instanceof Error ? err.message : String(err);
      }

      callbacks.onToolResult?.(call.toolCallId, result);
      toolResultMessages.push({
        id: `tool-result-${call.toolCallId}`,
        role: 'tool',
        toolCallId: call.toolCallId,
        content: result,
      });
    }

    // Re-run with tool results appended
    currentRequest = {
      ...currentRequest,
      messages: [
        ...currentRequest.messages,
        {
          id: `assistant-tool-call-${Date.now()}`,
          role: 'assistant',
          content: '',
          toolCalls: assistantToolCalls,
        },
        ...toolResultMessages,
      ],
    };
    shouldResetOutputForRerun = true;
  }
}

/** Execute a single playground stream request. */
async function executePlaygroundStream(
  request: PlaygroundRunRequest,
  callbacks: PlaygroundStreamCallbacks,
  getAccessToken: () => Promise<string | null>,
  signal?: AbortSignal,
  internalCallbacks?: {
    onToolCallStartInternal?: (toolCallId: string, toolName: string) => void;
    onToolCallArgsInternal?: (toolCallId: string, delta: string) => void;
    onToolCallResultInternal?: (toolCallId: string) => void;
  },
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

  const response = await fetch(`${apiConfig.baseUrl}/ai/playground/run`, {
    method: 'POST',
    headers,
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => 'Unknown error');
    throw new Error(`Playground request failed: ${response.status} ${errorText}`);
  }

  if (!response.body) {
    throw new Error('No response body (SSE streaming not supported)');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let currentData: string | null = null;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // Split on newlines; the last element may be an incomplete line
      const parts = buffer.split('\n');
      // Keep the last (potentially incomplete) segment in the buffer
      buffer = parts.pop() ?? '';

      for (const line of parts) {
        if (line.startsWith('data: ')) {
          // Accumulate data field (SSE allows multi-line data)
          const payload = line.slice(6);
          currentData = currentData === null ? payload : currentData + '\n' + payload;
        } else if (line.startsWith(':')) {
          // SSE comment — ignore
        } else if (line === '') {
          // Empty line = event boundary — dispatch accumulated data
          if (currentData !== null) {
            try {
              const event = JSON.parse(currentData);
              dispatchEvent(event, callbacks, internalCallbacks);
            } catch {
              console.warn('Failed to parse playground event:', currentData);
            }
            currentData = null;
          }
        }
        // Any other non-empty line is ignored (not part of SSE spec)
      }
    }

    // Process any remaining data in the buffer after stream ends
    if (buffer.startsWith('data: ')) {
      currentData = currentData === null ? buffer.slice(6) : currentData + '\n' + buffer.slice(6);
    }
    if (currentData !== null) {
      try {
        const event = JSON.parse(currentData);
        dispatchEvent(event, callbacks, internalCallbacks);
      } catch {
        console.warn('Failed to parse final playground event:', currentData);
      }
    }
  } finally {
    reader.releaseLock();
  }
}

// ─── Event Dispatch ──────────────────────────────────────────────────────────

// Accumulate tool call args across TOOL_CALL_ARGS events
const toolCallArgs = new Map<string, string[]>();
const toolCallNames = new Map<string, string>();

function dispatchEvent(
  event: Record<string, unknown>,
  callbacks: PlaygroundStreamCallbacks,
  internalCallbacks?: {
    onToolCallStartInternal?: (toolCallId: string, toolName: string) => void;
    onToolCallArgsInternal?: (toolCallId: string, delta: string) => void;
    onToolCallResultInternal?: (toolCallId: string) => void;
  },
): void {
  switch (event.type) {
    case 'RUN_STARTED':
      callbacks.onRunStarted?.(event.runId as string);
      break;

    case 'TEXT_MESSAGE_CONTENT':
      callbacks.onTextDelta?.(event.delta as string);
      break;

    case 'TOOL_CALL_START': {
      const toolCallId = event.toolCallId as string;
      const toolName = event.toolCallName as string;
      toolCallArgs.set(toolCallId, []);
      toolCallNames.set(toolCallId, toolName);
      callbacks.onToolCallStart?.(toolCallId, toolName);
      callbacks.onToolCall?.(toolCallId, toolName);
      internalCallbacks?.onToolCallStartInternal?.(toolCallId, toolName);
      break;
    }

    case 'TOOL_CALL_ARGS': {
      const id = event.toolCallId as string;
      const delta = event.delta as string;
      const fragments = toolCallArgs.get(id);
      if (fragments) fragments.push(delta);
      callbacks.onToolCallArgs?.(id, delta);
      internalCallbacks?.onToolCallArgsInternal?.(id, delta);
      break;
    }

    case 'TOOL_CALL_END': {
      const id = event.toolCallId as string;
      const fragments = toolCallArgs.get(id);
      if (fragments) {
        // Re-dispatch with complete args
        callbacks.onToolCallEnd?.(id);
        callbacks.onToolCall?.(id, toolCallNames.get(id) ?? '', fragments.join(''));
        toolCallArgs.delete(id);
        toolCallNames.delete(id);
      }
      break;
    }

    case 'TOOL_CALL_RESULT':
      callbacks.onToolResult?.(
        event.toolCallId as string,
        event.content as string,
      );
      internalCallbacks?.onToolCallResultInternal?.(event.toolCallId as string);
      toolCallArgs.delete(event.toolCallId as string);
      toolCallNames.delete(event.toolCallId as string);
      break;

    // Reasoning events (emitted when the model uses extended thinking)
    case 'REASONING_MESSAGE_CONTENT':
      callbacks.onReasoningDelta?.(event.delta as string);
      break;

    case 'REASONING_MESSAGE_END':
    case 'REASONING_END':
      callbacks.onReasoningEnd?.();
      break;

    case 'RUN_FINISHED': {
      const rawMetrics = event.metrics as Record<string, unknown> | undefined;
      const metrics: PlaygroundRunMetrics = rawMetrics
        ? {
            inputTokens: (rawMetrics.inputTokens as number) ?? 0,
            outputTokens: (rawMetrics.outputTokens as number) ?? 0,
            totalTokens: (rawMetrics.totalTokens as number) ?? 0,
            latencyMs: (rawMetrics.latencyMs as number) ?? 0,
            estimatedCostUsd: rawMetrics.estimatedCostUsd as number | undefined,
            modelName: (rawMetrics.modelName ?? event.modelName) as string | undefined,
            modelId: (rawMetrics.modelId ?? event.modelId) as string | undefined,
          }
        : {
            inputTokens: 0,
            outputTokens: 0,
            totalTokens: 0,
            latencyMs: 0,
            modelName: event.modelName as string | undefined,
            modelId: event.modelId as string | undefined,
          };
      callbacks.onRunFinished?.(metrics);
      break;
    }

    case 'RUN_ERROR':
      callbacks.onRunError?.(
        event.message as string,
        event.code as string | undefined,
      );
      break;

    case 'CUSTOM': {
      const value = event.value as Record<string, unknown> | undefined;
      const messageId = typeof value?.messageId === 'string' ? value.messageId : '';

      if (event.name === 'speech.chunk') {
        const speechText = typeof value?.speechText === 'string' ? value.speechText : '';
        if (!speechText || !messageId) break;

        const chunkIndex = typeof value?.chunkIndex === 'number' ? value.chunkIndex : 0;
        callbacks.onSpeechChunk?.({
          messageId,
          chunkIndex,
          speechText,
          isFinal: value?.isFinal === true,
        });
        break;
      }

      if (event.name === 'speech.render') {
        const speechText = typeof value?.speechText === 'string' ? value.speechText : '';
        if (!messageId) break;

        callbacks.onSpeechRender?.({
          messageId,
          speechText,
          requiresVisualAttention: value?.requiresVisualAttention === true,
          requiresApproval: value?.requiresApproval === true,
        });
        break;
      }

      break;
    }
  }
}
