import { useState, useEffect, useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Play, Pause, RefreshCw, ArrowLeft, AlertTriangle, Clock, ScrollText, Plus, X, Save } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  jobService,
  type ScheduledJobDetailResponse,
  type ScheduledJobRunSummary,
  type ScheduledJobCommandSummary,
  buildJobCommandAuditUrl,
  buildJobRunAuditUrl,
} from '@/services/jobService';
import type { PagedResult } from '@/types';
import { CronScheduleDisplay } from '@/components/cron-schedule-editor';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';

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

  // ── Agent Names Configuration (StaleSessionDetector only) ──
  const [agentNames, setAgentNames] = useState<string[]>([]);
  const [savedAgentNames, setSavedAgentNames] = useState<string[]>([]);
  const [newAgentName, setNewAgentName] = useState('');
  const [configSaving, setConfigSaving] = useState(false);
  const isStaleSessionDetector = jobName === 'StaleSessionDetectorJob';
  const configIsDirty = JSON.stringify(agentNames) !== JSON.stringify(savedAgentNames);

  const loadConfiguration = useCallback(async () => {
    if (!jobName || !isStaleSessionDetector) return;
    try {
      const result = await jobService.getJobConfiguration(jobName);
      if (result.configurationJson) {
        const parsed = JSON.parse(result.configurationJson) as { agentNames?: string[] };
        const names = parsed.agentNames ?? [];
        setAgentNames(names);
        setSavedAgentNames(names);
      }
    } catch (err) {
      console.error('Failed to load job configuration:', err);
    }
  }, [jobName, isStaleSessionDetector]);

  const addAgentName = () => {
    const name = newAgentName.trim();
    if (!name) return;
    if (agentNames.includes(name)) {
      toast.error('Agent already in the list.');
      return;
    }
    setAgentNames([...agentNames, name]);
    setNewAgentName('');
  };

  const removeAgentName = (name: string) => {
    setAgentNames(agentNames.filter((n) => n !== name));
  };

  const saveConfiguration = async () => {
    if (!jobName) return;
    setConfigSaving(true);
    try {
      const configJson = JSON.stringify({ agentNames });
      await jobService.updateJobConfiguration(jobName, configJson);
      setSavedAgentNames([...agentNames]);
      toast.success('Agent names configuration saved.');
    } catch {
      toast.error('Failed to save configuration.');
    } finally {
      setConfigSaving(false);
    }
  };

  const resetConfiguration = () => {
    setAgentNames([...savedAgentNames]);
    setNewAgentName('');
  };

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
    void loadConfiguration();

    const interval = setInterval(() => {
      void loadDetail();
      void loadRuns(runsPage);
      void loadCommands(commandsPage);
    }, 30_000);
    return () => clearInterval(interval);
  }, [loadDetail, loadRuns, loadCommands, loadConfiguration, runsPage, commandsPage]);

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
      await loadRuns(1);
      await loadCommands(1);
      setRunsPage(1);
      setCommandsPage(1);
    } catch {
      toast.error(`Failed to ${action} job.`);
    } finally {
      setActionInProgress(false);
    }
  };
  if (loading) {
    return <PageLoadingScreen message="Loading job" />;
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
  const latestRun = runs?.items[0] ?? null;
  const latestCommand = commands?.items[0] ?? null;

  return (
    <div className="h-full overflow-auto p-6">

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

      <div className="grid gap-4 mb-6 lg:grid-cols-3">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">Latest Run</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {latestRun ? (
              <>
                <div className="flex items-center justify-between gap-2">
                  {statusBadge(latestRun.outcome)}
                  <span className="text-[var(--color-text-tertiary)]">{formatDateTime(latestRun.firedAtUtc)}</span>
                </div>
                <div className="flex items-center gap-2 text-[var(--color-text-secondary)]">
                  <Clock className="w-4 h-4" />
                  {formatDuration(latestRun.durationMs)}
                </div>
                <p className="text-[var(--color-text-secondary)] break-words">
                  {latestRun.errorMessage ?? `Triggered by ${latestRun.triggeredBy}.`}
                </p>
                <Button asChild size="sm" variant="outline">
                  <Link to={buildJobRunAuditUrl(latestRun)}>Open run audit</Link>
                </Button>
              </>
            ) : (
              <p className="text-[var(--color-text-tertiary)]">No runs recorded yet.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">Latest Command</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {latestCommand ? (
              <>
                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">{latestCommand.commandType}</Badge>
                    {statusBadge(latestCommand.status)}
                  </div>
                  <span className="text-[var(--color-text-tertiary)]">{formatDateTime(latestCommand.createdAt)}</span>
                </div>
                <p className="text-[var(--color-text-secondary)] break-words">
                  {latestCommand.resultMessage ?? 'Awaiting worker processing.'}
                </p>
                <Button asChild size="sm" variant="outline">
                  <Link to={buildJobCommandAuditUrl(latestCommand)}>Open command audit</Link>
                </Button>
              </>
            ) : (
              <p className="text-[var(--color-text-tertiary)]">No commands recorded yet.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">Current Projection</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm text-[var(--color-text-secondary)]">
            <div><span className="text-[var(--color-text-tertiary)]">Next:</span> {formatRelativeTime(detail.nextFireTimeUtc)}</div>
            <div><span className="text-[var(--color-text-tertiary)]">Last:</span> {formatRelativeTime(detail.previousFireTimeUtc)}</div>
            <div><span className="text-[var(--color-text-tertiary)]">Duration:</span> {detail.lastDurationMs != null ? formatDuration(detail.lastDurationMs) : '--'}</div>
            {detail.lastOutcomeSummary && (
              <div className="rounded-sm bg-[var(--color-surface-inset)] p-3 text-xs whitespace-pre-wrap break-words text-[var(--color-text-secondary)]">
                {detail.lastOutcomeSummary}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="runs">Run History</TabsTrigger>
          <TabsTrigger value="commands">Commands</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          {isStaleSessionDetector && (
            <Card className="mb-4">
              <CardHeader>
                <CardTitle>Conversation Summarisation</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label className="text-sm">Agent Names</Label>
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    Only conversations with these agents will be summarised. If empty, no conversations are summarised.
                  </p>
                  <div className="flex items-center gap-2">
                    <Input
                      value={newAgentName}
                      onChange={(e) => setNewAgentName(e.target.value)}
                      onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addAgentName(); } }}
                      placeholder="Enter agent name..."
                      className="font-mono text-xs flex-1"
                    />
                    <Button variant="outline" size="sm" onClick={addAgentName} disabled={!newAgentName.trim()}>
                      <Plus className="w-3.5 h-3.5 mr-1" />
                      Add
                    </Button>
                  </div>
                  {agentNames.length > 0 ? (
                    <div className="flex flex-wrap gap-2 pt-1">
                      {agentNames.map((name) => (
                        <span
                          key={name}
                          className="inline-flex items-center gap-1 text-xs px-2.5 py-1 rounded-full bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] border border-[var(--color-border-light)] font-mono"
                        >
                          {name}
                          <button
                            type="button"
                            onClick={() => removeAgentName(name)}
                            className="ml-0.5 rounded-full hover:bg-[var(--color-error-light)] hover:text-[var(--color-error)] p-0.5 transition-colors"
                          >
                            <X className="w-3 h-3" />
                          </button>
                        </span>
                      ))}
                    </div>
                  ) : (
                    <p className="text-xs text-amber-600 dark:text-amber-400">No agents configured — summarisation is disabled.</p>
                  )}
                </div>
                {configIsDirty && (
                  <div className="flex items-center gap-2 pt-1">
                    <Button size="sm" onClick={() => void saveConfiguration()} disabled={configSaving}>
                      <Save className="w-3.5 h-3.5 mr-1" />
                      {configSaving ? 'Saving...' : 'Save'}
                    </Button>
                    <Button size="sm" variant="secondary" onClick={resetConfiguration} disabled={configSaving}>
                      Reset
                    </Button>
                    <span className="text-xs text-[var(--color-text-tertiary)]">Unsaved changes</span>
                  </div>
                )}
              </CardContent>
            </Card>
          )}
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
                  <dt className="text-[var(--color-text-tertiary)] mb-1">Schedule</dt>
                  <dd><CronScheduleDisplay cron={detail.cronExpression} /></dd>
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
                    <dt className="text-[var(--color-text-tertiary)]">Latest Output</dt>
                    <dd className="text-xs bg-[var(--color-surface-inset)] p-3 rounded mt-1 whitespace-pre-wrap break-words text-[var(--color-text-secondary)]">{detail.lastOutcomeSummary}</dd>
                  </div>
                ) : null}
                {detail.lastOutcome?.toLowerCase() === 'failed' && (
                  <div className="col-span-2 flex items-center gap-2 rounded-sm border border-red-200 bg-red-50/60 p-3 text-sm text-red-700 dark:border-red-900/30 dark:bg-red-950/10 dark:text-red-300">
                    <AlertTriangle className="h-4 w-4" />
                    <span>Failure details are persisted in the audit trail.</span>
                    {latestRun && (
                      <Button asChild size="sm" variant="outline" className="ml-auto">
                        <Link to={buildJobRunAuditUrl(latestRun)}>Open latest failure audit</Link>
                      </Button>
                    )}
                  </div>
                )}
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
                <div className="space-y-3">
                  {runs.items.map((run) => (
                    <div key={run.id} className="rounded-md border border-[var(--color-border-light)] p-4">
                      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                        <div className="space-y-2 min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            {statusBadge(run.outcome)}
                            <Badge variant="outline">{run.triggeredBy}</Badge>
                            <span className="text-xs text-[var(--color-text-tertiary)]">{formatDateTime(run.firedAtUtc)}</span>
                          </div>
                          <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                            <Clock className="w-4 h-4" />
                            {formatDuration(run.durationMs)}
                          </div>
                          <p className="text-sm text-[var(--color-text-secondary)] break-words">
                            {run.errorMessage ?? 'Run completed without a recorded error.'}
                          </p>
                          <div className="flex flex-wrap gap-2">
                            <Button asChild size="sm" variant="outline">
                              <Link to={buildJobRunAuditUrl(run)}>
                                <ScrollText className="h-3.5 w-3.5" />
                                Open audit
                              </Link>
                            </Button>
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}

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
                </div>
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
                <div className="space-y-3">
                  {commands.items.map((cmd) => (
                    <div key={cmd.id} className="rounded-md border border-[var(--color-border-light)] p-4">
                      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                        <div className="space-y-2 min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <Badge variant="outline">{cmd.commandType}</Badge>
                            {statusBadge(cmd.status)}
                            <span className="text-xs text-[var(--color-text-tertiary)]">Requested {formatDateTime(cmd.createdAt)}</span>
                          </div>
                          <p className="text-sm text-[var(--color-text-secondary)] break-words">
                            {cmd.resultMessage ?? 'Awaiting worker processing.'}
                          </p>
                          <div className="flex flex-wrap items-center gap-2 text-xs text-[var(--color-text-tertiary)]">
                            <span>Processed: {formatDateTime(cmd.processedAtUtc)}</span>
                          </div>
                          <Button asChild size="sm" variant="outline">
                            <Link to={buildJobCommandAuditUrl(cmd)}>
                              <ScrollText className="h-3.5 w-3.5" />
                              Open audit
                            </Link>
                          </Button>
                        </div>
                      </div>
                    </div>
                  ))}

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
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
