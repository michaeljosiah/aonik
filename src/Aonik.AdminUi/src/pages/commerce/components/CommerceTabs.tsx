// Orders ⇄ Carts switch (Spec 083 §4). The two pages answer one operator question from
// either end — what was ordered, and what is still in flight — so they read as one surface
// with two tabs rather than two unrelated nav entries.

import { useNavigate } from 'react-router-dom';

import { UnderlineTabs } from './UnderlineTabs';

const TABS = [
  { key: 'orders', label: 'Orders' },
  { key: 'carts', label: 'Carts' },
];

export function CommerceTabs({ active }: { active: 'orders' | 'carts' }) {
  const navigate = useNavigate();
  return (
    <UnderlineTabs
      tabs={TABS}
      active={active}
      onChange={(key) => navigate(key === 'orders' ? '/commerce/orders' : '/commerce/carts')}
    />
  );
}
