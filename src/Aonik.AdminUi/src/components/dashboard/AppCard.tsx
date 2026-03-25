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
          variant="success"
          size="sm"
          className="h-6 gap-1 rounded-[2px] px-2 text-[10px] font-medium"
          onClick={onLaunch}
          disabled={!onLaunch}
        >
          <Play className="w-2.5 h-2.5" />
          Launch
        </Button>
      );
    case 'pending':
      return (
        <Badge variant="pending" className="h-6 gap-1 rounded-[2px] px-2 text-[10px] font-medium">
          Pending
        </Badge>
      );
    case 'request':
      return (
        <Button variant="outline" size="sm" className="h-6 rounded-[2px] border-[var(--color-brand-primary)] px-2 text-[10px] font-medium text-[var(--color-brand-primary)]">
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
        className="h-24 w-24 rounded-xl flex items-center justify-center border border-[#cfcdd9]"
        style={{ backgroundColor: app.iconBgColor || '#055a60' }}
      >
        <svg viewBox="0 0 24 24" className="w-8 h-8 text-white" fill="none" stroke="currentColor" strokeWidth="1.5">
          <path d="M3 3v18h18" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M7 14l4-4 4 4 5-5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>
    );
  }

  return (
    <div className="h-24 w-24 rounded-xl bg-[#ECECEF] flex items-center justify-center border border-[#CFCDD9]">
      <Grid2x2Plus className="w-7 h-7 text-[#ABA7B7]" />
    </div>
  );
}

export function AppCard({ app, onLaunch }: AppCardProps) {
  return (
    <div className="relative w-full pt-24 cursor-pointer group">
      <div className="absolute left-6 top-0 z-[2]">
        <AppIcon app={app} />
      </div>

      <Card className={cn(
        'relative flex flex-col overflow-visible',
        'border border-[#d9d9e3] rounded-[4px]',
        'transition-all duration-300',
        'hoverBorder hover:border-[var(--color-brand-primary)] hover:shadow-lg hover:scale-[1.01]',
        'bg-[var(--color-surface-elevated)]',
      )}>
        <div className="relative w-full mt-4 p-2 flex flex-col flex-1">
          <div className="absolute -top-10 right-4 flex items-center justify-end gap-0.5">
            <StatusBadge status={app.status} onLaunch={onLaunch ? () => onLaunch(app.id) : undefined} />
            <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-[var(--color-text-tertiary)]">
              <CheckSquare className="w-3.5 h-3.5" />
            </Button>
            <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-[var(--color-text-tertiary)]">
              <MoreVertical className="w-3.5 h-3.5" />
            </Button>
          </div>

          <div className="flex flex-1 flex-col px-2 pb-4">
          <h3 className="mb-1.5 line-clamp-1 text-[18px] font-bold text-[var(--color-text-heading)]">
            {app.name}
          </h3>
          <p className="mb-4 min-h-[54px] line-clamp-3 text-[13px] leading-6 text-[var(--color-text-secondary)]">
            {app.description}
          </p>
        </div>

          <div className="w-4/5 h-px bg-[#E2E1E8] self-center" />

          <div className="grid grid-cols-2 gap-x-4 gap-y-3 px-2 pb-4 pt-4">
          <div>
            <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1">Owners</p>
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
                <p className="text-[16px] font-bold text-[var(--color-text-primary)]">{app.owners[0].name}</p>
                {app.owners[0].role && (
                  <p className="text-[12px] text-[var(--color-text-tertiary)]">{app.owners[0].role}</p>
                )}
              </div>
            </div>
          </div>

          {/* Date Modified */}
          <div>
            <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1">Date Modified</p>
            <p className="text-[16px] font-bold text-[var(--color-text-primary)]">{app.dateModified}</p>
            <p className="text-[12px] text-[var(--color-text-tertiary)]">by {app.modifiedBy}</p>
          </div>

          {/* Tags — spanning full width */}
          <div className="col-span-2">
            <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1.5">Tags</p>
            <div className="flex flex-wrap gap-1.5">
              {app.tags.slice(0, 2).map((tag) => (
                <span
                  key={tag}
                  className="bg-[#e2e1e8] text-[#3f3b47] px-3 py-1.5 rounded-full text-xs font-medium"
                >
                  {tag}
                </span>
              ))}
              {app.tags.length > 2 && (
                <span className="bg-[var(--color-brand-primary)]/10 text-[var(--color-brand-primary)] px-3 py-1.5 rounded-full text-xs font-medium">
                  +{app.tags.length - 2}
                </span>
              )}
            </div>
          </div>
        </div>
        </div>
      </Card>
    </div>
  );
}
