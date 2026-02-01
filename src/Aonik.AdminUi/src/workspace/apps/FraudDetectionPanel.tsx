import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import type { WorkspacePanelRenderProps } from '../types';

export function FraudDetectionPanel({ title }: WorkspacePanelRenderProps) {
  return (
    <div className="p-4 space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <p className="text-sm text-[var(--color-text-secondary)]">
            Monitor real-time risk signals and AI explainability outputs.
          </p>
        </div>
        <Button variant="secondary" size="sm">
          Review Alerts
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Alerts today</p>
          <p className="text-2xl font-semibold text-[var(--color-text-primary)]">18</p>
          <p className="text-xs text-[var(--color-text-secondary)]">4 escalated to review</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">False positive</p>
          <p className="text-2xl font-semibold text-[var(--color-text-primary)]">2.1%</p>
          <p className="text-xs text-[var(--color-text-secondary)]">Down 0.6% week-over-week</p>
        </Card>
      </div>

      <Card className="p-4 space-y-3">
        <div className="flex items-center justify-between">
          <p className="text-sm font-semibold text-[var(--color-text-primary)]">Top alerts</p>
          <Badge variant="pending">Live feed</Badge>
        </div>
        <div className="space-y-2 text-sm text-[var(--color-text-secondary)]">
          <div className="flex items-center justify-between">
            <span>Unusual refund loop - Lagos corridor</span>
            <Badge variant="warning">Medium</Badge>
          </div>
          <div className="flex items-center justify-between">
            <span>Multi-account login spike - West Africa</span>
            <Badge variant="error">High</Badge>
          </div>
          <div className="flex items-center justify-between">
            <span>Velocity breach - outbound payouts</span>
            <Badge variant="pending">Elevated</Badge>
          </div>
        </div>
      </Card>
    </div>
  );
}
