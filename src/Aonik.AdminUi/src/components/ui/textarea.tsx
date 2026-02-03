import * as React from "react";
import { cn } from "@/lib/utils";

export interface TextareaProps
  extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {}

const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ className, ...props }, ref) => {
    return (
      <textarea
        className={cn(
          "flex min-h-[60px] w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 py-2 text-sm text-[var(--color-form-field-text)] shadow-none placeholder:text-[var(--color-form-field-placeholder)] focus-visible:outline-none focus-visible:ring-0 focus-visible:border-[var(--color-form-field-border-focus)] disabled:cursor-not-allowed disabled:opacity-50",
          className
        )}
        ref={ref}
        {...props}
      />
    );
  }
);
Textarea.displayName = "Textarea";

export { Textarea };
