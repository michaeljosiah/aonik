import type { KeyboardEventHandler } from 'react';
import { ChevronDown, Mic, SquarePlus, Send, Square, Trash2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupTextarea,
} from '@/components/ui/input-group';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

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
 * Chat input composer (Spec 098): an InputGroup-style bordered group holding
 * the textarea, with a block-end toolbar (attach, model, voice) and the send
 * icon Button. The Stop button replaces Send while a reply is streaming.
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

  const voiceLabel = voiceModeAvailable ? `Voice mode ${voicePlaybackState}` : 'Voice unavailable';

  return (
    <div
      className={cn(
        'mx-auto w-full',
        isCenter ? 'max-w-[800px]' : 'lg:max-w-[800px]',
        className
      )}
    >
      <InputGroup className="rounded-xl bg-card">
        <InputGroupTextarea
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          rows={isCenter ? 3 : 1}
          aria-label="Message"
          className={cn(
            'max-h-[40vh] px-4 leading-6',
            isCenter ? 'min-h-[96px] pt-4' : 'min-h-9 pt-3'
          )}
        />

        <InputGroupAddon align="block-end" className="justify-between">
          <Tooltip>
            <TooltipTrigger asChild>
              <InputGroupButton size="icon-sm" className="rounded-full" aria-label="Attach">
                <SquarePlus />
              </InputGroupButton>
            </TooltipTrigger>
            <TooltipContent>Attach</TooltipContent>
          </Tooltip>

          <div className="flex items-center gap-2">
            <InputGroupButton size="sm" className="text-muted-foreground" title="Model">
              {modelLabel}
              <ChevronDown />
            </InputGroupButton>

            <Tooltip>
              <TooltipTrigger asChild>
                {/* Span keeps the tooltip reachable while the button is disabled. */}
                <span className="inline-flex">
                  <InputGroupButton
                    size="icon-sm"
                    variant={voiceModeAvailable && voiceModeEnabled ? 'default' : 'ghost'}
                    className="rounded-full"
                    aria-label={voiceLabel}
                    disabled={!voiceModeAvailable}
                    aria-pressed={voiceModeEnabled}
                    onClick={() => onToggleVoiceMode?.(!voiceModeEnabled)}
                  >
                    <Mic />
                  </InputGroupButton>
                </span>
              </TooltipTrigger>
              <TooltipContent>{voiceLabel}</TooltipContent>
            </Tooltip>

            {isStreaming ? (
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button type="button" size="icon" aria-label="Stop" onClick={onStop}>
                    <Square />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Stop</TooltipContent>
              </Tooltip>
            ) : hasText ? (
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button type="button" size="icon" aria-label="Send" onClick={onSend}>
                    <Send />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Send</TooltipContent>
              </Tooltip>
            ) : null}
          </div>
        </InputGroupAddon>
      </InputGroup>

      {/* Helper text */}
      {shouldShowHelper && (
        <div className="px-4 pt-2 flex items-center justify-between text-xs text-muted-foreground">
          <span>Shift+Enter for newline</span>
          {shouldShowClear && onClear && (
            <Button type="button" variant="ghost" size="sm" onClick={onClear} className="h-7 text-muted-foreground">
              <Trash2 />
              Clear
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
