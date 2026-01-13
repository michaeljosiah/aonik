// Navigation Types
export interface NavItem {
  id: string;
  label: string;
  icon: string;
  href?: string;
  badge?: string;
  children?: NavItem[];
}

// User Types
export interface User {
  id: string;
  name: string;
  role: string;
  avatar?: string;
}

// Activity Feed Types
export interface ActivityItem {
  id: string;
  title: string;
  description?: string;
  timestamp: string;
  icon?: string;
}

// Quick Link Types
export interface QuickLink {
  id: string;
  label: string;
  icon: string;
  href: string;
}

// App Card Types
export type AppStatus = 'active' | 'pending' | 'request';

export interface AppOwner {
  id: string;
  name: string;
  role?: string;
  avatar?: string;
}

export interface AppCard {
  id: string;
  name: string;
  description: string;
  icon?: string;
  iconBgColor?: string;
  status: AppStatus;
  owners: AppOwner[];
  dateModified: string;
  modifiedBy: string;
  tags: string[];
}

// Agent Card Types
export type VisibilityLevel = 'team' | 'enterprise' | 'private';

export interface AgentCard {
  id: string;
  name: string;
  description: string;
  avatar?: string;
  visibility: VisibilityLevel;
  source: string;
  skills: string[];
  plugins: string[];
}

// Databox Types
export interface Databox {
  id: string;
  name: string;
  description: string;
  color: string;
  lastModified: string;
  modifiedBy: string;
}
