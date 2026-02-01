import type { KeyboardEventHandler } from 'react';
import { ChevronDown, Mic, Plus, Send, Trash2 } from 'lucide-react';

import { cn } from '@/lib/utils';

type AiChatComposerMode = 'center' | 'footer';

export type AiChatComposerProps = {
  value: string;
  onChange: (value: string) => void;
  onSend: () => void;
  onClear?: () => void;
  mode?: AiChatComposerMode;
  placeholder?: string;
  modelLabel?: string;
  showHelper?: boolean;
  showClear?: boolean;
  className?: string;
};

export function AiChatComposer({
  value,
  onChange,
  onSend,
  onClear,
  mode = 'footer',
  placeholder = 'Ask me anything...',
  modelLabel = 'ChatGPT 5.2',
  showHelper,
  showClear,
  className,
}: AiChatComposerProps) {
  const isCenter = mode === 'center';
  const shouldShowHelper = showHelper ?? mode === 'footer';
  const shouldShowClear = showClear ?? mode === 'footer';

  const handleKeyDown: KeyboardEventHandler<HTMLTextAreaElement> = (event) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      onSend();
    }
  };

  return (
    <div
      className={cn(
        'rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm',
        isCenter ? 'w-full max-w-[640px]' : 'overflow-hidden',
        className
      )}
    >
      <div className={cn('px-4 pt-4', !isCenter && 'pt-3')}>
        <textarea
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          rows={isCenter ? 3 : 1}
          className={cn(
            'w-full resize-none bg-transparent text-sm text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] outline-none leading-6',
            isCenter ? 'min-h-[96px]' : 'min-h-9'
          )}
        />
      </div>

      <div className={cn('px-3 pb-3 flex items-center justify-between', isCenter && 'pb-3')}>
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
            {modelLabel}
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
              value.trim()
                ? 'bg-[var(--color-brand-primary)] text-white hover:bg-[var(--color-brand-primary-dark)]'
                : 'bg-[var(--color-background)] text-[var(--color-text-tertiary)]'
            )}
            title="Send"
            type="button"
            onClick={onSend}
            disabled={!value.trim()}
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </div>

      {shouldShowHelper && (
        <div className="px-4 pb-3 flex items-center justify-between text-[12px] text-[var(--color-text-tertiary)]">
          <span>Shift+Enter for newline</span>
          {shouldShowClear && onClear && (
            <button
              type="button"
              onClick={onClear}
              className="inline-flex items-center gap-1 px-2 py-1 rounded-md hover:bg-[var(--color-background)]"
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
