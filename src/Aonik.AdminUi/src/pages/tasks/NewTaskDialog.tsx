// NewTaskDialog — modal form for the Tasks page (Spec 034).
//
// Lets an admin schedule a notify_user task: pick a target user, a message,
// and a schedule (one-off at a time, or recurring via cron). Submits to
// POST /tasks via taskService.create. On success the parent refreshes its list.

import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Loader2, AlertCircle } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { taskService } from '@/services/taskService';
import { userService } from '@/services/userService';
import type { AccessUserSummary } from '@/types';

interface NewTaskDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Fired on a successful create so the parent can refresh its list. */
  onSuccess: () => void;
}

const fieldClassName =
  'flex h-10 w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 py-2 text-sm leading-5 text-[var(--color-form-field-text)] placeholder:text-[var(--color-form-field-placeholder)] focus-visible:outline-none focus-visible:ring-0 focus-visible:border-[var(--color-form-field-border-focus)]';

type ScheduleMode = 'oneoff' | 'recurring';

export function NewTaskDialog({ open, onOpenChange, onSuccess }: NewTaskDialogProps) {
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [severity, setSeverity] = useState('Warning');
  const [targetUserId, setTargetUserId] = useState('');
  const [scheduleMode, setScheduleMode] = useState<ScheduleMode>('oneoff');
  const [runAtLocal, setRunAtLocal] = useState(''); // datetime-local; empty = now
  const [cron, setCron] = useState('0 * * * * ?');

  const [users, setUsers] = useState<AccessUserSummary[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setUsersLoading(true);
    userService
      .list({ pageNumber: 1, pageSize: 100 })
      .then((result) => {
        setUsers(result.items);
        // Default the target to the first user so the form is submittable out of the box.
        setTargetUserId((current) => current || result.items[0]?.userId || '');
      })
      .catch(() => setUsers([]))
      .finally(() => setUsersLoading(false));
  }, [open]);

  const isValid = useMemo(
    () =>
      title.trim().length > 0 &&
      body.trim().length > 0 &&
      targetUserId.length > 0 &&
      (scheduleMode === 'oneoff' || cron.trim().length > 0),
    [title, body, targetUserId, scheduleMode, cron],
  );

  const resetForm = () => {
    setTitle('');
    setBody('');
    setSeverity('Warning');
    setScheduleMode('oneoff');
    setRunAtLocal('');
    setCron('0 * * * * ?');
    setError(null);
  };

  const handleClose = (nextOpen: boolean) => {
    if (!nextOpen) resetForm();
    onOpenChange(nextOpen);
  };

  const handleSubmit = async () => {
    if (!isValid || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const runAtUtc =
        scheduleMode === 'oneoff'
          ? (runAtLocal ? new Date(runAtLocal).toISOString() : new Date().toISOString())
          : null;

      await taskService.create({
        title: title.trim(),
        kind: 'Reminder',
        actionType: 'notify_user',
        actionPayloadJson: JSON.stringify({
          userId: targetUserId,
          title: title.trim(),
          body: body.trim(),
          severity,
        }),
        assigneeType: 'User',
        assigneeId: targetUserId,
        runAtUtc,
        recurrenceCron: scheduleMode === 'recurring' ? cron.trim() : null,
        sourceModule: 'AdminUi',
      });

      toast.success('Task scheduled.');
      onSuccess();
      handleClose(false);
    } catch (err: unknown) {
      const userMessage =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      const message = userMessage || (err instanceof Error ? err.message : 'Failed to schedule task.');
      setError(message);
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>New task</DialogTitle>
          <DialogDescription>
            Schedule a reminder notification for a user. It fires from the once-a-minute dispatcher.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {/* Title */}
          <div className="space-y-1.5">
            <label htmlFor="task-title" className="text-xs font-medium text-[var(--color-text-primary)]">
              Title <span className="text-[var(--color-danger)]">*</span>
            </label>
            <input
              id="task-title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Insurance renewal coming up"
              className={fieldClassName}
              disabled={submitting}
              autoFocus
            />
          </div>

          {/* Target user */}
          <div className="space-y-1.5">
            <label htmlFor="task-user" className="text-xs font-medium text-[var(--color-text-primary)]">
              Notify user <span className="text-[var(--color-danger)]">*</span>
            </label>
            <select
              id="task-user"
              value={targetUserId}
              onChange={(e) => setTargetUserId(e.target.value)}
              className={fieldClassName}
              disabled={submitting || usersLoading}
            >
              {usersLoading && <option value="">Loading users…</option>}
              {!usersLoading && users.length === 0 && <option value="">No users found</option>}
              {users.map((u) => (
                <option key={u.userId} value={u.userId}>
                  {u.displayName || u.email}
                </option>
              ))}
            </select>
          </div>

          {/* Message body */}
          <div className="space-y-1.5">
            <label htmlFor="task-body" className="text-xs font-medium text-[var(--color-text-primary)]">
              Message <span className="text-[var(--color-danger)]">*</span>
            </label>
            <textarea
              id="task-body"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              placeholder="Your policy is due soon."
              rows={2}
              className={`${fieldClassName} h-auto`}
              disabled={submitting}
            />
          </div>

          {/* Severity */}
          <div className="space-y-1.5">
            <label htmlFor="task-severity" className="text-xs font-medium text-[var(--color-text-primary)]">
              Severity
            </label>
            <select
              id="task-severity"
              value={severity}
              onChange={(e) => setSeverity(e.target.value)}
              className={fieldClassName}
              disabled={submitting}
            >
              <option value="Info">Info</option>
              <option value="Success">Success</option>
              <option value="Warning">Warning</option>
              <option value="Error">Error</option>
            </select>
          </div>

          {/* Schedule */}
          <div className="space-y-1.5">
            <label htmlFor="task-schedule" className="text-xs font-medium text-[var(--color-text-primary)]">
              Schedule
            </label>
            <select
              id="task-schedule"
              value={scheduleMode}
              onChange={(e) => setScheduleMode(e.target.value as ScheduleMode)}
              className={fieldClassName}
              disabled={submitting}
            >
              <option value="oneoff">One-off</option>
              <option value="recurring">Recurring (cron)</option>
            </select>

            {scheduleMode === 'oneoff' ? (
              <>
                <input
                  type="datetime-local"
                  value={runAtLocal}
                  onChange={(e) => setRunAtLocal(e.target.value)}
                  className={fieldClassName}
                  disabled={submitting}
                />
                <p className="text-[11px] text-[var(--color-text-tertiary)]">
                  Leave blank to fire on the next dispatch sweep (within a minute).
                </p>
              </>
            ) : (
              <>
                <input
                  type="text"
                  value={cron}
                  onChange={(e) => setCron(e.target.value)}
                  placeholder="0 * * * * ?"
                  className={fieldClassName}
                  disabled={submitting}
                />
                <p className="text-[11px] text-[var(--color-text-tertiary)]">
                  Quartz cron (6-field, with seconds). Default fires every minute.
                </p>
              </>
            )}
          </div>

          {error && (
            <div className="flex items-start gap-2 rounded border border-[var(--color-danger)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-danger)]">
              <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              <span>{error}</span>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleClose(false)} disabled={submitting}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={!isValid || submitting}>
            {submitting ? (
              <>
                <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                Scheduling…
              </>
            ) : (
              'Schedule task'
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
