import * as React from "react";
import { format, isValid, parseISO } from "date-fns";
import { CalendarIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "./button";
import { Calendar } from "./calendar";
import { Popover, PopoverContent, PopoverTrigger } from "./popover";

/**
 * Popover date picker. Takes and emits `yyyy-MM-dd` strings, the same value
 * shape as a native <input type="date">, so forms can switch over without
 * changing their state or payloads. An empty string means no date.
 */
export interface DatePickerProps {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  /** Dates before this (yyyy-MM-dd) cannot be picked. */
  min?: string;
  /** Dates after this (yyyy-MM-dd) cannot be picked. */
  max?: string;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
}

function toDate(value: string | undefined): Date | undefined {
  if (!value) return undefined;
  const d = parseISO(value);
  return isValid(d) ? d : undefined;
}

function DatePicker({
  id,
  value,
  onChange,
  placeholder = "Pick a date",
  disabled,
  className,
  min,
  max,
  ...aria
}: DatePickerProps) {
  const [open, setOpen] = React.useState(false);
  const selected = toDate(value);
  const minDate = toDate(min);
  const maxDate = toDate(max);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          disabled={disabled}
          data-empty={!selected}
          className={cn(
            "w-full justify-start px-3 text-left font-normal data-[empty=true]:text-muted-foreground",
            className
          )}
          {...aria}
        >
          <CalendarIcon className="text-muted-foreground" />
          {selected ? format(selected, "d MMM yyyy") : <span>{placeholder}</span>}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-0" align="start">
        <Calendar
          mode="single"
          selected={selected}
          defaultMonth={selected}
          captionLayout="dropdown"
          disabled={[
            ...(minDate ? [{ before: minDate }] : []),
            ...(maxDate ? [{ after: maxDate }] : []),
          ]}
          onSelect={(date) => {
            onChange(date ? format(date, "yyyy-MM-dd") : "");
            setOpen(false);
          }}
        />
      </PopoverContent>
    </Popover>
  );
}

export { DatePicker };
