/** Recharts defaults for shadcn-style charts; see ui/chart.tsx. */
export const chartAxisProps = {
  tickLine: false,
  axisLine: false,
  tickMargin: 8,
  tick: { fontSize: 12, fill: "var(--muted-foreground)" },
} as const;

export const chartGridProps = {
  vertical: false,
  stroke: "var(--border)",
} as const;

export const chartSeriesColors = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
] as const;
