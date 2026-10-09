import { api } from '@/lib/api';

export interface DeliveryCapacityDto {
  deliveryDate: string;
  unit: string;
  capacity: number;
  occupied: number;
  version: string;
}

export interface DeliveryAvailabilityDto {
  earliestDeliveryDate: string | null;
  timezone: string;
  fromDate: string;
  toDate: string;
  dates: string[];
  availability: { deliveryDate: string; status: string }[] | null;
  serverNowUtc: string | null;
}

export interface UpdateDeliveryCapacityRequest {
  unit: 'box';
  capacity: number;
  expectedVersion: string | null;
}

export const commerceDeliveryCapacityService = {
  list: (fromDate: string, days: number, config: object): Promise<DeliveryCapacityDto[]> =>
    api.get(`/commerce/admin/delivery-capacity?${new URLSearchParams({ fromDate, days: String(days) })}`, config),
  update: (date: string, request: UpdateDeliveryCapacityRequest, config: object): Promise<DeliveryCapacityDto> =>
    api.put(`/commerce/admin/delivery-capacity/${encodeURIComponent(date)}`, request, config),
  availability: (fromDate: string, days: number, config: object): Promise<DeliveryAvailabilityDto> =>
    api.get(`/commerce/config/delivery/dates?${new URLSearchParams({ fromDate, days: String(days) })}`, config),
};
