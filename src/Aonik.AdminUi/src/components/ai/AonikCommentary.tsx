import type { ReactNode } from 'react';
import { AgentAvatar } from './AgentAvatar';

/** Shared presentation for the main assistant's page commentary. Content remains source-owned. */
export function AonikCommentary({ children }: { children: ReactNode }) {
  return (
    <div className="mt-3 flex items-start gap-3" data-component="aonik-commentary">
      <AgentAvatar name="aonik" className="size-10" />
      <div className="min-w-0">
        <p className="text-xs font-medium text-foreground">AONIK <span className="ml-1 font-normal text-muted-foreground">AI assistant · Workspace brief</span></p>
        <p className="mt-1 text-[13px] text-muted-foreground">{children}</p>
      </div>
    </div>
  );
}
