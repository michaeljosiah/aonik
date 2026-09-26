import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Play, MoreVertical, CheckSquare, Grid2x2Plus } from 'lucide-react';
import type { AppCard as AppCardType, AppStatus } from '@/types';
import { cn } from '@/lib/utils';

interface AppCardProps {
  app: AppCardType;
  onLaunch?: (appId: string) => void;
}

function StatusBadge({ status, onLaunch }: { status: AppStatus; onLaunch?: () => void }) {
  switch (status) {
    case 'active':
      return (
        <Button
          size="sm"
          className="h-6 gap-1 px-2 text-[10px] font-medium"
          onClick={onLaunch}
          disabled={!onLaunch}
        >
          <Play className="w-2.5 h-2.5" />
          Launch
        </Button>
      );
    case 'pending':
      return (
        <Badge variant="warning" className="h-6 gap-1 px-2 text-[10px] font-medium">
          Pending
        </Badge>
      );
    case 'request':
      return (
        <Button variant="outline" size="sm" className="h-6 border-primary px-2 text-[10px] font-medium text-primary">
          Request
        </Button>
      );
    default:
      return null;
  }
}

function AppIcon({ app }: { app: AppCardType }) {
  if (app.icon === 'insights' || app.icon === 'semanticx') {
    return (
      <div
        className="h-[68px] w-[68px] rounded-lg flex items-center justify-center border"
        style={{ backgroundColor: app.iconBgColor || 'var(--primary)' }}
      >
        <svg viewBox="0 0 24 24" className="w-8 h-8 text-white" fill="none" stroke="currentColor" strokeWidth="1.5">
          <path d="M3 3v18h18" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M7 14l4-4 4 4 5-5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>
    );
  }

  return (
    <div className="h-[68px] w-[68px] rounded-lg bg-muted flex items-center justify-center border">
      <Grid2x2Plus className="w-7 h-7 text-muted-foreground" />
    </div>
  );
}

export function AppCard({ app, onLaunch }: AppCardProps) {
  return (
    <div className="relative w-full pt-10 cursor-pointer group">
      <div className="absolute left-6 -top-[6px] z-10">
        <AppIcon app={app} />
      </div>

      <Card className={cn(
        'relative flex flex-col min-h-[260px] overflow-visible',
        'border rounded-lg',
        'transition-all duration-300',
        'hover:border-primary hover:shadow-lg hover:scale-[1.01]',
        'bg-popover',
      )}>
        <div className="flex items-center justify-end gap-0.5 px-4 pt-3">
          <StatusBadge status={app.status} onLaunch={onLaunch ? () => onLaunch(app.id) : undefined} />
          <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-muted-foreground" aria-label="Select">
            <CheckSquare className="w-3.5 h-3.5" />
          </Button>
          <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-muted-foreground" aria-label="More actions">
            <MoreVertical className="w-3.5 h-3.5" />
          </Button>
        </div>

        <div className="flex flex-1 flex-col px-4 pb-4 mt-4 pt-2">
          <h3 className="mb-1.5 line-clamp-1 text-[18px] font-bold text-foreground">
            {app.name}
          </h3>
          <p className="mb-4 min-h-[54px] line-clamp-3 text-[13px] leading-6 text-muted-foreground">
            {app.description}
          </p>
        </div>

        <div className="w-4/5 h-px bg-border self-center" />

        <div className="grid grid-cols-2 gap-x-4 gap-y-3 px-4 pb-4 pt-4">
          <div>
            <p className="text-xs font-medium text-muted-foreground mb-1">Owners</p>
            <div className="flex items-center gap-2">
              <div className="flex -space-x-2">
                {app.owners.slice(0, 3).map((owner, index) => (
                  <Avatar key={owner.id} className={cn('w-6 h-6 border-2 border-card', index > 0 && '-ml-2')}>
                    {owner.avatar && <AvatarImage src={owner.avatar} alt={owner.name} />}
                    <AvatarFallback className="text-[10px] bg-agent/10 text-agent">
                      {owner.name.split(' ').map(n => n[0]).join('')}
                    </AvatarFallback>
                  </Avatar>
                ))}
                {app.owners.length > 3 && (
                  <div className="w-6 h-6 rounded-full bg-muted border-2 border-card flex items-center justify-center text-[10px] text-muted-foreground -ml-2">
                    +{app.owners.length - 3}
                  </div>
                )}
              </div>
              <div>
                <p className="text-[16px] font-bold text-foreground">{app.owners[0].name}</p>
                {app.owners[0].role && (
                  <p className="text-[12px] text-muted-foreground">{app.owners[0].role}</p>
                )}
              </div>
            </div>
          </div>

          {/* Date modified */}
          <div>
            <p className="text-xs font-medium text-muted-foreground mb-1">Date modified</p>
            <p className="text-[16px] font-bold text-foreground">{app.dateModified}</p>
            <p className="text-[12px] text-muted-foreground">by {app.modifiedBy}</p>
          </div>

          {/* Tags — spanning full width */}
          <div className="col-span-2">
            <p className="text-xs font-medium text-muted-foreground mb-1.5">Tags</p>
            <div className="flex flex-wrap gap-1.5">
              {app.tags.slice(0, 2).map((tag) => (
                <span
                  key={tag}
                  className="bg-secondary text-secondary-foreground px-3 py-1.5 rounded-md text-xs font-medium"
                >
                  {tag}
                </span>
              ))}
              {app.tags.length > 2 && (
                <span className="bg-primary/10 text-primary px-3 py-1.5 rounded-md text-xs font-medium">
                  +{app.tags.length - 2}
                </span>
              )}
            </div>
          </div>
        </div>
      </Card>
    </div>
  );
}
