import { menuGroupIcons, menuIconPaths, menuProfileOverrides, menuScreenIcons } from './menuIcons.generated';
import type { MenuIconName } from './menuIcons.generated';

export type MenuIconReference = `menu-${MenuIconName}`;

/** Use the selected profile, so host navigation retains the general product glyphs. */
export function getScreenMenuIcon(screenId: string, profileId: string): MenuIconReference | undefined {
  const overrides = Object.hasOwn(menuProfileOverrides, profileId) ? menuProfileOverrides[profileId] : undefined;
  const name = overrides && Object.hasOwn(overrides, screenId)
    ? overrides[screenId]
    : Object.hasOwn(menuScreenIcons, screenId) ? menuScreenIcons[screenId] : undefined;
  return name ? `menu-${name}` : undefined;
}

export function getGroupMenuIcon(groupId: string): MenuIconReference | undefined {
  return Object.hasOwn(menuGroupIcons, groupId) ? `menu-${menuGroupIcons[groupId]}` : undefined;
}

export function getMenuIconPath(reference: string): string | undefined {
  if (!reference.startsWith('menu-')) return undefined;
  const name = reference.slice(5);
  return Object.hasOwn(menuIconPaths, name) ? menuIconPaths[name as MenuIconName] : undefined;
}
