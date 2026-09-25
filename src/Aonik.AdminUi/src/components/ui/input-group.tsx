import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";
import { Button, type ButtonProps } from "./button";

/**
 * shadcn InputGroup: a bordered group that owns the focus ring, holding an
 * InputGroupInput or InputGroupTextarea plus addons (icons, text, buttons)
 * aligned inline-start/end or block-start/end.
 *
 *   <InputGroup>
 *     <InputGroupAddon><SearchIcon /></InputGroupAddon>
 *     <InputGroupInput placeholder="Search" />
 *   </InputGroup>
 */

const groupClassName =
  "group/input-group relative flex w-full min-w-0 items-center rounded-md border border-input shadow-xs outline-none transition-[color,box-shadow] dark:bg-input/30 h-9 has-[>textarea]:h-auto has-[>[data-align=inline-start]]:[&>input]:pl-2 has-[>[data-align=inline-end]]:[&>input]:pr-2 has-[>[data-align=block-start]]:h-auto has-[>[data-align=block-start]]:flex-col has-[>[data-align=block-start]]:[&>input]:pb-3 has-[>[data-align=block-end]]:h-auto has-[>[data-align=block-end]]:flex-col has-[>[data-align=block-end]]:[&>input]:pt-3 has-[[data-slot=input-group-control]:focus-visible]:border-ring has-[[data-slot=input-group-control]:focus-visible]:ring-[3px] has-[[data-slot=input-group-control]:focus-visible]:ring-ring/50 has-[[data-slot][aria-invalid=true]]:border-destructive has-[[data-slot][aria-invalid=true]]:ring-destructive/20 has-[:disabled]:opacity-50";

const InputGroup = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      data-slot="input-group"
      role="group"
      className={cn(groupClassName, className)}
      {...props}
    />
  )
);
InputGroup.displayName = "InputGroup";

const inputGroupAddonVariants = cva(
  "flex h-auto cursor-text select-none items-center justify-center gap-2 py-1.5 text-sm font-medium text-muted-foreground group-data-[disabled=true]/input-group:opacity-50 [&>kbd]:rounded-[calc(var(--radius)-5px)] [&>svg:not([class*='size-'])]:size-4",
  {
    variants: {
      align: {
        "inline-start": "order-first pl-3 has-[>button]:ml-[-0.45rem] has-[>kbd]:ml-[-0.35rem]",
        "inline-end": "order-last pr-3 has-[>button]:mr-[-0.45rem] has-[>kbd]:mr-[-0.35rem]",
        "block-start":
          "order-first w-full justify-start px-3 pt-3 group-has-[>input]/input-group:pt-2.5 [.border-b]:pb-3",
        "block-end":
          "order-last w-full justify-start px-3 pb-3 group-has-[>input]/input-group:pb-2.5 [.border-t]:pt-3",
      },
    },
    defaultVariants: { align: "inline-start" },
  }
);

function InputGroupAddon({
  className,
  align = "inline-start",
  ...props
}: React.HTMLAttributes<HTMLDivElement> & VariantProps<typeof inputGroupAddonVariants>) {
  return (
    <div
      role="group"
      data-slot="input-group-addon"
      data-align={align}
      className={cn(inputGroupAddonVariants({ align }), className)}
      onClick={(e) => {
        if ((e.target as HTMLElement).closest("button")) return;
        e.currentTarget.parentElement?.querySelector<HTMLElement>("input, textarea")?.focus();
      }}
      {...props}
    />
  );
}

const inputGroupButtonSizes = {
  xs: "h-6 gap-1 rounded-[calc(var(--radius)-5px)] px-2 has-[>svg]:px-2 [&>svg:not([class*='size-'])]:size-3.5",
  sm: "h-8 gap-1.5 rounded-md px-2.5 has-[>svg]:px-2.5",
  "icon-xs": "size-6 rounded-[calc(var(--radius)-5px)] p-0 has-[>svg]:p-0",
  "icon-sm": "size-8 p-0 has-[>svg]:p-0",
} as const;

function InputGroupButton({
  className,
  type = "button",
  variant = "ghost",
  size = "xs",
  ...props
}: Omit<ButtonProps, "size"> & { size?: keyof typeof inputGroupButtonSizes }) {
  return (
    <Button
      type={type}
      data-size={size}
      variant={variant}
      className={cn("flex items-center gap-2 text-sm shadow-none", inputGroupButtonSizes[size], className)}
      {...props}
    />
  );
}

function InputGroupText({ className, ...props }: React.HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      className={cn(
        "flex items-center gap-2 text-sm text-muted-foreground [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4",
        className
      )}
      {...props}
    />
  );
}

const InputGroupInput = React.forwardRef<HTMLInputElement, React.InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...props }, ref) => (
    <input
      ref={ref}
      data-slot="input-group-control"
      className={cn(
        "flex h-9 w-full min-w-0 flex-1 rounded-none border-0 bg-transparent px-3 py-1 text-base text-foreground shadow-none outline-none placeholder:text-muted-foreground focus-visible:ring-0 disabled:cursor-not-allowed md:text-sm dark:bg-transparent",
        className
      )}
      {...props}
    />
  )
);
InputGroupInput.displayName = "InputGroupInput";

const InputGroupTextarea = React.forwardRef<
  HTMLTextAreaElement,
  React.TextareaHTMLAttributes<HTMLTextAreaElement>
>(({ className, ...props }, ref) => (
  <textarea
    ref={ref}
    data-slot="input-group-control"
    className={cn(
      "flex field-sizing-content min-h-16 w-full flex-1 resize-none rounded-none border-0 bg-transparent px-3 py-3 text-base text-foreground shadow-none outline-none placeholder:text-muted-foreground focus-visible:ring-0 disabled:cursor-not-allowed md:text-sm dark:bg-transparent",
      className
    )}
    {...props}
  />
));
InputGroupTextarea.displayName = "InputGroupTextarea";

export {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupText,
  InputGroupInput,
  InputGroupTextarea,
};
