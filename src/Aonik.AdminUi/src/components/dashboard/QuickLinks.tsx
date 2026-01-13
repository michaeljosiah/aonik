import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { MoreVertical, FileText, Bot, Monitor, Grid3x3, Sparkles, FilePlus, ArrowRightLeft, UserCog, ScrollText } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
    <Card className="h-full">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
        <CardTitle className="text-base font-semibold">Quick links</CardTitle>
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
          <MoreVertical className="w-4 h-4" />
        </Button>
      </CardHeader>
      <CardContent className="space-y-2">
        {links.map((link) => {
          const Icon = iconMap[link.icon] || FileText;
          const isSpecial = link.label === 'AI Assistant';
          
          return (
            <a
              key={link.id}
              href={link.href}
              className="flex items-center gap-3 p-2 -mx-2 rounded-lg hover:bg-[var(--color-background)] transition-colors"
            >
              <div
                className={`p-2 rounded-lg ${
                  isSpecial
                    ? 'bg-[var(--color-brand-secondary-light)]'
                    : 'bg-[var(--color-background)]'
                }`}
              >
                <Icon
                  className={`w-4 h-4 ${
                    isSpecial
                      ? 'text-[var(--color-brand-secondary)]'
                      : 'text-[var(--color-text-secondary)]'
                  }`}
                />
              </div>
              <span className="text-sm text-[var(--color-text-primary)]">{link.label}</span>
            </a>
          );
        })}
      </CardContent>
    </Card>
  );
}
