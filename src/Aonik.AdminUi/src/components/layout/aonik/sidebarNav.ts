import type { NavItem } from '@/types';

export function collectNavItemHrefs(item: NavItem): string[] {
  const hrefs = new Set<string>();

  const visit = (current: NavItem) => {
    if (current.href) hrefs.add(current.href);
    current.children?.forEach(visit);
    current.childGroups?.forEach((group) => group.items.forEach(visit));
  };

  visit(item);
  return Array.from(hrefs);
}

export function navItemMatchesPath(item: NavItem, pathname: string): boolean {
  return collectNavItemHrefs(item).includes(pathname);
}
