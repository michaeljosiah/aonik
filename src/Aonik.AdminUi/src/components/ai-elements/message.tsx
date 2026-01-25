import type { HTMLAttributes, ComponentProps } from 'react';

import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

export type MessageRole = 'user' | 'assistant' | 'system';

export type MessageProps = HTMLAttributes<HTMLDivElement> & {
  from: MessageRole;
};

export function Message({ className, from, ...props }: MessageProps) {
  return (
    <div
      className={cn(
        'group flex w-full max-w-[92%] flex-col gap-2',
        from === 'user' ? 'ml-auto items-end' : 'items-start',
        className
      )}
      {...props}
    />
  );
}

export type MessageContentProps = HTMLAttributes<HTMLDivElement> & {
  from?: MessageRole;
};

export function MessageContent({ className, from, ...props }: MessageContentProps) {
  const isUser = from === 'user';

  return (
    <div
      className={cn(
        'w-fit max-w-full text-sm leading-relaxed',
        isUser
          ? 'rounded-2xl bg-[var(--color-brand-primary)] px-4 py-3 text-white shadow-sm'
          : 'rounded-2xl border border-[var(--color-border-light)] bg-[var(--color-surface)] px-4 py-3 text-[var(--color-text-primary)]',
        className
      )}
      {...props}
    />
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
