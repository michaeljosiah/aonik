// Compact horizontal step rail used inside each workflow card on the
// registry page. Mirrors the StepRail component in
// templates/aonik-admin-starterkit/screens/workflows.jsx — tinted chip
// per step + arrow connectors.

import {
  Bolt,
  Check,
  Clock,
  Columns,
  GitFork,
  Play,
  RefreshCw,
  Send,
  Sparkles,
  Users,
  Wrench,
  Zap,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { STEP_KIND } from './stepKindCatalog';
import type { WorkflowStep } from './workflowTypes';

const ICON_BY_NAME: Record<string, LucideIcon> = {
  Wrench,
  Sparkles,
  GitFork,
  Users,
  Clock,
  Check,
  Play,
  Send,
  Zap,
  Columns,
  RefreshCw,
  Bolt,
};

export interface StepRailProps {
  steps: WorkflowStep[];
  /** Tighter padding for embedded use. */
  dense?: boolean;
}

export function StepRail({ steps, dense = false }: StepRailProps) {
  return (
    <div className="flex items-center gap-0 flex-nowrap overflow-hidden">
      {steps.map((s, i) => {
        const meta = STEP_KIND[s.kind];
        const Icon = ICON_BY_NAME[meta.icon] ?? Bolt;
        const last = i === steps.length - 1;
        return (
          <div key={i} className="flex items-center flex-none">
            <div
              className="inline-flex items-center gap-1.5 flex-none whitespace-nowrap rounded-md"
              style={{
                padding: dense ? '4px 8px' : '6px 10px',
                background: meta.tint + '14',
                color: meta.tint,
                border: '1px solid ' + meta.tint + '30',
              }}
            >
              <Icon size={dense ? 10 : 11} />
              <span
                className="font-medium"
                style={{
                  fontSize: dense ? 10.5 : 11.5,
                  color: 'var(--color-text-primary)',
                  fontFamily: s.kind === 'tool' ? 'var(--font-mono)' : 'inherit',
                }}
              >
                {s.label}
              </span>
            </div>
            {!last && (
              <span
                className="relative flex-none"
                style={{
                  width: dense ? 12 : 16,
                  height: 1,
                  background: 'var(--color-border)',
                }}
              >
                <span
                  className="absolute"
                  style={{
                    right: -3,
                    top: -2,
                    width: 0,
                    height: 0,
                    borderLeft: '4px solid var(--color-border)',
                    borderTop: '3px solid transparent',
                    borderBottom: '3px solid transparent',
                  }}
                />
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
}
