import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:ring-offset-2",
  {
    variants: {
      variant: {
        default:
          "border-transparent bg-[var(--color-brand-primary)] text-white",
        secondary:
          "border-transparent bg-[var(--color-background)] text-[var(--color-text-secondary)]",
        outline: "border-[var(--color-border)] text-[var(--color-text-primary)]",
        success:
          "border-transparent bg-[var(--color-success-light)] text-[var(--color-success)]",
        warning:
          "border-transparent bg-[var(--color-warning-light)] text-[var(--color-warning)]",
        pending:
          "border-transparent bg-[var(--color-pending-light)] text-[var(--color-pending)]",
        error:
          "border-transparent bg-[var(--color-error-light)] text-[var(--color-error)]",
        team:
          "border-transparent bg-[var(--color-brand-primary)] text-white",
        enterprise:
          "border-transparent bg-[var(--color-brand-secondary)] text-white",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props} />
  );
}

export { Badge, badgeVariants };
