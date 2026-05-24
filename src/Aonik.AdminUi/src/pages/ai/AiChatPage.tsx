// Admin AI chat surface. Despite the historical filename ("AiChatMock"),
// this page is fully wired to the AG-UI streaming protocol via
// `useAguiChat` and pulls real persisted threads via `useThreads`.
//
// File renamed to `AiChatPage` so the surface matches what it actually is.

import { useMemo, useState, useCallback, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import {
  BookOpenText,
  Loader2,
  MoreHorizontal,
  Plus,
  Search,
  Sparkles,
  SquarePen,
  Trash2,
  WandSparkles,
} from 'lucide-react';

import { useAuth } from '@/auth/useAuth';
import { AiAgentSelector, type AiAgentSelectorItem } from '@/components/ai/AiAgentSelector';
import { AiChatComposer } from '@/components/ai/AiChatComposer';
import { ChatMessageList } from '@/components/ai/ChatMessageList';
import {
  Conversation,
  ConversationContent,
  ConversationEmptyState,
  ConversationScrollButton,
} from '@/components/ai-elements';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { resolveChatRunState, useAguiChat } from '@/hooks/useAguiChat';
import { useThreads, type ThreadSummary } from '@/hooks/useThreads';

type AiChatPageProps = {
  agentId?: string;
  agents?: AiAgentSelectorItem[];
  onSelectAgent?: (agentId: string) => void;
};

const welcomePrompts = [
  {
    id: 'prompt-1',
    title: 'Summarize the latest platform activity and flag anything that needs attention.',
    prompt: 'Summarize the latest platform activity and flag anything that needs attention.',
    icon: Sparkles,
  },
  {
    id: 'prompt-2',
    title: 'Find the most useful dashboards, agents, and docs for my current workspace.',
    prompt: 'Find the most useful dashboards, agents, and docs for my current workspace.',
    icon: BookOpenText,
  },
  {
    id: 'prompt-3',
    title: 'Help me plan the next steps for billing, collections, and reconciliation.',
    prompt: 'Help me plan the next steps for billing, collections, and reconciliation.',
    icon: WandSparkles,
  },
];

function getGreeting(name?: string) {
  const hour = new Date().getHours();
  const period = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
  return name ? `${period} ${name}!` : `${period}!`;
}

export function AiChatPage({ agentId, agents, onSelectAgent }: AiChatPageProps) {
  const params = useParams<{ agentId?: string }>();
  const [query, setQuery] = useState('');
  const [activeThreadId, setActiveThreadId] = useState<string | null>(null);
  const { user } = useAuth();
  const effectiveAgentId = agentId ?? params.agentId ?? undefined;
  const {
    messages,
    draft,
    setDraft,
    isStreaming,
    streamError,
    handleSend,
    stopStreaming,
    resetChat,
    pendingApprovals,
    approveAction,
    rejectAction,
    selectToolCallOptions,
    threadId,
    loadThread,
    voiceModeAvailable,
    voiceModeEnabled,
    setVoiceModeEnabled,
    voicePlaybackState,
    voiceError,
  } = useAguiChat(effectiveAgentId || undefined);

  const {
    threads,
    isLoading: isLoadingThreads,
    refresh: refreshThreads,
    loadThread: fetchThread,
    archiveThread,
  } = useThreads();

  useEffect(() => {
    if (params.agentId && onSelectAgent && params.agentId !== agentId) {
      onSelectAgent(params.agentId);
    }
  }, [agentId, onSelectAgent, params.agentId]);

  const agentLabel = useMemo(() => {
    if (!effectiveAgentId) return 'AONIK Orchestrator';
    return effectiveAgentId
      .replace(/-agent$/, '')
      .replace(/-/g, ' ')
      .replace(/\b\w/g, (c) => c.toUpperCase());
  }, [effectiveAgentId]);

  const filteredThreads = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return threads;
    return threads.filter((t) => t.title.toLowerCase().includes(q));
  }, [threads, query]);

  const activeThread = useMemo(
    () => threads.find((thread) => thread.id === activeThreadId || thread.id === threadId) ?? null,
    [activeThreadId, threadId, threads]
  );

  const threadTitle = activeThread?.title || (messages.length > 0 ? 'Conversation' : 'New conversation');
  const greeting = getGreeting(user?.name?.split(' ')[0]);
  const chatRunState = resolveChatRunState(messages, isStreaming);

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
    async (event: React.MouseEvent, targetThreadId: string) => {
      event.stopPropagation();
      await archiveThread(targetThreadId);
      if (activeThreadId === targetThreadId) {
        resetChat();
        setActiveThreadId(null);
      }
    },
    [activeThreadId, archiveThread, resetChat]
  );

  const handleSendAndRefresh = useCallback(async () => {
    await handleSend();
    setTimeout(() => refreshThreads(), 1000);
  }, [handleSend, refreshThreads]);

  const handlePromptClick = useCallback((prompt: string) => {
    setDraft(prompt);
  }, [setDraft]);

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
    <div className="chat-primary h-full flex bg-[var(--color-surface)]">
      <aside className="flex w-72 shrink-0 flex-col border-r border-[var(--color-border-light)] bg-[var(--color-sidebar-bg)]">
        <div className="flex h-[50px] items-center gap-2 border-b border-[var(--color-border-light)] px-4">
          <div className="flex min-w-0 items-center gap-2">
            <div className="grid h-7 w-7 place-items-center rounded-[2px] bg-[var(--color-surface)] border border-[var(--color-border)]">
              <span className="text-xs font-semibold text-[var(--color-text-primary)]">A</span>
            </div>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-[var(--color-text-primary)]">AONIK AI</div>
            </div>
          </div>
          <div className="ml-auto flex items-center gap-1">
            <button className="hover-theme-effect grid h-8 w-8 place-items-center text-[var(--color-text-tertiary)]" type="button">
              <MoreHorizontal className="h-4 w-4" />
            </button>
          </div>
        </div>

        <div className="flex-1 p-4">
          <button
            className="flex h-10 w-full items-center gap-2 rounded-[2px] border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm text-[var(--color-text-primary)] shadow-sm transition-colors hover:bg-[var(--color-background)]"
            type="button"
            onClick={handleNewChat}
          >
            <Plus className="h-4 w-4 text-[var(--color-text-secondary)]" />
            New chat
          </button>

          <div className="mt-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
              <input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search chats"
                className="h-10 w-full rounded-[2px] border border-[var(--color-border)] bg-[var(--color-surface)] pl-9 pr-3 text-sm text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] outline-none focus:border-[var(--color-brand-primary)]"
              />
            </div>
          </div>

          <div className="mt-5">
            <div className="text-[11px] font-semibold tracking-wider text-[var(--color-text-tertiary)]">
              CHATS
            </div>

            <div className="visible-scrollbar mt-2 max-h-[calc(100vh-240px)] space-y-1 overflow-y-auto pr-1">
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
                filteredThreads.map((thread) => {
                  const isActive = thread.id === activeThreadId || thread.id === threadId;
                  return (
                    <div
                      key={thread.id}
                      className={cn(
                        'chat-history-item group flex cursor-pointer items-center gap-2 rounded-[2px] px-3 py-2',
                        isActive
                          ? 'bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]'
                          : 'hover:bg-[var(--color-sidebar-hover)]'
                      )}
                      onClick={() => handleThreadClick(thread)}
                    >
                      <div className="min-w-0 flex-1">
                        <div
                          className={cn(
                            'truncate text-sm font-medium',
                            isActive ? 'text-[var(--color-text-primary)]' : 'text-[var(--color-text-secondary)]'
                          )}
                        >
                          {thread.title || 'Untitled'}
                        </div>
                        <div className="text-[10px] text-[var(--color-text-tertiary)]">
                          {formatDateLabel(thread.lastMessageAt ?? thread.createdAt)}
                        </div>
                      </div>
                      <button
                        className={cn(
                          'grid h-7 w-7 place-items-center rounded-[2px] text-[var(--color-text-tertiary)] hover:text-[var(--color-danger)]',
                          isActive ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'
                        )}
                        title="Archive chat"
                        onClick={(event) => handleArchiveThread(event, thread.id)}
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

      <section className="flex min-w-0 flex-1 flex-col bg-[var(--color-surface)]">
        <div className="m-2 flex h-[50px] items-center justify-between rounded-[2px] px-2">
          <div className="flex items-center gap-2">
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]" onClick={handleNewChat}>
              <SquarePen className="w-4 h-4" />
            </Button>
            {agents && onSelectAgent ? (
              <AiAgentSelector
                 agents={agents}
                 selectedAgentId={effectiveAgentId ?? ''}
                 onSelectAgent={onSelectAgent}
               />
            ) : (
              <div className="inline-flex items-center gap-2 px-2 py-1.5 text-sm text-[var(--color-text-primary)]">
                <div className="grid h-7 w-7 place-items-center rounded-full bg-[var(--color-brand-primary)] text-white">
                  <span className="text-xs font-semibold">A</span>
                </div>
                <span className="max-w-[270px] truncate font-medium">{agentLabel}</span>
              </div>
            )}
          </div>

          <div className="min-w-0 flex-1 px-4 text-center">
            <h2 className="truncate text-xl font-semibold text-[var(--color-text-heading)] lg:text-2xl">
              {threadTitle}
            </h2>
          </div>

          <div className="w-[124px]" />
        </div>

        <div className="flex-1 min-h-0 bg-[var(--color-surface)]">
          <Conversation className="h-full">
            <ConversationContent className="h-full">
              {messages.length === 0 ? (
                <ConversationEmptyState>
                  <div className="mx-auto flex w-full max-w-[820px] flex-col items-center gap-6 px-4 py-8">
                    <div className="text-center">
                      <h1 className="text-3xl font-semibold text-[var(--color-text-heading)]">{greeting}</h1>
                      <p className="mt-2 text-base text-[var(--color-text-secondary)]">
                        Ask anything about your workspace, agents, data products, or platform operations.
                      </p>
                    </div>

                    <div className="grid w-full gap-4 sm:grid-cols-2 xl:grid-cols-3 justify-items-center">
                      {welcomePrompts.map((promptCard) => {
                        const Icon = promptCard.icon;
                        return (
                          <button
                            key={promptCard.id}
                            type="button"
                            onClick={() => handlePromptClick(promptCard.prompt)}
                            className="chat-history-item flex h-[200px] w-full max-w-[220px] flex-col rounded-lg bg-[var(--color-gray-200)] p-3 text-left hover:bg-[var(--color-gray-300)]"
                          >
                            <div className="chat-prompt-icon mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-[var(--color-gray-300)] text-[var(--color-text-secondary)]">
                              <Icon className="h-5 w-5" />
                            </div>
                            <div className="text-xl leading-7 text-[var(--color-text-secondary)]">
                              {promptCard.title}
                            </div>
                          </button>
                        );
                      })}
                    </div>

                    <AiChatComposer
                      mode="center"
                      value={draft}
                      onChange={setDraft}
                      onSend={handleSendAndRefresh}
                      onStop={stopStreaming}
                      isStreaming={isStreaming}
                      showHelper={false}
                      voiceModeAvailable={voiceModeAvailable}
                      voiceModeEnabled={voiceModeEnabled}
                      onToggleVoiceMode={setVoiceModeEnabled}
                      voicePlaybackState={voicePlaybackState}
                    />

                    <button
                      type="button"
                      className="inline-flex items-center gap-2 text-sm text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
                    >
                      <Search className="h-4 w-4" />
                      Browse prompts
                    </button>
                  </div>
                </ConversationEmptyState>
              ) : (
                <div className="w-full px-4 py-6">
                  <ChatMessageList
                    messages={messages}
                    isStreaming={isStreaming}
                    pendingApprovals={pendingApprovals}
                    onApproveAction={approveAction}
                    onRejectAction={rejectAction}
                    onSelectToolCallOptions={selectToolCallOptions}
                  />
                </div>
              )}
            </ConversationContent>
            <ConversationScrollButton />
          </Conversation>
        </div>

        {messages.length > 0 && (
          <div className="border-t border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <div className="mx-auto w-full max-w-[900px] px-4 py-4">
              <AiChatComposer
                mode="footer"
                value={draft}
                onChange={setDraft}
                onSend={handleSendAndRefresh}
                onStop={stopStreaming}
                isStreaming={isStreaming}
                onClear={handleNewChat}
                voiceModeAvailable={voiceModeAvailable}
                voiceModeEnabled={voiceModeEnabled}
                onToggleVoiceMode={setVoiceModeEnabled}
                voicePlaybackState={voicePlaybackState}
              />
              <div className="mt-3 flex items-center justify-between text-xs text-[var(--color-text-tertiary)]">
                <span>
                  {chatRunState === 'streaming' ? (
                    <span className="inline-flex items-center gap-1">
                      <Loader2 className="h-3 w-3 animate-spin" />
                      Streaming...
                    </span>
                    ) : chatRunState === 'awaiting-selection' ? (
                      'Awaiting selection'
                    ) : chatRunState === 'awaiting-approval' ? (
                      'Awaiting approval'
                    ) : streamError ? (
                      <span className="text-[var(--color-danger)]">{streamError}</span>
                    ) : voiceError ? (
                      <span className="text-[var(--color-warning)]">{voiceError}</span>
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
