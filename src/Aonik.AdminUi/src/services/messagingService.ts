import { api } from '@/lib/api';
import type { MessagingHealth } from '@/types';

/**
 * Read-only snapshot of the platform's outbound messaging
 * configuration. The Admin UI calls this before showing flows that
 * depend on email/SMS dispatch (e.g. the user-invite dialog) so the
 * operator can be warned up-front rather than discovering after
 * submit that no email actually went out.
 */
export const messagingService = {
  health: async (): Promise<MessagingHealth> => {
    return api.get<MessagingHealth>('/admin/messaging/health');
  },
};
