import { readFileSync } from 'node:fs';
import vm from 'node:vm';

import { describe, expect, it } from 'vitest';

import { getGroupMenuIcon, getMenuIconPath, getScreenMenuIcon } from './menuIcons';
import { menuGroupIcons, menuIconPaths, menuProfileOverrides, menuScreenIcons } from './menuIcons.generated';

describe('shared menu icons', () => {
  it('matches the starter-kit vectors, screen mappings, groups and profile overrides exactly', () => {
    const context = vm.createContext({});
    const source = readFileSync(new URL('../../../../Templates/aonik-admin-starterkit/assets/menu-icons.js', import.meta.url), 'utf8');
    vm.runInContext(source, context, { timeout: 1000 });
    const kit = JSON.parse(JSON.stringify(context.AonikMenuIcons));

    expect(menuIconPaths).toEqual(kit.paths);
    expect(menuScreenIcons).toEqual(kit.screenIcons);
    expect(menuGroupIcons).toEqual(kit.groups);
    expect(menuProfileOverrides).toEqual(kit.businessOverrides);
  });

  it('uses domain-specific glyphs without changing general-profile product semantics', () => {
    expect(getScreenMenuIcon('commerce.products', 'food-commerce')).toBe('menu-menu');
    expect(getScreenMenuIcon('commerce.orders', 'food-commerce')).toBe('menu-food-orders');
    expect(getScreenMenuIcon('commerce.products', 'general')).toBe('menu-products');
    expect(getScreenMenuIcon('finance.orders', 'general')).toBe('menu-orders');
    expect(getScreenMenuIcon('finance.orders', 'remittance')).toBe('menu-transfer-orders');
  });

  it('returns undefined for unknown keys so registered extension icons can remain the fallback', () => {
    expect(getScreenMenuIcon('extension.custom-page', 'general')).toBeUndefined();
    expect(getScreenMenuIcon('toString', '__proto__')).toBeUndefined();
    expect(getGroupMenuIcon('extension.custom-group')).toBeUndefined();
    expect(getMenuIconPath('menu-toString')).toBeUndefined();
    expect(getMenuIconPath('menu-not-an-icon')).toBeUndefined();
    expect(getMenuIconPath('ledger')).toBeUndefined();
  });
});
