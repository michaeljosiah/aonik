import { useState, useEffect, useCallback } from 'react';
import {
  Timer,
  Play,
  Pause,
  RefreshCw,
  Activity,
  CheckCircle2,
  XCircle,
  Clock,
  FileWarning,
  ScrollText,
} from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { jobService, type ScheduledJobSummary, type SchedulerHealthResponse } from '@/services/jobService';
import { describeCron } from '@/lib/cronDescriber';
import type { WorkspacePanelRenderProps } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

// ---------------------------------------------------------------------------
// Helpers (shared with BackgroundJobsPage)
// ---------------------------------------------------------------------------

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
  return `${(ms / 1000).toFixed(1)}s`;
}

function statusBadge(status: string) {
  switch (status.toLowerCase()) {
    case 'active':
      return <Badge className="bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400 text-[10px] px-1.5 py-0">Active</Badge>;
    case 'paused':
      return <Badge className="bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400 text-[10px] px-1.5 py-0">Paused</Badge>;
    case 'error':
    case 'blocked':
      return <Badge className="bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400 text-[10px] px-1.5 py-0">{status}</Badge>;
    default:
      return <Badge className="bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400 text-[10px] px-1.5 py-0">{status}</Badge>;
  }
}

function outcomeBadge(outcome: string | null) {
  if (!outcome) return <Badge variant="outline" className="text-[10px] px-1.5 py-0">No runs</Badge>;
  switch (outcome.toLowerCase()) {
    case 'succeeded':
      return <Badge className="bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-400 gap-0.5 text-[10px] px-1.5 py-0"><CheckCircle2 className="w-2.5 h-2.5" />OK</Badge>;
    case 'failed':
      return <Badge className="bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-400 gap-0.5 text-[10px] px-1.5 py-0"><XCircle className="w-2.5 h-2.5" />Failed</Badge>;
    default:
      return <Badge variant="outline" className="text-[10px] px-1.5 py-0">{outcome}</Badge>;
  }
}

function getJobTone(job: ScheduledJobSummary) {
  if (job.lastOutcome?.toLowerCase() === 'failed')
    return 'border-red-200 bg-red-50/40 dark:border-red-900/40 dark:bg-red-950/10';
  if (job.status.toLowerCase() === 'paused')
    return 'border-amber-200 bg-amber-50/40 dark:border-amber-900/40 dark:bg-amber-950/10';
  return 'border-[var(--color-border-light)] bg-[var(--color-surface)]';
}

function summarizeOutcome(job: ScheduledJobSummary): string {
  if (job.lastOutcomeSummary?.trim()) return job.lastOutcomeSummary.trim();
  if (!job.previousFireTimeUtc) return 'No runs recorded yet.';
  return `Last run ${formatRelativeTime(job.previousFireTimeUtc)} in ${formatDuration(job.lastDurationMs)}.`;
}

// ---------------------------------------------------------------------------
// Panel
// ---------------------------------------------------------------------------

export function BackgroundJobsPanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { emit } = useWorkspaceEvents(panelId);
  const [jobs, setJobs] = useState<ScheduledJobSummary[]>([]);
  const [health, setHealth] = useState<SchedulerHealthResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [selectedJobName, setSelectedJobName] = useState<string | null>(null);

  const loadJobs = useCallback(async () => {
    try {
      const result = await jobService.listScheduledJobs();
      setJobs(result.jobs);
    } catch {
      /* swallow */
    } finally {
      setLoading(false);
    }
  }, []);

  const loadHealth = useCallback(async () => {
    try {
      setHealth(await jobService.getSchedulerHealth());
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

  // ── Context publishing ─────────────────────────────────────────────

  const selectJob = useCallback(
    (job: ScheduledJobSummary) => {
      setSelectedJobName(job.jobName);
      emit({
        type: 'job:selected',
        payload: {
          jobName: job.jobName,
          displayName: job.displayName ?? job.jobName,
          lastOutcome: job.lastOutcome,
          status: job.status,
        },
      });
    },
    [emit],
  );

  const viewAudit = useCallback(
    (job: ScheduledJobSummary) => {
      setSelectedJobName(job.jobName);
      emit({
        type: 'job:audit-requested',
        payload: {
          jobName: job.jobName,
          displayName: job.displayName ?? job.jobName,
          lastOutcome: job.lastOutcome,
          action: job.lastOutcome?.toLowerCase() === 'failed' ? 'ScheduledJobRunFailed' : undefined,
          resourceType: 'ScheduledJobRun',
        },
      });
    },
    [emit],
  );

  // ── Actions ────────────────────────────────────────────────────────

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await Promise.all([loadJobs(), loadHealth()]);
      toast.success('Jobs refreshed.');
    } finally {
      setRefreshing(false);
    }
  };

  const handleTrigger = async (job: ScheduledJobSummary) => {
    setActionInProgress(job.jobName);
    try {
      const result = await jobService.triggerJob(job.jobName);
      toast.success(result.message ?? `${job.displayName ?? job.jobName} trigger queued.`);

      let attempts = 0;
      const pollInterval = setInterval(async () => {
        attempts++;
        try {
          const updated = await jobService.listScheduledJobs();
          const updatedJob = updated.jobs.find((j) => j.jobName === job.jobName);
          if (updatedJob && updatedJob.previousFireTimeUtc !== job.previousFireTimeUtc) {
            clearInterval(pollInterval);
            setJobs(updated.jobs);
            setActionInProgress(null);
            if (updatedJob.lastOutcome === 'Succeeded') {
              toast.success(`${job.displayName ?? job.jobName} completed.`);
            } else if (updatedJob.lastOutcome === 'Failed') {
              toast.error(`${job.displayName ?? job.jobName} failed.`);
            }
            return;
          }
          setJobs(updated.jobs);
        } catch {
          /* swallow */
        }
        if (attempts >= 15) {
          clearInterval(pollInterval);
          setActionInProgress(null);
          await loadJobs();
        }
      }, 2000);
    } catch {
      toast.error('Failed to trigger job.');
      setActionInProgress(null);
    }
  };

  const handlePause = async (jobName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.pauseJob(jobName);
      toast.success(result.message ?? 'Pause queued.');
      setTimeout(async () => { await loadJobs(); setActionInProgress(null); }, 3000);
    } catch {
      toast.error('Failed to pause.');
      setActionInProgress(null);
    }
  };

  const handleResume = async (jobName: string) => {
    setActionInProgress(jobName);
    try {
      const result = await jobService.resumeJob(jobName);
      toast.success(result.message ?? 'Resume queued.');
      setTimeout(async () => { await loadJobs(); setActionInProgress(null); }, 3000);
    } catch {
      toast.error('Failed to resume.');
      setActionInProgress(null);
    }
  };

  // ── Render ─────────────────────────────────────────────────────────

  const failedCount = jobs.filter((j) => j.lastOutcome?.toLowerCase() === 'failed').length;
  const activeCount = jobs.filter((j) => j.status.toLowerCase() === 'active').length;

  return (
    <div className="h-full overflow-auto p-4 space-y-3">
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <p className="text-xs text-[var(--color-text-secondary)]">
            Select a job to view its audit trail in the companion panel.
          </p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => void handleRefresh()}
          disabled={refreshing}
        >
          <RefreshCw className={`w-3.5 h-3.5 ${refreshing ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Health summary */}
      {health && (
        <div className="grid grid-cols-3 gap-2">
          <Card className="p-3">
            <div className="flex items-center gap-1.5">
              <Activity className="w-3.5 h-3.5 text-[var(--color-brand-primary)]" />
              <span className="text-xs text-[var(--color-text-tertiary)]">Scheduler</span>
            </div>
            <p className="text-sm font-semibold text-[var(--color-text-primary)] mt-0.5">
              {health.isStarted ? (health.inStandbyMode ? 'Standby' : 'Running') : 'Stopped'}
            </p>
          </Card>
          <Card className="p-3">
            <p className="text-xs text-[var(--color-text-tertiary)]">Active</p>
            <p className="text-sm font-semibold text-[var(--color-text-primary)] mt-0.5">
              {activeCount} / {jobs.length}
            </p>
          </Card>
          <Card className="p-3">
            <p className="text-xs text-[var(--color-text-tertiary)]">Failed</p>
            <p className={`text-sm font-semibold mt-0.5 ${failedCount > 0 ? 'text-red-600 dark:text-red-400' : 'text-[var(--color-text-primary)]'}`}>
              {failedCount}
            </p>
          </Card>
        </div>
      )}

      {/* Job list */}
      {loading && jobs.length === 0 ? (
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">Loading jobs...</p>
      ) : jobs.length === 0 ? (
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">No scheduled jobs found.</p>
      ) : (
        <div className="space-y-2">
          {jobs.map((job) => {
            const isPaused = job.status.toLowerCase() === 'paused';
            const isDisabled = job.status.toLowerCase() === 'disabled';
            const isBusy = actionInProgress === job.jobName;
            const isSelected = selectedJobName === job.jobName;
            const hasFailed = job.lastOutcome?.toLowerCase() === 'failed';

            return (
              <button
                key={job.jobName}
                type="button"
                onClick={() => selectJob(job)}
                className={`w-full text-left rounded-md border px-3 py-2.5 transition-all ${getJobTone(job)} ${
                  isSelected
                    ? 'ring-2 ring-[var(--color-brand-primary)] ring-offset-1'
                    : 'hover:shadow-sm'
                }`}
              >
                <div className="flex flex-col gap-2">
                  {/* Name + badges */}
                  <div className="flex items-center gap-1.5 flex-wrap">
                    <span className="text-sm font-semibold text-[var(--color-text-primary)] truncate">
                      {job.displayName ?? job.jobName}
                    </span>
                    {statusBadge(job.status)}
                    {outcomeBadge(job.lastOutcome)}
                  </div>

                  {/* Stats row */}
                  <div className="grid grid-cols-2 gap-x-3 gap-y-0.5 text-[11px] text-[var(--color-text-secondary)]">
                    <span title={job.cronExpression ?? undefined}>{describeCron(job.cronExpression)}</span>
                    <span>Next: {formatRelativeTime(job.nextFireTimeUtc)}</span>
                    <span>Last: {formatRelativeTime(job.previousFireTimeUtc)}</span>
                    <span className="flex items-center gap-0.5">
                      <Clock className="w-2.5 h-2.5" />
                      {formatDuration(job.lastDurationMs)}
                    </span>
                  </div>

                  {/* Summary */}
                  <p className="text-[11px] text-[var(--color-text-secondary)] line-clamp-1">
                    {hasFailed && <FileWarning className="inline h-3 w-3 text-red-500 mr-0.5 -mt-0.5" />}
                    {summarizeOutcome(job)}
                  </p>

                  {/* Action bar */}
                  <div
                    className="flex items-center justify-between gap-1 pt-1 border-t border-[var(--color-border-light)]"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <div className="flex items-center gap-1">
                      {hasFailed && (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-5 px-1.5 text-[10px]"
                          onClick={() => viewAudit(job)}
                        >
                          <ScrollText className="w-2.5 h-2.5 mr-0.5" />
                          Audit
                        </Button>
                      )}
                    </div>
                    <div className="flex items-center gap-1">
                      {isPaused ? (
                        <Button size="sm" variant="ghost" className="h-5 px-1.5 text-[10px]" disabled={isBusy} onClick={() => void handleResume(job.jobName)}>
                          <Play className="w-2.5 h-2.5" />
                        </Button>
                      ) : (
                        <Button size="sm" variant="ghost" className="h-5 px-1.5 text-[10px]" disabled={isBusy || isDisabled} onClick={() => void handlePause(job.jobName)}>
                          <Pause className="w-2.5 h-2.5" />
                        </Button>
                      )}
                      <Button
                        size="sm"
                        className="h-5 px-1.5 text-[10px]"
                        disabled={isBusy || isDisabled}
                        onClick={() => void handleTrigger(job)}
                      >
                        {isBusy ? <RefreshCw className="w-2.5 h-2.5 animate-spin" /> : <Play className="w-2.5 h-2.5 mr-0.5" />}
                        {isBusy ? '...' : 'Run'}
                      </Button>
                    </div>
                  </div>
                </div>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
