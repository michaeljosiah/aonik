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

const riskTierStyles: Record<string, { text: string; bg: string }> = {
  low: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  medium: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  high: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

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

function AgentAvatar({ iconUrl }: { iconUrl?: string }) {
  return (
    <div className="w-[60px] h-[60px] rounded-full bg-[var(--color-brand-secondary-light)] flex items-center justify-center border border-[#CFCDD9] overflow-hidden">
      {iconUrl ? (
        <img src={iconUrl} alt="" className="w-full h-full object-cover" />
      ) : (
        <svg viewBox="0 0 48 48" className="w-9 h-9">
          {/* Default owl/agent avatar */}
          <circle cx="24" cy="24" r="20" fill="#eb5c37" />
          <circle cx="18" cy="20" r="6" fill="white" />
          <circle cx="30" cy="20" r="6" fill="white" />
          <circle cx="18" cy="20" r="3" fill="#2f2f2f" />
          <circle cx="30" cy="20" r="3" fill="#2f2f2f" />
          <ellipse cx="24" cy="30" rx="4" ry="3" fill="#d44a28" />
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
  const pluginColors = ['#eb5c37', '#055a60', '#3B82F6'];
  const riskStyle = agent.riskTier ? (riskTierStyles[agent.riskTier] ?? riskTierStyles.low) : null;

  return (
    <div
      className={cn('relative w-full pt-10', onClick && 'cursor-pointer')}
      onClick={onClick}
    >
      <div className="absolute left-4 top-4 z-[2]">
        <AgentAvatar iconUrl={agent.avatar} />
      </div>

      <Card className={cn(
        'flex flex-col h-full overflow-visible',
        'border border-[#d9d9e3] rounded-[4px]',
        'transition-all duration-300',
        'hoverBorder hover:border-[var(--color-brand-primary)] hover:shadow-lg hover:scale-[1.01]',
        'bg-[var(--color-surface-elevated)]',
      )}>
        <div className="flex items-center justify-end gap-0.5 px-4 pt-3">
          {actions ?? (
            <>
              <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-[var(--color-text-tertiary)]">
                <CheckSquare className="w-3.5 h-3.5" />
              </Button>
              <Button variant="ghost" size="icon-sm" className="h-6 w-6 text-[var(--color-text-tertiary)]">
                <MoreVertical className="w-3.5 h-3.5" />
              </Button>
            </>
          )}
        </div>

        <div className="flex flex-1 flex-col px-4 pb-4 mt-6 pt-2">
          <div className="flex items-center gap-2 mb-1.5">
            <h3 className="text-[18px] font-bold text-[var(--color-text-heading)] line-clamp-1">
              {agent.name}
            </h3>
            {agent.isOverride && (
              <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
                OVERRIDE
              </span>
            )}
          </div>
          <p className="text-[13px] leading-6 text-[var(--color-text-secondary)] line-clamp-3 mb-4 min-h-[54px]">
            {agent.description}
          </p>

          {/* Config metadata row (risk tier, model, active status) */}
          {showConfigMeta && (
            <div className="flex flex-wrap items-center gap-2 mb-4">
              {riskStyle && agent.riskTier && (
                <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${riskStyle.bg} ${riskStyle.text}`}>
                  <Shield className="w-3 h-3" /> {agent.riskTier}
                </span>
              )}
              {agent.isActive !== undefined && (
                agent.isActive ? (
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
                    <Check className="w-3 h-3" /> Active
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
                    <X className="w-3 h-3" /> Inactive
                  </span>
                )
              )}
              {agent.modelName && (
                <span className="text-xs text-[var(--color-text-tertiary)] bg-[var(--color-surface-inset)] px-2 py-0.5 rounded">
                  {agent.modelName}
                </span>
              )}
            </div>
          )}

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
              {agent.skills.length > 0 ? (
                <>
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
                </>
              ) : (
                <span className="text-xs text-[var(--color-text-tertiary)]">None configured</span>
              )}
            </div>
          </div>

          <div className="mb-4">
            <p className="font-bold text-[var(--color-text-heading)] text-[10px] uppercase tracking-wide mb-1.5">Plugins</p>
            <div className="flex gap-2">
              {agent.plugins.length > 0 ? (
                agent.plugins.slice(0, 3).map((_, index) => (
                  <PluginIcon key={index} color={pluginColors[index % pluginColors.length]} />
                ))
              ) : (
                <span className="text-xs text-[var(--color-text-tertiary)]">None configured</span>
              )}
              {agent.plugins.length > 3 && (
                <span className="flex items-center text-xs text-[var(--color-text-tertiary)]">
                  +{agent.plugins.length - 3}
                </span>
              )}
            </div>
          </div>

          <Button
            variant="default"
            className="w-full mt-auto gap-2 rounded-[2px]"
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
