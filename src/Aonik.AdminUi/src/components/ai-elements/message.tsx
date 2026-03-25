import type { HTMLAttributes, ComponentProps } from 'react';

import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

export type MessageRole = 'user' | 'assistant' | 'system';

export type MessageProps = HTMLAttributes<HTMLDivElement> & {
  from: MessageRole;
};

/**
 * Top-level message row — controls alignment (left for bot, right for user).
 *
 * Centrali: ChatItemContainer
 *  - bot:  justify-content: flex-start, max-width: calc(100% - 50px)
 *  - user: justify-content: flex-end, margin-left: 40px
 *  - margin-bottom: 1rem (gap handled by parent flex-col gap-4)
 */
export function Message({ className, from, ...props }: MessageProps) {
  return (
    <div
      className={cn(
        'group flex w-full flex-col gap-2',
        from === 'user'
          ? 'ml-auto items-end pl-10'       /* user: right-aligned, 40px left margin */
          : 'items-start max-w-[calc(100%-50px)]', /* bot: left-aligned, room for avatar gutter */
        className
      )}
      {...props}
    />
  );
}

export type MessageContentProps = HTMLAttributes<HTMLDivElement> & {
  from?: MessageRole;
};

/**
 * Message bubble — the styled content container.
 *
 * Centrali: ChatItemSubContainer
 *  - bot:  bg #f5f5f5, border-radius 8px, padding 10px
 *  - user: bg getRGBAColor(theme, '0.11'), border-radius 8px, padding 10px
 */
export function MessageContent({ className, from, ...props }: MessageContentProps) {
  const isUser = from === 'user';

  return (
    <div
      className={cn(
        'w-fit max-w-full text-sm leading-relaxed rounded-lg px-3 py-2.5',
        isUser
          ? 'bg-[var(--color-chat-user-bubble)] text-[var(--color-text-primary)]'
          : 'bg-[var(--color-chat-bot-bubble)] text-[var(--color-text-primary)]',
        className
      )}
      {...props}
    />
  );
}

/**
 * Bot avatar — small flex-shrink-0 avatar placed to the left of assistant messages.
 * Centrali: BotAvatar — flex: 0 0 auto, margin: 8px 8px 8px 0px.
 */
export type MessageAvatarProps = ComponentProps<'div'> & {
  initials?: string;
};

export function MessageAvatar({ className, initials = 'A', ...props }: MessageAvatarProps) {
  return (
    <div
      className={cn(
        'flex-shrink-0 h-8 w-8 rounded-full bg-[var(--color-brand-primary)] grid place-items-center mr-2 mt-2',
        className
      )}
      {...props}
    >
      <span className="text-xs font-bold text-white">{initials}</span>
    </div>
  );
}

export type MessageActionsProps = ComponentProps<'div'>;

export function MessageActions({ className, ...props }: MessageActionsProps) {
  return <div className={cn('flex items-center gap-1', className)} {...props} />;
}

export type MessageActionProps = ComponentProps<typeof Button> & {
  tooltip?: string;
  label?: string;
};

export function MessageAction({ tooltip, label, variant = 'ghost', size = 'icon-sm', children, ...props }: MessageActionProps) {
  const button = (
    <Button size={size} type="button" variant={variant} {...props}>
      {children}
      <span className="sr-only">{label || tooltip}</span>
    </Button>
  );

  if (!tooltip) return button;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>{button}</TooltipTrigger>
        <TooltipContent>
          <p>{tooltip}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
