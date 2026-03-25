import { Button } from '@/components/ui/button';
import { Store, Eye, Grid2x2Plus } from 'lucide-react';

interface SectionHeaderProps {
  icon?: React.ReactNode;
  title: string;
  description: string;
  actions?: React.ReactNode;
}

export function SectionHeader({ icon, title, description, actions }: SectionHeaderProps) {
  return (
    <div className="flex items-center justify-between mb-1">
      <div className="flex items-center gap-3">
        {icon && (
          <div className="w-12 h-12 rounded-lg bg-[var(--color-brand-primary)] flex items-center justify-center flex-shrink-0">
            {icon}
          </div>
        )}
        <div>
          <h2 className="font-bold text-xl lg:text-[24px] text-[var(--color-text-heading)]">{title}</h2>
          <p className="text-sm text-[var(--color-text-secondary)]">{description}</p>
        </div>
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}

export function MyAppsHeader() {
  return (
    <SectionHeader
      icon={
        <Grid2x2Plus className="w-6 h-6 text-white" />
      }
      title="My apps"
      description="Your apps, tailored to your activity and preferences."
      actions={
        <>
          <Button variant="outline" size="sm" className="gap-1.5">
            <Store className="w-4 h-4" />
            Browse Application Store
          </Button>
          <Button variant="default" size="sm" className="gap-1.5">
            <Eye className="w-4 h-4" />
            View all apps
          </Button>
        </>
      }
    />
  );
}

export function MyAgentsHeader() {
  return (
    <SectionHeader
      icon={
        <svg viewBox="0 0 24 24" className="w-6 h-6 text-white" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="8" r="5" />
          <path d="M20 21a8 8 0 00-16 0" />
        </svg>
      }
      title="My agents"
      description="Agents you've created, used, or saved for quick access."
      actions={
        <Button variant="default" size="sm" className="gap-1.5">
          <Eye className="w-4 h-4" />
          View all agents
        </Button>
      }
    />
  );
}
