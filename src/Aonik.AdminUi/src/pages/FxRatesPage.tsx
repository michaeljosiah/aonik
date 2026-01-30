import { useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ArrowRightLeft, Clock3, ShieldCheck } from 'lucide-react';

const rateSources = [
  {
    name: 'Primary market feed',
    provider: 'Provider A',
    status: 'Active',
    lastUpdated: '2 minutes ago',
    coverage: 'USD ⇄ NGN, GHS, KES',
  },
  {
    name: 'Backup market feed',
    provider: 'Provider B',
    status: 'Standby',
    lastUpdated: '15 minutes ago',
    coverage: 'USD ⇄ ZAR, GBP',
  },
  {
    name: 'Manual corridor overrides',
    provider: 'Ops team',
    status: 'Restricted',
    lastUpdated: 'Yesterday',
    coverage: 'USD ⇄ GHS',
  },
];

const spreadPolicies = [
  { corridor: 'USD → NGN', markup: '120 bps', min: '0.5%', max: '2.0%', tier: 'Retail' },
  { corridor: 'USD → GHS', markup: '85 bps', min: '0.3%', max: '1.5%', tier: 'Business' },
  { corridor: 'USD → KES', markup: '95 bps', min: '0.4%', max: '1.8%', tier: 'Enterprise' },
];

const refreshWindows = [
  { window: 'Every 5 minutes', active: true, note: 'Primary rate pull for top corridors' },
  { window: 'Hourly', active: true, note: 'Secondary feeds + volatility checks' },
  { window: 'Daily 02:00 UTC', active: false, note: 'Manual overrides review' },
];

export function FxRatesPage() {
  const sources = useMemo(() => rateSources, []);
  const spreads = useMemo(() => spreadPolicies, []);
  const windows = useMemo(() => refreshWindows, []);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings/general' },
          { label: 'FX Rates' },
        ]}
        className="mb-4"
      />

      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">FX Rate Management</h1>
          <p className="text-[var(--color-text-secondary)] max-w-2xl">
            Configure rate sources, corridor spreads, and refresh cadence. All material updates should follow the
            proposal → approval → apply flow and remain auditable in the pricing ledger.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline">Review proposals</Button>
          <Button>New rate source</Button>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-3 mb-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ArrowRightLeft className="w-4 h-4 text-[var(--color-brand-primary)]" />
              Active rate sources
            </CardTitle>
            <CardDescription>Feeds and manual overrides powering FX quotes.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {sources.map((source) => (
              <div
                key={source.name}
                className="flex flex-col gap-1 border border-[var(--color-border-light)] rounded-lg p-3"
              >
                <div className="flex items-center justify-between">
                  <div className="font-semibold text-[var(--color-text-primary)]">{source.name}</div>
                  <Badge variant={source.status === 'Active' ? 'success' : 'secondary'}>{source.status}</Badge>
                </div>
                <div className="text-xs text-[var(--color-text-secondary)]">Provider: {source.provider}</div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Coverage: {source.coverage}</div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Last updated: {source.lastUpdated}</div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ShieldCheck className="w-4 h-4 text-[var(--color-brand-primary)]" />
              Spread policies
            </CardTitle>
            <CardDescription>Margin ranges by corridor and customer tier.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {spreads.map((policy) => (
              <div key={policy.corridor} className="rounded-lg border border-[var(--color-border-light)] p-3">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-[var(--color-text-primary)]">{policy.corridor}</span>
                  <Badge variant="outline">{policy.tier}</Badge>
                </div>
                <div className="mt-1 text-xs text-[var(--color-text-secondary)]">
                  Markup: {policy.markup} · Range: {policy.min} - {policy.max}
                </div>
              </div>
            ))}
            <Button variant="ghost" className="w-full justify-center">
              Manage spread matrix
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Clock3 className="w-4 h-4 text-[var(--color-brand-primary)]" />
              Refresh cadence
            </CardTitle>
            <CardDescription>Schedules that keep quotes current and auditable.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {windows.map((window) => (
              <div
                key={window.window}
                className="flex items-start gap-3 rounded-lg border border-[var(--color-border-light)] p-3"
              >
                <div className="flex-1">
                  <div className="font-semibold text-[var(--color-text-primary)]">{window.window}</div>
                  <div className="text-xs text-[var(--color-text-secondary)]">{window.note}</div>
                </div>
                <Badge variant={window.active ? 'success' : 'secondary'}>
                  {window.active ? 'Enabled' : 'Paused'}
                </Badge>
              </div>
            ))}
            <Button variant="ghost" className="w-full justify-center">
              Edit refresh policy
            </Button>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>FX control checklist</CardTitle>
          <CardDescription>Ensure controls align with pricing policy and ledger truth.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-3">
          <div className="rounded-lg border border-[var(--color-border-light)] p-4 text-sm text-[var(--color-text-secondary)]">
            Confirm quote sources and corridor coverage for each product line.
          </div>
          <div className="rounded-lg border border-[var(--color-border-light)] p-4 text-sm text-[var(--color-text-secondary)]">
            Validate spread approvals and route all changes through policy workflows.
          </div>
          <div className="rounded-lg border border-[var(--color-border-light)] p-4 text-sm text-[var(--color-text-secondary)]">
            Monitor drift alerts and reconcile FX postings in the ledger daily.
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
