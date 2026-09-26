// ProposalCard: the signature "agents propose, systems apply" primitive.
// A Card with the coral agent rule on the left, the agent's name and
// confidence, an optional diff and reasoning, and Apply (agent) / Review
// (outline) / Dismiss (ghost). Used in the agent rail and inline on pages to
// surface proposals before a human applies them.

import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';

export type ProposalDiffLine = {
  type: 'add' | 'rm' | 'ctx';
  text: string;
};

export interface ProposalCardProps {
  agent: string;
  /** 0..1 confidence. */
  confidence: number;
  summary?: string;
  diff?: ProposalDiffLine[];
  reason?: string;
  compact?: boolean;
  /** When provided, shows the Apply action. */
  onApply?: () => void;
  /** When provided, shows the Review action. */
  onReview?: () => void;
  /** When provided, shows the Dismiss action. */
  onDismiss?: () => void;
  className?: string;
}

function agentInitials(agent: string): string {
  return agent
    .split(' ')
    .map((w) => w[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

const diffLineClass: Record<ProposalDiffLine['type'], string> = {
  add: 'text-success-foreground',
  rm: 'text-destructive',
  ctx: 'text-muted-foreground',
};

const diffLinePrefix: Record<ProposalDiffLine['type'], string> = {
  add: '+ ',
  rm: '- ',
  ctx: '  ',
};

export function ProposalCard({
  agent,
  confidence,
  summary,
  diff,
  reason,
  compact,
  onApply,
  onReview,
  onDismiss,
  className,
}: ProposalCardProps) {
  const showActions = !!(onApply || onReview || onDismiss);

  return (
    <Card
      className={cn(
        'flex flex-col gap-3 border-l-[3px] border-l-agent',
        compact ? 'p-3.5' : 'p-4',
        className,
      )}
    >
      <div className="flex items-center gap-2">
        <span className="flex size-6 shrink-0 items-center justify-center rounded-md bg-primary/10 text-[10px] font-semibold text-primary">
          {agentInitials(agent)}
        </span>
        <span className="text-sm font-medium">{agent} agent</span>
        <Badge variant="outline" className="ml-auto font-mono tabular-nums" title="Agent confidence">
          {Math.round(confidence * 100)}% confident
        </Badge>
      </div>

      {summary && <p className="text-sm leading-relaxed">{summary}</p>}

      {diff && diff.length > 0 && (
        <div className="rounded-md bg-muted px-3 py-2 font-mono text-xs leading-relaxed">
          {diff.map((line, i) => (
            <div key={i} className={cn('whitespace-pre-wrap', diffLineClass[line.type])}>
              {diffLinePrefix[line.type]}
              {line.text}
            </div>
          ))}
        </div>
      )}

      {reason && <p className="text-xs leading-relaxed text-muted-foreground">{reason}</p>}

      {showActions && (
        <div className="flex flex-wrap gap-2">
          {onApply && (
            <Button variant="agent" size="sm" onClick={onApply}>
              Apply
            </Button>
          )}
          {onReview && (
            <Button variant="outline" size="sm" onClick={onReview}>
              Review
            </Button>
          )}
          {onDismiss && (
            <Button variant="ghost" size="sm" onClick={onDismiss}>
              Dismiss
            </Button>
          )}
        </div>
      )}
    </Card>
  );
}
