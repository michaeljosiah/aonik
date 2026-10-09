import { api } from '@/lib/api';
import type { PagedResult } from '@/types';

export const contactTopics: Record<string, string> = {
  order: 'My order', new: 'Ordering for the first time', dish: 'Dishes and allergens',
  delivery: 'Delivery', gift: 'Gifting', other: 'Something else',
};
export interface ContactEnquirySummary {
  id: string; name: string; email: string; topic: string; receivedAtUtc: string; imageCount: number;
}
export interface ContactEnquiryDetail extends Omit<ContactEnquirySummary, 'imageCount'> {
  orderNumber: string | null; message: string;
  images: { id: string; fileName: string; contentType: string; sizeBytes: number }[];
}
const path = '/v1/admin/contact-enquiries';
export const contactEnquiryService = {
  list: (page: number, pageSize: number, topic?: string) =>
    api.get<PagedResult<ContactEnquirySummary>>(path, { params: { page, pageSize, topic } }),
  get: (id: string) => api.get<ContactEnquiryDetail>(`${path}/${encodeURIComponent(id)}`),
  image: (id: string, imageId: string, signal: AbortSignal) =>
    api.get<Blob>(`${path}/${encodeURIComponent(id)}/images/${encodeURIComponent(imageId)}`,
      { responseType: 'blob', signal }),
};
