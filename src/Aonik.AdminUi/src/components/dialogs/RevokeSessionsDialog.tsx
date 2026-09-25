import { useEffect, useState } from 'react';
import { AlertCircle, ShieldAlert, Loader2 } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
            <div className="w-10 h-10 rounded-full bg-warning-subtle flex items-center justify-center flex-shrink-0">
              <ShieldAlert className="w-5 h-5 text-warning" />
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
            <Label htmlFor="revoke-reason">
              Reason
            </Label>
            <Input
              id="revoke-reason"
              type="text"
              autoComplete="off"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g., laptop reported stolen"
              disabled={submitting}
            />
            <p className="text-xs text-muted-foreground">
              Captured on the audit log entry.
            </p>
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
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
