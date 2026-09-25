import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { MoreVertical, CheckSquare, MessageSquare, Shield, Check, X } from 'lucide-react';
import type { AgentCard as AgentCardType, VisibilityLevel } from '@/types';
import { cn } from '@/lib/utils';

interface AgentCardProps {
  agent: AgentCardType;
  onChat?: (agentId: string) => void;
  onClick?: () => void;
  /** Show additional config metadata (risk tier, model, active status). */
  showConfigMeta?: boolean;
  /** Render extra action buttons in the top-right corner. */
  actions?: React.ReactNode;
}

const riskTierBadge: Record<string, { variant: 'success' | 'warning' | 'outline'; className?: string }> = {
  low: { variant: 'success' },
  medium: { variant: 'warning' },
  high: { variant: 'outline', className: 'border-transparent bg-destructive/10 text-destructive' },
};

function VisibilityBadge({ visibility }: { visibility: VisibilityLevel }) {
  switch (visibility) {
    case 'team':
      return <Badge variant="default">Team</Badge>;
    case 'enterprise':
      return <Badge variant="outline">Enterprise</Badge>;
    case 'private':
      return <Badge variant="secondary">Private</Badge>;
    default:
      return null;
  }
}

function AgentAvatar({ iconUrl }: { iconUrl?: string }) {
  return (
    <div className="w-[60px] h-[60px] rounded-full bg-agent/10 flex items-center justify-center border overflow-hidden">
      {iconUrl ? (
        <img src={iconUrl} alt="" className="w-full h-full object-cover" />
      ) : (
        <svg viewBox="0 0 48 48" className="w-9 h-9">
          {/* Default owl/agent avatar */}
          <circle cx="24" cy="24" r="20" fill="var(--agent)" />
          <circle cx="18" cy="20" r="6" fill="white" />
          <circle cx="30" cy="20" r="6" fill="white" />
          <circle cx="18" cy="20" r="3" fill="color-mix(in oklab, var(--agent) 15%, black)" />
          <circle cx="30" cy="20" r="3" fill="color-mix(in oklab, var(--agent) 15%, black)" />
          <ellipse cx="24" cy="30" rx="4" ry="3" fill="color-mix(in oklab, var(--agent) 85%, black)" />
        </svg>
      )}
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

export function AgentCard({ agent, onChat, onClick, showConfigMeta, actions }: AgentCardProps) {
  const pluginColors = ['var(--chart-3)', 'var(--chart-1)', 'var(--chart-2)'];
  const riskStyle = agent.riskTier ? (riskTierBadge[agent.riskTier] ?? riskTierBadge.low) : null;

  return (
    <div
      className={cn('relative w-full pt-10', onClick && 'cursor-pointer')}
      onClick={onClick}
    >
      <div className="absolute left-4 top-4 z-10">
        <AgentAvatar iconUrl={agent.avatar} />
      </div>

      <Card className={cn(
        'flex flex-col h-full overflow-visible',
        'border rounded-lg',
        'transition-all duration-300',
        'hover:border-primary hover:shadow-lg hover:scale-[1.01]',
        'bg-popover',
      )}>
        <div className="flex items-center justify-end gap-0.5 px-4 pt-3">
          {actions ?? (
            <>
              <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-muted-foreground" aria-label="Select">
                <CheckSquare className="w-3.5 h-3.5" />
              </Button>
              <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-muted-foreground" aria-label="More actions">
                <MoreVertical className="w-3.5 h-3.5" />
              </Button>
            </>
          )}
        </div>

        <div className="flex flex-1 flex-col px-4 pb-4 mt-6 pt-2">
          <div className="flex items-center gap-2 mb-1.5">
            <h3 className="text-[18px] font-bold text-foreground line-clamp-1">
              {agent.name}
            </h3>
            {agent.isOverride && (
              <span className="px-1.5 py-0.5 rounded-md text-[10px] font-medium bg-primary/10 text-primary">
                OVERRIDE
              </span>
            )}
          </div>
          <p className="text-[13px] leading-6 text-muted-foreground line-clamp-3 mb-4 min-h-[54px]">
            {agent.description}
          </p>

          {/* Config metadata row (risk tier, model, active status) */}
          {showConfigMeta && (
            <div className="flex flex-wrap items-center gap-2 mb-4">
              {riskStyle && agent.riskTier && (
                <Badge variant={riskStyle.variant} className={riskStyle.className}>
                  <Shield /> {agent.riskTier}
                </Badge>
              )}
              {agent.isActive !== undefined && (
                agent.isActive ? (
                  <Badge variant="success">
                    <Check /> Active
                  </Badge>
                ) : (
                  <Badge variant="secondary" className="text-muted-foreground">
                    <X /> Inactive
                  </Badge>
                )
              )}
              {agent.modelName && (
                <span className="text-xs text-muted-foreground bg-muted px-2 py-0.5 rounded-md">
                  {agent.modelName}
                </span>
              )}
            </div>
          )}

          <div className="flex items-center justify-between text-xs mb-4">
            <div>
              <p className="text-xs font-medium text-muted-foreground mb-1.5">Visibility</p>
              <VisibilityBadge visibility={agent.visibility} />
            </div>
            <div className="text-right">
              <p className="text-xs font-medium text-muted-foreground mb-1.5">Source</p>
              <p className="font-medium text-foreground">{agent.source}</p>
            </div>
          </div>

          <div className="mb-4">
            <p className="text-xs font-medium text-muted-foreground mb-1.5">Skills</p>
            <div className="flex flex-wrap gap-1.5">
              {agent.skills.length > 0 ? (
                <>
                  {agent.skills.slice(0, 3).map((skill) => (
                    <span
                      key={skill}
                      className="bg-secondary text-secondary-foreground px-3 py-1.5 rounded-md text-xs font-medium"
                    >
                      {skill}
                    </span>
                  ))}
                  {agent.skills.length > 3 && (
                    <span className="bg-primary/10 text-primary px-3 py-1.5 rounded-md text-xs font-medium">
                      +{agent.skills.length - 3}
                    </span>
                  )}
                </>
              ) : (
                <span className="text-xs text-muted-foreground">None configured</span>
              )}
            </div>
          </div>

          <div className="mb-4">
            <p className="text-xs font-medium text-muted-foreground mb-1.5">Plugins</p>
            <div className="flex gap-2">
              {agent.plugins.length > 0 ? (
                agent.plugins.slice(0, 3).map((_, index) => (
                  <PluginIcon key={index} color={pluginColors[index % pluginColors.length]} />
                ))
              ) : (
                <span className="text-xs text-muted-foreground">None configured</span>
              )}
              {agent.plugins.length > 3 && (
                <span className="flex items-center text-xs text-muted-foreground">
                  +{agent.plugins.length - 3}
                </span>
              )}
            </div>
          </div>

          <Button
            variant="default"
            className="w-full mt-auto gap-2"
            onClick={(e) => {
              e.stopPropagation();
              onChat?.(agent.id);
            }}
            disabled={!onChat}
          >
            <MessageSquare className="w-4 h-4" />
            Chat with agent
          </Button>
        </div>
      </Card>
    </div>
  );
}
