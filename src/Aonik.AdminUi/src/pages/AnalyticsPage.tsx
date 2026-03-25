import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowUpRight, Minus, TrendingDown, TrendingUp } from 'lucide-react';

import { AiChatComposer } from '@/components/ai/AiChatComposer';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useAuth } from '@/auth/useAuth';
import { identityService } from '@/services/identityService';
import { analyticsOverviewCards, analyticsPerformanceCards, analyticsQuickActions } from '@/data/analyticsMockData';
import { cn } from '@/lib/utils';
import { isPortalAdmin } from '@/lib/roleUtils';

type TimeRange = '7d' | '30d' | '90d' | '1y';

type TrendMeta = {
  direction: 'up' | 'down' | 'neutral';
  value: string;
  label: string;
};

type AnalyticsCardProps = {
  title: string;
  description: string;
  value: string;
  valueLabel?: string;
  trend?: TrendMeta;
  footerLabel?: string;
  footerHref?: string;
  sparkline?: number[];
  accent?: string;
};

const trendIconMap = {
  up: TrendingUp,
  down: TrendingDown,
  neutral: Minus,
};

function TrendBadge({ trend }: { trend: TrendMeta }) {
  const Icon = trendIconMap[trend.direction];
  const colorClass = trend.direction === 'up'
    ? 'text-[var(--color-success)]'
    : trend.direction === 'down'
      ? 'text-[var(--color-danger)]'
      : 'text-[var(--color-text-tertiary)]';

  return (
    <div className={cn('inline-flex items-center gap-1 text-xs font-medium', colorClass)}>
      <Icon className="h-3.5 w-3.5" />
      {trend.value}
      <span className="text-[var(--color-text-tertiary)] font-normal">{trend.label}</span>
    </div>
  );
}

function MiniSparkline({ values }: { values: number[] }) {
  const { path, area } = useMemo(() => {
    if (values.length === 0) {
      return { path: '', area: '' };
    }

    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;
    const divider = values.length - 1 || 1;
    const points = values.map((value, index) => {
      const x = (index / divider) * 100;
      const y = 30 - ((value - min) / range) * 24;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    });

    const pathData = `M ${points.join(' L ')}`;
    const areaData = `M 0,30 L ${points.join(' L ')} L 100,30 Z`;

    return { path: pathData, area: areaData };
  }, [values]);

  return (
    <svg viewBox="0 0 100 30" className="h-10 w-28">
      <path d={area} fill="rgba(99, 102, 241, 0.18)" />
      <path d={path} stroke="rgba(99, 102, 241, 0.9)" strokeWidth="2" fill="none" />
    </svg>
  );
}

function AnalyticsCard({
  title,
  description,
  value,
  valueLabel,
  trend,
  footerLabel,
  footerHref,
  sparkline,
  accent,
}: AnalyticsCardProps) {
  return (
    <Card className="relative border-l-4" style={{ borderLeftColor: accent || 'var(--color-border-light)' }}>
      <CardContent className="p-5">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
              {title}
            </p>
            <p className="mt-1 text-sm text-[var(--color-text-secondary)]">{description}</p>
          </div>
          {trend && <TrendBadge trend={trend} />}
        </div>

        <div className="mt-4 flex items-end justify-between gap-4">
          <div>
            <div className="text-2xl font-semibold text-[var(--color-text-primary)]">{value}</div>
            {valueLabel && (
              <div className="mt-1 text-xs text-[var(--color-text-tertiary)]">{valueLabel}</div>
            )}
          </div>
          {sparkline && sparkline.length > 0 && <MiniSparkline values={sparkline} />}
        </div>
      </CardContent>

      {footerLabel && footerHref && (
        <CardFooter className="pt-0">
          <Button variant="ghost" size="sm" asChild className="gap-2">
            <Link to={footerHref}>
              {footerLabel}
              <ArrowUpRight className="h-4 w-4" />
            </Link>
          </Button>
        </CardFooter>
      )}
    </Card>
  );
}

export function AnalyticsPage() {
  const [timeRange, setTimeRange] = useState<TimeRange>('1y');
  const [draft, setDraft] = useState('');
  const { isLoading, user } = useAuth();
  const [resolvedRoles, setResolvedRoles] = useState<string[]>(user?.roles ?? []);
  const [isLoadingRoles, setIsLoadingRoles] = useState(false);

  useEffect(() => {
    const hydrateRoles = async () => {
      if (!user) {
        setResolvedRoles([]);
        return;
      }

      if (user.roleSource !== 'api' && user.roles && user.roles.length > 0) {
        setResolvedRoles(user.roles);
        return;
      }

      setIsLoadingRoles(true);
      try {
        const response = await identityService.getUserInfo();
        setResolvedRoles(response.roles);
      } catch (error) {
        console.error('Failed to load roles for analytics:', error);
        setResolvedRoles([]);
      } finally {
        setIsLoadingRoles(false);
      }
    };

    hydrateRoles();
  }, [user]);

  const isAdmin = isPortalAdmin(resolvedRoles);

  const handleSend = () => {
    if (!draft.trim()) return;
    setDraft('');
  };

  if (isLoading || isLoadingRoles) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="w-8 h-8 border-4 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (!isAdmin) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="text-center">
          <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">Analytics</h1>
          <p className="mt-2 text-sm text-[var(--color-text-secondary)]">
            This dashboard is available to platform administrators only.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6 space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Analytics</h1>
            <p className="text-[var(--color-text-secondary)]">
              Operational insights across billing, payments, ledger, and AI automation.
            </p>
          </div>
          <Tabs value={timeRange} onValueChange={(value) => setTimeRange(value as TimeRange)}>
            <TabsList>
              <TabsTrigger value="7d">7d</TabsTrigger>
              <TabsTrigger value="30d">30d</TabsTrigger>
              <TabsTrigger value="90d">90d</TabsTrigger>
              <TabsTrigger value="1y">1y</TabsTrigger>
            </TabsList>
          </Tabs>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-5">
          {analyticsOverviewCards.map((card) => (
            <AnalyticsCard key={card.id} {...card} />
          ))}
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-5">
          {analyticsPerformanceCards.map((card) => (
            <AnalyticsCard key={card.id} {...card} />
          ))}
        </div>

        <Card>
          <CardContent className="p-5">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Quick analysis</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Jump into deeper reports or operational views.
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                {analyticsQuickActions.map((action) => (
                  <Button key={action.id} variant="outline" size="sm" asChild>
                    <Link to={action.href}>{action.label}</Link>
                  </Button>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5">
            <div className="flex flex-col gap-4">
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Ask Aonik</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Ask for trends, anomalies, or operational recommendations.
                </p>
              </div>
              <AiChatComposer
                value={draft}
                onChange={setDraft}
                onSend={handleSend}
                showHelper={false}
                showClear={false}
                modelLabel={`Aonik Insights ${timeRange.toUpperCase()}`}
                placeholder="Ask about cash flow, orders, or risk signals..."
                className="bg-[var(--color-surface)]"
              />
              <div className="text-xs text-[var(--color-text-tertiary)]">
                Mock UI - insights will connect to AiRun once the analytics service is wired.
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
