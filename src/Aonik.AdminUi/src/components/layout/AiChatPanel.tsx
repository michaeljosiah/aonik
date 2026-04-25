import { useCallback, useEffect, useRef, useState } from 'react';
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
import { resolveChatRunState, useAguiChat } from '@/hooks/useAguiChat';

/** Default / min / max widths in vw units */
const DEFAULT_WIDTH_VW = 30;
const MIN_WIDTH_VW = 30;
const MAX_WIDTH_VW = 50;

/** Convert vw to px */
function vwToPx(vw: number) {
  return (vw / 100) * window.innerWidth;
}

/** Convert px to vw */
function pxToVw(px: number) {
  return (px / window.innerWidth) * 100;
}

/** Clamp a vw value within bounds */
function clampVw(vw: number) {
  return Math.min(MAX_WIDTH_VW, Math.max(MIN_WIDTH_VW, vw));
}

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
    stopStreaming,
    resetChat,
    pendingApprovals,
    approveAction,
    rejectAction,
    selectToolCallOptions,
  } = useAguiChat();
  const chatRunState = resolveChatRunState(messages, isStreaming);

  // --- Resize state ---
  const [widthVw, setWidthVw] = useState(DEFAULT_WIDTH_VW);
  const isDragging = useRef(false);
  const startX = useRef(0);
  const startWidthPx = useRef(0);

  const handleMouseDown = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      isDragging.current = true;
      startX.current = e.clientX;
      startWidthPx.current = vwToPx(widthVw);
      document.body.style.userSelect = 'none';
      document.body.style.cursor = 'ew-resize';
    },
    [widthVw],
  );

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDragging.current) return;
      // Dragging left = growing the panel (clientX decreases)
      const delta = startX.current - e.clientX;
      const newPx = startWidthPx.current + delta;
      setWidthVw(clampVw(pxToVw(newPx)));
    };

    const handleMouseUp = () => {
      if (!isDragging.current) return;
      isDragging.current = false;
      document.body.style.userSelect = '';
      document.body.style.cursor = '';
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, []);

  return (
    <aside
      aria-label="AI Chat"
      style={{ width: `${widthVw}vw`, minWidth: `${MIN_WIDTH_VW}vw`, maxWidth: `${MAX_WIDTH_VW}vw` }}
      className="shrink-0 border-l border-[var(--color-border-light)] bg-[var(--color-surface)] flex flex-col h-full relative"
    >
      {/* Resize handle */}
      <div
        onMouseDown={handleMouseDown}
        className="absolute left-0 top-0 bottom-0 w-[6px] -translate-x-1/2 cursor-ew-resize z-10 group"
      >
        <div className="h-full w-full bg-transparent transition-colors duration-150 group-hover:bg-[var(--color-brand-primary)] group-active:bg-[var(--color-brand-primary)]" />
      </div>
      {/* Header */}
      <div className="h-[50px] px-4 bg-[var(--color-brand-primary)] text-white flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2 font-semibold">
          <div className="h-7 w-7 rounded-[2px] bg-white/15 grid place-items-center">
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
                <div className="mx-auto flex w-full max-w-[520px] flex-col items-center justify-center gap-4 px-4 text-center">
                  <div className="h-12 w-12 rounded-[2px] bg-[var(--color-surface)] border border-[var(--color-border)] shadow-sm grid place-items-center">
                    <span className="text-base font-bold text-[var(--color-text-primary)]">A</span>
                  </div>
                  <div>
                    <div className="text-lg font-semibold text-[var(--color-text-primary)]">
                      Good {new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'}.
                    </div>
                    <div className="mt-1 text-sm text-[var(--color-text-secondary)]">
                      Ask me anything about your AONIK platform, agents, workspaces, or operations.
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
          placeholder="Ask me anything..."
        />
        <div className="mt-2 flex items-center justify-between text-xs text-[var(--color-text-tertiary)] px-1">
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
