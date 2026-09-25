import * as React from "react";
import { ChevronDownIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { fieldClassName } from "./field-styles";

/**
 * A styled native <select>. Prefer Select (Radix) for most pickers; use this
 * where native behaviour matters (very long lists, mobile pickers, forms that
 * must work before hydration).
 */
const NativeSelect = React.forwardRef<
  HTMLSelectElement,
  React.SelectHTMLAttributes<HTMLSelectElement> & { size?: "sm" | "default" }
>(({ className, size = "default", ...props }, ref) => (
  <div data-slot="native-select-wrapper" className="relative w-full has-[select:disabled]:opacity-50">
    <select
      ref={ref}
      data-slot="native-select"
      data-size={size}
      className={cn(
        fieldClassName,
        "h-9 appearance-none py-1 pr-9 pl-3 data-[size=sm]:h-8",
        className
      )}
      {...props}
    />
    <ChevronDownIcon
      aria-hidden="true"
      className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-muted-foreground opacity-50"
    />
  </div>
));
NativeSelect.displayName = "NativeSelect";

export { NativeSelect };
