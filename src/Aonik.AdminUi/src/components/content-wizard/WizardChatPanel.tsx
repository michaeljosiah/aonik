import { Loader2 } from 'lucide-react';
import { AiChatComposer } from '@/components/ai/AiChatComposer';
import { ChatMessageList } from '@/components/ai/ChatMessageList';
import {
  Conversation,
  ConversationContent,
  ConversationEmptyState,
  ConversationScrollButton,
} from '@/components/ai-elements';
import type { UseAguiChatReturn } from '@/hooks/useAguiChat';

interface WizardChatPanelProps {
  chat: UseAguiChatReturn;
}

export function WizardChatPanel({ chat }: WizardChatPanelProps) {
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
  } = chat;

  return (
    <div className="flex flex-col h-full border-l border-[var(--color-border-light)] bg-[var(--color-surface)]">
      {/* Header */}
      <div className="h-[50px] px-4 bg-[var(--color-brand-primary)] text-white flex items-center shrink-0">
        <div className="flex items-center gap-2 font-semibold">
          <div className="h-7 w-7 rounded-[2px] bg-white/15 grid place-items-center">
            <span className="text-xs font-bold">A</span>
          </div>
          Content AI Assistant
        </div>
      </div>

      {/* Conversation area */}
      <div className="flex-1 min-h-0 bg-[var(--color-surface-inset)]">
        <Conversation className="h-full">
          <ConversationContent className="h-full">
            {messages.length === 0 ? (
              <ConversationEmptyState>
                <div className="mx-auto flex w-full max-w-[400px] flex-col items-center justify-center gap-3 px-4 text-center">
                  <div className="h-10 w-10 rounded-[2px] bg-[var(--color-surface)] border border-[var(--color-border)] shadow-sm grid place-items-center">
                    <span className="text-sm font-bold text-[var(--color-text-primary)]">A</span>
                  </div>
                  <div>
                    <div className="text-base font-semibold text-[var(--color-text-primary)]">
                      Content AI Assistant
                    </div>
                    <div className="mt-1 text-xs text-[var(--color-text-secondary)]">
                      Configure your content settings and click Generate. I'll create suggestions that appear in the wizard. You can refine them here.
                    </div>
                  </div>
                </div>
              </ConversationEmptyState>
            ) : (
              <div className="py-4 px-3">
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

      {/* Composer */}
      <div className="border-t border-[var(--color-border-light)] bg-[var(--color-surface)] p-3 shrink-0">
        <AiChatComposer
          mode="footer"
          value={draft}
          onChange={setDraft}
          onSend={handleSend}
          onStop={stopStreaming}
          onClear={resetChat}
          isStreaming={isStreaming}
          placeholder="Refine suggestions, ask for changes..."
        />
        <div className="mt-1.5 flex items-center justify-between text-xs text-[var(--color-text-tertiary)] px-1">
          <span>
            {isStreaming ? (
              <span className="inline-flex items-center gap-1">
                <Loader2 className="h-3 w-3 animate-spin" />
                Generating...
              </span>
            ) : streamError ? (
              <span className="text-[var(--color-danger)]">{streamError}</span>
            ) : (
              'Ready'
            )}
          </span>
        </div>
      </div>
    </div>
  );
}
