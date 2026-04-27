import { api } from '@/lib/api';
import type {
  BillPaymentOrderResponse,
  OrderListItem,
  CreateBillPaymentOrderRequest,
  CreateBillPaymentItemRequest,
  UpdateBillPaymentItemRequest,
  CancelOrderRequest,
  OrderItemResponse,
  PagedResult,
} from '@/types';

export interface ListOrdersParams {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
  orderType?: string;
  search?: string;
  payerPartyId?: string;
  /** Inclusive lower bound on Order.CreatedAt — ISO-8601 UTC string. */
  createdFromUtc?: string;
  /** Exclusive upper bound on Order.CreatedAt — ISO-8601 UTC string. */
  createdToUtc?: string;
}

export const orderService = {
  listOrders: async (params: ListOrdersParams = {}): Promise<PagedResult<OrderListItem>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.status) queryParams.append('status', params.status);
    if (params.orderType) queryParams.append('orderType', params.orderType);
    if (params.search) queryParams.append('search', params.search);
    if (params.payerPartyId) queryParams.append('payerPartyId', params.payerPartyId);
    if (params.createdFromUtc) queryParams.append('createdFromUtc', params.createdFromUtc);
    if (params.createdToUtc) queryParams.append('createdToUtc', params.createdToUtc);

    const query = queryParams.toString();
    return api.get<PagedResult<OrderListItem>>(`/orders${query ? `?${query}` : ''}`);
  },

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
