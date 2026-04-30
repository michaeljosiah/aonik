// Single node rendering inside the workflow canvas SVG. Mirrors NodeShape
// from templates/aonik-admin-starterkit/screens/workflow-editor.jsx.
//
// Renders the rounded card body + tint header band + left rail + icon chip,
// the kind label and node label/summary, an error pip when validation
// flags the node, a green check when a trace marks it done, and one input
// + N output ports.

import {
  Bolt,
  Check,
  Clock,
  GitFork,
  RefreshCw,
  Send,
  Sparkles,
  Users,
  Wrench,
  Zap,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { NODE_KIND } from './stepKindCatalog';
import {
  HEADER_H,
  NODE_H,
  NODE_W,
  PORT_R,
  outPortPos,
} from './editorGeometry';
import type { WorkflowNode } from './workflowMockData';

const ICON_BY_NAME: Record<string, LucideIcon> = {
  Wrench,
  Sparkles,
  GitFork,
  Users,
  Clock,
  Check,
  Send,
  Zap,
  RefreshCw,
  Bolt,
};

export interface NodeShapeProps {
  node: WorkflowNode;
  selected: boolean;
  traceCurrent?: boolean;
  traceDone?: boolean;
  hasError?: boolean;
  onMouseDown: (e: React.MouseEvent<SVGElement>) => void;
  onPortOutDown: (e: React.MouseEvent<SVGCircleElement>, idx: number) => void;
  onPortInUp: (e: React.MouseEvent<SVGCircleElement>) => void;
}

export function NodeShape({
  node,
  selected,
  traceCurrent = false,
  traceDone = false,
  hasError = false,
  onMouseDown,
  onPortOutDown,
  onPortInUp,
}: NodeShapeProps) {
  const meta = NODE_KIND[node.kind];
  const Icon = ICON_BY_NAME[meta.icon] ?? Bolt;
  const tint = meta.tint;
  const total = meta.outputs ?? 1;

  let ringColor = 'transparent';
  if (traceCurrent) ringColor = '#3ab795';
  else if (selected) ringColor = 'var(--color-brand-primary)';
  else if (hasError) ringColor = '#c44536';
  const ringWidth = traceCurrent || selected || hasError ? 2 : 0;

  return (
    <g transform={`translate(${node.x}, ${node.y})`} style={{ cursor: 'grab' }}>
      {ringWidth > 0 && (
        <rect
          x={-3}
          y={-3}
          width={NODE_W + 6}
          height={NODE_H + 6}
          rx={9}
          fill="none"
          stroke={ringColor}
          strokeWidth={ringWidth}
          strokeDasharray={traceCurrent ? '6 3' : 'none'}
          style={{
            animation: traceCurrent ? 'aonik-pulse 1.4s infinite' : 'none',
          }}
        />
      )}

      {/* card body */}
      <rect
        x={0}
        y={0}
        width={NODE_W}
        height={NODE_H}
        rx={7}
        fill="var(--color-surface)"
        stroke="var(--color-border-light)"
        strokeWidth={1}
        filter="drop-shadow(0 1px 2px rgba(0,0,0,0.04))"
        onMouseDown={onMouseDown}
      />

      {/* tint header band — duplicated below to mask the bottom corners */}
      <rect
        x={0}
        y={0}
        width={NODE_W}
        height={HEADER_H}
        rx={7}
        fill={tint + '14'}
        stroke="none"
        onMouseDown={onMouseDown}
      />
      <rect
        x={0}
        y={HEADER_H - 7}
        width={NODE_W}
        height={7}
        fill={tint + '14'}
        stroke="none"
        onMouseDown={onMouseDown}
      />

      {/* tint left rail */}
      <rect x={0} y={0} width={3} height={NODE_H} fill={tint} />

      {/* icon chip */}
      <foreignObject x={10} y={6} width={18} height={18} pointerEvents="none">
        <div
          style={{
            width: 18,
            height: 18,
            borderRadius: 4,
            background: tint,
            color: '#fff',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Icon size={10} />
        </div>
      </foreignObject>

      {/* kind label */}
      <text
        x={34}
        y={20}
        fontSize="10"
        fontWeight={600}
        fill={tint}
        letterSpacing="0.06em"
        style={{ textTransform: 'uppercase', pointerEvents: 'none' }}
      >
        {meta.label}
      </text>

      {/* error pip */}
      {hasError && (
        <g transform={`translate(${NODE_W - 22}, 6)`} pointerEvents="none">
          <circle cx={8} cy={8} r={7} fill="#c44536" />
          <text
            x={8}
            y={11.5}
            textAnchor="middle"
            fontSize="10"
            fontWeight={700}
            fill="#fff"
          >
            !
          </text>
        </g>
      )}

      {/* node label + summary */}
      <foreignObject
        x={10}
        y={HEADER_H + 4}
        width={NODE_W - 20}
        height={NODE_H - HEADER_H - 6}
        pointerEvents="none"
      >
        <div
          style={{
            fontFamily: 'var(--font-sans)',
            display: 'flex',
            flexDirection: 'column',
            gap: 2,
            padding: '2px 0',
          }}
        >
          <div
            style={{
              fontSize: 12.5,
              fontWeight: 600,
              color: 'var(--color-text-primary)',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {node.label}
          </div>
          {node.summary && (
            <div
              style={{
                fontSize: 10.5,
                color: 'var(--color-text-tertiary)',
                fontFamily: 'var(--font-mono)',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }}
            >
              {node.summary}
            </div>
          )}
        </div>
      </foreignObject>

      {/* trace done check */}
      {traceDone && !traceCurrent && (
        <g transform={`translate(${NODE_W - 20}, ${NODE_H - 20})`} pointerEvents="none">
          <circle cx={8} cy={8} r={8} fill="#3ab795" />
          <path d="M 4 8 L 7 11 L 12 5" stroke="#fff" strokeWidth={2} fill="none" />
        </g>
      )}

      {/* Input port */}
      {(meta.inputs ?? 0) > 0 && (
        <g transform={`translate(0, ${NODE_H / 2})`}>
          <circle cx={0} cy={0} r={9} fill="transparent" onMouseUp={onPortInUp} />
          <circle
            cx={0}
            cy={0}
            r={PORT_R}
            fill="var(--color-surface)"
            stroke={tint}
            strokeWidth={2}
            pointerEvents="none"
          />
        </g>
      )}

      {/* Output port(s) */}
      {Array.from({ length: total }).map((_, idx) => {
        const p = outPortPos({ x: 0, y: 0 }, idx, total);
        return (
          <g
            key={idx}
            transform={`translate(${p.x}, ${p.y})`}
            style={{ cursor: 'crosshair' }}
          >
            <circle
              cx={0}
              cy={0}
              r={9}
              fill="transparent"
              onMouseDown={(e) => onPortOutDown(e, idx)}
            />
            <circle
              cx={0}
              cy={0}
              r={PORT_R}
              fill={tint}
              stroke="var(--color-surface)"
              strokeWidth={2}
              pointerEvents="none"
            />
            {total > 1 && (
              <text
                x={14}
                y={3}
                fontSize="9"
                fontFamily="var(--font-mono)"
                fill="var(--color-text-tertiary)"
                pointerEvents="none"
              >
                {node.kind === 'decision'
                  ? idx === 0
                    ? 'yes'
                    : 'no'
                  : idx === 0
                    ? 'body'
                    : 'done'}
              </text>
            )}
          </g>
        );
      })}
    </g>
  );
}
