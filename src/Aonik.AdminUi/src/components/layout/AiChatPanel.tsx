import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2, X, Maximize2, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

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
    sendMessage,
    stopStreaming,
    resetChat,
    pendingApprovals,
    approveAction,
    rejectAction,
    decideServerApproval,
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
      ? 'var(--warning)'
      : streamError
        ? 'var(--destructive)'
        : 'var(--success)';
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
      className="relative flex h-full shrink-0 flex-col border-l bg-background"
    >
      {/* Resize handle */}
      <div
        onMouseDown={handleMouseDown}
        className="absolute left-0 top-0 bottom-0 w-[6px] -translate-x-1/2 cursor-ew-resize z-10 group"
      >
        <div className="mx-auto h-full w-px bg-transparent transition-colors duration-150 group-hover:bg-ring group-active:bg-ring" />
      </div>

      {/* Header: title, connection status, open-full-chat and close. */}
      <div className="flex h-14 shrink-0 items-center gap-2.5 border-b px-3">
        <div className="grid size-8 shrink-0 place-items-center rounded-md bg-primary/10 text-primary">
          <Sparkles className="size-4" />
        </div>
        <div className="min-w-0 flex-1 leading-tight">
          <div className="text-sm font-medium">Ask Aonik</div>
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            {/* Connection state indicator, not decoration. */}
            <span className="size-1.5 shrink-0 rounded-full" style={{ background: statusDotColor }} aria-hidden />
            <span className="truncate">{statusLabel}</span>
          </div>
        </div>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button variant="ghost" size="icon-sm" onClick={onExpand} aria-label="Open full chat">
              <Maximize2 />
            </Button>
          </TooltipTrigger>
          <TooltipContent side="bottom">Open full chat</TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button variant="ghost" size="icon-sm" onClick={onClose} aria-label="Close Ask Aonik">
              <X />
            </Button>
          </TooltipTrigger>
          <TooltipContent side="bottom">Close</TooltipContent>
        </Tooltip>
      </div>

      {/* Conversation area. */}
      <div className="min-h-0 flex-1 bg-background">
        <Conversation className="h-full">
          <ConversationContent className="h-full">
            {messages.length === 0 ? (
              <ConversationEmptyState>
                <div className="mx-auto flex w-full max-w-[520px] flex-col items-center justify-center gap-4 px-4 text-center">
                  <div className="grid size-10 place-items-center rounded-lg bg-muted text-primary">
                    <Sparkles className="size-5" />
                  </div>
                  <div>
                    <div className="text-lg font-semibold text-foreground">
                      Good {new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'}.
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">
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
                  onDecideApproval={decideServerApproval}
                  onSelectToolCallOptions={selectToolCallOptions}
                  onSelectFollowUpSuggestion={(prompt) => void sendMessage(prompt)}
                />
              </div>
            )}
          </ConversationContent>
          <ConversationScrollButton />
        </Conversation>
      </div>

      {/* Composer */}
      <div className="border-t border-border bg-card p-3 shrink-0">
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
        <div className="mt-2 flex items-center justify-between text-xs text-muted-foreground px-1">
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
              <span className="text-destructive">{streamError}</span>
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
