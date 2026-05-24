// InviteUserDialog — modal form for the Users list page.
//
// Captures email (required), display name (optional), and an optional
// role multi-select. Submits to /admin/users/invite via userService.
// On success the parent refreshes its list. The backend's
// `emailSent: false` case is surfaced as a warning so the operator
// knows the placeholder + token were created but the email didn't go
// out — they can use "Resend invite" once delivery is fixed.
//
// Spec 026 Part 1. Backend wiring already exists in
// AccessUserInviteHelper.cs; this fills the missing UI surface so an
// admin can actually initiate an invite from the Users page.

import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import * as Checkbox from '@radix-ui/react-checkbox';
import { Loader2, AlertCircle, AlertTriangle, Check, Link2 } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { userService } from '@/services/userService';
import { roleService } from '@/services/roleService';
import { messagingService } from '@/services/messagingService';
import type { AccessRoleSummary, MessagingChannelHealth } from '@/types';

interface InviteUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Fired on a successful invite so the parent can refresh its list. */
  onSuccess: () => void;
  /**
   * Optional — when supplied, the dialog renders in "link-to-existing-party"
   * mode: the invitee will be attached to this party instead of getting a
   * fresh Individual party. Used by CustomerDetailPage to invite a customer
   * contact as a platform user.
   */
  prefilledPartyId?: string | null;
  /** Human-readable label shown in the "Linking to" row. */
  prefilledPartyLabel?: string | null;
}

const fieldClassName =
  'flex h-10 w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 py-2 text-sm leading-5 text-[var(--color-form-field-text)] placeholder:text-[var(--color-form-field-placeholder)] focus-visible:outline-none focus-visible:ring-0 focus-visible:border-[var(--color-form-field-border-focus)]';

// Cheap client-side check before we bother the API. The server does
// its own validation (the FastEndpoints validator + helper assert
// the @ position is plausible) — this just stops us from hitting it
// with obvious garbage.
function isValidEmail(value: string): boolean {
  const trimmed = value.trim();
  if (trimmed.length === 0) return false;
  const at = trimmed.indexOf('@');
  if (at <= 0 || at === trimmed.length - 1) return false;
  return /\S+@\S+\.\S+/.test(trimmed);
}

export function InviteUserDialog({
  open,
  onOpenChange,
  onSuccess,
  prefilledPartyId,
  prefilledPartyLabel,
}: InviteUserDialogProps) {
  const linkingToExistingParty = Boolean(prefilledPartyId);
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [selectedRoleIds, setSelectedRoleIds] = useState<Set<string>>(new Set());

  const [roles, setRoles] = useState<AccessRoleSummary[]>([]);
  const [rolesLoading, setRolesLoading] = useState(false);
  const [rolesError, setRolesError] = useState<string | null>(null);

  const [emailHealth, setEmailHealth] = useState<MessagingChannelHealth | null>(null);
  const [emailHealthChecked, setEmailHealthChecked] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load roles + messaging health whenever the dialog opens. Cheap
  // calls — the health probe is a synchronous in-memory check on the
  // backend. We deliberately don't gate submit on the health result:
  // even with email broken, creating the placeholder is still useful
  // (the admin can hit Resend invite once delivery is fixed) and the
  // record + token are correctly persisted either way.
  useEffect(() => {
    if (!open) return;

    setRolesLoading(true);
    setRolesError(null);
    roleService
      .list({ pageNumber: 1, pageSize: 100 })
      .then((result) => setRoles(result.items))
      .catch((err: unknown) => {
        const message =
          err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '';
        setRolesError(message || 'Failed to load roles.');
      })
      .finally(() => setRolesLoading(false));

    setEmailHealthChecked(false);
    messagingService
      .health()
      .then((health) => setEmailHealth(health.email))
      .catch(() => {
        // Health probe failure is non-fatal — we don't want to block
        // the invite UI if the new endpoint isn't reachable yet. Leave
        // emailHealth null; the warning banner only fires on a
        // definitive `configured: false`.
        setEmailHealth(null);
      })
      .finally(() => setEmailHealthChecked(true));
  }, [open]);

  const isValid = useMemo(() => isValidEmail(email), [email]);

  const resetForm = () => {
    setEmail('');
    setDisplayName('');
    setSelectedRoleIds(new Set());
    setError(null);
  };

  const handleClose = (nextOpen: boolean) => {
    if (!nextOpen) resetForm();
    onOpenChange(nextOpen);
  };

  const toggleRole = (roleId: string) => {
    setSelectedRoleIds((prev) => {
      const next = new Set(prev);
      if (next.has(roleId)) next.delete(roleId);
      else next.add(roleId);
      return next;
    });
  };

  const handleSubmit = async () => {
    if (!isValid || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const trimmedDisplayName = displayName.trim();
      const response = await userService.invite({
        email: email.trim(),
        displayName: trimmedDisplayName.length > 0 ? trimmedDisplayName : null,
        roleIds: selectedRoleIds.size > 0 ? Array.from(selectedRoleIds) : null,
        partyId: prefilledPartyId ?? null,
      });

      // Backend creates the placeholder even when the email send fails
      // (the operator can hit "Resend invite" on the row afterwards).
      // Surface both outcomes clearly.
      if (response.emailSent) {
        toast.success(`Invite sent to ${response.email}.`);
      } else {
        toast.warning(
          `Invite created for ${response.email}, but the email did not send. ` +
            `Use "Resend invite" once the delivery issue is resolved.`,
        );
      }

      onSuccess();
      handleClose(false);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to invite user.';
      const userMessage =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      const finalMessage = userMessage || message;
      setError(finalMessage);
      toast.error(finalMessage);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>
            {linkingToExistingParty ? 'Invite as user' : 'Invite user'}
          </DialogTitle>
          <DialogDescription>
            {linkingToExistingParty
              ? 'Send an invitation email. The new user account will be linked to the existing party record shown below.'
              : "Send an invitation email to a new teammate. They'll receive a link to sign in and join this tenant."}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {/* Email-provider warning — shown when the probe explicitly
              reports the channel is not configured. We don't block
              submit because the placeholder + invite token are still
              useful (admin can Resend invite once delivery is fixed). */}
          {emailHealthChecked && emailHealth && !emailHealth.configured && (
            <div className="flex items-start gap-2 rounded border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-xs text-[var(--color-warning)]">
              <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              <div className="space-y-1">
                <div className="font-medium">Email delivery is not configured.</div>
                <div>
                  {emailHealth.reason ?? 'No email provider is wired up.'} The invite
                  will be created and a one-time link generated, but no email will be
                  sent. Configure an email provider (e.g. Azure Communication Services
                  or SendGrid) and use <span className="font-medium">Resend invite</span>{' '}
                  to deliver it once ready.
                </div>
              </div>
            </div>
          )}

          {/* Linking-to badge — only when invoked with a prefilled party */}
          {linkingToExistingParty && (
            <div className="flex items-center gap-2 rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2 text-xs">
              <Link2 className="h-3.5 w-3.5 text-[var(--color-text-tertiary)] shrink-0" />
              <span className="text-[var(--color-text-tertiary)]">Linking to</span>
              <span className="font-medium text-[var(--color-text-primary)] truncate">
                {prefilledPartyLabel ?? prefilledPartyId}
              </span>
            </div>
          )}

          {/* Email — required */}
          <div className="space-y-1.5">
            <label htmlFor="invite-email" className="text-xs font-medium text-[var(--color-text-primary)]">
              Email address <span className="text-[var(--color-danger)]">*</span>
            </label>
            <input
              id="invite-email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="teammate@example.com"
              className={fieldClassName}
              disabled={submitting}
              autoFocus
            />
          </div>

          {/* Display name — optional */}
          <div className="space-y-1.5">
            <label htmlFor="invite-display-name" className="text-xs font-medium text-[var(--color-text-primary)]">
              Display name <span className="text-[var(--color-text-tertiary)]">(optional)</span>
            </label>
            <input
              id="invite-display-name"
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="Jane Doe"
              className={fieldClassName}
              disabled={submitting}
            />
            <p className="text-[11px] text-[var(--color-text-tertiary)]">
              If blank, the email's local part is used in the invitation copy.
            </p>
          </div>

          {/* Roles — optional, multi-select */}
          <div className="space-y-1.5">
            <label className="text-xs font-medium text-[var(--color-text-primary)]">
              Roles <span className="text-[var(--color-text-tertiary)]">(optional)</span>
            </label>
            {rolesLoading ? (
              <div className="flex items-center gap-2 text-xs text-[var(--color-text-tertiary)]">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Loading roles…
              </div>
            ) : rolesError ? (
              <div className="flex items-center gap-2 text-xs text-[var(--color-danger)]">
                <AlertCircle className="h-3.5 w-3.5" />
                {rolesError}
              </div>
            ) : roles.length === 0 ? (
              <p className="text-xs text-[var(--color-text-tertiary)]">
                No roles defined in this tenant yet. The user can still be invited and roles assigned later.
              </p>
            ) : (
              <div className="max-h-[180px] overflow-y-auto rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-2 space-y-1">
                {roles.map((role) => {
                  const checked = selectedRoleIds.has(role.roleId);
                  return (
                    <label
                      key={role.roleId}
                      className="flex cursor-pointer items-start gap-2 rounded px-2 py-1.5 hover:bg-[var(--color-surface)]"
                    >
                      <Checkbox.Root
                        checked={checked}
                        onCheckedChange={() => toggleRole(role.roleId)}
                        disabled={submitting}
                        className="mt-0.5 w-4 h-4 rounded border border-[var(--color-border)] bg-[var(--color-surface)] flex items-center justify-center data-[state=checked]:bg-[var(--color-brand-primary)] data-[state=checked]:border-[var(--color-brand-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:ring-offset-1 shrink-0"
                      >
                        <Checkbox.Indicator>
                          <Check className="w-3 h-3 text-white" />
                        </Checkbox.Indicator>
                      </Checkbox.Root>
                      <div className="min-w-0 flex-1">
                        <div className="text-sm text-[var(--color-text-primary)]">{role.name}</div>
                        {role.description && (
                          <div className="text-[11px] text-[var(--color-text-tertiary)]">
                            {role.description}
                          </div>
                        )}
                      </div>
                    </label>
                  );
                })}
              </div>
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
                Sending…
              </>
            ) : (
              'Send invite'
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
