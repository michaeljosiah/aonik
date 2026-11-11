import { useState } from 'react';
import { useLocation } from 'react-router-dom';
import { MailCheck } from 'lucide-react';
import { apiConfig, useAuth } from '@/auth';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

/** The invitation stays in the IdP return state until its one-shot acceptance succeeds. */
export function InvitationPage() {
  const location = useLocation();
  const { isAuthenticated, isLoading, user, login, logout, getAccessToken } = useAuth();
  const query = new URLSearchParams(location.search);
  const inviteToken = query.get('invite');
  const tenantId = query.get('tenant');
  const [busy, setBusy] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function accept() {
    if (!inviteToken || busy) return;
    setBusy(true);
    setError(null);
    try {
      const token = await getAccessToken();
      if (!token) throw new Error('Sign in with the invited email address to continue.');
      const response = await fetch(`${apiConfig.baseUrl.replace(/\/$/, '')}/identity/invite/accept`, {
        method: 'POST',
        signal: AbortSignal.timeout(30_000),
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}) },
        body: JSON.stringify({ inviteToken }),
      });
      const result = await response.json().catch(() => null) as { accepted?: boolean; failureReason?: string } | null;
      if (!response.ok || !result?.accepted) {
        if (result?.failureReason === 'invite_expired') throw new Error('This invitation has expired. Ask your administrator to send a new one.');
        throw new Error('Could not accept this invitation. Verify your invited email address and sign in again, or ask your administrator for a new invitation.');
      }
      setAccepted(true);
      // Remove the consumed capability from the address bar and browser history entry.
      window.history.replaceState({}, '', location.pathname);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not accept this invitation. Please try again.');
    } finally {
      setBusy(false);
    }
  }

  async function signIn() {
    setBusy(true);
    setError(null);
    try { await login({ returnTo: `${location.pathname}${location.search}` }); }
    catch { setError('Could not start sign-in. Please try again.'); }
    finally { setBusy(false); }
  }

  return <main className="min-h-screen bg-background flex items-center justify-center p-6">
    <Card className="w-full max-w-lg">
      <CardHeader>
        <MailCheck className="h-8 w-8 text-primary mb-3" aria-hidden="true" />
        <CardTitle>{accepted ? 'Invitation accepted' : 'Your Aonik invitation'}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        {accepted ? <p>Your account is ready. Return to the application you were invited to use and sign in with {user?.email}.</p>
          : !inviteToken ? <p>This invitation link is incomplete. Open the link from your invitation email.</p>
          : <>
            <p className="text-muted-foreground">Use the email address on your invitation. If you are new, choose Sign up on the sign-in page, then verify your email before accepting.</p>
            {isAuthenticated ? <>
              <p className="text-sm">Signed in as <strong>{user?.email}</strong></p>
              <Button onClick={() => void accept()} disabled={busy || isLoading}>{busy ? 'Accepting…' : 'Accept invitation'}</Button>
              <Button variant="ghost" onClick={() => void logout()} disabled={busy}>Use another account</Button>
            </> : <Button onClick={() => void signIn()} disabled={busy || isLoading}>Sign in or create account</Button>}
          </>}
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
      </CardContent>
    </Card>
  </main>;
}
