import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { BarChart3, Loader2, AlertCircle, RefreshCw } from 'lucide-react';
import {
  ActivityFeed,
  BannerCarousel,
  QuickLinks,
  AgentCard,
  DataboxesTable,
  SectionHeader,
  MyAgentsHeader,
  FinancialSnapshotCard,
} from '@/components/dashboard';
import type { FinancialSnapshotData } from '@/components/dashboard/FinancialSnapshotCard';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { quickLinks, myDataboxes } from '@/data/mockData';
import { mySpaceService } from '@/services/mySpaceService';
import { agentConfigService } from '@/services/aiService';
import type { MySpaceSummaryResponse, ActivityItem, AgentCard as AgentCardType } from '@/types';
import type { AgentConfigurationResponse } from '@/types/ai';

const metricDisplayConfig: Record<string, {
  title: string;
  description: string;
  footerLabel: string;
  footerHref: string;
  accent: string;
}> = {
  'burn-rate': {
    title: 'Burn rate',
    description: 'Average monthly operating spend.',
    footerLabel: 'View burn analysis',
    footerHref: '/ledger/accounts',
    accent: '#EF4444',
  },
  'revenue': {
    title: 'Revenue',
    description: 'Total revenue recognised this month.',
    footerLabel: 'View revenue',
    footerHref: '/billing/invoices',
    accent: '#22C55E',
  },
  'outstanding-invoices': {
    title: 'Outstanding invoices',
    description: 'Unpaid invoices requiring attention.',
    footerLabel: 'View invoices',
    footerHref: '/billing/invoices',
    accent: '#F97316',
  },
  'expenses': {
    title: 'Expenses',
    description: 'Total spend this month across all categories.',
    footerLabel: 'View expenses',
    footerHref: '/ledger/journal-entries',
    accent: '#A855F7',
  },
  'cash-position': {
    title: 'Cash position',
    description: 'Liquid balance across all linked accounts.',
    footerLabel: 'View accounts',
    footerHref: '/ledger/accounts',
    accent: '#0EA5E9',
  },
  'profit-loss': {
    title: 'Profit / Loss',
    description: 'Net profit after all expenses this month.',
    footerLabel: 'View P&L',
    footerHref: '/ledger/accounts',
    accent: '#055a60',
  },
};

function mapMetricToSnapshot(metric: MySpaceSummaryResponse['financialMetrics'][number]): FinancialSnapshotData {
  const config = metricDisplayConfig[metric.metricKey];
  return {
    id: metric.metricKey,
    title: config?.title ?? metric.metricKey,
    description: config?.description ?? '',
    value: metric.formattedValue,
    valueLabel: metric.valueLabel ?? undefined,
    trend: {
      direction: metric.trendDirection,
      value: `${metric.trendPercent}%`,
      label: 'vs last month',
    },
    sparkline: metric.sparkline,
    footerLabel: config?.footerLabel,
    footerHref: config?.footerHref,
    accent: config?.accent,
  };
}

function mapAgentConfig(cfg: AgentConfigurationResponse): AgentCardType {
  let toolsetIds: string[] = [];
  try { toolsetIds = JSON.parse(cfg.toolsetIdsJson || '[]'); } catch { /* ignore */ }
  return {
    id: cfg.id,
    name: cfg.name,
    description: cfg.description,
    visibility: 'team',
    source: cfg.domain || 'Agent',
    skills: [],
    plugins: toolsetIds,
  };
}

export function MySpacePage() {
  const navigate = useNavigate();
  const defaultBannerImages = [
    { src: '/images/banners/myspace-default-01.png', alt: 'Banner placeholder' },
    { src: '/images/banners/myspace-default-02.png', alt: 'Banner placeholder' },
    { src: '/images/banners/myspace-default-03.png', alt: 'Banner placeholder' },
  ];

  const [summaryData, setSummaryData] = useState<MySpaceSummaryResponse | null>(null);
  const [agents, setAgents] = useState<AgentConfigurationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [summary, agentConfigs] = await Promise.all([
        mySpaceService.getSummary(),
        agentConfigService.list(),
      ]);
      setSummaryData(summary);
      setAgents(agentConfigs);
    } catch (err: unknown) {
      const message = (err as { userMessage?: string })?.userMessage
        ?? (err instanceof Error ? err.message : 'Failed to load dashboard data.');
      setError(message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      await loadData();
      if (cancelled) return;
    })();
    return () => { cancelled = true; };
  }, [loadData]);

  const financialSnapshots = summaryData?.financialMetrics.map(mapMetricToSnapshot) ?? [];
  const activityItems: ActivityItem[] = summaryData?.recentActivity.map(a => ({
    id: a.id,
    title: a.title,
    description: a.description ?? '',
    timestamp: a.timestamp,
    icon: a.icon,
  })) ?? [];
  const agentCards = agents.filter(a => a.isActive).slice(0, 4).map(mapAgentConfig);

  const handleChatAgent = (agentId: string) => {
    void agentId;
    navigate('/ai/chat');
  };

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center bg-[var(--color-background)]">
        <div className="flex flex-col items-center gap-3 text-[var(--color-text-secondary)]">
          <Loader2 className="h-8 w-8 animate-spin" />
          <p className="text-sm">Loading dashboard...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex-1 flex items-center justify-center bg-[var(--color-background)]">
        <div className="flex flex-col items-center gap-3 text-center">
          <AlertCircle className="h-8 w-8 text-[var(--color-danger)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">{error}</p>
          <Button variant="outline" size="sm" onClick={loadData} className="gap-2">
            <RefreshCw className="h-4 w-4" />
            Retry
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-auto bg-[var(--color-background)]">
      <div className="p-6 pb-8">
        <div className="mb-5">
          <h1 className="text-[20px] font-bold text-[var(--color-text-primary)] sm:text-[24px]">My Space</h1>
          <p className="mt-1 text-sm text-[var(--color-text-secondary)]">
            View and access your personal space with quick links, recent activity, and key resources in one place.
          </p>
        </div>

        <div className="grid grid-cols-12 gap-5 mb-6">
          <div className="col-span-12 xl:col-span-3 xl:h-[290px]">
            <ActivityFeed items={activityItems} />
          </div>

          <div className="col-span-12 xl:col-span-6 xl:h-[290px]">
            <Card className="h-full rounded-[4px] p-4">
              <CardContent className="h-full p-0">
                <BannerCarousel images={defaultBannerImages} className="h-full" />
              </CardContent>
            </Card>
          </div>

          <div className="col-span-12 xl:col-span-3 xl:h-[290px]">
            <QuickLinks links={quickLinks} />
          </div>
        </div>

        <div className="mb-6">
          <Card className="shadow-sm rounded-[4px]">
            <CardContent className="p-5">
              <SectionHeader
                icon={<BarChart3 className="w-6 h-6 text-white" />}
                title="Financial snapshot"
                description="Key metrics and insights on how your business is performing."
              />
              <div className="grid grid-cols-1 gap-5 pt-2 mt-1 pb-4 md:grid-cols-2 xl:grid-cols-3">
                {financialSnapshots.map((card) => (
                  <FinancialSnapshotCard key={card.id} card={card} />
                ))}
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="grid grid-cols-12 gap-5">
          <div className="col-span-12 lg:col-span-6">
            <Card className="h-full shadow-sm rounded-[4px]">
              <CardContent className="p-5">
                <MyAgentsHeader />
                <div className="grid grid-cols-1 gap-5 pt-2 mt-1 pb-4 xl:grid-cols-2">
                  {agentCards.map((agent) => (
                    <AgentCard key={agent.id} agent={agent} onChat={handleChatAgent} />
                  ))}
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="col-span-12 lg:col-span-6">
            <DataboxesTable databoxes={myDataboxes} />
          </div>
        </div>
      </div>
    </div>
  );
}
