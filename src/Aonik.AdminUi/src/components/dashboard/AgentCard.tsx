import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { MoreVertical, CheckSquare, MessageSquare } from 'lucide-react';
import type { AgentCard as AgentCardType, VisibilityLevel } from '@/types';

interface AgentCardProps {
  agent: AgentCardType;
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
    <div className="w-16 h-16 rounded-full bg-[var(--color-brand-secondary-light)] flex items-center justify-center">
      <svg viewBox="0 0 48 48" className="w-10 h-10">
        {/* Simple owl/agent avatar */}
        <circle cx="24" cy="24" r="20" fill="#E8A838" />
        <circle cx="18" cy="20" r="6" fill="white" />
        <circle cx="30" cy="20" r="6" fill="white" />
        <circle cx="18" cy="20" r="3" fill="#1A1A1A" />
        <circle cx="30" cy="20" r="3" fill="#1A1A1A" />
        <ellipse cx="24" cy="30" rx="4" ry="3" fill="#D4942A" />
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

export function AgentCard({ agent }: AgentCardProps) {
  const pluginColors = ['#E8A838', '#0D7377', '#3B82F6'];

  return (
    <div className="relative pt-8">
      {/* Avatar positioned to overlap the card top */}
      <div className="absolute top-0 left-4 z-10">
        <AgentAvatar />
      </div>
      
      <Card className="flex flex-col h-full overflow-visible hover:shadow-md transition-shadow bg-[var(--color-surface-elevated)] border-[var(--color-border)]">
        {/* Header with Actions (avatar space reserved) */}
        <div className="p-4 pb-0 flex items-start justify-end">
          <div className="flex items-center gap-1">
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
              <CheckSquare className="w-4 h-4" />
            </Button>
            <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
              <MoreVertical className="w-4 h-4" />
            </Button>
          </div>
        </div>

        {/* Content */}
        <div className="p-4 flex-1 flex flex-col">
          <h3 className="text-base font-semibold text-[var(--color-text-primary)] mb-1.5">
            {agent.name}
          </h3>
          <p className="text-sm text-[var(--color-text-secondary)] line-clamp-3 mb-4">
            {agent.description}
          </p>

          {/* Visibility and Source */}
          <div className="flex items-center justify-between text-xs mb-4">
            <div>
              <p className="text-[var(--color-text-tertiary)] uppercase tracking-wide mb-1.5">Visibility</p>
              <VisibilityBadge visibility={agent.visibility} />
            </div>
            <div className="text-right">
              <p className="text-[var(--color-text-tertiary)] uppercase tracking-wide mb-1.5">Source</p>
              <p className="font-medium text-[var(--color-text-primary)]">{agent.source}</p>
            </div>
          </div>

          {/* Skills */}
          <div className="mb-4">
            <p className="text-[var(--color-text-tertiary)] uppercase tracking-wide text-xs mb-1.5">Skills</p>
            <div className="flex flex-wrap gap-1.5">
              {agent.skills.slice(0, 3).map((skill) => (
                <Badge key={skill} variant="secondary" className="text-xs">
                  {skill}
                </Badge>
              ))}
              {agent.skills.length > 3 && (
                <Badge variant="default" className="text-xs">
                  +{agent.skills.length - 3}
                </Badge>
              )}
            </div>
          </div>

          {/* Plugins */}
          <div className="mb-4">
            <p className="text-[var(--color-text-tertiary)] uppercase tracking-wide text-xs mb-1.5">Plugins</p>
            <div className="flex gap-2">
              {agent.plugins.slice(0, 3).map((_, index) => (
                <PluginIcon key={index} color={pluginColors[index % pluginColors.length]} />
              ))}
            </div>
          </div>

          {/* Chat Button */}
          <Button variant="default" className="w-full mt-auto gap-2">
            <MessageSquare className="w-4 h-4" />
            Chat with agent
          </Button>
        </div>
      </Card>
    </div>
  );
}
