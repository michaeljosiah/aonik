import * as React from 'react';

import { cn } from '@/lib/utils';

export interface InputGroupProps
  extends React.InputHTMLAttributes<HTMLInputElement> {
  icon: React.ReactNode;
  containerClassName?: string;
}

const InputGroup = React.forwardRef<HTMLInputElement, InputGroupProps>(
  ({ icon, containerClassName, className, disabled, ...props }, ref) => {
    return (
      <div
        className={cn(
          'flex h-10 w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] text-sm text-[var(--color-form-field-text)] transition-colors focus-within:border-[var(--color-form-field-border-focus)]',
          disabled && 'cursor-not-allowed opacity-50',
          containerClassName
        )}
      >
        <span className="inline-flex items-center justify-center px-3 text-[var(--color-text-tertiary)] border-r border-[var(--color-form-field-border)]">
          {icon}
        </span>
        <input
          ref={ref}
          disabled={disabled}
          className={cn(
            'h-full w-full bg-transparent px-3 py-2 leading-5 text-[var(--color-form-field-text)] placeholder:text-[var(--color-form-field-placeholder)] focus-visible:outline-none focus-visible:ring-0',
            className
          )}
          {...props}
        />
      </div>
    );
  }
);

InputGroup.displayName = 'InputGroup';

export { InputGroup };
