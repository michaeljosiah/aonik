import { Link, useParams } from 'react-router-dom';
import { Blocks } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { useIsHostAdmin } from '@/hooks/useIsHostAdmin';
import { getSelectedTenant } from '@/lib/tenantContext';
import { useModules } from '@/modules';

export interface ModuleDisabledPageProps {
  /** Backend module id. Falls back to the `:moduleId` route param. */
  moduleId?: string;
}

function humanise(moduleId: string): string {
  const words = moduleId.replace(/[-_]+/g, ' ').trim();
  if (words.length === 0) return 'This module';
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/**
 * Shown when a route or API call belongs to a module the selected
 * organisation does not have enabled (Spec 097 §10). Reached either from
 * the router fallback (route owned by a disabled module) or from the API
 * client after a 403 `module.disabled` response.
 */
export function ModuleDisabledPage({ moduleId: moduleIdProp }: ModuleDisabledPageProps) {
  const params = useParams<{ moduleId?: string }>();
  const moduleId = moduleIdProp ?? params.moduleId ?? '';
  const { manifest } = useModules();
  const { isHostAdmin } = useIsHostAdmin();
  const tenant = getSelectedTenant();

  const described = manifest?.modules.find((m) => m.id === moduleId);
  const name = described?.name ?? humanise(moduleId);
  const description = described?.description;

  return (
    <div className="flex h-full items-center justify-center px-6 py-10">
      <div className="w-full max-w-[32rem] rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-8">
        <div className="mb-4 flex h-10 w-10 items-center justify-center rounded-lg bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
          <Blocks className="h-5 w-5" />
        </div>
        <p className="text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">Module not enabled</p>
        <h1 className="mt-1 text-2xl font-bold text-[var(--color-text-primary)]">
          {name} is not enabled for this organisation
        </h1>
        {description && (
          <p className="mt-3 text-sm leading-relaxed text-[var(--color-text-secondary)]">{description}</p>
        )}
        <p className="mt-3 text-sm leading-relaxed text-[var(--color-text-secondary)]">
          Module state is managed by the host administrator. You can review which modules are enabled for this organisation in Settings.
        </p>
        <div className="mt-6 flex flex-col gap-2 sm:flex-row">
          <Button asChild>
            <Link to="/settings/modules">View enabled modules</Link>
          </Button>
          {isHostAdmin && tenant?.tenantId && (
            <Button asChild variant="outline">
              <Link to={`/tenants/${tenant.tenantId}`}>Manage modules for this organisation</Link>
            </Button>
          )}
          <Button asChild variant="ghost">
            <Link to="/">Back to home</Link>
          </Button>
        </div>
      </div>
    </div>
  );
}
