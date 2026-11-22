import type { ReactNode } from 'react';
import { Sheet, SheetContent, SheetHeader } from '@/components/ui/sheet';

export function EditorSidePanel({ compact, open, onOpenChange, title, side = 'right', width = 320, children }: {
  compact: boolean; open: boolean; onOpenChange: (value: boolean) => void; title: string;
  side?: 'left' | 'right'; width?: number; children: ReactNode;
}) {
  if (!compact) return <>{children}</>;
  return <Sheet open={open} onOpenChange={onOpenChange}>
    <SheetContent side={side} aria-describedby={undefined} className="p-0" style={{ width, maxWidth: '100vw' }}>
      <SheetHeader title={title} />
      <div className="flex min-h-0 flex-1 overflow-hidden">{children}</div>
    </SheetContent>
  </Sheet>;
}
