// Primitives barrel.
//
// IMPORTANT: do not re-export AonikSidebar / AonikTopBar from this file.
// Those components depend on the module registry (`useModules`), and the
// module registry statically imports page files. Re-exporting them here
// creates a cycle when a page imports from this barrel:
//
//   pages/X → @/components/layout/aonik → AonikSidebar → useModules
//     → registry → finance module → pages/X (initialising)
//
// Import the shell components directly from their source files instead:
//
//   import { AonikSidebar } from '@/components/layout/aonik/AonikSidebar';
//   import { AonikTopBar } from '@/components/layout/aonik/AonikTopBar';

export { AonikMark, AonikWordmark } from './AonikMark';
export { NavPopover } from './NavPopover';
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
