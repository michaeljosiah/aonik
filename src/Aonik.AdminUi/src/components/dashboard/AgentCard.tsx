import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { MoreVertical, CheckSquare, MessageSquare } from 'lucide-react';
import type { AgentCard as AgentCardType, VisibilityLevel } from '@/types';
import { cn } from '@/lib/utils';

interface AgentCardProps {
  agent: AgentCardType;
  onChat?: (agentId: string) => void;
}

function VisibilityBadge({ visibility }: { visibility: VisibilityLevel }) {
  switch (visibility) {
    case 'team':
      return <Badge variant="team">Team</Badge>;
    case 'enterprise':
      return <Badge variant="enterprise">Enterprise</Badge>;
    case 'private':
      return <Badge variant="secondary">Private</Badge>;
    default:
      return null;
  }
}

function AgentAvatar() {
  return (
    <div className="w-24 h-24 rounded-full bg-[var(--color-brand-secondary-light)] flex items-center justify-center border-2 border-gray-300">
      <svg viewBox="0 0 48 48" className="w-12 h-12">
        {/* Simple owl/agent avatar */}
        <circle cx="24" cy="24" r="20" fill="#eb5c37" />
        <circle cx="18" cy="20" r="6" fill="white" />
        <circle cx="30" cy="20" r="6" fill="white" />
        <circle cx="18" cy="20" r="3" fill="#2f2f2f" />
        <circle cx="30" cy="20" r="3" fill="#2f2f2f" />
        <ellipse cx="24" cy="30" rx="4" ry="3" fill="#d44a28" />
      </svg>
    </div>
  );
}

function PluginIcon({ color }: { color: string }) {
  return (
    <div
      className="w-8 h-8 rounded-md flex items-center justify-center"
      style={{ backgroundColor: color }}
    >
      <svg viewBox="0 0 16 16" className="w-4 h-4 text-white" fill="currentColor">
        <rect x="3" y="3" width="10" height="10" rx="2" />
      </svg>
    </div>
  );
}

export function AgentCard({ agent, onChat }: AgentCardProps) {
  const pluginColors = ['#eb5c37', '#055a60', '#3B82F6'];

  return (
    <div className="relative w-full pt-24 cursor-pointer group">
      <div className="absolute left-6 top-0 z-[2]">
        <AgentAvatar />
      </div>

      <Card className={cn(
        'relative flex flex-col overflow-visible',
        'border border-[#d9d9e3] rounded-[4px]',
        'transition-all duration-300',
        'hoverBorder hover:border-[var(--color-brand-primary)] hover:shadow-lg hover:scale-[1.01]',
        'bg-[var(--color-surface-elevated)]',
      )}>
        <div className="relative w-full mt-4 p-2 flex flex-col flex-1">
          <div className="absolute -top-6 right-4 flex items-center justify-end gap-0.5">
            <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-[var(--color-text-tertiary)]">
              <CheckSquare className="w-3.5 h-3.5" />
            </Button>
            <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-[var(--color-text-tertiary)]">
              <MoreVertical className="w-3.5 h-3.5" />
            </Button>
          </div>

          <div className="flex flex-1 flex-col px-2 pb-4">
            <h3 className="text-[18px] font-bold text-[var(--color-text-heading)] line-clamp-1 mb-1.5">
              {agent.name}
            </h3>
            <p className="text-[13px] leading-6 text-[var(--color-text-secondary)] line-clamp-3 mb-4 min-h-[54px]">
              {agent.description}
            </p>

            <div className="flex items-center justify-between text-xs mb-4">
              <div>
                <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1.5">Visibility</p>
                <VisibilityBadge visibility={agent.visibility} />
              </div>
              <div className="text-right">
                <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1.5">Source</p>
                <p className="font-medium text-[var(--color-text-primary)]">{agent.source}</p>
              </div>
            </div>

            <div className="mb-4">
              <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1.5">Skills</p>
              <div className="flex flex-wrap gap-1.5">
                {agent.skills.slice(0, 3).map((skill) => (
                  <span
                    key={skill}
                    className="bg-[#e2e1e8] text-[#3f3b47] px-3 py-1.5 rounded-full text-xs font-medium"
                  >
                    {skill}
                  </span>
                ))}
                {agent.skills.length > 3 && (
                  <span className="bg-[var(--color-brand-primary)]/10 text-[var(--color-brand-primary)] px-3 py-1.5 rounded-full text-xs font-medium">
                    +{agent.skills.length - 3}
                  </span>
                )}
              </div>
            </div>

            <div className="mb-4">
              <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1.5">Plugins</p>
              <div className="flex gap-2">
                {agent.plugins.slice(0, 3).map((_, index) => (
                  <PluginIcon key={index} color={pluginColors[index % pluginColors.length]} />
                ))}
              </div>
            </div>

            <Button
              variant="default"
              className="w-full mt-auto gap-2 rounded-[2px]"
              onClick={() => onChat?.(agent.id)}
              disabled={!onChat}
            >
              <MessageSquare className="w-4 h-4" />
              Chat with agent
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
