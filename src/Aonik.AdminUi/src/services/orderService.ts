import { api } from '@/lib/api';
import type {
  BillPaymentOrderResponse,
  CreateBillPaymentOrderRequest,
  CreateBillPaymentItemRequest,
  UpdateBillPaymentItemRequest,
  CancelOrderRequest,
  OrderItemResponse,
} from '@/types';

export const orderService = {
  createBillPaymentOrder: async (
    request: CreateBillPaymentOrderRequest,
    idempotencyKey?: string
  ): Promise<BillPaymentOrderResponse> => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined;
    return api.post<BillPaymentOrderResponse>('/orders/bill-payments', request, headers ? { headers } : undefined);
  },

  getOrder: async (orderId: string): Promise<BillPaymentOrderResponse> => {
    return api.get<BillPaymentOrderResponse>(`/orders/${orderId}`);
  },

  addBillPaymentItem: async (
    orderId: string,
    request: CreateBillPaymentItemRequest
  ): Promise<OrderItemResponse> => {
    return api.post<OrderItemResponse>(`/orders/${orderId}/items/bill-payments`, request);
  },

  updateBillPaymentItem: async (
    orderId: string,
    orderItemId: string,
    request: UpdateBillPaymentItemRequest
  ): Promise<OrderItemResponse> => {
    return api.put<OrderItemResponse>(`/orders/${orderId}/items/${orderItemId}`, request);
  },

  removeBillPaymentItem: async (orderId: string, orderItemId: string): Promise<void> => {
    await api.delete<void>(`/orders/${orderId}/items/${orderItemId}`);
  },

  submitOrder: async (orderId: string): Promise<BillPaymentOrderResponse> => {
    return api.post<BillPaymentOrderResponse>(`/orders/${orderId}/submit`);
  },

  cancelOrder: async (orderId: string, request: CancelOrderRequest): Promise<BillPaymentOrderResponse> => {
    return api.post<BillPaymentOrderResponse>(`/orders/${orderId}/cancel`, request);
  },
};
