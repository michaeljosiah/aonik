import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { FileText, Bot, Monitor, Grid3x3, Sparkles, FilePlus, ArrowRightLeft, UserCog, ScrollText, MoreVertical } from 'lucide-react';
import type { QuickLink } from '@/types';

const iconMap: Record<string, React.ElementType> = {
  FileText,
  Bot,
  Monitor,
  Grid3x3,
  Sparkles,
  FilePlus,
  ArrowRightLeft,
  UserCog,
  ScrollText,
};

interface QuickLinksProps {
  links: QuickLink[];
}

export function QuickLinks({ links }: QuickLinksProps) {
  return (
    <Card className="h-full rounded-[4px] px-4 py-3 flex flex-col overflow-hidden">
      <div className="mb-3 flex items-center justify-between shrink-0">
        <span className="text-[18px] font-bold text-[var(--color-text-primary)]">Quick links</span>
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
          <MoreVertical className="w-4 h-4" />
        </Button>
      </div>

      <div className="flex flex-col gap-4 mt-1 ml-1 overflow-y-auto flex-1 visible-scrollbar">
        {links.map((link) => {
          const Icon = iconMap[link.icon] || FileText;

          return (
            <a
              key={link.id}
              href={link.href}
              className="flex items-center gap-3 text-sm font-medium group"
            >
              <Icon className="w-[16px] h-[16px] text-[var(--color-text-tertiary)] flex-shrink-0" />
              <span className="text-[var(--color-text-secondary)] cursor-pointer group-hover:text-[var(--color-text-primary)] group-hover:underline transition-all">
                {link.label}
              </span>
            </a>
          );
        })}
      </div>
    </Card>
  );
}
