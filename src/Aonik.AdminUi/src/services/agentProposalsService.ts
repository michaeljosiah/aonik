// Wraps the proposal approval pipeline (Wave 4c.2 endpoints under /ai/proposals/*).
// Read by MySpacePage to drive the dashboard ProposalCard's Apply / Review /
// Dismiss actions.

import { api } from '@/lib/api';
import type { ProposalDetailResponse } from '@/types';

export const agentProposalsService = {
  /** Returns the full proposal detail — used by the Review dialog. */
  get: (id: string): Promise<ProposalDetailResponse> =>
    api.get<ProposalDetailResponse>(`/ai/proposals/${encodeURIComponent(id)}`),

  /** Transitions a Proposed proposal to Approved; returns the updated detail. */
  approve: (id: string): Promise<ProposalDetailResponse> =>
    api.post<ProposalDetailResponse>(`/ai/proposals/${encodeURIComponent(id)}/approve`),

  /** Transitions a Proposed proposal to Rejected; surfaced as "Dismiss" in the UI. */
  dismiss: (id: string): Promise<ProposalDetailResponse> =>
    api.post<ProposalDetailResponse>(`/ai/proposals/${encodeURIComponent(id)}/dismiss`),
};
