import { api } from '@/lib/api';
import type {
  InvoiceResponse,
  CreateInvoiceRequest,
  CreateInvoiceLineItemRequest,
} from '@/types';

export const billingService = {
  listInvoices: async (status?: string): Promise<InvoiceResponse[]> => {
    const params = new URLSearchParams();
    if (status) params.append('Status', status);
    const query = params.toString();
    return api.get<InvoiceResponse[]>(`/billing/invoices${query ? `?${query}` : ''}`);
  },

  getInvoice: async (id: string): Promise<InvoiceResponse> => {
    return api.get<InvoiceResponse>(`/billing/invoices/${id}`);
  },

  createInvoice: async (request: CreateInvoiceRequest): Promise<InvoiceResponse> => {
    return api.post<InvoiceResponse>('/billing/invoices', request);
  },

  issueInvoice: async (id: string): Promise<InvoiceResponse> => {
    return api.post<InvoiceResponse>(`/billing/invoices/${id}/issue`);
  },

  markPaid: async (id: string): Promise<InvoiceResponse> => {
    return api.post<InvoiceResponse>(`/billing/invoices/${id}/mark-paid`);
  },

  cancelInvoice: async (id: string): Promise<InvoiceResponse> => {
    return api.post<InvoiceResponse>(`/billing/invoices/${id}/cancel`);
  },

  addLine: async (id: string, line: CreateInvoiceLineItemRequest): Promise<InvoiceResponse> => {
    return api.post<InvoiceResponse>(`/billing/invoices/${id}/lines`, line);
  },

  updateLineQuantity: async (id: string, lineId: string, quantity: number): Promise<InvoiceResponse> => {
    return api.put<InvoiceResponse>(`/billing/invoices/${id}/lines/${lineId}/quantity`, { quantity });
  },

  updateLineUnitPrice: async (id: string, lineId: string, unitPrice: number): Promise<InvoiceResponse> => {
    return api.put<InvoiceResponse>(`/billing/invoices/${id}/lines/${lineId}/unit-price`, { unitPrice });
  },

  applyDiscount: async (id: string, discountTotal: number): Promise<InvoiceResponse> => {
    return api.post<InvoiceResponse>(`/billing/invoices/${id}/discount`, { discountTotal });
  },
};
