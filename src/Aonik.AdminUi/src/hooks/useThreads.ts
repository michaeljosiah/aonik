import { useCallback, useEffect, useState } from 'react';
import { useAuth, apiConfig } from '@/auth';
import { getSelectedTenant } from '@/lib/tenantContext';

// ─── Types ────────────────────────────────────────────────────────────────────

/** Summary of a chat thread (matches backend ChatThreadSummary). */
export interface ThreadSummary {
  id: string;
  title: string;
  status: string;
  agentName?: string;
  lastMessageAt?: string;
  messageCount: number;
  createdAt: string;
}

/** A single message within a thread (matches backend ChatThreadMessageDto). */
export interface ThreadMessageDto {
  id: string;
  role: string;
  content: string;
  agentName?: string;
  toolCallsJson?: string;
  sortOrder: number;
  createdAt: string;
}

/** Full thread detail with messages (matches backend ChatThreadDetail). */
export interface ThreadDetail {
  id: string;
  title: string;
  status: string;
  agentName?: string;
  lastMessageAt?: string;
  messageCount: number;
  createdAt: string;
  messages: ThreadMessageDto[];
}

// ─── Hook Return Type ─────────────────────────────────────────────────────────

export interface UseThreadsReturn {
  /** List of thread summaries. */
  threads: ThreadSummary[];
  /** Whether the thread list is currently loading. */
  isLoading: boolean;
  /** Error message from the last fetch, if any. */
  error: string | null;
  /** Refresh the thread list from the server. */
  refresh: () => Promise<void>;
  /** Load a specific thread's full detail (with messages). */
  loadThread: (threadId: string) => Promise<ThreadDetail | null>;
  /** Archive (soft-delete) a thread by ID and refresh the list. */
  archiveThread: (threadId: string) => Promise<void>;
}

// ─── Hook Implementation ──────────────────────────────────────────────────────

export function useThreads(): UseThreadsReturn {
  const [threads, setThreads] = useState<ThreadSummary[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { getAccessToken } = useAuth();

  const buildHeaders = useCallback(async (includeContentType = false): Promise<Record<string, string>> => {
    const headers: Record<string, string> = {
      Accept: 'application/json',
    };

    if (includeContentType) {
      headers['Content-Type'] = 'application/json';
    }

    const token = await getAccessToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const tenant = getSelectedTenant();
    if (tenant?.tenantId) {
      headers['X-Tenant-Id'] = tenant.tenantId;
    }

    return headers;
  }, [getAccessToken]);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const headers = await buildHeaders();
      const response = await fetch(
        `${apiConfig.baseUrl}/ai/threads?page=1&pageSize=50`,
        { method: 'GET', headers }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch threads: ${response.status}`);
      }

      const data = await response.json();
      const threadList = (data.threads ?? []) as ThreadSummary[];
      setThreads(threadList);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load threads';
      setError(message);
      console.error('useThreads.refresh:', err);
    } finally {
      setIsLoading(false);
    }
  }, [buildHeaders]);

  const loadThread = useCallback(
    async (threadId: string): Promise<ThreadDetail | null> => {
      try {
        const headers = await buildHeaders();
        const response = await fetch(
          `${apiConfig.baseUrl}/ai/threads/${threadId}`,
          { method: 'GET', headers }
        );

        if (!response.ok) {
          if (response.status === 404) return null;
          throw new Error(`Failed to load thread: ${response.status}`);
        }

        return (await response.json()) as ThreadDetail;
      } catch (err) {
        console.error('useThreads.loadThread:', err);
        return null;
      }
    },
    [buildHeaders]
  );

  const archiveThread = useCallback(
    async (threadId: string): Promise<void> => {
      try {
        const headers = await buildHeaders();
        const response = await fetch(
          `${apiConfig.baseUrl}/ai/threads/${threadId}`,
          { method: 'DELETE', headers }
        );

        if (!response.ok) {
          throw new Error(`Failed to archive thread: ${response.status}`);
        }

        // Refresh the list to reflect the archived thread
        await refresh();
      } catch (err) {
        console.error('useThreads.archiveThread:', err);
      }
    },
    [buildHeaders, refresh]
  );

  // Load threads on mount
  useEffect(() => {
    refresh();
  }, [refresh]);

  return {
    threads,
    isLoading,
    error,
    refresh,
    loadThread,
    archiveThread,
  };
}
