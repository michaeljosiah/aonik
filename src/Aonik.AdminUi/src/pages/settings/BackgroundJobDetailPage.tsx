import { useState, useEffect, useCallback } from 'react';
import { useParams } from 'react-router-dom';
import { Timer, Play, Pause, RefreshCw, ServerCog, ArrowLeft } from 'lucide-react';
import { toast } from 'sonner';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  jobService,
  type ScheduledJobDetailResponse,
  type ScheduledJobRunSummary,
  type ScheduledJobCommandSummary,
} from '@/services/jobService';
import type { PagedResult } from '@/types';

function formatRelativeTime(dateStr: string | null): string {
  if (!dateStr) return '--';
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSec = Math.floor(Math.abs(diffMs) / 1000);

  if (diffSec < 60) return diffMs < 0 ? 'in a few seconds' : 'just now';
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return diffMs < 0 ? `in ${diffMin}m` : `${diffMin}m ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return diffMs < 0 ? `in ${diffHr}h` : `${diffHr}h ago`;
  const diffDay = Math.floor(diffHr / 24);
  return diffMs < 0 ? `in ${diffDay}d` : `${diffDay}d ago`;
}

function formatDateTime(dateStr: string | null): string {
  if (!dateStr) return '--';
  return new Date(dateStr).toLocaleString();
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function statusBadge(status: string) {
  const lower = status.toLowerCase();
  const colors: Record<string, string> = {
    active: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
    paused: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
    disabled: 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400',
    succeeded: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
    failed: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
    pending: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
    processing: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
  };
  return <Badge className={colors[lower] ?? 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400'}>{status}</Badge>;
}

function wait(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

export function BackgroundJobDetailPage() {
  const { jobName } = useParams<{ jobName: string }>();
  const [detail, setDetail] = useState<ScheduledJobDetailResponse | null>(null);
  const [runs, setRuns] = useState<PagedResult<ScheduledJobRunSummary> | null>(null);
  const [commands, setCommands] = useState<PagedResult<ScheduledJobCommandSummary> | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionInProgress, setActionInProgress] = useState(false);
  const [runsPage, setRunsPage] = useState(1);
  const [commandsPage, setCommandsPage] = useState(1);

  const loadDetail = useCallback(async () => {
    if (!jobName) return;
    try {
      const result = await jobService.getJobDetail(jobName);
      setDetail(result);
    } catch (err) {
      console.error('Failed to load job detail:', err);
    } finally {
      setLoading(false);
    }
  }, [jobName]);

  const loadRuns = useCallback(async (page: number) => {
    if (!jobName) return;
    try {
      const result = await jobService.listJobRuns(jobName, page, 10);
      setRuns(result);
    } catch (err) {
      console.error('Failed to load job runs:', err);
    }
  }, [jobName]);

  const loadCommands = useCallback(async (page: number) => {
    if (!jobName) return;
    try {
      const result = await jobService.listJobCommands(jobName, page, 10);
      setCommands(result);
    } catch (err) {
      console.error('Failed to load job commands:', err);
    }
  }, [jobName]);

  useEffect(() => {
    void loadDetail();
    void loadRuns(1);
    void loadCommands(1);

    const interval = setInterval(() => {
      void loadDetail();
      void loadRuns(runsPage);
      void loadCommands(commandsPage);
    }, 30_000);
    return () => clearInterval(interval);
  }, [loadDetail, loadRuns, loadCommands, runsPage, commandsPage]);

  const handleAction = async (action: 'trigger' | 'pause' | 'resume') => {
    if (!jobName) return;
    setActionInProgress(true);
    try {
      const fn = action === 'trigger' ? jobService.triggerJob
        : action === 'pause' ? jobService.pauseJob
        : jobService.resumeJob;
      const result = await fn(jobName);
      toast.success(result.message ?? `${action} command queued.`);
      await wait(1500);
      await loadDetail();
      await loadCommands(1);
      setCommandsPage(1);
    } catch {
      toast.error(`Failed to ${action} job.`);
    } finally {
      setActionInProgress(false);
    }
  };

  const breadcrumbItems = [
    { label: 'Settings', href: '/settings', icon: <ServerCog className="w-3.5 h-3.5" /> },
    { label: 'Background Jobs', href: '/settings/background-jobs', icon: <Timer className="w-3.5 h-3.5" /> },
    { label: detail?.displayName ?? jobName ?? '...' },
  ];

  if (loading) {
    return (
      <div className="h-full overflow-auto p-6">
        <p className="text-sm text-[var(--color-text-tertiary)]">Loading...</p>
      </div>
    );
  }

  if (!detail) {
    return (
      <div className="h-full overflow-auto p-6">
        <p className="text-sm text-[var(--color-text-tertiary)]">Job not found.</p>
      </div>
    );
  }

  const isPaused = detail.state.toLowerCase() === 'paused';
  const isDisabled = detail.state.toLowerCase() === 'disabled';

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-start justify-between gap-4 mb-6">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{detail.displayName}</h1>
            {statusBadge(detail.state)}
          </div>
          <p className="text-[var(--color-text-secondary)]">{detail.description}</p>
        </div>
        <div className="flex items-center gap-2">
          {isPaused ? (
            <Button size="sm" variant="secondary" disabled={actionInProgress} onClick={() => void handleAction('resume')}>
              <Play className="w-3.5 h-3.5 mr-1" /> Resume
            </Button>
          ) : (
            <Button size="sm" variant="secondary" disabled={actionInProgress || isDisabled} onClick={() => void handleAction('pause')}>
              <Pause className="w-3.5 h-3.5 mr-1" /> Pause
            </Button>
          )}
          <Button size="sm" disabled={actionInProgress || isDisabled} onClick={() => void handleAction('trigger')}>
            <Play className="w-3.5 h-3.5 mr-1" /> Trigger Now
          </Button>
          <Button size="sm" variant="secondary" onClick={() => { void loadDetail(); void loadRuns(runsPage); void loadCommands(commandsPage); }}>
            <RefreshCw className="w-3.5 h-3.5" />
          </Button>
        </div>
      </div>

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="runs">Run History</TabsTrigger>
          <TabsTrigger value="commands">Commands</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <Card>
            <CardHeader>
              <CardTitle>Job Configuration</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid grid-cols-2 gap-x-8 gap-y-3 text-sm">
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Job Name</dt>
                  <dd className="font-mono">{detail.jobName}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Group</dt>
                  <dd className="font-mono">{detail.groupName}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Cron Expression</dt>
                  <dd className="font-mono bg-[var(--color-surface-inset)] px-2 py-0.5 rounded inline-block">{detail.cronExpression}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Time Zone</dt>
                  <dd>{detail.timeZoneId}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Next Fire Time</dt>
                  <dd>{formatRelativeTime(detail.nextFireTimeUtc)} <span className="text-[var(--color-text-tertiary)]">({formatDateTime(detail.nextFireTimeUtc)})</span></dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Previous Fire Time</dt>
                  <dd>{formatRelativeTime(detail.previousFireTimeUtc)} <span className="text-[var(--color-text-tertiary)]">({formatDateTime(detail.previousFireTimeUtc)})</span></dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Last Outcome</dt>
                  <dd>{detail.lastOutcome ? statusBadge(detail.lastOutcome) : '--'}</dd>
                </div>
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Last Duration</dt>
                  <dd>{detail.lastDurationMs != null ? formatDuration(detail.lastDurationMs) : '--'}</dd>
                </div>
                {detail.lastOutcomeSummary ? (
                  <div className="col-span-2">
                    <dt className="text-[var(--color-text-tertiary)]">Last Error</dt>
                    <dd className="text-red-600 dark:text-red-400 font-mono text-xs bg-[var(--color-surface-inset)] p-2 rounded mt-1">{detail.lastOutcomeSummary}</dd>
                  </div>
                ) : null}
                <div>
                  <dt className="text-[var(--color-text-tertiary)]">Last Synced</dt>
                  <dd>{formatRelativeTime(detail.lastSyncedAtUtc)}</dd>
                </div>
              </dl>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="runs">
          <Card>
            <CardHeader>
              <CardTitle>Run History</CardTitle>
            </CardHeader>
            <CardContent>
              {!runs || runs.items.length === 0 ? (
                <p className="text-sm text-[var(--color-text-tertiary)]">No runs recorded yet.</p>
              ) : (
                <>
                  <div className="rounded-md border border-[var(--color-border-light)] overflow-hidden">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="bg-[var(--color-surface-inset)] border-b border-[var(--color-border-light)]">
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Outcome</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Duration</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Triggered By</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Fired At</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Error</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[var(--color-border-light)]">
                        {runs.items.map((run) => (
                          <tr key={run.id}>
                            <td className="px-3 py-2">{statusBadge(run.outcome)}</td>
                            <td className="px-3 py-2 font-mono">{formatDuration(run.durationMs)}</td>
                            <td className="px-3 py-2">{run.triggeredBy}</td>
                            <td className="px-3 py-2 text-[var(--color-text-tertiary)]">{formatDateTime(run.firedAtUtc)}</td>
                            <td className="px-3 py-2 text-red-600 dark:text-red-400 text-xs max-w-xs truncate">{run.errorMessage ?? ''}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  {runs.totalPages > 1 && (
                    <div className="flex items-center justify-between mt-3">
                      <span className="text-xs text-[var(--color-text-tertiary)]">Page {runs.pageNumber} of {runs.totalPages} ({runs.totalCount} total)</span>
                      <div className="flex gap-2">
                        <Button size="sm" variant="secondary" disabled={runsPage <= 1} onClick={() => { setRunsPage(runsPage - 1); void loadRuns(runsPage - 1); }}>
                          <ArrowLeft className="w-3.5 h-3.5" />
                        </Button>
                        <Button size="sm" variant="secondary" disabled={runsPage >= runs.totalPages} onClick={() => { setRunsPage(runsPage + 1); void loadRuns(runsPage + 1); }}>
                          <ArrowLeft className="w-3.5 h-3.5 rotate-180" />
                        </Button>
                      </div>
                    </div>
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="commands">
          <Card>
            <CardHeader>
              <CardTitle>Admin Commands</CardTitle>
            </CardHeader>
            <CardContent>
              {!commands || commands.items.length === 0 ? (
                <p className="text-sm text-[var(--color-text-tertiary)]">No commands recorded yet.</p>
              ) : (
                <>
                  <div className="rounded-md border border-[var(--color-border-light)] overflow-hidden">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="bg-[var(--color-surface-inset)] border-b border-[var(--color-border-light)]">
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Command</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Status</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Result</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Requested</th>
                          <th className="text-left px-3 py-2 font-medium text-[var(--color-text-secondary)]">Processed</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[var(--color-border-light)]">
                        {commands.items.map((cmd) => (
                          <tr key={cmd.id}>
                            <td className="px-3 py-2 font-medium">{cmd.commandType}</td>
                            <td className="px-3 py-2">{statusBadge(cmd.status)}</td>
                            <td className="px-3 py-2 text-xs max-w-xs truncate">{cmd.resultMessage ?? ''}</td>
                            <td className="px-3 py-2 text-[var(--color-text-tertiary)]">{formatDateTime(cmd.createdAt)}</td>
                            <td className="px-3 py-2 text-[var(--color-text-tertiary)]">{formatDateTime(cmd.processedAtUtc)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  {commands.totalPages > 1 && (
                    <div className="flex items-center justify-between mt-3">
                      <span className="text-xs text-[var(--color-text-tertiary)]">Page {commands.pageNumber} of {commands.totalPages} ({commands.totalCount} total)</span>
                      <div className="flex gap-2">
                        <Button size="sm" variant="secondary" disabled={commandsPage <= 1} onClick={() => { setCommandsPage(commandsPage - 1); void loadCommands(commandsPage - 1); }}>
                          <ArrowLeft className="w-3.5 h-3.5" />
                        </Button>
                        <Button size="sm" variant="secondary" disabled={commandsPage >= commands.totalPages} onClick={() => { setCommandsPage(commandsPage + 1); void loadCommands(commandsPage + 1); }}>
                          <ArrowLeft className="w-3.5 h-3.5 rotate-180" />
                        </Button>
                      </div>
                    </div>
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
