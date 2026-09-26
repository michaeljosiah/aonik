import { cn } from "@/lib/utils";

/**
 * shadcn-style chart pieces for Recharts. Series colours come from
 * --chart-1..5; axis ticks are muted; tooltip values are tabular figures.
 *
 *   <CartesianGrid {...chartGridProps} />
 *   <XAxis {...chartAxisProps} dataKey="day" />
 *   <Tooltip cursor={false} content={(p) => <ChartTooltipContent {...p} />} />
 */

interface TooltipEntry {
  name?: string | number;
  value?: unknown;
  color?: string;
  dataKey?: unknown;
}

export interface ChartTooltipContentProps {
  active?: boolean;
  payload?: ReadonlyArray<TooltipEntry>;
  label?: unknown;
  /** Formats each value; defaults to String(value). */
  formatValue?: (value: number) => string;
  /** Maps a series key to its display name. */
  nameFor?: (key: string) => string;
  hideLabel?: boolean;
  className?: string;
}

function ChartTooltipContent({
  active,
  payload,
  label,
  formatValue,
  nameFor,
  hideLabel,
  className,
}: ChartTooltipContentProps) {
  if (!active || !payload?.length) return null;

  return (
    <div
      className={cn(
        "grid min-w-[8rem] items-start gap-1.5 rounded-lg border bg-background px-2.5 py-1.5 text-xs text-foreground shadow-xl",
        className
      )}
    >
      {!hideLabel && label != null && <div className="font-medium">{String(label)}</div>}
      <div className="grid gap-1.5">
        {payload.map((entry, index) => {
          const key = String(entry.dataKey ?? entry.name ?? index);
          const numeric = typeof entry.value === "number" ? entry.value : Number(entry.value);
          const display =
            formatValue && Number.isFinite(numeric) ? formatValue(numeric) : String(entry.value ?? "");
          return (
            <div key={key} className="flex w-full items-center gap-2">
              <span
                aria-hidden="true"
                className="size-2.5 shrink-0 rounded-[2px]"
                style={{ backgroundColor: entry.color }}
              />
              <span className="flex-1 text-muted-foreground">{nameFor ? nameFor(key) : String(entry.name ?? key)}</span>
              <span className="font-mono font-medium tabular-nums text-foreground">{display}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ChartLegendContent({
  payload,
  nameFor,
  className,
}: {
  payload?: ReadonlyArray<{ value?: unknown; color?: string; dataKey?: unknown }>;
  nameFor?: (key: string) => string;
  className?: string;
}) {
  if (!payload?.length) return null;
  return (
    <div className={cn("flex items-center justify-center gap-4 pt-3 text-xs", className)}>
      {payload.map((item) => {
        const key = String(item.dataKey ?? item.value);
        return (
          <div key={key} className="flex items-center gap-1.5 text-muted-foreground">
            <span aria-hidden="true" className="size-2 shrink-0 rounded-[2px]" style={{ backgroundColor: item.color }} />
            {nameFor ? nameFor(key) : String(item.value)}
          </div>
        );
      })}
    </div>
  );
}

export { ChartTooltipContent, ChartLegendContent };
