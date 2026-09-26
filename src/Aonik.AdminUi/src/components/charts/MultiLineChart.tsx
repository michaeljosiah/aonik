import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  Legend,
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ChartLegendContent, ChartTooltipContent } from '@/components/ui/chart';
import { chartAxisProps, chartGridProps } from '@/components/ui/chart-config';

interface Series {
  key: string;
  label: string;
  color: string;
  data: { timestamp: string; value: number }[];
}

interface MultiLineChartProps {
  series: Series[];
  height?: number;
  label?: string;
  formatValue?: (v: number) => string;
}

function formatTimestamp(ts: string): string {
  const d = new Date(ts);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffDays = diffMs / (1000 * 60 * 60 * 24);
  if (diffDays > 2) {
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
  return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

export function MultiLineChart({
  series,
  height = 200,
  label,
  formatValue,
}: MultiLineChartProps) {
  // Merge all series into a single data array keyed by timestamp
  const timestampMap = new Map<string, Record<string, number | string>>();

  for (const s of series) {
    for (const point of s.data) {
      const existing = timestampMap.get(point.timestamp) ?? {
        timestamp: point.timestamp,
        displayTime: formatTimestamp(point.timestamp),
      };
      existing[s.key] = point.value;
      timestampMap.set(point.timestamp, existing);
    }
  }

  const chartData = Array.from(timestampMap.values()).sort(
    (a, b) => new Date(a.timestamp as string).getTime() - new Date(b.timestamp as string).getTime(),
  );

  const content = (
    <ResponsiveContainer width="100%" height={height}>
      <LineChart data={chartData} margin={{ top: 5, right: 10, left: 0, bottom: 0 }}>
        <CartesianGrid {...chartGridProps} />
        <XAxis
          {...chartAxisProps}
          dataKey="displayTime"
        />
        <YAxis
          {...chartAxisProps}
          tickFormatter={formatValue}
          width={45}
        />
        <Tooltip
          cursor={false}
          content={(props) => (
            <ChartTooltipContent
              {...props}
              formatValue={formatValue}
              nameFor={(key) => series.find((x) => x.key === key)?.label ?? key}
            />
          )}
        />
        <Legend
          content={(props) => (
            <ChartLegendContent
              payload={props.payload}
              nameFor={(key) => series.find((x) => x.key === key)?.label ?? key}
            />
          )}
        />
        {series.map((s) => (
          <Line
            key={s.key}
            type="monotone"
            dataKey={s.key}
            stroke={s.color}
            strokeWidth={2}
            dot={false}
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  );

  if (label) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">{label}</CardTitle>
        </CardHeader>
        <CardContent>{content}</CardContent>
      </Card>
    );
  }

  return content;
}
