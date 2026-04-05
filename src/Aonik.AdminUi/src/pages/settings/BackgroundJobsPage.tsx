import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Timer, Play, Pause, RefreshCw, ServerCog, Activity, CheckCircle2, XCircle, Clock } from 'lucide-react';
import { toast } from 'sonner';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { jobService, type ScheduledJobSummary, type SchedulerHealthResponse } from '@/services/jobService';

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
    default:
      return <Badge className="bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">{status}</Badge>;
  }
}

function outcomeBadge(outcome: string | null) {
  if (!outcome) return null;
  switch (outcome.toLowerCase()) {
    case 'succeeded':
      return (
        <Badge className="bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-400 gap-1">
          <CheckCircle2 className="w-3 h-3" />
          Succeeded
        </Badge>
      );
    case 'failed':
      return (
        <Badge className="bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-400 gap-1">
          <XCircle className="w-3 h-3" />
          Failed
        </Badge>
      );
    default:
      return <Badge className="bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">{outcome}</Badge>;
  }
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
      // Health endpoint may not be available yet
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
      toast.success('Refreshed.');
    } finally {
      setRefreshing(false);
    }
  };

  const handleTrigger = async (jobName: string, displayName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.triggerJob(jobName);
      toast.success(result.message ?? `${displayName} trigger queued.`);

      // Poll for execution result (the worker processes commands every ~2s)
      let attempts = 0;
      const pollInterval = setInterval(async () => {
        attempts++;
        try {
          const updated = await jobService.listScheduledJobs();
          const updatedJob = updated.jobs.find(j => j.jobName === jobName);
          const currentJob = jobs.find(j => j.jobName === jobName);

          // Detect that a new execution happened (previousFireTimeUtc changed or lastOutcome updated)
          if (updatedJob && currentJob &&
            updatedJob.previousFireTimeUtc !== currentJob.previousFireTimeUtc) {
            clearInterval(pollInterval);
            setJobs(updated.jobs);
            setActionInProgress(null);

            if (updatedJob.lastOutcome === 'Succeeded') {
              toast.success(`${displayName} completed in ${formatDuration(updatedJob.lastDurationMs)}.${updatedJob.lastOutcomeSummary ? ` ${updatedJob.lastOutcomeSummary}` : ''}`);
            } else if (updatedJob.lastOutcome === 'Failed') {
              toast.error(`${displayName} failed.${updatedJob.lastOutcomeSummary ? ` ${updatedJob.lastOutcomeSummary}` : ''}`);
            }
            return;
          }

          // Also update the list in the meantime
          setJobs(updated.jobs);
        } catch {
          // Ignore poll errors
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
      // Brief delay for the worker to process
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
            View and manage scheduled background jobs running in the Worker service.
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
            <div className="flex items-center gap-6 text-sm">
              <div className="flex items-center gap-2">
                <div className={`w-2 h-2 rounded-full ${health.isStarted && !health.inStandbyMode ? 'bg-emerald-500' : 'bg-amber-500'}`} />
                <span>{health.isStarted ? (health.inStandbyMode ? 'Standby' : 'Running') : 'Stopped'}</span>
              </div>
              <span className="text-[var(--color-text-tertiary)]">
                {health.totalJobCount} jobs &middot; {health.totalTriggerCount} triggers &middot; {health.activeJobCount} active &middot; {health.threadPoolSize} threads
              </span>
              <span className="text-[var(--color-text-tertiary)]">
                Updated {formatRelativeTime(health.recordedAtUtc)}
              </span>
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
            Quartz-managed cron jobs. Status is synced from the Worker service after each execution.
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
            <div className="rounded-md border border-[var(--color-border-light)] divide-y divide-[var(--color-border-light)]">
              {jobs.map((job) => {
                const isPaused = job.status.toLowerCase() === 'paused';
                const isDisabled = job.status.toLowerCase() === 'disabled';
                const isBusy = actionInProgress === job.jobName;

                return (
                  <div key={job.jobName} className="flex items-center justify-between px-4 py-4 gap-4">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <button
                          type="button"
                          className="text-sm font-medium text-[var(--color-brand-primary)] hover:underline cursor-pointer"
                          onClick={() => navigate(`/settings/background-jobs/${encodeURIComponent(job.jobName)}`)}
                        >
                          {job.displayName ?? job.jobName}
                        </button>
                        {statusBadge(job.status)}
                        {outcomeBadge(job.lastOutcome)}
                      </div>
                      {job.description ? (
                        <p className="text-xs text-[var(--color-text-tertiary)] mb-2">
                          {job.description}
                        </p>
                      ) : null}
                      <div className="flex items-center gap-4 text-xs text-[var(--color-text-tertiary)]">
                        <span title="Cron expression">
                          Cron: <code className="font-mono bg-[var(--color-surface-inset)] px-1 py-0.5 rounded">{job.cronExpression ?? '--'}</code>
                        </span>
                        <span>Next: {formatRelativeTime(job.nextFireTimeUtc)}</span>
                        <span>Last: {formatRelativeTime(job.previousFireTimeUtc)}</span>
                        {job.lastDurationMs !== null && (
                          <span className="flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            {formatDuration(job.lastDurationMs)}
                          </span>
                        )}
                      </div>
                      {job.lastOutcomeSummary && (
                        <p className="text-xs text-[var(--color-text-secondary)] mt-1 italic">
                          {job.lastOutcomeSummary}
                        </p>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      {isPaused ? (
                        <Button
                          size="sm"
                          variant="secondary"
                          disabled={isBusy}
                          onClick={() => void handleResume(job.jobName)}
                        >
                          <Play className="w-3.5 h-3.5 mr-1" />
                          Resume
                        </Button>
                      ) : (
                        <Button
                          size="sm"
                          variant="secondary"
                          disabled={isBusy || isDisabled}
                          onClick={() => void handlePause(job.jobName)}
                        >
                          <Pause className="w-3.5 h-3.5 mr-1" />
                          Pause
                        </Button>
                      )}
                      <Button
                        size="sm"
                        disabled={isBusy || isDisabled}
                        onClick={() => void handleTrigger(job.jobName, job.displayName ?? job.jobName)}
                      >
                        {isBusy ? (
                          <RefreshCw className="w-3.5 h-3.5 mr-1 animate-spin" />
                        ) : (
                          <Play className="w-3.5 h-3.5 mr-1" />
                        )}
                        {isBusy ? 'Running...' : 'Trigger'}
                      </Button>
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
