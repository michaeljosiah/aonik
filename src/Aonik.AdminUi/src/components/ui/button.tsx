import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-[2px] text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-[var(--color-brand-primary)] disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "bg-[var(--color-brand-primary)] text-white shadow hover:bg-[var(--color-brand-primary-dark)]",
        secondary:
          "bg-[var(--color-brand-secondary)] text-white shadow-sm hover:bg-[var(--color-brand-secondary-dark)]",
        outline:
          "border border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm hover:bg-[var(--color-background)] hover:text-[var(--color-text-primary)]",
        ghost:
          "hover:bg-[var(--color-background)] hover:text-[var(--color-text-primary)]",
        link: "text-[var(--color-brand-primary)] underline-offset-4 hover:underline",
        success:
          "bg-[var(--color-success)] text-white shadow-sm hover:bg-[var(--color-success)]/90",
        warning:
          "bg-[var(--color-pending)] text-white shadow-sm hover:bg-[var(--color-pending)]/90",
        destructive:
          "bg-red-600 text-white shadow-sm hover:bg-red-700",
      },
      size: {
        default: "h-9 px-4 py-2",
        sm: "h-8 rounded-[2px] px-3 text-xs",
        lg: "h-10 rounded-[2px] px-8",
        icon: "h-9 w-9",
        "icon-sm": "h-8 w-8",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button";
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    );
  }
);
Button.displayName = "Button";

export { Button, buttonVariants };
