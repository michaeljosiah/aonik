import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { alertService, type AlertSummary } from '@/services/alertService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';

function formatRelativeTime(value: string | null): string {
  if (!value) return '--';
  const date = new Date(value);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(Math.abs(diffMs) / 60000);

  if (diffMinutes < 1) return 'just now';
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;

  return `${Math.floor(diffHours / 24)}d ago`;
}

function severityBadge(severity: string, monitorCondition: string) {
  if (monitorCondition.toLowerCase() === 'resolved') {
    return <Badge className="bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400">Resolved</Badge>;
  }

  const lower = severity.toLowerCase();
  if (lower === 'sev0' || lower === 'sev1' || lower === 'sev2') {
    return <Badge className="bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400">{severity}</Badge>;
  }

  if (lower === 'sev3') {
    return <Badge className="bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400">{severity}</Badge>;
  }

  return <Badge className="bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400">{severity}</Badge>;
}

export function AlertsPage() {
  const navigate = useNavigate();
  const [alerts, setAlerts] = useState<AlertSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);

  const loadAlerts = useCallback(async () => {
    try {
      const result = await alertService.list();
      setAlerts(result.alerts);
    } catch (error) {
      console.error('Failed to load platform alerts:', error);
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, []);

  useEffect(() => {
    void loadAlerts();
    const interval = setInterval(() => {
      void loadAlerts();
    }, 30_000);

    return () => clearInterval(interval);
  }, [loadAlerts]);

  if (initialLoad) {
    return <PageLoadingScreen message="Loading alerts" />;
  }

  return (
    <div className="h-full overflow-auto p-6">

      <div className="mb-6 flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Platform Alerts</h1>
          <p className="text-[var(--color-text-secondary)]">
            Azure Monitor alerts that have been ingested, analyzed, and surfaced to platform administrators.
          </p>
        </div>

        <Button variant="secondary" className="rounded-sm" onClick={() => void loadAlerts()} disabled={loading}>
          <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-[var(--color-brand-primary)]" />
            Alert Feed
          </CardTitle>
          <CardDescription>
            Fired and resolved infrastructure events across platform health, performance, security, and operations.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && alerts.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">Loading alerts...</p>
          ) : alerts.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">No platform alerts have been ingested yet.</p>
          ) : (
            <div className="space-y-3">
              {alerts.map((alert) => (
                <button
                  key={alert.id}
                  type="button"
                  className="w-full rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 text-left shadow-sm transition-colors hover:bg-[var(--color-surface-inset)]"
                  onClick={() => navigate(`/admin/alerts/${alert.id}`)}
                >
                  <div className="mb-2 flex items-start justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">{alert.alertRuleName}</h2>
                        {severityBadge(alert.severity, alert.monitorCondition)}
                      </div>
                      <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                        {alert.normalizedType} · {alert.signalType} · received {formatRelativeTime(alert.receivedAtUtc)}
                      </p>
                    </div>
                    <span className="text-xs text-[var(--color-text-tertiary)]">{alert.status}</span>
                  </div>

                  <p className="text-sm text-[var(--color-text-secondary)]">
                    {alert.analysisSummary || 'Analysis is still being prepared for this alert.'}
                  </p>

                  {alert.resourceIds.length > 0 && (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {alert.resourceIds.slice(0, 2).map((resourceId) => (
                        <Badge key={resourceId} variant="outline" className="max-w-full truncate text-xs">
                          {resourceId}
                        </Badge>
                      ))}
                      {alert.resourceIds.length > 2 && (
                        <Badge variant="outline" className="text-xs">+{alert.resourceIds.length - 2} more</Badge>
                      )}
                    </div>
                  )}
                </button>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
