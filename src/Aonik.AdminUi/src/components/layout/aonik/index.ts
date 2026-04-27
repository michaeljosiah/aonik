// Primitives barrel.
//
// IMPORTANT: do not re-export anything that depends on `@/modules` or
// `@/workspace/registry`. Pages from the finance module (and other modules
// whose registry is statically imported on app boot) consume this barrel.
// Re-exporting registry-aware components creates an initialisation cycle:
//
//   pages/X → @/components/layout/aonik → <registry-aware> → useModules
//     → registry → finance module → pages/X (still initialising) → TDZ
//
// Excluded for that reason — import them directly from their source files
// when needed by the app shell:
//
//   import { AonikSidebar } from '@/components/layout/aonik/AonikSidebar';
//   import { AonikTopBar } from '@/components/layout/aonik/AonikTopBar';
//   import { NavPopover } from '@/components/layout/aonik/NavPopover';

export { AonikMark, AonikWordmark } from './AonikMark';
export { ProposalCard } from './ProposalCard';
export type { ProposalCardProps, ProposalDiffLine } from './ProposalCard';
export { Card } from './Card';
export type { CardProps } from './Card';
export { KpiTile } from './KpiTile';
export type { KpiTileProps } from './KpiTile';
export { PageHeader } from './PageHeader';
export type { PageHeaderProps } from './PageHeader';
export { FilterBar } from './FilterBar';
export type { FilterBarProps, FilterBarTab } from './FilterBar';
export { Pill } from './Pill';
export type { PillProps, PillTone } from './Pill';
export { AgentAvatar } from './AgentAvatar';
export type { AgentAvatarProps } from './AgentAvatar';
