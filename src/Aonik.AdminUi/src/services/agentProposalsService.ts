// Wraps the proposal approval pipeline (Wave 4c.2 endpoints under /ai/proposals/*).
// Read by MySpacePage's ProposalCard and the Approvals queue (Wave 6).

import { api } from '@/lib/api';
import type {
  ListProposalsResponse,
  ProposalDetailResponse,
} from '@/types';

export interface ListProposalsParams {
  proposalType?: string;
  agentDomain?: string;
  riskTier?: string;
  take?: number;
}

export const agentProposalsService = {
  /** Lists pending proposals for the Approvals queue, filterable. */
  list: async (params: ListProposalsParams = {}): Promise<ListProposalsResponse> => {
    const query = new URLSearchParams();
    if (params.proposalType) query.append('proposalType', params.proposalType);
    if (params.agentDomain) query.append('agentDomain', params.agentDomain);
    if (params.riskTier) query.append('riskTier', params.riskTier);
    if (params.take) query.append('take', params.take.toString());
    const qs = query.toString();
    return api.get<ListProposalsResponse>(`/ai/proposals${qs ? `?${qs}` : ''}`);
  },

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
