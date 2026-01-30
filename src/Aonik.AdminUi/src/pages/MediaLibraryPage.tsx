import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Image, Search, ExternalLink } from 'lucide-react';
import { getContentBlocks, type ContentBlock, type ContentBlockMedia } from '@/services/contentBlockService';

interface MediaItem extends ContentBlockMedia {
  contentBlockId: string;
  contentBlockTitle: string;
  contentBlockKey: string;
}

export function MediaLibraryPage() {
  const [allMedia, setAllMedia] = useState<MediaItem[]>([]);
  const [filteredMedia, setFilteredMedia] = useState<MediaItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    loadAllMedia();
  }, []);

  useEffect(() => {
    if (searchQuery.trim()) {
      const filtered = allMedia.filter(
        (item) =>
          item.url.toLowerCase().includes(searchQuery.toLowerCase()) ||
          item.alt?.toLowerCase().includes(searchQuery.toLowerCase()) ||
          item.contentBlockTitle.toLowerCase().includes(searchQuery.toLowerCase())
      );
      setFilteredMedia(filtered);
    } else {
      setFilteredMedia(allMedia);
    }
  }, [searchQuery, allMedia]);

  async function loadAllMedia() {
    try {
      setLoading(true);
      const blocks = await getContentBlocks();
      const media: MediaItem[] = [];

      blocks.forEach((block: ContentBlock) => {
        block.media.forEach((m: ContentBlockMedia) => {
          media.push({
            ...m,
            contentBlockId: block.id,
            contentBlockTitle: block.title,
            contentBlockKey: block.contentKey,
          });
        });
      });

      setAllMedia(media);
      setFilteredMedia(media);
    } catch (error) {
      console.error('Failed to load media:', error);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        {/* Page Header */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Media Library</h1>
          <p className="text-[var(--color-text-secondary)]">
            Browse and manage all media assets used across content blocks.
          </p>
        </div>

        {/* Search */}
        <div className="mb-6">
          <div className="relative max-w-md">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-secondary)]" />
            <Input
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by URL, alt text, or content block..."
              className="pl-10"
            />
          </div>
        </div>

        {/* Media Grid */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-md bg-[var(--color-brand-primary)]">
                <Image className="w-5 h-5 text-white" />
              </div>
              <div>
                <CardTitle className="text-base font-semibold">Media Assets</CardTitle>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  {filteredMedia.length} item{filteredMedia.length !== 1 ? 's' : ''}
                </p>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="flex items-center justify-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[var(--color-brand-primary)]" />
              </div>
            ) : filteredMedia.length === 0 ? (
              <div className="text-center py-12">
                <Image className="w-12 h-12 mx-auto mb-4 text-[var(--color-text-tertiary)]" />
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No media found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  {searchQuery
                    ? 'Try adjusting your search query'
                    : 'Media will appear here when added to content blocks'}
                </p>
              </div>
            ) : (
              <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                {filteredMedia.map((item) => (
                  <div
                    key={item.id}
                    className="group relative rounded-lg border border-[var(--color-border-light)] overflow-hidden hover:shadow-md transition-shadow"
                  >
                    {/* Image Preview */}
                    <div className="aspect-video bg-gray-100 relative">
                      {item.url ? (
                        <img
                          src={item.url}
                          alt={item.alt || ''}
                          className="w-full h-full object-cover"
                          onError={(e) => {
                            (e.target as HTMLImageElement).style.display = 'none';
                            (e.target as HTMLImageElement).parentElement!.innerHTML = `
                              <div class="w-full h-full flex items-center justify-center bg-gray-200">
                                <svg class="w-8 h-8 text-gray-400" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                  <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
                                  <circle cx="8.5" cy="8.5" r="1.5"/>
                                  <polyline points="21 15 16 10 5 21"/>
                                </svg>
                              </div>
                            `;
                          }}
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center bg-gray-200">
                          <Image className="w-8 h-8 text-gray-400" />
                        </div>
                      )}
                      
                      {/* Overlay Actions */}
                      <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
                        <Button
                          variant="secondary"
                          size="icon-sm"
                          className="w-8 h-8"
                          onClick={() => window.open(item.url, '_blank')}
                        >
                          <ExternalLink className="w-4 h-4" />
                        </Button>
                      </div>
                    </div>

                    {/* Info */}
                    <div className="p-3 space-y-1">
                      <p className="text-sm font-medium text-[var(--color-text-primary)] truncate">
                        {item.contentBlockTitle}
                      </p>
                      <p className="text-xs text-[var(--color-text-secondary)] truncate">
                        {item.contentBlockKey}
                      </p>
                      {item.alt && (
                        <p className="text-xs text-[var(--color-text-tertiary)] truncate">
                          {item.alt}
                        </p>
                      )}
                      <div className="flex items-center gap-2 pt-1">
                        <span className="text-xs px-2 py-0.5 rounded-full bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
                          {item.mimeType || 'Image'}
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
