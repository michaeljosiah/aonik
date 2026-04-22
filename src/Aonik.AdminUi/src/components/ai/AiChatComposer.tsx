import type { KeyboardEventHandler } from 'react';
import { ChevronDown, Mic, SquarePlus, Send, Square, Trash2 } from 'lucide-react';

import { cn } from '@/lib/utils';

type AiChatComposerMode = 'center' | 'footer';

export type AiChatComposerProps = {
  value: string;
  onChange: (value: string) => void;
  onSend: () => void;
  onStop?: () => void;
  onClear?: () => void;
  mode?: AiChatComposerMode;
  placeholder?: string;
  modelLabel?: string;
  showHelper?: boolean;
  showClear?: boolean;
  isStreaming?: boolean;
  className?: string;
  voiceModeAvailable?: boolean;
  voiceModeEnabled?: boolean;
  onToggleVoiceMode?: (enabled: boolean) => void;
  voicePlaybackState?: 'idle' | 'loading' | 'playing' | 'error';
};

/**
 * Chat input composer styled to match Centrali's ChatInput:
 *  - rounded-[1rem] container with border
 *  - themed bottom-border on focus (uses .input-focused CSS class from index.css)
 *  - SquarePlus attach button (Centrali: IconSquarePlus)
 *  - Send button: rounded-[0.7rem], solid theme bg, white icon
 *  - Stop button replaces send when streaming
 */
export function AiChatComposer({
  value,
  onChange,
  onSend,
  onStop,
  onClear,
  mode = 'footer',
  placeholder = 'Ask me anything...',
  modelLabel = 'ChatGPT 5.2',
  showHelper,
  showClear,
  isStreaming,
  className,
  voiceModeAvailable = false,
  voiceModeEnabled = false,
  onToggleVoiceMode,
  voicePlaybackState = 'idle',
}: AiChatComposerProps) {
  const isCenter = mode === 'center';
  const shouldShowHelper = showHelper ?? mode === 'footer';
  const shouldShowClear = showClear ?? mode === 'footer';
  const hasText = value.trim().length > 0;

  const handleKeyDown: KeyboardEventHandler<HTMLTextAreaElement> = (event) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      if (hasText) onSend();
    }
  };

  return (
    <div
      className={cn(
        'chat-primary mx-auto w-full',
        isCenter ? 'max-w-[800px]' : 'lg:max-w-[800px]',
        className
      )}
    >
      {/* Input container — Centrali: rounded-[1rem], border, focus: themed bottom border */}
      <div
        className={cn(
          'rounded-[1rem] border border-[var(--color-border)] bg-[var(--color-surface)]',
          'transition-all duration-150',
          'focus-within:border-b-2 focus-within:border-b-[var(--color-brand-primary)]',
          'focus-within:shadow-[0px_4px_0px_-2px_var(--color-brand-primary-60)]'
        )}
      >
        {/* Textarea */}
        <div className={cn('px-4 pt-3', isCenter && 'pt-4')}>
          <textarea
            value={value}
            onChange={(event) => onChange(event.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={placeholder}
            rows={isCenter ? 3 : 1}
            className={cn(
              'w-full resize-none bg-transparent text-sm text-[var(--color-text-primary)]',
              'placeholder:text-[var(--color-gray-400)] outline-none leading-6',
              'max-h-[40vh]',
              isCenter ? 'min-h-[96px]' : 'min-h-9'
            )}
          />
        </div>

        {/* Toolbar */}
        <div className="px-3 pb-3 flex items-center justify-between">
          {/* Left: Attach button — Centrali: IconSquarePlus, 40x40, gray-400, hover bg-gray-200, rounded-full */}
          <button
            className="h-10 w-10 rounded-full grid place-items-center text-[var(--color-gray-400)] hover:bg-[var(--color-gray-200)] transition-colors"
            title="Attach"
            type="button"
          >
            <SquarePlus className="h-5 w-5" />
          </button>

          {/* Right: Model selector, Mic, Send/Stop */}
          <div className="flex items-center gap-2">
            <button
              type="button"
              className="inline-flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-[var(--color-text-secondary)] hover:bg-[var(--color-gray-200)] transition-colors"
              title="Model"
            >
              {modelLabel}
              <ChevronDown className="h-4 w-4 text-[var(--color-text-tertiary)]" />
            </button>

            <button
              type="button"
              className={cn(
                'h-9 w-9 rounded-full grid place-items-center transition-colors',
                voiceModeAvailable
                  ? voiceModeEnabled
                    ? 'bg-[var(--color-brand-primary)] text-white hover:bg-[var(--color-brand-primary-dark)]'
                    : 'text-[var(--color-gray-400)] hover:bg-[var(--color-gray-200)]'
                  : 'text-[var(--color-gray-300)] cursor-not-allowed'
              )}
              title={voiceModeAvailable ? `Voice mode ${voicePlaybackState}` : 'Voice unavailable'}
              disabled={!voiceModeAvailable}
              aria-pressed={voiceModeEnabled}
              onClick={() => onToggleVoiceMode?.(!voiceModeEnabled)}
            >
              <Mic className="h-4 w-4" />
            </button>

            {/* Send / Stop — Centrali: rounded-[0.7rem], p-[0.5rem], theme-bg, white icon */}
            {isStreaming ? (
              <button
                className="h-10 w-10 rounded-[0.7rem] grid place-items-center bg-[var(--color-brand-primary)] text-white hover:bg-[var(--color-brand-primary-dark)] transition-colors"
                title="Stop"
                type="button"
                onClick={onStop}
              >
                <Square className="h-4 w-4" />
              </button>
            ) : hasText ? (
              <button
                className="h-10 w-10 rounded-[0.7rem] grid place-items-center bg-[var(--color-brand-primary)] text-white hover:bg-[var(--color-brand-primary-dark)] transition-colors"
                title="Send"
                type="button"
                onClick={onSend}
              >
                <Send className="h-4 w-4" />
              </button>
            ) : null}
          </div>
        </div>
      </div>

      {/* Helper text */}
      {shouldShowHelper && (
        <div className="px-4 pt-2 flex items-center justify-between text-[12px] text-[var(--color-text-tertiary)]">
          <span>Shift+Enter for newline</span>
          {shouldShowClear && onClear && (
            <button
              type="button"
              onClick={onClear}
              className="inline-flex items-center gap-1 px-2 py-1 rounded-md hover:bg-[var(--color-gray-200)] transition-colors"
            >
              <Trash2 className="h-3.5 w-3.5" />
              Clear
            </button>
          )}
        </div>
      )}
    </div>
  );
}
