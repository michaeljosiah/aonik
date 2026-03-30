import { api } from '@/lib/api';

export interface ContentBlockMedia {
  id: string;
  storageType: string;
  url: string;
  alt?: string;
  caption?: string;
  mimeType?: string;
  order: number;
  linkUrl?: string;
}

export interface ContentBlock {
  id: string;
  contentKey: string;
  title: string;
  slug?: string;
  area: string;
  format: string;
  body?: string;
  locale: string;
  isEnabled: boolean;
  startAt?: string;
  endAt?: string;
  priority: number;
  aiRunId?: string;
  media: ContentBlockMedia[];
  createdAt: string;
  updatedAt?: string;
}

export interface CreateContentBlockRequest {
  contentKey: string;
  title: string;
  slug?: string;
  area: string;
  format: string;
  body?: string;
  locale: string;
  isEnabled: boolean;
  startAt?: string;
  endAt?: string;
  priority: number;
  targetingJson?: string;
  aiRunId?: string;
}

export interface UpdateContentBlockRequest {
  title: string;
  slug?: string;
  area: string;
  format: string;
  body?: string;
  locale: string;
  isEnabled: boolean;
  startAt?: string;
  endAt?: string;
  priority: number;
  targetingJson?: string;
}

export interface AddContentBlockMediaRequest {
  url: string;
  alt?: string;
  caption?: string;
  mimeType?: string;
  linkUrl?: string;
}

export async function getContentBlocks(
  area?: string,
  contentKey?: string,
  locale: string = 'en',
  isEnabled?: boolean
): Promise<ContentBlock[]> {
  const params = new URLSearchParams();
  if (area) params.append('area', area);
  if (contentKey) params.append('contentKey', contentKey);
  params.append('locale', locale);
  if (isEnabled !== undefined) params.append('isEnabled', String(isEnabled));

  return api.get<ContentBlock[]>(`/cms/content-blocks?${params}`);
}

export async function getContentBlock(id: string): Promise<ContentBlock> {
  return api.get<ContentBlock>(`/cms/content-blocks/${id}`);
}

export async function createContentBlock(request: CreateContentBlockRequest): Promise<ContentBlock> {
  return api.post<ContentBlock>('/cms/content-blocks', request);
}

export async function updateContentBlock(id: string, request: UpdateContentBlockRequest): Promise<ContentBlock> {
  return api.put<ContentBlock>(`/cms/content-blocks/${id}`, request);
}

export async function deleteContentBlock(id: string): Promise<void> {
  await api.delete<void>(`/cms/content-blocks/${id}`);
}

export async function addContentBlockMedia(
  contentBlockId: string,
  request: AddContentBlockMediaRequest
): Promise<ContentBlockMedia> {
  return api.post<ContentBlockMedia>(`/cms/content-blocks/${contentBlockId}/media`, request);
}

export async function generateContentImage(
  contentBlockId: string,
  request: { prompt: string; alt?: string; width?: number; height?: number }
): Promise<ContentBlockMedia> {
  return api.post<ContentBlockMedia>(`/cms/content-blocks/${contentBlockId}/generate-image`, request);
}

export async function removeContentBlockMedia(contentBlockId: string, mediaId: string): Promise<void> {
  await api.delete<void>(`/cms/content-blocks/${contentBlockId}/media/${mediaId}`);
}

export async function reorderContentBlockMedia(
  contentBlockId: string,
  mediaIds: string[]
): Promise<void> {
  await api.put<void>(`/cms/content-blocks/${contentBlockId}/media/reorder`, { mediaIds });
}

export async function getActiveContentBlocks(
  area: string,
  locale: string = 'en'
): Promise<ContentBlock[]> {
  const params = new URLSearchParams();
  params.append('area', area);
  params.append('locale', locale);

  return api.get<ContentBlock[]>(`/cms/content/active?${params}`);
}
