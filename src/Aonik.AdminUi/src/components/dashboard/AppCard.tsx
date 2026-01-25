import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Play, MoreVertical, CheckSquare, Grid2x2Plus } from 'lucide-react';
import type { AppCard as AppCardType, AppStatus } from '@/types';
import { cn } from '@/lib/utils';

interface AppCardProps {
  app: AppCardType;
}

function StatusBadge({ status }: { status: AppStatus }) {
  switch (status) {
    case 'active':
      return (
        <Button variant="success" size="sm" className="h-7 text-xs gap-1.5">
          <Play className="w-3 h-3" />
          Launch
        </Button>
      );
    case 'pending':
      return (
        <Badge variant="pending" className="gap-1">
          Pending
        </Badge>
      );
    case 'request':
      return (
        <Button variant="outline" size="sm" className="h-7 text-xs border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]">
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
        className="w-14 h-14 rounded-md flex items-center justify-center"
        style={{ backgroundColor: app.iconBgColor || '#0D7377' }}
      >
        <svg viewBox="0 0 24 24" className="w-8 h-8 text-white" fill="none" stroke="currentColor" strokeWidth="1.5">
          <path d="M3 3v18h18" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M7 14l4-4 4 4 5-5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>
    );
  }
  
  return (
    <div className="w-14 h-14 rounded-md bg-[var(--color-background)] flex items-center justify-center border border-dashed border-[var(--color-border)]">
      <Grid2x2Plus className="w-6 h-6 text-[var(--color-text-tertiary)]" />
    </div>
  );
}

export function AppCard({ app }: AppCardProps) {
  return (
    <div className="relative pt-7">
      {/* Icon positioned to overlap the card top */}
      <div className="absolute top-0 left-4 z-10">
        <AppIcon app={app} />
      </div>
      
      <Card className="flex flex-col h-full overflow-visible hover:shadow-md transition-shadow bg-[var(--color-surface-elevated)] border-[var(--color-border)]">
        {/* Header with Actions (icon space reserved) */}
        <div className="p-4 pb-0 flex items-start justify-end">
          <div className="flex items-center gap-1">
            <StatusBadge status={app.status} />
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
              <CheckSquare className="w-4 h-4" />
            </Button>
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
              <MoreVertical className="w-4 h-4" />
            </Button>
          </div>
        </div>

      {/* Content */}
      <div className="p-4 flex-1 flex flex-col">
        <h3 className="text-base font-semibold text-[var(--color-text-primary)] mb-1.5">
          {app.name}
        </h3>
        <p className="text-sm text-[var(--color-text-secondary)] line-clamp-3 mb-4">
          {app.description}
        </p>

        {/* Owners and Date */}
        <div className="flex items-center justify-between text-xs mt-auto mb-3">
          <div>
            <p className="text-[var(--color-text-tertiary)] uppercase tracking-wide mb-1">Owner(s)</p>
            <div className="flex items-center gap-2">
              <div className="flex -space-x-2">
                {app.owners.slice(0, 3).map((owner, index) => (
                  <Avatar key={owner.id} className={cn('w-6 h-6 border-2 border-[var(--color-surface)]', index > 0 && '-ml-2')}>
                    {owner.avatar && <AvatarImage src={owner.avatar} alt={owner.name} />}
                    <AvatarFallback className="text-[10px] bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)]">
                      {owner.name.split(' ').map(n => n[0]).join('')}
                    </AvatarFallback>
                  </Avatar>
                ))}
                {app.owners.length > 3 && (
                  <div className="w-6 h-6 rounded-full bg-[var(--color-background)] border-2 border-[var(--color-surface)] flex items-center justify-center text-[10px] text-[var(--color-text-secondary)] -ml-2">
                    +{app.owners.length - 3}
                  </div>
                )}
              </div>
              <div>
                <p className="font-medium text-[var(--color-text-primary)]">{app.owners[0].name}</p>
                {app.owners[0].role && (
                  <p className="text-[var(--color-text-tertiary)]">{app.owners[0].role}</p>
                )}
              </div>
            </div>
          </div>
          <div className="text-right">
            <p className="text-[var(--color-text-tertiary)] uppercase tracking-wide mb-1">Date Modified</p>
            <p className="font-medium text-[var(--color-text-primary)]">{app.dateModified}</p>
            <p className="text-[var(--color-text-tertiary)]">by {app.modifiedBy}</p>
          </div>
        </div>

        {/* Tags */}
        <div>
          <p className="text-[var(--color-text-tertiary)] uppercase tracking-wide text-xs mb-1.5">Tags</p>
          <div className="flex flex-wrap gap-1.5">
            {app.tags.slice(0, 2).map((tag) => (
              <Badge key={tag} variant="secondary" className="text-xs">
                {tag}
              </Badge>
            ))}
            {app.tags.length > 2 && (
              <Badge variant="default" className="text-xs">
                +{app.tags.length - 2}
              </Badge>
            )}
          </div>
        </div>
      </div>
    </Card>
    </div>
  );
}
