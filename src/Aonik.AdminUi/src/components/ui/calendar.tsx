import * as React from "react";
import { ChevronDownIcon, ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { DayPicker, getDefaultClassNames, type DayButton } from "react-day-picker";
import { cn } from "@/lib/utils";
import { Button } from "./button";
import { buttonVariants } from "./button-variants";

/** shadcn Calendar on react-day-picker. Use inside a Popover for a date picker. */
function Calendar({
  className,
  classNames,
  showOutsideDays = true,
  captionLayout = "label",
  components,
  ...props
}: React.ComponentProps<typeof DayPicker>) {
  const defaults = getDefaultClassNames();

  return (
    <DayPicker
      showOutsideDays={showOutsideDays}
      className={cn(
        "group/calendar bg-background p-3 [--cell-size:--spacing(8)] [[data-slot=card-content]_&]:bg-transparent [[data-slot=popover-content]_&]:bg-transparent",
        className
      )}
      captionLayout={captionLayout}
      classNames={{
        root: cn("w-fit", defaults.root),
        months: cn("relative flex flex-col gap-4 md:flex-row", defaults.months),
        month: cn("flex w-full flex-col gap-4", defaults.month),
        nav: cn("absolute inset-x-0 top-0 flex w-full items-center justify-between gap-1", defaults.nav),
        button_previous: cn(
          buttonVariants({ variant: "ghost" }),
          "size-(--cell-size) select-none p-0 aria-disabled:opacity-50",
          defaults.button_previous
        ),
        button_next: cn(
          buttonVariants({ variant: "ghost" }),
          "size-(--cell-size) select-none p-0 aria-disabled:opacity-50",
          defaults.button_next
        ),
        month_caption: cn("flex h-(--cell-size) w-full items-center justify-center px-(--cell-size)", defaults.month_caption),
        dropdowns: cn("flex h-(--cell-size) w-full items-center justify-center gap-1.5 text-sm font-medium", defaults.dropdowns),
        dropdown_root: cn(
          "relative rounded-md border border-input shadow-xs has-focus:border-ring has-focus:ring-[3px] has-focus:ring-ring/50",
          defaults.dropdown_root
        ),
        dropdown: cn("absolute inset-0 bg-popover opacity-0", defaults.dropdown),
        caption_label: cn(
          "select-none font-medium",
          captionLayout === "label"
            ? "text-sm"
            : "flex h-8 items-center gap-1 rounded-md pl-2 pr-1 text-sm [&>svg]:size-3.5 [&>svg]:text-muted-foreground",
          defaults.caption_label
        ),
        month_grid: "w-full border-collapse",
        weekdays: cn("flex", defaults.weekdays),
        weekday: cn("flex-1 select-none rounded-md text-[0.8rem] font-normal text-muted-foreground", defaults.weekday),
        week: cn("mt-2 flex w-full", defaults.week),
        week_number_header: cn("w-(--cell-size) select-none", defaults.week_number_header),
        week_number: cn("select-none text-[0.8rem] text-muted-foreground", defaults.week_number),
        day: cn(
          "group/day relative aspect-square h-full w-full select-none p-0 text-center [&:first-child[data-selected=true]_button]:rounded-l-md [&:last-child[data-selected=true]_button]:rounded-r-md",
          defaults.day
        ),
        range_start: cn("rounded-l-md bg-accent", defaults.range_start),
        range_middle: cn("rounded-none", defaults.range_middle),
        range_end: cn("rounded-r-md bg-accent", defaults.range_end),
        today: cn("rounded-md bg-accent text-accent-foreground data-[selected=true]:rounded-none", defaults.today),
        outside: cn("text-muted-foreground aria-selected:text-muted-foreground", defaults.outside),
        disabled: cn("text-muted-foreground opacity-50", defaults.disabled),
        hidden: cn("invisible", defaults.hidden),
        ...classNames,
      }}
      components={{
        Root: ({ className, rootRef, ...rootProps }) => (
          <div data-slot="calendar" ref={rootRef} className={cn(className)} {...rootProps} />
        ),
        Chevron: ({ className, orientation, ...chevronProps }) => {
          if (orientation === "left") return <ChevronLeftIcon className={cn("size-4", className)} {...chevronProps} />;
          if (orientation === "right") return <ChevronRightIcon className={cn("size-4", className)} {...chevronProps} />;
          return <ChevronDownIcon className={cn("size-4", className)} {...chevronProps} />;
        },
        DayButton: CalendarDayButton,
        ...components,
      }}
      {...props}
    />
  );
}

function CalendarDayButton({
  className,
  day,
  modifiers,
  ...props
}: React.ComponentProps<typeof DayButton>) {
  const defaults = getDefaultClassNames();
  const ref = React.useRef<HTMLButtonElement>(null);
  React.useEffect(() => {
    if (modifiers.focused) ref.current?.focus();
  }, [modifiers.focused]);

  return (
    <Button
      ref={ref}
      variant="ghost"
      size="icon"
      data-day={day.date.toLocaleDateString()}
      data-selected-single={
        modifiers.selected && !modifiers.range_start && !modifiers.range_end && !modifiers.range_middle
      }
      data-range-start={modifiers.range_start}
      data-range-end={modifiers.range_end}
      data-range-middle={modifiers.range_middle}
      className={cn(
        "flex aspect-square size-auto w-full min-w-(--cell-size) flex-col gap-1 font-normal leading-none data-[range-end=true]:rounded-md data-[range-middle=true]:rounded-none data-[range-start=true]:rounded-md data-[range-end=true]:bg-primary data-[range-middle=true]:bg-accent data-[range-start=true]:bg-primary data-[selected-single=true]:bg-primary data-[range-end=true]:text-primary-foreground data-[range-middle=true]:text-accent-foreground data-[range-start=true]:text-primary-foreground data-[selected-single=true]:text-primary-foreground group-data-[focused=true]/day:relative group-data-[focused=true]/day:z-10 group-data-[focused=true]/day:border-ring group-data-[focused=true]/day:ring-[3px] group-data-[focused=true]/day:ring-ring/50 [&>span]:text-xs [&>span]:opacity-70",
        defaults.day,
        className
      )}
      {...props}
    />
  );
}

export { Calendar, CalendarDayButton };
