import type { AxiosRequestConfig } from 'axios';
import { api } from '@/lib/api';
import type { RefundContextDto, RefundDraft, RefundDto, RefundPreviewDto, RefundRequest } from '@/types/commerceRefunds';

const path = (orderId: string) => `/commerce/admin/orders/${encodeURIComponent(orderId)}/refunds`;

export const commerceRefundService = {
  getContext: (orderId: string, config: AxiosRequestConfig): Promise<RefundContextDto> =>
    api.get(path(orderId), config),
  getRefund: (orderId: string, refundId: string, config: AxiosRequestConfig): Promise<RefundDto> =>
    api.get(`${path(orderId)}/${encodeURIComponent(refundId)}`, config),
  preview: (orderId: string, draft: RefundDraft, config: AxiosRequestConfig): Promise<RefundPreviewDto> =>
    api.post(`${path(orderId)}/preview`, draft, config),
  request: (orderId: string, request: RefundRequest, config: AxiosRequestConfig): Promise<RefundDto> =>
    api.post(path(orderId), request, config),
  reconcile: (orderId: string, refundId: string, config: AxiosRequestConfig): Promise<RefundDto> =>
    api.post(`${path(orderId)}/${encodeURIComponent(refundId)}/reconcile`, undefined, config),
};
