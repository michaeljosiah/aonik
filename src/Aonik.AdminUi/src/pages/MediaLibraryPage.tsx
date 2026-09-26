import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Image, Search, ExternalLink } from 'lucide-react';
import { getContentBlocks, type ContentBlock, type ContentBlockMedia } from '@/services/contentBlockService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';

interface MediaItem extends ContentBlockMedia {
  contentBlockId: string;
  contentBlockTitle: string;
  contentBlockKey: string;
}

export function MediaLibraryPage() {
  const [allMedia, setAllMedia] = useState<MediaItem[]>([]);
  const [filteredMedia, setFilteredMedia] = useState<MediaItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
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
      setInitialLoad(false);
    }
  }

  if (initialLoad) {
    return <PageLoadingScreen message="Loading media library" />;
  }

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        {/* Page Header */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-foreground">Media Library</h1>
          <p className="text-muted-foreground">
            Browse and manage all media assets used across content blocks.
          </p>
        </div>

        {/* Search */}
        <div className="mb-6">
          <div className="relative max-w-[28rem]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
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
              <div className="p-2 rounded-md bg-primary">
                <Image className="w-5 h-5 text-primary-foreground" />
              </div>
              <div>
                <CardTitle className="text-base font-semibold">Media Assets</CardTitle>
                <p className="text-sm text-muted-foreground">
                  {filteredMedia.length} item{filteredMedia.length !== 1 ? 's' : ''}
                </p>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="flex items-center justify-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary" />
              </div>
            ) : filteredMedia.length === 0 ? (
              <div className="text-center py-12">
                <Image className="w-12 h-12 mx-auto mb-4 text-muted-foreground" />
                <p className="text-foreground font-medium mb-1">No media found</p>
                <p className="text-sm text-muted-foreground">
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
                    className="group relative rounded-lg border border-border overflow-hidden hover:shadow-md transition-shadow"
                  >
                    {/* Image Preview */}
                    <div className="aspect-video bg-muted relative">
                      {item.url ? (
                        <img
                          src={item.url}
                          alt={item.alt || ''}
                          className="w-full h-full object-cover"
                          onError={(e) => {
                            (e.target as HTMLImageElement).style.display = 'none';
                            (e.target as HTMLImageElement).parentElement!.innerHTML = `
                              <div class="w-full h-full flex items-center justify-center bg-accent">
                                <svg class="w-8 h-8 text-muted-foreground" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                  <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
                                  <circle cx="8.5" cy="8.5" r="1.5"/>
                                  <polyline points="21 15 16 10 5 21"/>
                                </svg>
                              </div>
                            `;
                          }}
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center bg-accent">
                          <Image className="w-8 h-8 text-muted-foreground" />
                        </div>
                      )}
                      
                      {/* Overlay Actions */}
                      <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
                        <Button
                          variant="secondary"
                          size="icon-sm"
                          className="w-8 h-8"
                          aria-label="Open media in new tab"
                          onClick={() => window.open(item.url, '_blank')}
                        >
                          <ExternalLink className="w-4 h-4" />
                        </Button>
                      </div>
                    </div>

                    {/* Info */}
                    <div className="p-3 space-y-1">
                      <p className="text-sm font-medium text-foreground truncate">
                        {item.contentBlockTitle}
                      </p>
                      <p className="text-xs text-muted-foreground truncate">
                        {item.contentBlockKey}
                      </p>
                      {item.alt && (
                        <p className="text-xs text-muted-foreground truncate">
                          {item.alt}
                        </p>
                      )}
                      <div className="flex items-center gap-2 pt-1">
                        <Badge variant="secondary">
                          {item.mimeType || 'Image'}
                        </Badge>
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
