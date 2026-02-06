import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { ClipboardList, Receipt, Layers, ArrowUpRight } from 'lucide-react';

const orderTiles = [
  {
    title: 'Bill Payments',
    description: 'Create and manage multi-item bill payment orders.',
    href: '/orders/bill-payments/new',
    icon: Receipt,
    badge: 'Create',
  },
  {
    title: 'Order Activity',
    description: 'Track order submissions, compliance status, and fulfilment.',
    href: '/orders/activity',
    icon: Layers,
    badge: 'View',
  },
];

export function OrdersLandingPage() {
  const tiles = useMemo(() => orderTiles, []);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={[{ label: 'Orders', icon: <ClipboardList className="w-3.5 h-3.5" /> }]} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Orders</h1>
          <p className="text-[var(--color-text-secondary)]">
            Orchestrate business intent across bill payments and downstream fulfilment workflows.
          </p>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {tiles.map((tile) => (
          <Link key={tile.title} to={tile.href} className="group">
            <Card className="h-full transition-all duration-200 group-hover:shadow-md group-hover:-translate-y-0.5">
              <CardContent className="p-5">
                <div className="flex items-center justify-between mb-3">
                  <div className="w-10 h-10 rounded-md bg-[var(--color-brand-primary-light)] flex items-center justify-center">
                    <tile.icon className="w-5 h-5 text-[var(--color-brand-primary)]" />
                  </div>
                  <ArrowUpRight className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                </div>
                <h3 className="text-lg font-semibold text-[var(--color-text-primary)] mb-2">{tile.title}</h3>
                <p className="text-sm text-[var(--color-text-secondary)] mb-4">{tile.description}</p>
                <Badge variant={tile.badge === 'Coming soon' ? 'outline' : 'secondary'}>{tile.badge}</Badge>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
