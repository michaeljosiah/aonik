// ProposalCard — the signature "agents propose, systems apply" primitive.
// 1:1 port of templates/aonik-admin-starterkit/kit/shell-aonik.jsx ProposalCard:
//   • coral 3px left border on a surface card
//   • agent avatar (brand-primary tint) + "{agent} Agent" + confidence (mono)
//   • optional summary paragraph
//   • optional diff block (mono, surface-inset bg, color-coded add/rm/ctx)
//   • optional reasoning paragraph
//   • Apply (coral) / Review (outline) / Dismiss (ghost) actions
//
// Used inside the agent rail and inline on data tables to surface agent
// proposals before a human applies them.

import { cn } from '@/lib/utils';

export type ProposalDiffLine = {
  type: 'add' | 'rm' | 'ctx';
  text: string;
};

export interface ProposalCardProps {
  agent: string;
  /** 0..1 confidence — rendered as `conf · 0.94`. */
  confidence: number;
  summary?: string;
  diff?: ProposalDiffLine[];
  reason?: string;
  compact?: boolean;
  /** When provided, shows the Apply CTA. */
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

const diffLineColor: Record<ProposalDiffLine['type'], string> = {
  add: 'var(--color-success)',
  rm: 'var(--color-error)',
  ctx: 'var(--color-text-secondary)',
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
    <div
      className={cn(
        'flex flex-col gap-2.5 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]',
        compact ? 'px-3.5 py-3' : 'px-4 py-3.5',
        className,
      )}
      style={{ borderLeft: '3px solid var(--color-brand-secondary)' }}
    >
      <div className="flex items-center gap-2">
        <span
          className="flex h-[22px] w-[22px] shrink-0 items-center justify-center rounded-md text-[10px] font-bold"
          style={{
            background: 'var(--color-brand-primary-10)',
            color: 'var(--color-brand-primary)',
            fontFamily: 'var(--font-brand)',
          }}
        >
          {agentInitials(agent)}
        </span>
        <span className="text-[12px] font-semibold text-[var(--color-text-primary)]">
          {agent} Agent
        </span>
        <span className="ml-auto font-mono text-[10px] text-[var(--color-text-secondary)]">
          conf · {confidence.toFixed(2)}
        </span>
      </div>

      {summary && (
        <div className="text-[13px] leading-[1.5] text-[var(--color-text-primary)]">{summary}</div>
      )}

      {diff && diff.length > 0 && (
        <div className="rounded-md bg-[var(--color-surface-inset)] px-2.5 py-2 font-mono text-[11px] leading-[1.6]">
          {diff.map((line, i) => (
            <div key={i} style={{ color: diffLineColor[line.type] }}>
              {diffLinePrefix[line.type]}
              {line.text}
            </div>
          ))}
        </div>
      )}

      {reason && (
        <div className="text-[11px] italic leading-[1.5] text-[var(--color-text-secondary)]">
          {reason}
        </div>
      )}

      {showActions && (
        <div className="mt-0.5 flex gap-1.5">
          {onApply && (
            <button
              type="button"
              onClick={onApply}
              className="inline-flex h-[30px] items-center justify-center rounded-md bg-[var(--color-brand-secondary)] px-3 text-[12px] font-medium text-white transition-colors hover:bg-[var(--color-brand-secondary-dark)]"
            >
              Apply
            </button>
          )}
          {onReview && (
            <button
              type="button"
              onClick={onReview}
              className="inline-flex h-[30px] items-center justify-center rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-[12px] font-medium text-[var(--color-text-primary)] transition-colors hover:bg-[var(--color-surface-inset)]"
            >
              Review
            </button>
          )}
          {onDismiss && (
            <button
              type="button"
              onClick={onDismiss}
              className="inline-flex h-[30px] items-center justify-center rounded-md bg-transparent px-3 text-[12px] font-medium text-[var(--color-text-primary)] transition-colors hover:bg-[var(--color-surface-inset)]"
            >
              Dismiss
            </button>
          )}
        </div>
      )}
    </div>
  );
}
