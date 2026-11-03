import { useLocation, Link } from 'react-router-dom';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { useModules } from '@/modules/useModules';
import workspace from '@/config/arke-worlds-workspace.json';

/** Registered operational destinations, with truthful connection states until staff APIs land. */
export function ArkeWorkspacePage() {
  const { pathname } = useLocation();
  const { isPathVisible, enabledModules } = useModules();
  const current = workspace.find(area => area.path === pathname) ?? workspace[0];
  const overview = current.key === 'overview';
  const shared = [
    { path: '/customers', label: 'Customers', icon: 'customers', description: 'Adult account contacts and their shared customer records.' },
    { path: '/orders/activity', label: 'Orders', icon: 'orders', description: 'Business intent, linked parties and fulfilment references.' },
    { path: '/billing/invoices', label: 'Invoices', icon: 'invoices', description: 'Billing documents and outstanding balances.' },
    { path: '/ledger', label: 'Ledger', icon: 'ledger', description: 'The source of financial truth.' },
    { path: '/compliance/documents', label: 'Documents', icon: 'documents', description: 'Tenant-scoped document management.' },
    { path: '/settings/modules', label: 'Enabled modules', icon: 'modules', description: 'The capabilities configured for this organisation.' },
  ].filter(item => isPathVisible(item.path));

  return (
    <div className="h-full overflow-auto p-6 md:p-8">
      <header className="mb-8 flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="mb-2 text-xs font-medium text-muted-foreground">Arke Worlds · Business workspace</p>
          <h1 className="text-2xl font-semibold tracking-tight">{current.label}</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">{current.description}</p>
        </div>
        <Badge variant="outline">{overview ? 'Workspace configured' : 'Connection required'}</Badge>
      </header>

      {overview ? (
        <>
          <Card className="mb-6">
            <CardHeader>
              <CardTitle>One workspace for the Arke portfolio</CardTitle>
              <CardDescription>Arke Kids is the first product focus. Studio and Player have their own release, editorial and distribution workflows.</CardDescription>
            </CardHeader>
            <CardContent className="text-sm leading-6 text-muted-foreground">
              The business type and menu are installed. Arke-specific staff data connections remain to be implemented;
              these pages do not display simulated customers, jobs, subscriptions or revenue.
              The shared AONIK tools below remain available according to your permissions.
            </CardContent>
          </Card>
          <div className="mb-8 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {workspace.filter(area => area.key !== 'overview' && isPathVisible(area.path)).map(area => (
              <Link key={area.id} to={area.path} className="rounded-lg border bg-card p-5 transition-colors hover:bg-muted/50">
                <AonikTemplateIcon name={'menu-' + area.icon} size={21} className="mb-4 text-muted-foreground" />
                <div className="mb-2 flex items-center justify-between gap-3"><h2 className="text-sm font-medium">{area.label}</h2><span className="text-xs text-muted-foreground">Open →</span></div>
                <p className="text-xs leading-5 text-muted-foreground">{area.description}</p>
              </Link>
            ))}
          </div>
        </>
      ) : (
        <Card className="mb-8">
          <CardHeader>
            <AonikTemplateIcon name={'menu-' + current.icon} size={28} className="mb-3 text-muted-foreground" />
            <CardTitle>{current.label} is awaiting its staff connection</CardTitle>
            <CardDescription>This destination reserves the operational workflow. No records have been loaded and no action has been performed.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5 text-sm leading-6">
            <p>{current.boundary}</p>
            <p className="text-muted-foreground">Connect and qualify the staff workflow before using this area for customer operations. The installed modules provide foundations; module enablement does not establish an end-to-end service.</p>
            {current.requires.length > 0 && <div className="flex flex-wrap gap-2">{current.requires.map(id => <Badge variant="outline" key={id}>{id} · {enabledModules.includes(id) ? 'Enabled' : 'Unavailable'}</Badge>)}</div>}
            {isPathVisible('/arke/overview') && <Link className="inline-flex text-sm font-medium text-primary underline-offset-4 hover:underline" to="/arke/overview">Return to workspace overview →</Link>}
          </CardContent>
        </Card>
      )}

      {shared.length > 0 && <section>
        <h2 className="mb-4 text-sm font-medium">Shared AONIK tools</h2>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{shared.map(item => <Link key={item.path} to={item.path} className="flex items-start gap-3 rounded-lg border bg-card p-4 hover:bg-muted/50"><AonikTemplateIcon name={'menu-' + item.icon} size={18} className="mt-0.5 text-muted-foreground" /><span><strong className="block text-sm font-medium">{item.label}</strong><span className="mt-1 block text-xs leading-5 text-muted-foreground">{item.description}</span></span></Link>)}</div>
      </section>}
    </div>
  );
}
