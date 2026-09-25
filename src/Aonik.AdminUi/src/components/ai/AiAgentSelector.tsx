import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import { Check, ChevronDown } from 'lucide-react';

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

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <button
          type="button"
          className="inline-flex items-center gap-2 rounded-lg px-2 py-1 -ml-2 hover:bg-sidebar-accent"
        >
          <div className="h-7 w-7 rounded-full bg-card border border-border grid place-items-center">
            <span className="text-xs font-semibold text-foreground">C</span>
          </div>
          <span className="text-sm font-medium text-foreground">{selectedAgent?.title ?? 'Agent'}</span>
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        </button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          sideOffset={10}
          align="start"
          className="w-[280px] rounded-md border border-border bg-card shadow-lg p-2"
        >
          <div className="px-3 py-2 text-[11px] font-semibold text-muted-foreground tracking-wider">
            PERSONAL ASSISTANT
          </div>
          {agents
            .filter((a) => a.group === 'personal')
            .map((a) => (
              <DropdownMenu.Item key={a.id} onSelect={() => onSelectAgent(a.id)} className="outline-none">
                <div className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-accent">
                  <div className="h-8 w-8 rounded-lg bg-card border border-border grid place-items-center">
                    <span className="text-xs font-semibold text-foreground">{renderIcon(a.icon)}</span>
                  </div>
                  <div className="min-w-0">
                    <div className="text-sm font-medium text-foreground truncate">{a.title}</div>
                    <div className="text-xs text-muted-foreground truncate">{a.description}</div>
                  </div>
                  {a.id === selectedAgentId && (
                    <Check className="ml-auto h-4 w-4 text-primary" />
                  )}
                </div>
              </DropdownMenu.Item>
            ))}

          <div className="px-3 pt-3 pb-2 text-[11px] font-semibold text-muted-foreground tracking-wider">
            AGENTS
          </div>
          {agents
            .filter((a) => a.group === 'agents')
            .map((a) => (
              <DropdownMenu.Item key={a.id} onSelect={() => onSelectAgent(a.id)} className="outline-none">
                <div className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-accent">
                  <div className="h-8 w-8 rounded-lg bg-card border border-border grid place-items-center">
                    <span className="text-xs font-semibold text-foreground">{renderIcon(a.icon)}</span>
                  </div>
                  <div className="min-w-0">
                    <div className="text-sm font-medium text-foreground truncate">{a.title}</div>
                    <div className="text-xs text-muted-foreground truncate">{a.description}</div>
                  </div>
                  {a.id === selectedAgentId && (
                    <Check className="ml-auto h-4 w-4 text-primary" />
                  )}
                </div>
              </DropdownMenu.Item>
            ))}

          <div className="p-3">
            <button
              type="button"
              className="w-full h-10 rounded-lg border border-border bg-card text-sm text-foreground hover:bg-accent"
            >
              Manage agents
            </button>
          </div>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
