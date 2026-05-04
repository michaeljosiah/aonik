import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, ExternalLink, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { alertService, type AlertDetail } from '@/services/alertService';

function formatDateTime(value: string | null): string {
  if (!value) return '--';
  return new Date(value).toLocaleString();
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

export function AlertDetailPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const [alert, setAlert] = useState<AlertDetail | null>(null);
  const [loading, setLoading] = useState(true);

  const loadAlert = useCallback(async () => {
    if (!id) return;

    try {
      const result = await alertService.get(id);
      setAlert(result);
    } catch (error) {
      console.error('Failed to load alert detail:', error);
      setAlert(null);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void loadAlert();
  }, [loadAlert]);
  if (loading) {
    return <PageLoadingScreen message="Loading alert" />;
  }

  if (!alert) {
    return (
      <div className="h-full overflow-auto p-6">
        <p className="text-sm text-[var(--color-text-tertiary)]">Alert not found.</p>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-6">

      <div className="mb-6 flex items-start justify-between gap-4">
        <div>
          <div className="mb-1 flex items-center gap-2">
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{alert.alertRuleName}</h1>
            {severityBadge(alert.severity, alert.monitorCondition)}
          </div>
          <p className="text-[var(--color-text-secondary)]">
            {alert.normalizedType} · {alert.signalType} · {alert.monitoringService}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button variant="secondary" className="rounded-sm" onClick={() => navigate('/admin/alerts')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <Button variant="secondary" className="rounded-sm" onClick={() => void loadAlert()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Analysis</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 text-sm">
              <div>
                <p className="mb-1 font-medium text-[var(--color-text-primary)]">Summary</p>
                <p className="text-[var(--color-text-secondary)]">{alert.analysis?.summary || 'Analysis is not available yet.'}</p>
              </div>

              {alert.analysis?.likelyCause && (
                <div>
                  <p className="mb-1 font-medium text-[var(--color-text-primary)]">Likely Cause</p>
                  <p className="text-[var(--color-text-secondary)]">{alert.analysis.likelyCause}</p>
                </div>
              )}

              {alert.analysis?.impact && (
                <div>
                  <p className="mb-1 font-medium text-[var(--color-text-primary)]">Impact</p>
                  <p className="text-[var(--color-text-secondary)]">{alert.analysis.impact}</p>
                </div>
              )}

              {alert.analysis?.affectedComponent && (
                <div>
                  <p className="mb-1 font-medium text-[var(--color-text-primary)]">Affected Component</p>
                  <p className="text-[var(--color-text-secondary)]">{alert.analysis.affectedComponent}</p>
                </div>
              )}

              {alert.analysis?.recommendedActions?.length ? (
                <div>
                  <p className="mb-2 font-medium text-[var(--color-text-primary)]">Recommended Actions</p>
                  <div className="space-y-2">
                    {alert.analysis.recommendedActions.map((action) => (
                      <div key={action} className="rounded-md bg-[var(--color-surface-inset)] px-3 py-2 text-[var(--color-text-secondary)]">
                        {action}
                      </div>
                    ))}
                  </div>
                </div>
              ) : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Raw Alert Payload</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <p className="mb-2 text-sm font-medium text-[var(--color-text-primary)]">Essentials</p>
                <pre className="overflow-auto rounded-md bg-[var(--color-surface-inset)] p-3 text-xs text-[var(--color-text-secondary)]">
                  {alert.essentialsJson}
                </pre>
              </div>

              <div>
                <p className="mb-2 text-sm font-medium text-[var(--color-text-primary)]">Alert Context</p>
                <pre className="overflow-auto rounded-md bg-[var(--color-surface-inset)] p-3 text-xs text-[var(--color-text-secondary)]">
                  {alert.alertContextJson}
                </pre>
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Overview</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="space-y-3 text-sm">
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Monitor condition</dt>
                  <dd className="text-[var(--color-text-primary)]">{alert.monitorCondition}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Processing status</dt>
                  <dd className="text-[var(--color-text-primary)]">{alert.status}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Received</dt>
                  <dd className="text-[var(--color-text-primary)]">{formatDateTime(alert.receivedAtUtc)}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Fired</dt>
                  <dd className="text-[var(--color-text-primary)]">{formatDateTime(alert.firedAtUtc)}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Resolved</dt>
                  <dd className="text-[var(--color-text-primary)]">{formatDateTime(alert.resolvedAtUtc)}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Processed</dt>
                  <dd className="text-[var(--color-text-primary)]">{formatDateTime(alert.processedAtUtc)}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">AI Run</dt>
                  <dd className="font-mono text-[var(--color-text-primary)]">{alert.aiRunId ?? '--'}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Correlation key</dt>
                  <dd className="font-mono text-[var(--color-text-primary)] break-all">{alert.correlationKey}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">External alert ID</dt>
                  <dd className="font-mono text-[var(--color-text-primary)] break-all">{alert.externalAlertId}</dd>
                </div>
              </dl>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Resources</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              {alert.resourceIds.length === 0 ? (
                <p className="text-[var(--color-text-tertiary)]">No resource IDs were included.</p>
              ) : (
                alert.resourceIds.map((resourceId) => (
                  <div key={resourceId} className="rounded-md bg-[var(--color-surface-inset)] px-3 py-2 font-mono text-xs text-[var(--color-text-secondary)] break-all">
                    {resourceId}
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          {alert.investigationLink && (
            <Card>
              <CardHeader>
                <CardTitle>Azure Investigation</CardTitle>
              </CardHeader>
              <CardContent>
                <a
                  href={alert.investigationLink}
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex items-center gap-2 text-sm text-[var(--color-brand-primary)] hover:underline"
                >
                  Open Azure investigation link
                  <ExternalLink className="h-4 w-4" />
                </a>
              </CardContent>
            </Card>
          )}

          {Object.keys(alert.customProperties).length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Custom Properties</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                {Object.entries(alert.customProperties).map(([key, value]) => (
                  <div key={key} className="flex items-start justify-between gap-3 rounded-md bg-[var(--color-surface-inset)] px-3 py-2">
                    <span className="font-medium text-[var(--color-text-primary)]">{key}</span>
                    <span className="text-right text-[var(--color-text-secondary)] break-all">{value}</span>
                  </div>
                ))}
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
