import { useRef, useState, type ReactNode } from 'react';
import { Sheet, SheetContent, SheetHeader } from '@/components/ui/sheet';

export function EditorSidePanel({ compact, open, onOpenChange, title, side = 'right', width = 320, children }: {
  compact: boolean; open: boolean; onOpenChange: (value: boolean) => void; title: string;
  side?: 'left' | 'right'; width?: number; children: ReactNode;
}) {
  const content = useRef<HTMLDivElement>(null);
  const [closeBlocked, setCloseBlocked] = useState(false);
  if (!compact) return <>{children}</>;
  return <Sheet open={open} onOpenChange={nextOpen => {
    const invalid = content.current?.querySelector<HTMLTextAreaElement>('textarea:invalid');
    if (!nextOpen && invalid) { setCloseBlocked(true); invalid.focus(); return; }
    setCloseBlocked(false); onOpenChange(nextOpen);
  }}>
    <SheetContent ref={content} side={side} aria-describedby={undefined} className="p-0" style={{ width, maxWidth: '100vw' }}>
      <SheetHeader title={title} />
      {closeBlocked && <p role="alert" className="px-4 pt-3 text-sm text-destructive">Fix the invalid JSON or discard that field's invalid edit before closing.</p>}
      <div className="flex min-h-0 flex-1 overflow-hidden">{children}</div>
    </SheetContent>
  </Sheet>;
}
