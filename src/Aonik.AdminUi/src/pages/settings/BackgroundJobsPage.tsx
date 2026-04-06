import { useState, useEffect, useCallback } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Timer,
  Play,
  Pause,
  RefreshCw,
  ServerCog,
  Activity,
  CheckCircle2,
  XCircle,
  Clock,
  ArrowRight,
  FileWarning,
} from 'lucide-react';
import { toast } from 'sonner';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { jobService, type ScheduledJobSummary, type SchedulerHealthResponse } from '@/services/jobService';
import { describeCron } from '@/lib/cronDescriber';

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

function formatDuration(ms: number | null): string {
  if (ms === null) return '--';
  if (ms < 1000) return `${ms}ms`;
  const seconds = (ms / 1000).toFixed(1);
  return `${seconds}s`;
}

function statusBadge(status: string) {
  switch (status.toLowerCase()) {
    case 'active':
      return <Badge className="bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400">Active</Badge>;
    case 'paused':
      return <Badge className="bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400">Paused</Badge>;
    case 'disabled':
      return <Badge className="bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">Disabled</Badge>;
    case 'error':
    case 'blocked':
      return <Badge className="bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400">{status}</Badge>;
    default:
      return <Badge className="bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">{status}</Badge>;
  }
}

function outcomeBadge(outcome: string | null) {
  if (!outcome) return <Badge variant="outline">No runs yet</Badge>;

  switch (outcome.toLowerCase()) {
    case 'succeeded':
      return (
        <Badge className="bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-400 gap-1">
          <CheckCircle2 className="w-3 h-3" />
          Healthy
        </Badge>
      );
    case 'failed':
      return (
        <Badge className="bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-400 gap-1">
          <XCircle className="w-3 h-3" />
          Needs attention
        </Badge>
      );
    default:
      return <Badge variant="outline">{outcome}</Badge>;
  }
}

function getAuditLink(job: ScheduledJobSummary): string | null {
  if (job.lastOutcome?.toLowerCase() !== 'failed') {
    return null;
  }

  const params = new URLSearchParams({
    action: 'ScheduledJobRunFailed',
    resourceType: 'ScheduledJobRun',
    search: job.jobName,
  });

  return `/settings/audit-logs?${params.toString()}`;
}

function getJobTone(job: ScheduledJobSummary) {
  if (job.lastOutcome?.toLowerCase() === 'failed') {
    return 'border-red-200 bg-red-50/40 dark:border-red-900/40 dark:bg-red-950/10';
  }

  if (job.status.toLowerCase() === 'paused') {
    return 'border-amber-200 bg-amber-50/40 dark:border-amber-900/40 dark:bg-amber-950/10';
  }

  return 'border-[var(--color-border-light)] bg-[var(--color-surface)]';
}

function summarizeOutcome(job: ScheduledJobSummary): string {
  if (job.lastOutcomeSummary?.trim()) {
    return job.lastOutcomeSummary.trim();
  }

  if (!job.previousFireTimeUtc) {
    return 'This job has not recorded a run yet. Trigger it manually to verify the pipeline.';
  }

  return `Last run completed ${formatRelativeTime(job.previousFireTimeUtc)} in ${formatDuration(job.lastDurationMs)}.`;
}

export function BackgroundJobsPage() {
  const navigate = useNavigate();
  const [jobs, setJobs] = useState<ScheduledJobSummary[]>([]);
  const [health, setHealth] = useState<SchedulerHealthResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  const loadJobs = useCallback(async () => {
    try {
      const result = await jobService.listScheduledJobs();
      setJobs(result.jobs);
    } catch (err) {
      console.error('Failed to load scheduled jobs:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadHealth = useCallback(async () => {
    try {
      const result = await jobService.getSchedulerHealth();
      setHealth(result);
    } catch {
      setHealth(null);
    }
  }, []);

  useEffect(() => {
    void loadJobs();
    void loadHealth();

    const interval = setInterval(() => {
      void loadJobs();
      void loadHealth();
    }, 30_000);
    return () => clearInterval(interval);
  }, [loadJobs, loadHealth]);

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await Promise.all([loadJobs(), loadHealth()]);
      toast.success('Background jobs refreshed.');
    } finally {
      setRefreshing(false);
    }
  };

  const handleTrigger = async (jobName: string, displayName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.triggerJob(jobName);
      toast.success(result.message ?? `${displayName} trigger queued.`);

      let attempts = 0;
      const pollInterval = setInterval(async () => {
        attempts++;
        try {
          const updated = await jobService.listScheduledJobs();
          const updatedJob = updated.jobs.find(j => j.jobName === jobName);
          const currentJob = jobs.find(j => j.jobName === jobName);

          if (updatedJob && currentJob && updatedJob.previousFireTimeUtc !== currentJob.previousFireTimeUtc) {
            clearInterval(pollInterval);
            setJobs(updated.jobs);
            setActionInProgress(null);

            const message = updatedJob.lastOutcomeSummary?.trim();
            if (updatedJob.lastOutcome === 'Succeeded') {
              toast.success(`${displayName} completed. ${message ?? ''}`.trim());
            } else if (updatedJob.lastOutcome === 'Failed') {
              toast.error(`${displayName} failed. ${message ?? ''}`.trim());
            }
            return;
          }

          setJobs(updated.jobs);
        } catch {
        }

        if (attempts >= 15) {
          clearInterval(pollInterval);
          setActionInProgress(null);
          await loadJobs();
        }
      }, 2000);
    } catch (err) {
      console.error('Trigger failed:', err);
      toast.error('Failed to trigger job.');
      setActionInProgress(null);
    }
  };

  const handlePause = async (jobName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.pauseJob(jobName);
      toast.success(result.message ?? 'Pause command queued.');
      setTimeout(async () => {
        await loadJobs();
        setActionInProgress(null);
      }, 3000);
    } catch (err) {
      console.error('Pause failed:', err);
      toast.error('Failed to pause job.');
      setActionInProgress(null);
    }
  };

  const handleResume = async (jobName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.resumeJob(jobName);
      toast.success(result.message ?? 'Resume command queued.');
      setTimeout(async () => {
        await loadJobs();
        setActionInProgress(null);
      }, 3000);
    } catch (err) {
      console.error('Resume failed:', err);
      toast.error('Failed to resume job.');
      setActionInProgress(null);
    }
  };

  const breadcrumbItems = [
    { label: 'Settings', href: '/settings', icon: <ServerCog className="w-3.5 h-3.5" /> },
    { label: 'Background Jobs', icon: <Timer className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Background Jobs</h1>
          <p className="text-[var(--color-text-secondary)]">
            Operate Worker-scheduled pipelines with clearer run health, richer output summaries, and direct audit drill-downs.
          </p>
        </div>
        <Button
          onClick={() => void handleRefresh()}
          disabled={refreshing}
          variant="secondary"
          className="rounded-sm"
        >
          <RefreshCw className={`w-4 h-4 mr-2 ${refreshing ? 'animate-spin' : ''}`} />
          {refreshing ? 'Refreshing...' : 'Refresh'}
        </Button>
      </div>

      {health && (
        <Card className="mb-6">
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Activity className="w-4 h-4 text-[var(--color-brand-primary)]" />
              Scheduler Health
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 text-sm md:grid-cols-4">
              <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                <div className="text-xs text-[var(--color-text-tertiary)]">Status</div>
                <div className="mt-1 font-medium text-[var(--color-text-primary)]">
                  {health.isStarted ? (health.inStandbyMode ? 'Standby' : 'Running') : 'Stopped'}
                </div>
              </div>
              <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                <div className="text-xs text-[var(--color-text-tertiary)]">Registered Jobs</div>
                <div className="mt-1 font-medium text-[var(--color-text-primary)]">{health.totalJobCount}</div>
              </div>
              <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                <div className="text-xs text-[var(--color-text-tertiary)]">Active Executions</div>
                <div className="mt-1 font-medium text-[var(--color-text-primary)]">{health.activeJobCount}</div>
              </div>
              <div className="rounded-sm border border-[var(--color-border-light)] p-3">
                <div className="text-xs text-[var(--color-text-tertiary)]">Last Snapshot</div>
                <div className="mt-1 font-medium text-[var(--color-text-primary)]">{formatRelativeTime(health.recordedAtUtc)}</div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Timer className="w-5 h-5 text-[var(--color-brand-primary)]" />
            Scheduled Jobs
          </CardTitle>
          <CardDescription>
            Each card shows schedule health, the latest run output, and the fastest route into command/run audit evidence.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && jobs.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">Loading jobs...</p>
          ) : jobs.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">
              No scheduled jobs found. Ensure the Worker service has run at least once to register jobs.
            </p>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
              {jobs.map((job) => {
                const isPaused = job.status.toLowerCase() === 'paused';
                const isDisabled = job.status.toLowerCase() === 'disabled';
                const isBusy = actionInProgress === job.jobName;
                const auditLink = getAuditLink(job);

                return (
                  <div
                    key={job.jobName}
                    className={`rounded-md border px-4 py-3 shadow-sm transition-colors ${getJobTone(job)}`}
                  >
                    <div className="flex flex-col gap-2.5">
                      {/* Header */}
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <button
                          type="button"
                          className="text-sm font-semibold text-[var(--color-brand-primary)] hover:underline text-left truncate max-w-full"
                          onClick={() => navigate(`/settings/background-jobs/${encodeURIComponent(job.jobName)}`)}
                        >
                          {job.displayName ?? job.jobName}
                        </button>
                        {statusBadge(job.status)}
                        {outcomeBadge(job.lastOutcome)}
                      </div>

                      {/* Stats */}
                      <div className="grid grid-cols-2 gap-x-3 gap-y-1 text-xs text-[var(--color-text-secondary)]">
                        <span title={job.cronExpression ?? undefined}>{describeCron(job.cronExpression)}</span>
                        <span>Next: {formatRelativeTime(job.nextFireTimeUtc)}</span>
                        <span>Last: {formatRelativeTime(job.previousFireTimeUtc)}</span>
                        <span className="flex items-center gap-1">
                          <Clock className="w-3 h-3" />
                          {formatDuration(job.lastDurationMs)}
                        </span>
                      </div>

                      {/* Output summary */}
                      <p className="text-xs text-[var(--color-text-secondary)] line-clamp-2">
                        {job.lastOutcome?.toLowerCase() === 'failed' && (
                          <FileWarning className="inline h-3 w-3 text-red-500 mr-1 -mt-0.5" />
                        )}
                        {summarizeOutcome(job)}
                      </p>

                      {/* Actions */}
                      <div className="flex items-center justify-between gap-2 pt-0.5 border-t border-[var(--color-border-light)]">
                        <div className="flex items-center gap-1">
                          {auditLink && (
                            <Button asChild size="sm" variant="outline" className="h-6 px-2 text-[11px]">
                              <Link to={auditLink}>Audit</Link>
                            </Button>
                          )}
                          <Button asChild size="sm" variant="ghost" className="h-6 px-2 text-[11px]">
                            <Link to={`/settings/background-jobs/${encodeURIComponent(job.jobName)}`}>
                              History
                              <ArrowRight className="h-3 w-3 ml-0.5" />
                            </Link>
                          </Button>
                        </div>
                        <div className="flex items-center gap-1">
                          {isPaused ? (
                            <Button size="sm" variant="ghost" className="h-6 px-1.5 text-[11px]" disabled={isBusy} onClick={() => void handleResume(job.jobName)}>
                              <Play className="w-3 h-3 mr-0.5" /> Resume
                            </Button>
                          ) : (
                            <Button size="sm" variant="ghost" className="h-6 px-1.5 text-[11px]" disabled={isBusy || isDisabled} onClick={() => void handlePause(job.jobName)}>
                              <Pause className="w-3 h-3 mr-0.5" /> Pause
                            </Button>
                          )}
                          <Button size="sm" className="h-6 px-2 text-[11px]" disabled={isBusy || isDisabled} onClick={() => void handleTrigger(job.jobName, job.displayName ?? job.jobName)}>
                            {isBusy ? <RefreshCw className="w-3 h-3 mr-0.5 animate-spin" /> : <Play className="w-3 h-3 mr-0.5" />}
                            {isBusy ? 'Running...' : 'Trigger'}
                          </Button>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
