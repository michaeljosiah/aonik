import { useEffect, useState } from 'react';
import { AlertCircle, AlertTriangle, Loader2 } from 'lucide-react';
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
import { Textarea } from '@/components/ui/textarea';
import type { DeleteUserResponse } from '@/types';

interface ConfirmDeleteUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  userEmail: string;
  userDisplayName?: string | null;
  onConfirm: (reason: string) => Promise<DeleteUserResponse>;
  onDeleted?: (result: DeleteUserResponse) => void;
}

/**
 * Spec 026 Part 2 — destructive deletion confirm dialog. Requires the
 * operator to type the user's email back exactly AND supply a reason of
 * at least 10 characters. Surfaces the IdP-side delete outcome on
 * success so operators see whether the IdP cleanup succeeded.
 */
export function ConfirmDeleteUserDialog({
  open,
  onOpenChange,
  userEmail,
  userDisplayName,
  onConfirm,
  onDeleted,
}: ConfirmDeleteUserDialogProps) {
  const [emailInput, setEmailInput] = useState('');
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setEmailInput('');
      setReason('');
      setError(null);
      setSubmitting(false);
    }
  }, [open]);

  const trimmedEmail = userEmail.trim().toLowerCase();
  const emailMatches = emailInput.trim().toLowerCase() === trimmedEmail;
  const reasonValid = reason.trim().length >= 10;
  const canSubmit = emailMatches && reasonValid && !submitting;

  const handleSubmit = async () => {
    if (!canSubmit) return;

    setSubmitting(true);
    setError(null);
    try {
      const result = await onConfirm(reason.trim());
      onDeleted?.(result);
      onOpenChange(false);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to delete user. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[520px]">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-destructive/10 flex items-center justify-center flex-shrink-0">
              <AlertTriangle className="w-5 h-5 text-destructive" />
            </div>
            <div>
              <DialogTitle>Permanently delete this user?</DialogTitle>
              <DialogDescription className="mt-1">
                This action cannot be undone. The user record, identity-provider
                account, and personally identifiable information in audit logs
                will all be removed. A tombstone is retained for compliance review.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          <div className="rounded-md border border-border bg-muted p-3 text-sm">
            <p className="text-muted-foreground">User</p>
            <p className="text-foreground font-medium">
              {userDisplayName ?? userEmail}
            </p>
            <p className="text-xs text-muted-foreground">{userEmail}</p>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="confirm-email">
              Type the user's email to confirm
            </Label>
            <Input
              id="confirm-email"
              type="text"
              autoComplete="off"
              value={emailInput}
              onChange={(e) => setEmailInput(e.target.value)}
              placeholder={userEmail}
              disabled={submitting}
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="delete-reason">
              Reason (≥ 10 characters)
            </Label>
            <Textarea
              id="delete-reason"
              rows={3}
              className="field-sizing-fixed"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g., GDPR erasure request received 2026-05-19"
              disabled={submitting}
            />
            <p className="text-xs text-muted-foreground">
              {reason.trim().length}/10 characters minimum. Captured on the tombstone for compliance review.
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
          <Button variant="destructive" onClick={handleSubmit} disabled={!canSubmit}>
            {submitting ? (
              <>
                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                Deleting…
              </>
            ) : (
              'Permanently delete user'
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
