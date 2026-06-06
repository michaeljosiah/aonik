import { useCallback, useEffect, useState } from 'react';
import { ListChecks, Plus, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { taskService, type TaskItem } from '@/services/taskService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { NewTaskDialog } from './NewTaskDialog';

const TERMINAL_STATUSES = new Set(['Completed', 'Cancelled', 'Failed']);

function formatDateTime(value: string | null): string {
  if (!value) return '--';
  const date = new Date(value);
  return date.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function statusBadge(status: string) {
  const styles: Record<string, string> = {
    Scheduled: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
    InProgress: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
    Completed: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
    Paused: 'bg-slate-100 text-slate-700 dark:bg-slate-800/50 dark:text-slate-300',
    Cancelled: 'bg-slate-100 text-slate-700 dark:bg-slate-800/50 dark:text-slate-300',
    Failed: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
  };
  return <Badge className={styles[status] ?? 'bg-blue-100 text-blue-700'}>{status}</Badge>;
}

export function TasksPage() {
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const loadTasks = useCallback(async () => {
    try {
      const result = await taskService.list();
      setTasks(result);
    } catch (error) {
      console.error('Failed to load tasks:', error);
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, []);

  useEffect(() => {
    void loadTasks();
    const interval = setInterval(() => {
      void loadTasks();
    }, 30_000);

    return () => clearInterval(interval);
  }, [loadTasks]);

  const runAction = useCallback(
    async (id: string, action: (id: string) => Promise<TaskItem>) => {
      setBusyId(id);
      try {
        await action(id);
        await loadTasks();
      } catch (error) {
        console.error('Task action failed:', error);
      } finally {
        setBusyId(null);
      }
    },
    [loadTasks],
  );

  if (initialLoad) {
    return <PageLoadingScreen message="Loading tasks" />;
  }

  return (
    <div className="h-full overflow-auto p-6">
      <div className="mb-6 flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Tasks</h1>
          <p className="text-[var(--color-text-secondary)]">
            Scheduled units of future work — reminders, scheduled actions, and agent jobs — fired by the
            once-a-minute dispatcher.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button className="rounded-sm" onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New Task
          </Button>
          <Button variant="secondary" className="rounded-sm" onClick={() => void loadTasks()} disabled={loading}>
            <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      <NewTaskDialog open={createOpen} onOpenChange={setCreateOpen} onSuccess={() => void loadTasks()} />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ListChecks className="h-5 w-5 text-[var(--color-brand-primary)]" />
            Scheduled Tasks
          </CardTitle>
          <CardDescription>
            Each row is a durable WorkItem. High-risk actions never run here — they raise a proposal for approval.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && tasks.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">Loading tasks...</p>
          ) : tasks.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">No tasks have been scheduled yet.</p>
          ) : (
            <div className="space-y-3">
              {tasks.map((task) => {
                const isTerminal = TERMINAL_STATUSES.has(task.status);
                const isBusy = busyId === task.id;
                return (
                  <div
                    key={task.id}
                    className="w-full rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 shadow-sm"
                  >
                    <div className="mb-2 flex items-start justify-between gap-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">{task.title}</h2>
                          {statusBadge(task.status)}
                          <Badge variant="outline" className="text-xs">{task.kind}</Badge>
                        </div>
                        <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                          {task.actionType} · {task.scheduleType}
                          {task.recurrenceCron ? ` (${task.recurrenceCron})` : ''} · next run {formatDateTime(task.nextRunAtUtc)} · runs {task.runCount}
                          {task.maxRuns != null ? `/${task.maxRuns}` : ''}
                        </p>
                      </div>

                      <div className="flex shrink-0 items-center gap-2">
                        {task.status === 'Scheduled' && (
                          <Button
                            variant="secondary"
                            className="rounded-sm"
                            disabled={isBusy}
                            onClick={() => void runAction(task.id, taskService.pause)}
                          >
                            Pause
                          </Button>
                        )}
                        {task.status === 'Paused' && (
                          <Button
                            variant="secondary"
                            className="rounded-sm"
                            disabled={isBusy}
                            onClick={() => void runAction(task.id, taskService.resume)}
                          >
                            Resume
                          </Button>
                        )}
                        {!isTerminal && (
                          <Button
                            variant="destructive"
                            className="rounded-sm"
                            disabled={isBusy}
                            onClick={() => void runAction(task.id, taskService.cancel)}
                          >
                            Cancel
                          </Button>
                        )}
                      </div>
                    </div>

                    {task.description && (
                      <p className="text-sm text-[var(--color-text-secondary)]">{task.description}</p>
                    )}

                    {task.lastError && (
                      <p className="mt-2 text-xs text-red-600 dark:text-red-400">Last error: {task.lastError}</p>
                    )}

                    <div className="mt-2 flex flex-wrap gap-2">
                      {task.subjectType && (
                        <Badge variant="outline" className="text-xs">
                          {task.subjectType}
                        </Badge>
                      )}
                      <Badge variant="outline" className="text-xs">{task.sourceModule}</Badge>
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
