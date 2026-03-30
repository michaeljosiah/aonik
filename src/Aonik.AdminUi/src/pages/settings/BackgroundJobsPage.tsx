import { useState, useEffect, useCallback } from 'react';
import { Timer, Play, Pause, RefreshCw, ServerCog } from 'lucide-react';
import { toast } from 'sonner';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { jobService, type ScheduledJobSummary } from '@/services/jobService';

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

function friendlyJobName(name: string): string {
  return name
    .replace(/Job$/, '')
    .replace(/([a-z])([A-Z])/g, '$1 $2');
}

export function BackgroundJobsPage() {
  const [jobs, setJobs] = useState<ScheduledJobSummary[]>([]);
  const [loading, setLoading] = useState(true);
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

  useEffect(() => {
    void loadJobs();

    // Auto-refresh every 30 seconds
    const interval = setInterval(() => void loadJobs(), 30_000);
    return () => clearInterval(interval);
  }, [loadJobs]);

  const handleTrigger = async (jobName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.triggerJob(jobName);
      toast.success(result.message ?? 'Job triggered.');
      await loadJobs();
    } catch (err) {
      console.error('Trigger failed:', err);
      toast.error('Failed to trigger job.');
    } finally {
      setActionInProgress(null);
    }
  };

  const handlePause = async (jobName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.pauseJob(jobName);
      toast.success(result.message ?? 'Job paused.');
      await loadJobs();
    } catch (err) {
      console.error('Pause failed:', err);
      toast.error('Failed to pause job.');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleResume = async (jobName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.resumeJob(jobName);
      toast.success(result.message ?? 'Job resumed.');
      await loadJobs();
    } catch (err) {
      console.error('Resume failed:', err);
      toast.error('Failed to resume job.');
    } finally {
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
          onClick={() => void loadJobs()}
          disabled={loading}
          variant="secondary"
          className="rounded-sm"
        >
          <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

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
                        <p className="text-sm font-medium text-[var(--color-text-primary)]">
                          {friendlyJobName(job.jobName)}
                        </p>
                        {statusBadge(job.status)}
                      </div>
                      <div className="flex items-center gap-4 text-xs text-[var(--color-text-tertiary)]">
                        <span title="Cron expression">
                          Cron: <code className="font-mono bg-[var(--color-surface-inset)] px-1 py-0.5 rounded">{job.cronExpression ?? '--'}</code>
                        </span>
                        <span>Last run: {formatRelativeTime(job.previousFireTimeUtc)}</span>
                      </div>
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
                        onClick={() => void handleTrigger(job.jobName)}
                      >
                        <Play className="w-3.5 h-3.5 mr-1" />
                        Trigger
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
