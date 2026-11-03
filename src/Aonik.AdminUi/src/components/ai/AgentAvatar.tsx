import { Bot } from 'lucide-react';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { agentDisplayName, resolveAgentPortrait } from '@/lib/agentIdentity';
import { cn } from '@/lib/utils';

export function AgentAvatar({ name, className }: { name?: string; className?: string }) {
  const portrait = name ? resolveAgentPortrait(name) : undefined;
  const label = agentDisplayName(name);
  return (
    <Avatar className={cn('size-8 shrink-0 rounded-full border border-border', className)} aria-label={`${label} · AI agent`}>
      {portrait && <AvatarImage src={portrait} alt={`${label} · AI agent`} className="object-cover" />}
      <AvatarFallback className="bg-muted text-muted-foreground"><Bot className="size-4" /></AvatarFallback>
    </Avatar>
  );
}
