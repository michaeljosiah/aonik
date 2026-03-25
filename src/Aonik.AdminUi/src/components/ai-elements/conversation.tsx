import type { ComponentProps } from 'react';
import { useCallback } from 'react';
import { ArrowDown } from 'lucide-react';
import { StickToBottom, useStickToBottomContext } from 'use-stick-to-bottom';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export type ConversationProps = ComponentProps<typeof StickToBottom>;

export function Conversation({ className, ...props }: ConversationProps) {
  return (
    <StickToBottom
      className={cn('relative flex-1 overflow-y-hidden', className)}
      initial="smooth"
      resize="smooth"
      role="log"
      {...props}
    />
  );
}

export type ConversationContentProps = ComponentProps<typeof StickToBottom.Content>;

/**
 * Scrollable content area inside a Conversation.
 *
 * Centrali: ChatHistoryContainer — max-width: 900px, margin: auto (centered).
 * Gap: 16px (1rem) between messages.
 */
export function ConversationContent({ className, ...props }: ConversationContentProps) {
  return (
    <StickToBottom.Content
      className={cn('mx-auto flex w-full max-w-[900px] flex-col gap-4 p-4', className)}
      {...props}
    />
  );
}

export type ConversationEmptyStateProps = ComponentProps<'div'> & {
  title?: string;
  description?: string;
  icon?: React.ReactNode;
};

export function ConversationEmptyState({
  className,
  title = 'No messages yet',
  description = 'Start a conversation to see messages here',
  icon,
  children,
  ...props
}: ConversationEmptyStateProps) {
  return (
    <div
      className={cn('flex size-full flex-col items-center justify-center gap-3 p-8 text-center', className)}
      {...props}
    >
      {children ?? (
        <>
          {icon && <div className="text-[var(--color-text-tertiary)]">{icon}</div>}
          <div className="space-y-1">
            <h3 className="text-sm font-medium text-[var(--color-text-primary)]">{title}</h3>
            {description && <p className="text-sm text-[var(--color-text-secondary)]">{description}</p>}
          </div>
        </>
      )}
    </div>
  );
}

export type ConversationScrollButtonProps = ComponentProps<typeof Button>;

export function ConversationScrollButton({ className, ...props }: ConversationScrollButtonProps) {
  const { isAtBottom, scrollToBottom } = useStickToBottomContext();

  const handleScrollToBottom = useCallback(() => {
    scrollToBottom();
  }, [scrollToBottom]);

  if (isAtBottom) return null;

  return (
    <Button
      className={cn(
        'absolute bottom-4 left-1/2 -translate-x-1/2 rounded-full bg-[var(--color-surface)] hover:bg-[var(--color-background)]',
        className
      )}
      onClick={handleScrollToBottom}
      size="icon"
      type="button"
      variant="outline"
      {...props}
    >
      <ArrowDown className="h-4 w-4" />
    </Button>
  );
}
