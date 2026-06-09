import { api } from '@/lib/api';
import type {
  BillerImportSourcesResponse,
  BillerImportPreviewRequest,
  BillerImportPreviewResponse,
  BillerImportRequest,
  BillerImportSummaryResponse,
} from '@/types';

// Partner biller catalogue import (Spec 040). All routes are tenant-scoped — the API resolves the
// current tenant from the X-Tenant-Id header (added automatically by lib/api). Catalog.Write gated.
export const billerImportService = {
  // The wizard's Source step: configured connectors that can supply a catalogue.
  getSources: async (): Promise<BillerImportSourcesResponse> => {
    return api.get<BillerImportSourcesResponse>('/catalog/billers/import/sources');
  },

  // Preview the live partner catalogue, tagged New / Mapped / Changed. Persists nothing.
  preview: async (request: BillerImportPreviewRequest): Promise<BillerImportPreviewResponse> => {
    return api.post<BillerImportPreviewResponse>('/catalog/billers/import/preview', request);
  },

  // Idempotent upsert of the selected billers. Returns created/updated/deactivated counts.
  import: async (request: BillerImportRequest): Promise<BillerImportSummaryResponse> => {
    return api.post<BillerImportSummaryResponse>('/catalog/billers/import', request);
  },
};
