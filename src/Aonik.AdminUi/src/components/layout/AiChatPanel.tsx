import { Loader2, X, Maximize2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { AiChatComposer } from '@/components/ai/AiChatComposer';
import { ChatMessageList } from '@/components/ai/ChatMessageList';
import {
  Conversation,
  ConversationContent,
  ConversationEmptyState,
  ConversationScrollButton,
} from '@/components/ai-elements';
import { useAguiChat } from '@/hooks/useAguiChat';

interface AiChatPanelProps {
  onClose: () => void;
  onExpand: () => void;
}

export function AiChatPanel({ onClose, onExpand }: AiChatPanelProps) {
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
  } = useAguiChat();

  return (
    <aside
      aria-label="AI Chat"
      className="w-[400px] shrink-0 border-l border-[var(--color-border-light)] bg-[var(--color-surface)] flex flex-col h-full"
    >
      {/* Header */}
      <div className="h-14 px-4 bg-[var(--color-brand-primary)] text-white flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2 font-semibold">
          <div className="h-7 w-7 rounded-lg bg-white/15 grid place-items-center">
            <span className="text-xs font-bold">A</span>
          </div>
          AONIK AI
        </div>
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="icon-sm"
            className="text-white hover:bg-white/15"
            onClick={onExpand}
            aria-label="Open full chat"
            title="Open full chat"
          >
            <Maximize2 className="w-4 h-4" />
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            className="text-white hover:bg-white/15"
            onClick={onClose}
            aria-label="Close AI chat panel"
          >
            <X className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {/* Conversation area */}
      <div className="flex-1 min-h-0 bg-[var(--color-surface-inset)]">
        <Conversation className="h-full">
          <ConversationContent className="h-full">
            {messages.length === 0 ? (
              <ConversationEmptyState>
                <div className="mx-auto flex w-full flex-col items-center justify-center gap-4 px-4">
                  <div className="h-12 w-12 rounded-2xl bg-[var(--color-surface)] border border-[var(--color-border)] shadow-sm grid place-items-center">
                    <span className="text-base font-bold text-[var(--color-text-primary)]">A</span>
                  </div>
                  <div className="text-center">
                    <div className="text-base font-semibold text-[var(--color-text-primary)]">
                      Hi, I'm ready when you are.
                    </div>
                    <div className="mt-1 text-sm text-[var(--color-text-secondary)]">
                      Ask me anything about your AONIK platform...
                    </div>
                  </div>
                </div>
              </ConversationEmptyState>
            ) : (
              <div className="py-4 px-2">
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

      {/* Composer */}
      <div className="border-t border-[var(--color-border-light)] bg-[var(--color-surface)] p-3 shrink-0">
        <AiChatComposer
          mode="footer"
          value={draft}
          onChange={setDraft}
          onSend={handleSend}
          onClear={resetChat}
          placeholder="Ask me anything..."
        />
        <div className="mt-2 flex items-center justify-between text-xs text-[var(--color-text-tertiary)] px-1">
          <span>
            {isStreaming ? (
              <span className="inline-flex items-center gap-1">
                <Loader2 className="h-3 w-3 animate-spin" />
                Streaming...
              </span>
            ) : streamError ? (
              <span className="text-red-500">{streamError}</span>
            ) : (
              'AG-UI connected'
            )}
          </span>
          <span>AONIK Orchestrator</span>
        </div>
      </div>
    </aside>
  );
}
