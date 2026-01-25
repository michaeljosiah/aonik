import { api } from '@/lib/api';
import type { CreatePartyRequest, PartyResponse } from '@/types';

export const partyService = {
  createParty: async (request: CreatePartyRequest): Promise<PartyResponse> => {
    return api.post<PartyResponse>('/parties', request);
  },
};
