export function isTenantScopedHostname(hostname: string): boolean {
  const normalized = hostname.trim().toLowerCase();
  const parts = normalized.split('.');

  if (normalized.length === 0) return false;
  if (normalized.startsWith('www.')) return false;
  if (normalized.includes('localhost')) return false;

  // Azure Container Apps assigns multi-part infrastructure hostnames like
  // `aonik-dev-adminui.<env>.<region>.azurecontainerapps.io`. These are not
  // tenant subdomains and should stay on the host experience.
  if (normalized.endsWith('.azurecontainerapps.io')) return false;

  return parts.length >= 3;
}
