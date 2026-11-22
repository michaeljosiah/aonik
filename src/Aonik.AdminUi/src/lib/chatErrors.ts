export function describeChatError(error: string): { title: string; message: string; code: string } {
  if (/insufficient_quota|credit_balance_exhausted/i.test(error)) return {
    title: 'AI credits unavailable', code: 'insufficient_quota',
    message: 'Your workspace’s AI provider has run out of API credits. A workspace administrator needs to review provider billing before Aonik can respond.',
  };
  if (/429|rate_limit|too many requests/i.test(error)) return {
    title: 'AI is busy', code: 'rate_limit', message: 'The AI provider is handling too many requests. Wait a moment, then try again.',
  };
  if (/invalid_api_key|authentication_error|HTTP\s*40[13]/i.test(error)) return {
    title: 'AI setup needs attention', code: 'provider_authentication', message: 'The AI provider could not verify this workspace’s configuration. Ask a workspace administrator to review AI settings.',
  };
  if (/timeout|timed out|network|failed to fetch/i.test(error)) return {
    title: 'AI could not be reached', code: 'provider_unavailable', message: 'The AI service did not respond. Check your connection and try again in a moment.',
  };
  return { title: 'AI response unavailable', code: 'request_failed', message: 'Your request could not be completed. Try again, or ask a workspace administrator for help if the problem continues.' };
}
