import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Globe2, Layers, Building2, Wrench, ArrowUpRight, Network } from 'lucide-react';

const catalogTiles = [
  {
    title: 'Countries',
    description: 'Reference markets enabled for catalog bill pay.',
    href: '/catalog/countries',
    icon: Globe2,
  },
  {
    title: 'Categories',
    description: 'Biller groupings by service domain and country.',
    href: '/catalog/categories',
    icon: Layers,
  },
  {
    title: 'Billers',
    description: 'All billers available across configured markets.',
    href: '/catalog/billers',
    icon: Building2,
  },
  {
    title: 'Partners',
    description: 'Manage payout and bill payment partners used for routing.',
    href: '/catalog/partners',
    icon: Network,
  },
  {
    title: 'Services',
    description: 'Inspect biller services and input requirements.',
    href: '/catalog/billers',
    icon: Wrench,
  },
];

export function CatalogLandingPage() {
  const tiles = useMemo(() => catalogTiles, []);

  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog</h1>
          <p className="text-[var(--color-text-secondary)]">
            Review catalog coverage, categories, billers, and service definitions used by MyBillAfrica.
          </p>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
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
                <Badge variant="secondary">Open</Badge>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
