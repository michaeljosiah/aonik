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
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { NativeSelect } from '@/components/ui/native-select';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { taskService } from '@/services/taskService';
import { userService } from '@/services/userService';
import type { AccessUserSummary } from '@/types';

interface NewTaskDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Fired on a successful create so the parent can refresh its list. */
  onSuccess: () => void;
}

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
            <label htmlFor="task-title" className="text-xs font-medium text-foreground">
              Title <span className="text-destructive">*</span>
            </label>
            <Input
              id="task-title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Insurance renewal coming up"
              disabled={submitting}
              autoFocus
            />
          </div>

          {/* Target user */}
          <div className="space-y-1.5">
            <label htmlFor="task-user" className="text-xs font-medium text-foreground">
              Notify user <span className="text-destructive">*</span>
            </label>
            <NativeSelect
              id="task-user"
              value={targetUserId}
              onChange={(e) => setTargetUserId(e.target.value)}
              disabled={submitting || usersLoading}
            >
              {usersLoading && <option value="">Loading users…</option>}
              {!usersLoading && users.length === 0 && <option value="">No users found</option>}
              {users.map((u) => (
                <option key={u.userId} value={u.userId}>
                  {u.displayName || u.email}
                </option>
              ))}
            </NativeSelect>
          </div>

          {/* Message body */}
          <div className="space-y-1.5">
            <label htmlFor="task-body" className="text-xs font-medium text-foreground">
              Message <span className="text-destructive">*</span>
            </label>
            <Textarea
              id="task-body"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              placeholder="Your policy is due soon."
              rows={2}
              disabled={submitting}
            />
          </div>

          {/* Severity */}
          <div className="space-y-1.5">
            <label htmlFor="task-severity" className="text-xs font-medium text-foreground">
              Severity
            </label>
            <Select value={severity} onValueChange={setSeverity} disabled={submitting}>
              <SelectTrigger id="task-severity">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Info">Info</SelectItem>
                <SelectItem value="Success">Success</SelectItem>
                <SelectItem value="Warning">Warning</SelectItem>
                <SelectItem value="Error">Error</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Schedule */}
          <div className="space-y-1.5">
            <label htmlFor="task-schedule" className="text-xs font-medium text-foreground">
              Schedule
            </label>
            <Select
              value={scheduleMode}
              onValueChange={(value) => setScheduleMode(value as ScheduleMode)}
              disabled={submitting}
            >
              <SelectTrigger id="task-schedule">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="oneoff">One-off</SelectItem>
                <SelectItem value="recurring">Recurring (cron)</SelectItem>
              </SelectContent>
            </Select>

            {scheduleMode === 'oneoff' ? (
              <>
                <Input
                  type="datetime-local"
                  value={runAtLocal}
                  onChange={(e) => setRunAtLocal(e.target.value)}
                  disabled={submitting}
                />
                <p className="text-xs text-muted-foreground">
                  Leave blank to fire on the next dispatch sweep (within a minute).
                </p>
              </>
            ) : (
              <>
                <Input
                  type="text"
                  value={cron}
                  onChange={(e) => setCron(e.target.value)}
                  placeholder="0 * * * * ?"
                  className="font-mono"
                  disabled={submitting}
                />
                <p className="text-xs text-muted-foreground">
                  Quartz cron (6-field, with seconds). Default fires every minute.
                </p>
              </>
            )}
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
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
