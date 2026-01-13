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

export function MySpacePage() {
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
          <div className="col-span-12 lg:col-span-3">
            <ActivityFeed items={activityFeed} />
          </div>

          {/* Banner Carousel */}
          <div className="col-span-12 lg:col-span-6 h-[280px]">
            <BannerCarousel />
          </div>

          {/* Quick Links */}
          <div className="col-span-12 lg:col-span-3">
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