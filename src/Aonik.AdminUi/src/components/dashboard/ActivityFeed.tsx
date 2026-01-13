import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { MoreVertical, FileText, CheckCircle, Calendar, AlertCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { ActivityItem } from '@/types';

const iconMap: Record<string, React.ElementType> = {
  FileText,
  CheckCircle,
  Calendar,
  AlertCircle,
};

interface ActivityFeedProps {
  items: ActivityItem[];
}

export function ActivityFeed({ items }: ActivityFeedProps) {
  return (
    <Card className="h-full">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
        <CardTitle className="text-base font-semibold">Activity feed</CardTitle>
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
          <MoreVertical className="w-4 h-4" />
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {items.map((item) => {
          const Icon = iconMap[item.icon || 'FileText'] || FileText;
          return (
            <div key={item.id} className="flex items-start gap-3">
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-[var(--color-text-primary)] truncate">
                  {item.title}
                </p>
                {item.description && (
                  <p className="text-xs text-[var(--color-text-secondary)] truncate">
                    {item.description}
                  </p>
                )}
                <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">{item.timestamp}</p>
              </div>
              <div className="flex-shrink-0 p-2 rounded-full bg-[var(--color-brand-primary-light)]">
                <Icon className="w-4 h-4 text-[var(--color-brand-primary)]" />
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
