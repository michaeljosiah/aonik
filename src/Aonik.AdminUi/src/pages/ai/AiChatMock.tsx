import { useMemo, useState } from 'react';
import {
  Bell,
  Check,
  ChevronDown,
  Menu,
  Mic,
  MoreHorizontal,
  Plus,
  Search,
  Send,
  Trash2,
} from 'lucide-react';

import * as DropdownMenu from '@radix-ui/react-dropdown-menu';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  Conversation,
  ConversationContent,
  ConversationEmptyState,
  ConversationScrollButton,
  Message,
  MessageContent,
} from '@/components/ai-elements';

type ChatItem = {
  id: string;
  title: string;
  isActive?: boolean;
};

type ProjectItem = {
  id: string;
  title: string;
  isActive?: boolean;
};

type ChatMessage = {
  id: string;
  from: 'user' | 'assistant';
  content: string;
};

type AgentItem = {
  id: string;
  title: string;
  description: string;
  group: 'personal' | 'agents';
  icon: 'centrali' | 'fox';
};

export function AiChatMock() {
  const [query, setQuery] = useState('');
  const [draft, setDraft] = useState('');

  const agents = useMemo<AgentItem[]>(
    () => [
      {
        id: 'a-personal',
        title: 'Agent name',
        description: 'Short description',
        group: 'personal',
        icon: 'fox',
      },
      {
        id: 'a-centrali',
        title: 'Centrali Ai',
        description: 'Short description',
        group: 'agents',
        icon: 'centrali',
      },
      {
        id: 'a-2',
        title: 'Agent name',
        description: 'Short description',
        group: 'agents',
        icon: 'fox',
      },
      {
        id: 'a-3',
        title: 'Agent name',
        description: 'Short description',
        group: 'agents',
        icon: 'fox',
      },
      {
        id: 'a-4',
        title: 'Agent name',
        description: 'Short description',
        group: 'agents',
        icon: 'fox',
      },
    ],
    []
  );

  const [selectedAgentId, setSelectedAgentId] = useState('a-centrali');
  const selectedAgent = agents.find((a) => a.id === selectedAgentId) ?? agents[0];

  const projects = useMemo<ProjectItem[]>(
    () => [
      { id: 'p-1', title: 'Project name' },
      { id: 'p-2', title: 'Project name (active)', isActive: true },
      { id: 'p-3', title: 'Project name' },
      { id: 'p-4', title: 'Project name' },
      { id: 'p-5', title: 'Project name' },
    ],
    []
  );

  const chats = useMemo<ChatItem[]>(
    () => [
      { id: 'c-1', title: 'Chat name (active)', isActive: true },
      { id: 'c-2', title: 'Chat name' },
      { id: 'c-3', title: 'Chat name' },
      { id: 'c-4', title: 'Chat name' },
      { id: 'c-5', title: 'Chat name' },
      { id: 'c-6', title: 'Chat name' },
    ],
    []
  );

  const filteredChats = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return chats;
    return chats.filter((c) => c.title.toLowerCase().includes(q));
  }, [chats, query]);

  const [messages, setMessages] = useState<ChatMessage[]>([]);

  const resetChat = () => {
    setMessages([]);
    setDraft('');
  };

  const handleSend = () => {
    const text = draft.trim();
    if (!text) return;

    setMessages((prev) => [
      ...prev,
      { id: `m-${Date.now()}`, from: 'user', content: text },
      {
        id: `m-${Date.now()}-a`,
        from: 'assistant',
        content:
          'Mocked response. Endpoints are not wired yet, but the chat UI is ready for integration.',
      },
    ]);
    setDraft('');
  };

  const ChatComposer = ({ mode }: { mode: 'center' | 'footer' }) => {
    const isCenter = mode === 'center';

    const handleKeyDown: React.KeyboardEventHandler<HTMLTextAreaElement> = (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    };

    return (
      <div
        className={cn(
          'rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm',
          isCenter ? 'w-full max-w-[640px]' : 'overflow-hidden'
        )}
      >
        <div className={cn('px-4 pt-4', !isCenter && 'pt-3')}>
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Ask me anything..."
            rows={isCenter ? 3 : 1}
            className={cn(
              'w-full resize-none bg-transparent text-sm text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] outline-none leading-6',
              isCenter ? 'min-h-[96px]' : 'min-h-9'
            )}
          />
        </div>

        <div className={cn('px-3 pb-3 flex items-center justify-between', isCenter && 'pb-3')}
        >
          <button
            className="h-9 w-9 rounded-xl grid place-items-center text-[var(--color-text-secondary)] hover:bg-[var(--color-background)]"
            title="Attach"
            type="button"
          >
            <Plus className="h-4 w-4" />
          </button>

          <div className="flex items-center gap-2">
            <button
              type="button"
              className="inline-flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-[var(--color-text-secondary)] hover:bg-[var(--color-background)]"
              title="Model"
            >
              ChatGPT 5.2
              <ChevronDown className="h-4 w-4 text-[var(--color-text-tertiary)]" />
            </button>
            <button
              type="button"
              className="h-9 w-9 rounded-xl grid place-items-center text-[var(--color-text-tertiary)] hover:bg-[var(--color-background)]"
              title="Voice"
            >
              <Mic className="h-4 w-4" />
            </button>
            <button
              className={cn(
                'h-10 w-10 rounded-xl grid place-items-center',
                draft.trim()
                  ? 'bg-[var(--color-brand-primary)] text-white hover:bg-[var(--color-brand-primary-dark)]'
                  : 'bg-[var(--color-background)] text-[var(--color-text-tertiary)]'
              )}
              title="Send"
              type="button"
              onClick={handleSend}
              disabled={!draft.trim()}
            >
              <Send className="h-4 w-4" />
            </button>
          </div>
        </div>

        {!isCenter && (
          <div className="px-4 pb-3 flex items-center justify-between text-[12px] text-[var(--color-text-tertiary)]">
            <span>Shift+Enter for newline</span>
            <button
              type="button"
              onClick={resetChat}
              className="inline-flex items-center gap-1 px-2 py-1 rounded-md hover:bg-[var(--color-background)]"
            >
              <Trash2 className="h-3.5 w-3.5" />
              Clear
            </button>
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="h-full flex bg-[var(--color-background)]">
      {/* Chat list sidebar (sits next to the main collapsed nav) */}
      <aside className="w-72 shrink-0 border-r border-[var(--color-border-light)] bg-[var(--color-sidebar-bg)]">
        <div className="h-14 px-4 flex items-center gap-2 border-b border-[var(--color-border-light)]">
          <div className="flex items-center gap-2 min-w-0">
            <div className="h-7 w-7 rounded-lg bg-[var(--color-surface)] border border-[var(--color-border)] grid place-items-center">
              <span className="text-xs font-semibold text-[var(--color-text-primary)]">C</span>
            </div>
            <div className="min-w-0">
              <div className="text-sm font-medium text-[var(--color-text-primary)] truncate">Centrali AI</div>
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
            onClick={resetChat}
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
            <div className="flex items-center justify-between text-[11px] font-semibold text-[var(--color-text-tertiary)] tracking-wider">
              <span>PROJECTS</span>
              <button className="h-7 w-7 rounded-md grid place-items-center text-[var(--color-text-tertiary)] hover:bg-[var(--color-sidebar-hover)]">
                <Plus className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-2 space-y-1">
              {projects.map((p) => (
                <button
                  key={p.id}
                  className={cn(
                    'w-full flex items-center gap-2 h-9 px-3 rounded-lg text-sm text-left',
                    p.isActive
                      ? 'bg-[var(--color-sidebar-hover)] text-[var(--color-text-primary)]'
                      : 'text-[var(--color-text-secondary)] hover:bg-[var(--color-sidebar-hover)]'
                  )}
                >
                  <span className="h-4 w-4 rounded-sm border border-[var(--color-border)] bg-[var(--color-surface)]" />
                  <span className="truncate">{p.title}</span>
                </button>
              ))}

              <button className="w-full text-left h-9 px-3 rounded-lg text-sm text-[var(--color-text-tertiary)] hover:bg-[var(--color-sidebar-hover)]">
                View more
              </button>
            </div>
          </div>

          <div className="mt-6">
            <div className="text-[11px] font-semibold text-[var(--color-text-tertiary)] tracking-wider">
              CHATS
            </div>

            <div className="mt-2 space-y-1">
              {filteredChats.map((c) => (
                <div
                  key={c.id}
                  className={cn(
                    'group flex items-center gap-2 h-9 px-3 rounded-lg',
                    c.isActive
                      ? 'bg-[var(--color-sidebar-hover)]'
                      : 'hover:bg-[var(--color-sidebar-hover)]'
                  )}
                >
                  <button
                    className={cn(
                      'flex-1 text-left text-sm truncate',
                      c.isActive
                        ? 'text-[var(--color-text-primary)]'
                        : 'text-[var(--color-text-secondary)]'
                    )}
                  >
                    {c.title}
                  </button>
                  <button
                    className={cn(
                      'h-7 w-7 rounded-md grid place-items-center text-[var(--color-text-tertiary)]',
                      c.isActive ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'
                    )}
                    title="Chat options"
                  >
                    <MoreHorizontal className="h-4 w-4" />
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>
      </aside>

      {/* Main chat area */}
      <section className="flex-1 min-w-0 flex flex-col bg-[var(--color-surface-inset)]">
        <div className="h-14 px-6 flex items-center justify-between border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <DropdownMenu.Root>
            <DropdownMenu.Trigger asChild>
              <button
                type="button"
                className="inline-flex items-center gap-2 rounded-lg px-2 py-1 -ml-2 hover:bg-[var(--color-background)]"
              >
                <div className="h-7 w-7 rounded-full bg-[var(--color-surface)] border border-[var(--color-border)] grid place-items-center">
                  <span className="text-xs font-semibold text-[var(--color-text-primary)]">C</span>
                </div>
                <span className="text-sm font-medium text-[var(--color-text-primary)]">{selectedAgent.title}</span>
                <ChevronDown className="h-4 w-4 text-[var(--color-text-tertiary)]" />
              </button>
            </DropdownMenu.Trigger>

            <DropdownMenu.Portal>
              <DropdownMenu.Content
                sideOffset={10}
                align="start"
                className="w-[280px] rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] shadow-lg p-2"
              >
                <div className="px-3 py-2 text-[11px] font-semibold text-[var(--color-text-tertiary)] tracking-wider">
                  PERSONAL ASSISTANT
                </div>
                {agents
                  .filter((a) => a.group === 'personal')
                  .map((a) => (
                    <DropdownMenu.Item
                      key={a.id}
                      onSelect={() => setSelectedAgentId(a.id)}
                      className="outline-none"
                    >
                      <div className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-[var(--color-background)]">
                        <div className="h-8 w-8 rounded-lg bg-[var(--color-surface)] border border-[var(--color-border)] grid place-items-center">
                          <span className="text-sm">{a.icon === 'fox' ? '🦊' : 'C'}</span>
                        </div>
                        <div className="min-w-0">
                          <div className="text-sm font-medium text-[var(--color-text-primary)] truncate">{a.title}</div>
                          <div className="text-xs text-[var(--color-text-tertiary)] truncate">{a.description}</div>
                        </div>
                        {a.id === selectedAgentId && <Check className="ml-auto h-4 w-4 text-[var(--color-brand-primary)]" />}
                      </div>
                    </DropdownMenu.Item>
                  ))}

                <div className="px-3 pt-3 pb-2 text-[11px] font-semibold text-[var(--color-text-tertiary)] tracking-wider">
                  AGENTS
                </div>
                {agents
                  .filter((a) => a.group === 'agents')
                  .map((a) => (
                    <DropdownMenu.Item
                      key={a.id}
                      onSelect={() => setSelectedAgentId(a.id)}
                      className="outline-none"
                    >
                      <div className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-[var(--color-background)]">
                        <div className="h-8 w-8 rounded-lg bg-[var(--color-surface)] border border-[var(--color-border)] grid place-items-center">
                          <span className="text-sm">{a.icon === 'fox' ? '🦊' : 'C'}</span>
                        </div>
                        <div className="min-w-0">
                          <div className="text-sm font-medium text-[var(--color-text-primary)] truncate">{a.title}</div>
                          <div className="text-xs text-[var(--color-text-tertiary)] truncate">{a.description}</div>
                        </div>
                        {a.id === selectedAgentId && <Check className="ml-auto h-4 w-4 text-[var(--color-brand-primary)]" />}
                      </div>
                    </DropdownMenu.Item>
                  ))}

                <div className="p-3">
                  <button
                    type="button"
                    className="w-full h-10 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-background)]"
                  >
                    Manage agents
                  </button>
                </div>
              </DropdownMenu.Content>
            </DropdownMenu.Portal>
          </DropdownMenu.Root>
          <div className="flex items-center gap-2">
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]" title="Menu">
              <Menu className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]">
              <Bell className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <div className="flex-1 min-h-0">
          <Conversation className="h-full">
            <ConversationContent className="h-full">
              {messages.length === 0 ? (
                <ConversationEmptyState>
                  <div className="mx-auto flex w-full max-w-3xl flex-col items-center justify-center gap-5 px-4">
                    <div className="h-14 w-14 rounded-2xl bg-[var(--color-surface)] border border-[var(--color-border)] shadow-sm grid place-items-center">
                      <span className="text-lg font-bold text-[var(--color-text-primary)]">C</span>
                    </div>
                    <div className="text-center">
                      <div className="text-lg font-semibold text-[var(--color-text-primary)]">John, I'm ready when you are.</div>
                      <div className="mt-1 text-sm text-[var(--color-text-secondary)]">Ask me anything...</div>
                    </div>
                    <ChatComposer mode="center" />
                  </div>
                </ConversationEmptyState>
              ) : (
                <div className="mx-auto w-full max-w-3xl">
                  {messages.map((m) => (
                    <Message from={m.from} key={m.id}>
                      <MessageContent from={m.from}>{m.content}</MessageContent>
                    </Message>
                  ))}
                </div>
              )}
            </ConversationContent>
            <ConversationScrollButton />
          </Conversation>
        </div>

        {messages.length > 0 && (
          <div className="border-t border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <div className="mx-auto w-full max-w-3xl px-4 py-4">
              <ChatComposer mode="footer" />
              <div className="mt-3 flex items-center justify-between text-xs text-[var(--color-text-tertiary)]">
                <span>Mock UI - endpoints not wired.</span>
                <span>Agent: {selectedAgent.title}</span>
              </div>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
