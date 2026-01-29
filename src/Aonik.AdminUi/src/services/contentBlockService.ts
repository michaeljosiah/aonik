const API_BASE_URL = import.meta.env.VITE_API_URL || '';

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

async function getAuthHeaders(): Promise<Record<string, string>> {
  const token = localStorage.getItem('access_token');
  return {
    'Content-Type': 'application/json',
    ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
  };
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

  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks?${params}`, {
    headers: await getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch content blocks');
  }

  return response.json();
}

export async function getContentBlock(id: string): Promise<ContentBlock> {
  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks/${id}`, {
    headers: await getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch content block');
  }

  return response.json();
}

export async function createContentBlock(request: CreateContentBlockRequest): Promise<ContentBlock> {
  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks`, {
    method: 'POST',
    headers: await getAuthHeaders(),
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to create content block');
  }

  return response.json();
}

export async function updateContentBlock(id: string, request: UpdateContentBlockRequest): Promise<ContentBlock> {
  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks/${id}`, {
    method: 'PUT',
    headers: await getAuthHeaders(),
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to update content block');
  }

  return response.json();
}

export async function deleteContentBlock(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks/${id}`, {
    method: 'DELETE',
    headers: await getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete content block');
  }
}

export async function addContentBlockMedia(
  contentBlockId: string,
  request: AddContentBlockMediaRequest
): Promise<ContentBlockMedia> {
  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks/${contentBlockId}/media`, {
    method: 'POST',
    headers: await getAuthHeaders(),
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to add media');
  }

  return response.json();
}

export async function removeContentBlockMedia(contentBlockId: string, mediaId: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks/${contentBlockId}/media/${mediaId}`, {
    method: 'DELETE',
    headers: await getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to remove media');
  }
}

export async function reorderContentBlockMedia(
  contentBlockId: string,
  mediaIds: string[]
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/cms/content-blocks/${contentBlockId}/media/reorder`, {
    method: 'PUT',
    headers: await getAuthHeaders(),
    body: JSON.stringify({ mediaIds }),
  });

  if (!response.ok) {
    throw new Error('Failed to reorder media');
  }
}

export async function getActiveContentBlocks(
  area: string,
  locale: string = 'en'
): Promise<ContentBlock[]> {
  const params = new URLSearchParams();
  params.append('area', area);
  params.append('locale', locale);

  const response = await fetch(`${API_BASE_URL}/api/cms/content/active?${params}`, {
    headers: await getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch active content blocks');
  }

  return response.json();
}
