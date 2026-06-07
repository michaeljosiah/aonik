import { api } from '@/lib/api';
import type {
  PaymentGatewaySettingsResponse,
  PaymentGatewaySettingsUpdateRequest,
  TestPaymentGatewayRequest,
  TestPaymentGatewayResponse,
} from '@/types';

export const paymentGatewaysService = {
  get: async (): Promise<PaymentGatewaySettingsResponse> => {
    return api.get<PaymentGatewaySettingsResponse>('/admin/settings/payment-gateways');
  },

  update: async (
    request: PaymentGatewaySettingsUpdateRequest,
  ): Promise<PaymentGatewaySettingsResponse> => {
    return api.put<PaymentGatewaySettingsResponse>('/admin/settings/payment-gateways', request);
  },

  test: async (request: TestPaymentGatewayRequest): Promise<TestPaymentGatewayResponse> => {
    return api.post<TestPaymentGatewayResponse>('/admin/settings/payment-gateways/test', request);
  },
};
