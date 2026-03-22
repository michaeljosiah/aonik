import { useMemo, useState, useCallback } from 'react';
import { Loader2, MoreHorizontal, Plus, Search, Trash2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import { AiChatComposer } from '@/components/ai/AiChatComposer';
import { ChatMessageList } from '@/components/ai/ChatMessageList';
import {
  Conversation,
  ConversationContent,
  ConversationEmptyState,
  ConversationScrollButton,
} from '@/components/ai-elements';
import { useAguiChat } from '@/hooks/useAguiChat';
import { useThreads, type ThreadSummary } from '@/hooks/useThreads';

type AiChatMockProps = {
  agentId?: string;
};

export function AiChatMock({ agentId }: AiChatMockProps) {
  const [query, setQuery] = useState('');
  const [activeThreadId, setActiveThreadId] = useState<string | null>(null);
  const {
    messages,
    draft,
    setDraft,
    isStreaming,
    streamError,
    handleSend,
    resetChat,
    pendingApprovals,
    approveAction,
    rejectAction,
    threadId,
    loadThread,
  } = useAguiChat();

  const {
    threads,
    isLoading: isLoadingThreads,
    refresh: refreshThreads,
    loadThread: fetchThread,
    archiveThread,
  } = useThreads();

  const agentLabel = useMemo(() => {
    if (!agentId) return 'AONIK Orchestrator';
    if (agentId === 'a-personal') return 'Agent name';
    if (agentId === 'a-centrali') return 'AONIK Orchestrator';
    return 'Agent name';
  }, [agentId]);

  const filteredThreads = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return threads;
    return threads.filter((t) => t.title.toLowerCase().includes(q));
  }, [threads, query]);

  const handleNewChat = useCallback(() => {
    resetChat();
    setActiveThreadId(null);
  }, [resetChat]);

  const handleThreadClick = useCallback(
    async (thread: ThreadSummary) => {
      if (thread.id === activeThreadId) return;

      setActiveThreadId(thread.id);
      const detail = await fetchThread(thread.id);
      if (detail) {
        loadThread(detail);
      }
    },
    [activeThreadId, fetchThread, loadThread]
  );

  const handleArchiveThread = useCallback(
    async (e: React.MouseEvent, threadId: string) => {
      e.stopPropagation();
      await archiveThread(threadId);
      if (activeThreadId === threadId) {
        resetChat();
        setActiveThreadId(null);
      }
    },
    [archiveThread, activeThreadId, resetChat]
  );

  // After sending a message, refresh thread list (new threads will appear)
  const handleSendAndRefresh = useCallback(async () => {
    await handleSend();
    // Small delay to let the backend persist the thread
    setTimeout(() => refreshThreads(), 1000);
  }, [handleSend, refreshThreads]);

  // Format a date string into a relative label
  const formatDateLabel = (dateStr?: string) => {
    if (!dateStr) return '';
    try {
      const date = new Date(dateStr);
      const now = new Date();
      const diffMs = now.getTime() - date.getTime();
      const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

      if (diffDays === 0) return 'Today';
      if (diffDays === 1) return 'Yesterday';
      if (diffDays < 7) return `${diffDays} days ago`;
      return date.toLocaleDateString();
    } catch {
      return '';
    }
  };

  return (
    <div className="h-full flex bg-[var(--color-background)]">
      {/* Chat list sidebar (sits next to the main collapsed nav) */}
      <aside className="w-72 shrink-0 border-r border-[var(--color-border-light)] bg-[var(--color-sidebar-bg)]">
        <div className="h-14 px-4 flex items-center gap-2 border-b border-[var(--color-border-light)]">
          <div className="flex items-center gap-2 min-w-0">
            <div className="h-7 w-7 rounded-lg bg-[var(--color-surface)] border border-[var(--color-border)] grid place-items-center">
              <span className="text-xs font-semibold text-[var(--color-text-primary)]">A</span>
            </div>
            <div className="min-w-0">
              <div className="text-sm font-medium text-[var(--color-text-primary)] truncate">AONIK AI</div>
            </div>
          </div>
          <div className="ml-auto flex items-center gap-2">
            <button className="h-8 w-8 rounded-md grid place-items-center text-[var(--color-text-tertiary)] hover:bg-[var(--color-sidebar-hover)]">
              <MoreHorizontal className="h-4 w-4" />
            </button>
          </div>
        </div>

        <div className="p-4">
          <button
            className="w-full h-10 rounded-lg bg-[var(--color-surface)] border border-[var(--color-border)] shadow-sm flex items-center gap-2 px-3 text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-background)]"
            type="button"
            onClick={handleNewChat}
          >
            <Plus className="h-4 w-4 text-[var(--color-text-secondary)]" />
            New chat
          </button>

          <div className="mt-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--color-text-tertiary)]" />
              <input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search chats"
                className="w-full h-10 pl-9 pr-3 rounded-lg bg-[var(--color-surface)] border border-[var(--color-border)] text-sm text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)]"
              />
            </div>
          </div>

          <div className="mt-5">
            <div className="text-[11px] font-semibold text-[var(--color-text-tertiary)] tracking-wider">
              CHATS
            </div>

            <div className="mt-2 space-y-1">
              {isLoadingThreads && threads.length === 0 ? (
                <div className="flex items-center justify-center py-4">
                  <Loader2 className="h-4 w-4 animate-spin text-[var(--color-text-tertiary)]" />
                  <span className="ml-2 text-xs text-[var(--color-text-tertiary)]">Loading...</span>
                </div>
              ) : filteredThreads.length === 0 ? (
                <div className="py-4 text-center text-xs text-[var(--color-text-tertiary)]">
                  {query ? 'No matching chats' : 'No conversations yet'}
                </div>
              ) : (
                filteredThreads.map((t) => {
                  const isActive = t.id === activeThreadId || t.id === threadId;
                  return (
                    <div
                      key={t.id}
                      className={cn(
                        'group flex items-center gap-2 h-9 px-3 rounded-lg cursor-pointer',
                        isActive
                          ? 'bg-[var(--color-sidebar-hover)]'
                          : 'hover:bg-[var(--color-sidebar-hover)]'
                      )}
                      onClick={() => handleThreadClick(t)}
                    >
                      <div className="flex-1 min-w-0">
                        <div
                          className={cn(
                            'text-sm truncate',
                            isActive
                              ? 'text-[var(--color-text-primary)]'
                              : 'text-[var(--color-text-secondary)]'
                          )}
                        >
                          {t.title || 'Untitled'}
                        </div>
                        <div className="text-[10px] text-[var(--color-text-tertiary)]">
                          {formatDateLabel(t.lastMessageAt ?? t.createdAt)}
                        </div>
                      </div>
                      <button
                        className={cn(
                          'h-7 w-7 rounded-md grid place-items-center text-[var(--color-text-tertiary)] hover:text-red-400',
                          isActive ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'
                        )}
                        title="Archive chat"
                        onClick={(e) => handleArchiveThread(e, t.id)}
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  );
                })
              )}
            </div>
          </div>
        </div>
      </aside>

      {/* Main chat area */}
      <section className="flex-1 min-w-0 flex flex-col bg-[var(--color-surface-inset)]">
        <div className="flex-1 min-h-0">
          <Conversation className="h-full">
            <ConversationContent className="h-full">
              {messages.length === 0 ? (
                <ConversationEmptyState>
                  <div className="mx-auto flex w-full max-w-3xl flex-col items-center justify-center gap-5 px-4">
                    <div className="h-14 w-14 rounded-2xl bg-[var(--color-surface)] border border-[var(--color-border)] shadow-sm grid place-items-center">
                      <span className="text-lg font-bold text-[var(--color-text-primary)]">A</span>
                    </div>
                    <div className="text-center">
                      <div className="text-lg font-semibold text-[var(--color-text-primary)]">Hi, I'm ready when you are.</div>
                      <div className="mt-1 text-sm text-[var(--color-text-secondary)]">Ask me anything about your AONIK platform...</div>
                    </div>
                    <AiChatComposer
                      mode="center"
                      value={draft}
                      onChange={setDraft}
                      onSend={handleSendAndRefresh}
                      showHelper={false}
                    />
                  </div>
                </ConversationEmptyState>
              ) : (
                <div className="mx-auto w-full max-w-3xl px-4 py-6">
                  <ChatMessageList
                    messages={messages}
                    isStreaming={isStreaming}
                    pendingApprovals={pendingApprovals}
                    onApproveAction={approveAction}
                    onRejectAction={rejectAction}
                  />
                </div>
              )}
            </ConversationContent>
            <ConversationScrollButton />
          </Conversation>
        </div>

        {messages.length > 0 && (
          <div className="border-t border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <div className="mx-auto w-full max-w-3xl px-4 py-4">
              <AiChatComposer
                mode="footer"
                value={draft}
                onChange={setDraft}
                onSend={handleSendAndRefresh}
                onClear={handleNewChat}
              />
              <div className="mt-3 flex items-center justify-between text-xs text-[var(--color-text-tertiary)]">
                <span>
                  {isStreaming ? (
                    <span className="inline-flex items-center gap-1">
                      <Loader2 className="h-3 w-3 animate-spin" />
                      Streaming...
                    </span>
                  ) : streamError ? (
                    <span className="text-red-500">{streamError}</span>
                  ) : (
                    'Connected via AG-UI protocol'
                  )}
                </span>
                <span>Agent: {agentLabel}</span>
              </div>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
