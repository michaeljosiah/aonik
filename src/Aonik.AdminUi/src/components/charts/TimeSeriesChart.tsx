import {
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ChartTooltipContent } from '@/components/ui/chart';
import { chartAxisProps, chartGridProps } from '@/components/ui/chart-config';

interface TimeSeriesChartProps {
  data: { timestamp: string; value: number }[];
  color?: string;
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

export function TimeSeriesChart({
  data,
  color = 'var(--color-brand-primary)',
  height = 200,
  label,
  formatValue,
}: TimeSeriesChartProps) {
  const chartData = data.map((d) => ({
    ...d,
    displayTime: formatTimestamp(d.timestamp),
  }));

  const content = (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={chartData} margin={{ top: 5, right: 10, left: 0, bottom: 0 }}>
        <defs>
          <linearGradient id={`gradient-${label ?? 'default'}`} x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor={color} stopOpacity={0.2} />
            <stop offset="95%" stopColor={color} stopOpacity={0} />
          </linearGradient>
        </defs>
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
            <ChartTooltipContent {...props} formatValue={formatValue} nameFor={() => label ?? 'Value'} />
          )}
        />
        <Area
          type="monotone"
          dataKey="value"
          stroke={color}
          strokeWidth={2}
          fill={`url(#gradient-${label ?? 'default'})`}
        />
      </AreaChart>
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
