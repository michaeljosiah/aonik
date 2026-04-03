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
  enabledToolNames?: string[];
  messages: PlaygroundMessage[];
  temperature?: number;
  maxTokens?: number;
}

export interface PlaygroundMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface PlaygroundRunMetrics {
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  latencyMs: number;
  estimatedCostUsd?: number;
}

export interface PlaygroundStreamCallbacks {
  onRunStarted?: (runId: string) => void;
  onTextDelta?: (delta: string) => void;
  onToolCall?: (toolCallId: string, toolName: string, args?: string) => void;
  onToolResult?: (toolCallId: string, content: string) => void;
  onRunFinished?: (metrics: PlaygroundRunMetrics) => void;
  onRunError?: (message: string, code?: string) => void;
}

export interface StreamPlaygroundOptions {
  request: PlaygroundRunRequest;
  callbacks: PlaygroundStreamCallbacks;
  getAccessToken: () => Promise<string | null>;
  signal?: AbortSignal;
}

// ─── Core Streaming Function ─────────────────────────────────────────────────

export async function streamPlaygroundRun(
  options: StreamPlaygroundOptions,
): Promise<void> {
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
              dispatchEvent(event, callbacks);
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
        dispatchEvent(event, callbacks);
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

function dispatchEvent(
  event: Record<string, unknown>,
  callbacks: PlaygroundStreamCallbacks,
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
      toolCallArgs.set(toolCallId, []);
      callbacks.onToolCall?.(toolCallId, event.toolCallName as string);
      break;
    }

    case 'TOOL_CALL_ARGS': {
      const id = event.toolCallId as string;
      const fragments = toolCallArgs.get(id);
      if (fragments) fragments.push(event.delta as string);
      break;
    }

    case 'TOOL_CALL_END': {
      const id = event.toolCallId as string;
      const fragments = toolCallArgs.get(id);
      if (fragments) {
        // Re-dispatch with complete args
        callbacks.onToolCall?.(id, '', fragments.join(''));
        toolCallArgs.delete(id);
      }
      break;
    }

    case 'TOOL_CALL_RESULT':
      callbacks.onToolResult?.(
        event.toolCallId as string,
        event.content as string,
      );
      break;

    case 'RUN_FINISHED': {
      const metrics: PlaygroundRunMetrics = event.metrics
        ? (event.metrics as PlaygroundRunMetrics)
        : {
            inputTokens: 0,
            outputTokens: 0,
            totalTokens: 0,
            latencyMs: 0,
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
  }
}
