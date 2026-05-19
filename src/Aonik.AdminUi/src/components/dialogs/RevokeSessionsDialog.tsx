import { useEffect, useState } from 'react';
import { ShieldAlert, Loader2 } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import type { RevokeUserSessionsResponse } from '@/types';

interface RevokeSessionsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  userEmail: string;
  onConfirm: (reason: string) => Promise<RevokeUserSessionsResponse>;
  onRevoked?: (result: RevokeUserSessionsResponse) => void;
}

/**
 * Spec 026 Part 3 — confirm dialog for the "Revoke sessions" action.
 * The reason is free-form (single-line input) and surfaced in the
 * audit log entry and on the sessions tab.
 */
export function RevokeSessionsDialog({
  open,
  onOpenChange,
  userEmail,
  onConfirm,
  onRevoked,
}: RevokeSessionsDialogProps) {
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setReason('');
      setError(null);
      setSubmitting(false);
    }
  }, [open]);

  const handleSubmit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const result = await onConfirm(reason.trim());
      onRevoked?.(result);
      onOpenChange(false);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to revoke sessions. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[500px]">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-[var(--color-warning-light)] flex items-center justify-center flex-shrink-0">
              <ShieldAlert className="w-5 h-5 text-[var(--color-warning)]" />
            </div>
            <div>
              <DialogTitle>Revoke active sessions?</DialogTitle>
              <DialogDescription className="mt-1">
                <strong>{userEmail}</strong> will be forced offline within seconds.
                Tokens issued after this point continue to work — for permanent
                ban semantics, deactivate the user instead.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          <div className="grid gap-2">
            <label htmlFor="revoke-reason" className="text-sm font-medium text-[var(--color-text-primary)]">
              Reason
            </label>
            <input
              id="revoke-reason"
              type="text"
              autoComplete="off"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-warning)] focus-visible:ring-offset-2"
              placeholder="e.g., laptop reported stolen"
              disabled={submitting}
            />
            <p className="text-xs text-[var(--color-text-tertiary)]">
              Captured on the audit log entry.
            </p>
          </div>

          {error && (
            <div className="rounded-md bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
              {error}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={submitting}>
            {submitting ? (
              <>
                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                Revoking…
              </>
            ) : (
              'Revoke sessions'
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
