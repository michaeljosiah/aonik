import { api } from '@/lib/api';
import type {
  ConnectorKindSchema,
  CreateCredentialBundleRequest,
  CredentialBundleListItem,
  LiftLegacyFlutterwaveResult,
  RotateCredentialFieldRequest,
  UpdateCredentialBundleRequest,
} from '@/types/credentials';

// Partner-owned credential bundles + connector registry schema (Spec 042 §12). Secrets are write-only;
// no call returns a secret value.
export const credentialBundleService = {
  getConnectorKinds: async (): Promise<ConnectorKindSchema[]> => {
    return api.get<ConnectorKindSchema[]>('/admin/connector-kinds');
  },

  list: async (): Promise<CredentialBundleListItem[]> => {
    return api.get<CredentialBundleListItem[]>('/admin/credential-bundles');
  },

  create: async (request: CreateCredentialBundleRequest): Promise<CredentialBundleListItem> => {
    return api.post<CredentialBundleListItem>('/admin/credential-bundles', request);
  },

  update: async (
    bundleRef: string,
    request: UpdateCredentialBundleRequest,
  ): Promise<CredentialBundleListItem> => {
    return api.patch<CredentialBundleListItem>(
      `/admin/credential-bundles/${encodeURIComponent(bundleRef)}`,
      request,
    );
  },

  rotate: async (
    bundleRef: string,
    request: RotateCredentialFieldRequest,
  ): Promise<CredentialBundleListItem> => {
    return api.post<CredentialBundleListItem>(
      `/admin/credential-bundles/${encodeURIComponent(bundleRef)}/rotate`,
      request,
    );
  },

  liftLegacyFlutterwave: async (): Promise<LiftLegacyFlutterwaveResult> => {
    return api.post<LiftLegacyFlutterwaveResult>('/admin/partners/flutterwave/lift-legacy', {});
  },
};
