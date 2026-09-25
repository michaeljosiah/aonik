import * as React from "react";
import { CheckIcon, ChevronsUpDownIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "./button";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "./command";
import { Popover, PopoverContent, PopoverTrigger } from "./popover";

/**
 * Searchable single-select: a Popover holding a cmdk Command list, with an
 * outline-button trigger. Use for long option lists (countries, currencies,
 * billers, customers, agents); use Select for short fixed lists.
 */
export interface ComboboxOption {
  value: string;
  label: string;
  /** Extra text matched by search but not shown (e.g. an ISO code). */
  keywords?: string[];
  description?: string;
  icon?: React.ReactNode;
}

export interface ComboboxProps {
  id?: string;
  options: ComboboxOption[];
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: React.ReactNode;
  disabled?: boolean;
  loading?: boolean;
  className?: string;
  contentClassName?: string;
  /** Adds an entry at the top that clears the value. */
  clearLabel?: string;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
}

function Combobox({
  id,
  options,
  value,
  onValueChange,
  placeholder = "Select an option",
  searchPlaceholder = "Search…",
  emptyText = "No results.",
  disabled,
  loading,
  className,
  contentClassName,
  clearLabel,
  ...aria
}: ComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const selected = options.find((o) => o.value === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className={cn("w-full justify-between px-3 font-normal", !selected && "text-muted-foreground", className)}
          {...aria}
        >
          <span className="flex min-w-0 items-center gap-2 truncate">
            {selected?.icon}
            <span className="truncate">{loading ? "Loading…" : selected?.label ?? placeholder}</span>
          </span>
          <ChevronsUpDownIcon className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        className={cn("w-(--radix-popover-trigger-width) min-w-[12rem] p-0", contentClassName)}
        align="start"
      >
        <Command>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            <CommandEmpty>{emptyText}</CommandEmpty>
            <CommandGroup>
              {clearLabel && value && (
                <CommandItem
                  value="__clear__"
                  onSelect={() => {
                    onValueChange("");
                    setOpen(false);
                  }}
                  className="text-muted-foreground"
                >
                  {clearLabel}
                </CommandItem>
              )}
              {options.map((option) => (
                <CommandItem
                  key={option.value}
                  value={option.value}
                  keywords={[option.label, ...(option.keywords ?? [])]}
                  onSelect={() => {
                    onValueChange(option.value);
                    setOpen(false);
                  }}
                >
                  {option.icon}
                  <span className="flex min-w-0 flex-col">
                    <span className="truncate">{option.label}</span>
                    {option.description && (
                      <span className="truncate text-xs text-muted-foreground">{option.description}</span>
                    )}
                  </span>
                  <CheckIcon className={cn("ml-auto", option.value === value ? "opacity-100" : "opacity-0")} />
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

export { Combobox };
