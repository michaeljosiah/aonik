export function isPortalAdmin(roles: string[] = []): boolean {
  return roles.some((role) => {
    const normalized = role.toLowerCase();
    const isAdmin = normalized.includes('admin') || normalized.includes('administrator');
    const isHostScope = normalized.includes('platform') || normalized.includes('portal') || normalized.includes('host');
    return isAdmin && isHostScope;
  });
}
