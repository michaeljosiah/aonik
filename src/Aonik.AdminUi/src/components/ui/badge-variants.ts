import { cva } from "class-variance-authority";

/**
 * shadcn Badge (new-york v4) plus Aonik status variants that use the
 * `-subtle` tint with `-foreground` text (Spec 098 §7.3).
 */
export const badgeVariants = cva(
  "inline-flex w-fit shrink-0 items-center justify-center gap-1 overflow-hidden whitespace-nowrap rounded-md border px-2 py-0.5 text-xs font-medium transition-[color,box-shadow] [&>svg]:pointer-events-none [&>svg]:size-3 focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive aria-invalid:ring-destructive/20",
  {
    variants: {
      variant: {
        default: "border-transparent bg-primary text-primary-foreground [a&]:hover:bg-primary/90",
        secondary: "border-transparent bg-secondary text-secondary-foreground [a&]:hover:bg-secondary/90",
        destructive:
          "border-transparent bg-destructive text-destructive-foreground [a&]:hover:bg-destructive/90",
        outline: "text-foreground [a&]:hover:bg-accent [a&]:hover:text-accent-foreground",
        success: "border-transparent bg-success-subtle text-success-foreground",
        warning: "border-transparent bg-warning-subtle text-warning-foreground",
        info: "border-transparent bg-info-subtle text-info-foreground",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
);
