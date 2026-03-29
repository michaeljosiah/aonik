import { useState, useCallback, useEffect, useRef } from 'react';
import { usePlaidLink } from 'react-plaid-link';
import { Button } from '@/components/ui/button';
import { Landmark, Loader2 } from 'lucide-react';
import { accountService } from '@/services/accountService';
import type { AccountLinkExchangeResponse } from '@/types';

interface PlaidLinkButtonProps {
  onSuccess: (result: AccountLinkExchangeResponse) => void;
  onError?: (error: string) => void;
  className?: string;
  children?: React.ReactNode;
}

export function PlaidLinkButton({ onSuccess, onError, className, children }: PlaidLinkButtonProps) {
  const [linkToken, setLinkToken] = useState<string | null>(null);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const openedRef = useRef(false);

  const handleOnSuccess = useCallback(
    async (publicToken: string) => {
      if (!sessionId) return;
      try {
        setLoading(true);
        const result = await accountService.exchangeSession({
          sessionId,
          temporaryCode: publicToken,
        });
        onSuccess(result);
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : 'Failed to link account';
        onError?.(message);
      } finally {
        setLoading(false);
        setLinkToken(null);
        setSessionId(null);
        openedRef.current = false;
      }
    },
    [sessionId, onSuccess, onError]
  );

  const handleOnExit = useCallback(() => {
    setLoading(false);
    setLinkToken(null);
    setSessionId(null);
    openedRef.current = false;
  }, []);

  const { open, ready } = usePlaidLink({
    token: linkToken,
    onSuccess: handleOnSuccess,
    onExit: handleOnExit,
  });

  // Open Plaid Link once the token is ready — only once per session
  useEffect(() => {
    if (linkToken && ready && !openedRef.current) {
      openedRef.current = true;
      open();
    }
  }, [linkToken, ready, open]);

  const handleClick = useCallback(async () => {
    try {
      setLoading(true);
      openedRef.current = false;
      const session = await accountService.createSession({ provider: 'Plaid' });
      setLinkToken(session.launchToken);
      setSessionId(session.sessionId);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to create link session';
      onError?.(message);
      setLoading(false);
    }
  }, [onError]);

  return (
    <Button onClick={handleClick} disabled={loading} className={className}>
      {loading ? (
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
      ) : (
        <Landmark className="mr-2 h-4 w-4" />
      )}
      {children || 'Link Bank Account'}
    </Button>
  );
}
