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
import { getWorkspacePanelForApp } from '@/workspace/registry';

export function MySpacePage() {
  const navigate = useNavigate();
  const defaultBannerImages = [
    { src: '/images/banners/myspace-default-01.png', alt: 'Banner placeholder' },
    { src: '/images/banners/myspace-default-02.png', alt: 'Banner placeholder' },
    { src: '/images/banners/myspace-default-03.png', alt: 'Banner placeholder' },
  ];

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
    <div className="flex-1 overflow-auto bg-[var(--color-background)]">
      <div className="p-6 pb-8">
        <div className="mb-5">
          <h1 className="text-[20px] font-bold text-[var(--color-text-primary)] sm:text-[24px]">My Space</h1>
          <p className="mt-1 text-sm text-[var(--color-text-secondary)]">
            View and access your personal space with quick links, recent activity, and key resources in one place.
          </p>
        </div>

        <div className="grid grid-cols-12 gap-5 mb-6">
          <div className="col-span-12 xl:col-span-3 xl:h-[290px]">
            <ActivityFeed items={activityFeed} />
          </div>

          <div className="col-span-12 xl:col-span-6 xl:h-[290px]">
            <Card className="h-full rounded-[4px] p-4">
              <CardContent className="h-full p-0">
                <BannerCarousel images={defaultBannerImages} className="h-full" />
              </CardContent>
            </Card>
          </div>

          <div className="col-span-12 xl:col-span-3 xl:h-[290px]">
            <QuickLinks links={quickLinks} />
          </div>
        </div>

        <div className="mb-6">
            <Card className="shadow-sm rounded-[4px]">
              <CardContent className="p-5">
                <MyAppsHeader />
              <div className="grid grid-cols-1 gap-5 pt-2 mt-1 pb-4 md:grid-cols-2 xl:grid-cols-4">
                {myApps.map((app) => (
                  <AppCard key={app.id} app={app} onLaunch={handleLaunchApp} />
                ))}
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="grid grid-cols-12 gap-5">
          <div className="col-span-12 lg:col-span-6">
            <Card className="h-full shadow-sm rounded-[4px]">
              <CardContent className="p-5">
                <MyAgentsHeader />
                <div className="grid grid-cols-1 gap-5 pt-2 mt-1 pb-4 xl:grid-cols-2">
                  {myAgents.map((agent) => (
                    <AgentCard key={agent.id} agent={agent} onChat={handleChatAgent} />
                  ))}
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="col-span-12 lg:col-span-6">
            <DataboxesTable databoxes={myDataboxes} />
          </div>
        </div>
      </div>
    </div>
  );
}
