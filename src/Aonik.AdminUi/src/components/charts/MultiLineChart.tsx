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
        <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border-light)" />
        <XAxis
          dataKey="displayTime"
          tick={{ fontSize: 11, fill: 'var(--color-text-tertiary)' }}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          tick={{ fontSize: 11, fill: 'var(--color-text-tertiary)' }}
          tickLine={false}
          axisLine={false}
          tickFormatter={formatValue}
          width={45}
        />
        <Tooltip
          contentStyle={{
            backgroundColor: 'var(--color-surface)',
            border: '1px solid var(--color-border-light)',
            borderRadius: 4,
            fontSize: 12,
          }}
          labelStyle={{ color: 'var(--color-text-secondary)', fontSize: 11 }}
          formatter={(value, name) => {
            const s = series.find((x) => x.key === name);
            return [formatValue ? formatValue(Number(value)) : value, s?.label ?? String(name)];
          }}
        />
        <Legend
          wrapperStyle={{ fontSize: 12, color: 'var(--color-text-secondary)' }}
          formatter={(value: string) => {
            const s = series.find((x) => x.key === value);
            return s?.label ?? value;
          }}
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
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium">{label}</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">{content}</CardContent>
      </Card>
    );
  }

  return content;
}
