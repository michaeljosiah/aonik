import { useEffect, useState } from 'react';
import {
  ActivityFeed,
  BannerCarousel,
  QuickLinks,
  AppCard,
  AgentCard,
  DataboxesTable,
  MyAppsHeader,
  MyAgentsHeader,
} from '@/components/dashboard';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ArrowUpRight } from 'lucide-react';
import {
  activityFeed,
  quickLinks,
  myApps,
  myAgents,
  myDataboxes,
} from '@/data/mockData';
import { getActiveContentBlocks, type ContentBlock } from '@/services/contentBlockService';

export function MySpacePage() {
  const [showResumeSetup, setShowResumeSetup] = useState(false);
  const [bannerContent, setBannerContent] = useState<ContentBlock | null>(null);
  const [bannerImages, setBannerImages] = useState<Array<{ src: string; alt: string }>>([]);
  const defaultBannerImages = [
    { src: '/images/banners/myspace-default-01.png', alt: 'Aonik platform overview' },
    { src: '/images/banners/myspace-default-02.png', alt: 'Billing and payments workflows' },
    { src: '/images/banners/myspace-default-03.png', alt: 'AI-powered finance operations' },
  ];

  useEffect(() => {
    const skipped = localStorage.getItem('aonik:onboarding:skip');
    const complete = localStorage.getItem('aonik:onboarding:complete');
    setShowResumeSetup(Boolean(skipped) && !complete);
  }, []);

  useEffect(() => {
    async function loadBanner() {
      try {
        const blocks = await getActiveContentBlocks('MySpaceBanner', 'en');
        if (blocks.length > 0) {
          const block = blocks[0];
          setBannerContent(block);
          const images = block.media.map(m => ({
            src: m.url,
            alt: m.alt || block.title,
          }));
          setBannerImages(images);
        } else {
          setBannerImages(defaultBannerImages);
        }
      } catch (error) {
        console.error('Failed to load banner content:', error);
        setBannerImages(defaultBannerImages);
      }
    }

    loadBanner();
  }, []);

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        {/* Page Header */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">My Space</h1>
          <p className="text-[var(--color-text-secondary)]">
            View and access your personal space with quick links, recent activity, and key resources in one place.
          </p>
        </div>

        {showResumeSetup && (
          <div className="mb-6">
            <Card className="border-[var(--color-brand-primary)]/30 bg-[var(--color-brand-primary-light)]/40">
              <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
                <div className="space-y-1">
                  <p className="text-sm font-semibold text-[var(--color-text-primary)]">Resume tenant setup</p>
                  <p className="text-sm text-[var(--color-text-secondary)]">
                    Pick up where you left off in the setup journey and finish the required steps.
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Button variant="secondary" onClick={() => (window.location.href = '/setup/journey')}
                    className="inline-flex items-center">
                    Continue setup
                    <ArrowUpRight className="ml-2 h-4 w-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => setShowResumeSetup(false)}
                  >
                    Dismiss
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        )}

        {/* Top Section: Activity Feed, Banner, Quick Links */}
        <div className="grid grid-cols-12 gap-5 mb-6">
          {/* Activity Feed */}
          <div className="col-span-12 lg:col-span-3 h-full">
            <ActivityFeed items={activityFeed} />
          </div>

          {/* Banner Carousel */}
          <div className="col-span-12 lg:col-span-6 h-full">
            <BannerCarousel images={bannerImages.length > 0 ? bannerImages : undefined} />
          </div>

          {/* Quick Links */}
          <div className="col-span-12 lg:col-span-3 h-full">
            <QuickLinks links={quickLinks} />
          </div>
        </div>

        {/* My Apps Section - wrapped in a container card */}
        <div className="mb-6">
          <Card>
            <CardContent className="p-5">
              <MyAppsHeader />
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5 pt-6">
                {myApps.map((app) => (
                  <AppCard key={app.id} app={app} />
                ))}
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Bottom Section: My Agents and Databoxes */}
        <div className="grid grid-cols-12 gap-5">
          {/* My Agents - wrapped in a container card */}
          <div className="col-span-12 lg:col-span-6">
            <Card className="h-full">
              <CardContent className="p-5">
                <MyAgentsHeader />
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                  {myAgents.map((agent) => (
                    <AgentCard key={agent.id} agent={agent} />
                  ))}
                </div>
              </CardContent>
            </Card>
          </div>

          {/* My Databoxes */}
          <div className="col-span-12 lg:col-span-6">
            <DataboxesTable databoxes={myDataboxes} />
          </div>
        </div>
      </div>
    </div>
  );
}
