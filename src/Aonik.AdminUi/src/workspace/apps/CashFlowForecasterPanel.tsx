import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import type { WorkspacePanelRenderProps } from '../types';

export function CashFlowForecasterPanel({ title }: WorkspacePanelRenderProps) {
  return (
    <div className="p-4 space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <p className="text-sm text-[var(--color-text-secondary)]">
            Forecast liquidity and explore scenarios across corridors.
          </p>
        </div>
        <Button variant="secondary" size="sm">
          New Scenario
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Next 30 days</p>
          <p className="text-2xl font-semibold text-[var(--color-text-primary)]">+$1.82M</p>
          <p className="text-xs text-[var(--color-text-secondary)]">Base scenario</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Variance</p>
          <p className="text-2xl font-semibold text-[var(--color-text-primary)]">6.4%</p>
          <p className="text-xs text-[var(--color-text-secondary)]">Projected volatility</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Risk tier</p>
          <Badge variant="warning">Moderate</Badge>
          <p className="text-xs text-[var(--color-text-secondary)]">4 inflow gaps detected</p>
        </Card>
      </div>

      <Card className="p-4">
        <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Scenario highlights</p>
        <ul className="mt-2 space-y-2 text-sm text-[var(--color-text-secondary)]">
          <li>Optimize settlement timing on NGN corridors to release $220k in trapped liquidity.</li>
          <li>Shift payout mix to reduce fee exposure by 2.1%.</li>
          <li>Projected FX exposure spikes in week 3 if GBP demand rises.</li>
        </ul>
      </Card>
    </div>
  );
}
