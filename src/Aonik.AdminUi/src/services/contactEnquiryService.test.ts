import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import { contactEnquiryService } from './contactEnquiryService';

vi.mock('@/lib/api', () => ({ api: { get: vi.fn() } }));

describe('private contact photos', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads binary data through the authenticated client with cancellation', async () => {
    const controller = new AbortController();
    const photo = new Blob(['photo'], { type: 'image/jpeg' });
    vi.mocked(api.get).mockResolvedValue(photo);
    await expect(contactEnquiryService.image('enquiry/id', 'image/id', controller.signal)).resolves.toBe(photo);
    expect(api.get).toHaveBeenCalledWith('/v1/admin/contact-enquiries/enquiry%2Fid/images/image%2Fid', {
      responseType: 'blob', signal: controller.signal,
    });
  });

  it('surfaces denied reads without falling back to public storage', async () => {
    const denied = new Error('Access denied');
    vi.mocked(api.get).mockRejectedValue(denied);
    await expect(contactEnquiryService.image('enquiry', 'photo', new AbortController().signal)).rejects.toBe(denied);
    expect(api.get).toHaveBeenCalledTimes(1);
  });
});
