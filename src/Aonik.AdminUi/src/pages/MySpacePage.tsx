import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
import {
  activityFeed,
  quickLinks,
  myApps,
  myAgents,
  myDataboxes,
} from '@/data/mockData';
import { getActiveContentBlocks } from '@/services/contentBlockService';
import { getWorkspacePanelForApp } from '@/workspace/registry';

export function MySpacePage() {
  const navigate = useNavigate();
  const [bannerImages, setBannerImages] = useState<Array<{ src: string; alt: string }>>([]);
  const defaultBannerImages = [
    { src: '/images/banners/myspace-default-01.png', alt: 'Aonik platform overview' },
    { src: '/images/banners/myspace-default-02.png', alt: 'Billing and payments workflows' },
    { src: '/images/banners/myspace-default-03.png', alt: 'AI-powered finance operations' },
  ];

  useEffect(() => {
    async function loadBanner() {
      try {
        const blocks = await getActiveContentBlocks('MySpaceBanner', 'en');
        if (blocks.length > 0) {
          const block = blocks[0];
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

  const handleLaunchApp = (appId: string) => {
    const panel = getWorkspacePanelForApp(appId);
    if (!panel) return;
    navigate(`/workspace?panel=${panel.id}`);
  };

  const handleChatAgent = (agentId: string) => {
    void agentId;
    navigate('/ai/chat');
  };

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
                  <AppCard key={app.id} app={app} onLaunch={handleLaunchApp} />
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
                    <AgentCard key={agent.id} agent={agent} onChat={handleChatAgent} />
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
