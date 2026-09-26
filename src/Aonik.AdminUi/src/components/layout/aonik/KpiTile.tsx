import { TrendingDownIcon, TrendingUpIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Card, CardAction, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export interface KpiTileProps {
  label: string;
  value: string;
  delta?: string;
  deltaTone?: 'up' | 'down' | 'neutral';
  sparkline?: number[];
  sparkColor?: string;
  className?: string;
}

function sparkPoints(data: number[]): string {
  if (!data.length) return '';
  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  return data
    .map((v, i) => {
      const x = (i / Math.max(1, data.length - 1)) * 100;
      const y = 30 - ((v - min) / range) * 26 - 2;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}

/**
 * KPI tile as a Card composition: label (CardDescription), figure in mono
 * tabular (CardTitle), delta Badge (CardAction), optional 1.5px sparkline.
 */
export function KpiTile({
  label,
  value,
  delta,
  deltaTone = 'up',
  sparkline,
  sparkColor = 'var(--chart-1)',
  className,
}: KpiTileProps) {
  const points = sparkline ? sparkPoints(sparkline) : '';

  return (
    <Card className={cn('@container/card', className)}>
      <CardHeader className={cn(points && 'pb-3')}>
        <CardDescription>{label}</CardDescription>
        <CardTitle className="font-mono text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">{value}</CardTitle>
        {delta && (
          <CardAction>
            <Badge variant={deltaTone === 'down' ? 'outline' : deltaTone === 'neutral' ? 'outline' : 'success'}
              className={cn(deltaTone === 'down' && 'border-transparent bg-destructive/10 text-destructive dark:bg-destructive/20')}
            >
              {deltaTone === 'up' && <TrendingUpIcon />}
              {deltaTone === 'down' && <TrendingDownIcon />}
              {delta}
            </Badge>
          </CardAction>
        )}
      </CardHeader>
      {points && (
        <div className="px-6 pb-6">
          <svg viewBox="0 0 100 30" preserveAspectRatio="none" className="block h-8 w-full" aria-hidden>
            <polyline
              fill="none"
              stroke={sparkColor}
              strokeWidth="1.5"
              vectorEffect="non-scaling-stroke"
              points={points}
            />
          </svg>
        </div>
      )}
    </Card>
  );
}
