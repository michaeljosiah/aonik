import { Check, ChevronDown } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

export type AiAgentSelectorItem = {
  id: string;
  title: string;
  description: string;
  group: 'personal' | 'agents';
  icon: 'centrali' | 'fox';
};

type AiAgentSelectorProps = {
  agents: AiAgentSelectorItem[];
  selectedAgentId: string;
  onSelectAgent: (agentId: string) => void;
};

export function AiAgentSelector({ agents, selectedAgentId, onSelectAgent }: AiAgentSelectorProps) {
  const selectedAgent = agents.find((a) => a.id === selectedAgentId) ?? agents[0];

  const renderIcon = (icon: AiAgentSelectorItem['icon']) => {
    if (icon === 'centrali') return 'C';
    return 'A';
  };

  const renderItem = (a: AiAgentSelectorItem) => (
    <DropdownMenuItem key={a.id} onSelect={() => onSelectAgent(a.id)} className="gap-3 px-3 py-2">
      <div className="h-8 w-8 shrink-0 rounded-lg bg-card border border-border grid place-items-center">
        <span className="text-xs font-semibold text-foreground">{renderIcon(a.icon)}</span>
      </div>
      <div className="min-w-0">
        <div className="text-sm font-medium text-foreground truncate">{a.title}</div>
        <div className="text-xs text-muted-foreground truncate">{a.description}</div>
      </div>
      {a.id === selectedAgentId && <Check className="ml-auto h-4 w-4 text-primary" />}
    </DropdownMenuItem>
  );

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" variant="ghost" className="-ml-2 h-auto gap-2 px-2 py-1 hover:bg-sidebar-accent">
          <div className="h-7 w-7 rounded-full bg-card border border-border grid place-items-center">
            <span className="text-xs font-semibold text-foreground">C</span>
          </div>
          <span className="text-sm font-medium text-foreground">{selectedAgent?.title ?? 'Agent'}</span>
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent sideOffset={10} align="start" className="w-[280px] p-2">
        <DropdownMenuLabel className="px-3 text-xs text-muted-foreground">Personal assistant</DropdownMenuLabel>
        {agents.filter((a) => a.group === 'personal').map(renderItem)}

        <DropdownMenuLabel className="px-3 pt-3 text-xs text-muted-foreground">Agents</DropdownMenuLabel>
        {agents.filter((a) => a.group === 'agents').map(renderItem)}

        <div className="p-3">
          <Button type="button" variant="outline" className="w-full">
            Manage agents
          </Button>
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
