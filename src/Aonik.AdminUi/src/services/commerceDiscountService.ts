import { api } from '@/lib/api';
import type { PagedResult } from '@/types';
import type { CommercePagedResult, DiscountDto, DiscountKind } from '@/types/commerce';
import { normalizeCommercePage } from '@/types/commerce';

export interface DiscountDefinition {
  kind: DiscountKind;
  value: number;
  currency: string | null;
  maxRedemptions: number | null;
  expiresAt: string | null;
  eligibleProductIds: string[] | null;
}

export interface CreateDiscountRequest extends DiscountDefinition {
  code: string;
}

export interface UpdateDiscountRequest extends DiscountDefinition {
  isActive: boolean;
  expectedVersion: string;
}

export interface ListDiscountsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  isActive?: boolean;
}

export const commerceDiscountService = {
  list: async (params: ListDiscountsParams = {}): Promise<PagedResult<DiscountDto>> => {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== '') query.set(key, String(value));
    }
    const suffix = query.size ? `?${query}` : '';
    const result = await api.get<CommercePagedResult<DiscountDto>>(`/commerce/admin/discounts${suffix}`);
    return normalizeCommercePage(result);
  },
  create: (request: CreateDiscountRequest): Promise<DiscountDto> =>
    api.post<DiscountDto>('/commerce/admin/discounts', request),
  update: (id: string, request: UpdateDiscountRequest): Promise<DiscountDto> =>
    api.put<DiscountDto>(`/commerce/admin/discounts/${id}`, request),
};
