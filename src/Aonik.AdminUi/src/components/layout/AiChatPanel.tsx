import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2, X, Maximize2, Sparkles } from 'lucide-react';

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

  // Status line shown in the rail header — a one-glance summary of the agent's
  // current state. Mirrors the bottom-bar status but lives next to the brand
  // glyph in the header where the template puts "reading this page · 4 tools".
  const statusDotColor =
    chatRunState === 'streaming'
      ? 'var(--color-warning)'
      : streamError
        ? 'var(--color-error)'
        : 'var(--color-success)';
  const statusLabel =
    chatRunState === 'streaming'
      ? 'Working on it…'
      : chatRunState === 'awaiting-selection'
        ? 'Awaiting selection'
        : chatRunState === 'awaiting-approval'
          ? 'Awaiting approval'
          : streamError
            ? 'Connection error'
            : 'Ready · Orchestrator';

  return (
    <aside
      aria-label="Ask Aonik"
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

      {/* Header — template AgentRail vocabulary: gradient teal square +
          "Ask Aonik" + status line + hover-halo controls. */}
      <div className="flex shrink-0 items-center gap-2.5 border-b border-[var(--color-border-light)] bg-[var(--color-surface)] px-4 py-3">
        <div
          className="grid h-8 w-8 shrink-0 place-items-center rounded-[9px] text-white"
          style={{ background: 'linear-gradient(135deg, var(--color-brand-primary) 0%, #077278 100%)' }}
        >
          <Sparkles className="h-4 w-4" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">
            Ask Aonik
          </div>
          <div className="flex items-center gap-1.5 text-[11px] text-[var(--color-text-secondary)]">
            <span
              className="h-1.5 w-1.5 rounded-full"
              style={{ background: statusDotColor }}
              aria-hidden
            />
            <span className="truncate">{statusLabel}</span>
          </div>
        </div>
        <button
          type="button"
          onClick={onExpand}
          className="hover-halo"
          aria-label="Open full chat"
          title="Open full chat"
        >
          <Maximize2 className="h-4 w-4" />
        </button>
        <button
          type="button"
          onClick={onClose}
          className="hover-halo"
          aria-label="Close AI chat panel"
          title="Close"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      {/* Conversation area — scoped in .chat-primary so descendant agent
          components (chat bubbles, tool traces) read the brand-primary
          theme variables (--theme-color, --theme-color-100, etc.). */}
      <div className="chat-primary flex-1 min-h-0 bg-[var(--color-surface-inset)]">
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
