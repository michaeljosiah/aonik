import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

interface MetricCardProps {
  label: string;
  value: string | number;
  subtitle?: string;
  status?: 'good' | 'warning' | 'critical';
  className?: string;
}

const statusBorderColor: Record<string, string> = {
  good: 'border-l-emerald-500',
  warning: 'border-l-amber-500',
  critical: 'border-l-red-500',
};

export function MetricCard({ label, value, subtitle, status, className }: MetricCardProps) {
  return (
    <Card
      className={cn(
        status ? `border-l-4 ${statusBorderColor[status]}` : '',
        className,
      )}
    >
      <CardContent className="p-5">
        <p className="text-sm text-[var(--color-text-secondary)]">{label}</p>
        <p className="text-2xl font-bold text-[var(--color-text-primary)] mt-1">
          {value}
        </p>
        {subtitle && (
          <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
            {subtitle}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
