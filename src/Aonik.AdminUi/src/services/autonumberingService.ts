import { api } from '@/lib/api';
import type {
  AutonumberProfile,
  UpsertAutonumberProfileRequest,
  GenerateAutonumberRequest,
  GenerateAutonumberResponse,
} from '@/types';

export const autonumberingService = {
  // List all autonumbering profiles
  list: async (): Promise<AutonumberProfile[]> => {
    return api.get<AutonumberProfile[]>('/admin/autonumbering/profiles');
  },

  // Get a specific autonumbering profile by entity type
  get: async (entityType: string): Promise<AutonumberProfile> => {
    return api.get<AutonumberProfile>(`/admin/autonumbering/profiles/${entityType}`);
  },

  // Create or update an autonumbering profile
  upsert: async (request: UpsertAutonumberProfileRequest): Promise<AutonumberProfile> => {
    return api.put<AutonumberProfile>('/admin/autonumbering/profiles', request);
  },

  // Generate a test autonumber reference
  generate: async (request: GenerateAutonumberRequest): Promise<GenerateAutonumberResponse> => {
    return api.post<GenerateAutonumberResponse>('/admin/autonumbering/generate', request);
  },
};
