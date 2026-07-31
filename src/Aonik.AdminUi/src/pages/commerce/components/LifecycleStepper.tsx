// Order lifecycle stepper (Spec 083 §2). Rendering only — every claim it makes comes from
// `orderLifecycle`, which is pure and tested.

import { Check } from 'lucide-react';

import type { LifecycleStep, OrderLifecycle } from '../lib/orderLifecycle';

export function LifecycleStepper({ lifecycle }: { lifecycle: OrderLifecycle }) {
  return (
    <div className="flex flex-col gap-2">
      <ol className="flex flex-wrap items-start gap-x-1 gap-y-2">
        {lifecycle.steps.map((step, index) => (
          <li key={step.key} className="flex items-start gap-1">
            <Step step={step} />
            {index < lifecycle.steps.length - 1 && (
              <span className="mt-[11px] h-px w-5 bg-[var(--color-border)]" aria-hidden />
            )}
          </li>
        ))}
      </ol>

      {lifecycle.halted && (
        <p className="text-[11.5px] text-[var(--color-error)]">
          <span className="font-semibold">{lifecycle.halted.label}</span> — {lifecycle.halted.reason}
        </p>
      )}
    </div>
  );
}

function Step({ step }: { step: LifecycleStep }) {
  const done = step.state === 'done';
  const current = step.state === 'current';
  const untracked = step.state === 'untracked';

  return (
    <span className="flex flex-col items-center gap-1 px-1">
      <span
        aria-hidden
        className={[
          'flex h-[22px] w-[22px] items-center justify-center rounded-full border text-[10px] font-semibold',
          done
            ? 'border-[var(--color-success)] bg-[var(--color-success)] text-white'
            : current
              ? 'border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]'
              : // Untracked and pending look alike deliberately — neither has happened. The
                // difference is in the note, which says WHY, rather than in a colour the
                // operator would have to interpret.
                'border-dashed border-[var(--color-border)] text-[var(--color-text-tertiary)]',
        ].join(' ')}
      >
        {done ? <Check className="h-3 w-3" /> : ''}
      </span>
      <span
        className={`text-[11px] ${
          done || current
            ? 'text-[var(--color-text-primary)]'
            : 'text-[var(--color-text-tertiary)]'
        }`}
      >
        {step.label}
      </span>
      {untracked && step.note && (
        <span className="max-w-[92px] text-center text-[10px] leading-tight text-[var(--color-text-tertiary)]">
          {step.note}
        </span>
      )}
    </span>
  );
}
