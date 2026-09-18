import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import {
  BoxPlansPage,
  CommerceCartsPage,
  CommerceOrdersPage,
  CommerceOverviewPage,
  CommerceProductsPage,
  DeliveryCalendarPage,
  MerchandisingPage,
  PersonalisationPage,
  ProductContentPage,
  StorefrontConfigPage,
} from '@/pages/commerce';

// ---------------------------------------------------------------------------
// Commerce module (Spec 073) — the product-agnostic storefront engine's admin
// surface: catalogue, personalisation, content, box plans, delivery,
// merchandising, storefront config, and the orders/carts projections.
// The rendered sidebar reads SIDEBAR_NAV (layout/aonik/sidebarNav.ts); this
// module-level navigation array exists for aggregation parity with the other
// modules and is not currently rendered.
// ---------------------------------------------------------------------------
const navigation: NavigationSection[] = [
  {
    id: 'products',
    items: [
      {
        id: 'commerce',
        label: 'Commerce',
        icon: 'cart',
        href: '/commerce',
        moduleId: 'commerce',
      },
    ],
  },
];

// ---------------------------------------------------------------------------
// Routes — the full table from Spec 073 §2; each page spec (074–084) replaces
// its placeholder component in place, so the paths are stable from day one.
// ---------------------------------------------------------------------------
const routes = [
  { path: '/commerce', element: CommerceOverviewPage },
  { path: '/commerce/products', element: CommerceProductsPage },
  { path: '/commerce/products/:productId', element: CommerceProductsPage, isDynamic: true },
  { path: '/commerce/personalisation', element: PersonalisationPage },
  { path: '/commerce/content', element: ProductContentPage },
  { path: '/commerce/box-plans', element: BoxPlansPage },
  { path: '/commerce/delivery', element: DeliveryCalendarPage },
  { path: '/commerce/merchandising', element: MerchandisingPage },
  { path: '/commerce/storefront-config', element: StorefrontConfigPage },
  { path: '/commerce/orders', element: CommerceOrdersPage },
  // Route-addressable order drawer (Spec 083 §2) — deep links, including Spec 084's
  // recent-orders rows, open it directly.
  { path: '/commerce/orders/:orderId', element: CommerceOrdersPage, isDynamic: true },
  { path: '/commerce/carts', element: CommerceCartsPage },
];

// ---------------------------------------------------------------------------
// Breadcrumbs — longest-prefix entries per route (resolution sorts by length).
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/commerce/products', trail: [{ label: 'Commerce', href: '/commerce' }, 'Products'] },
  { pathPrefix: '/commerce/personalisation', trail: [{ label: 'Commerce', href: '/commerce' }, 'Personalisation'] },
  { pathPrefix: '/commerce/content', trail: [{ label: 'Commerce', href: '/commerce' }, 'Product content'] },
  { pathPrefix: '/commerce/box-plans', trail: [{ label: 'Commerce', href: '/commerce' }, 'Box plans'] },
  { pathPrefix: '/commerce/delivery', trail: [{ label: 'Commerce', href: '/commerce' }, 'Delivery'] },
  { pathPrefix: '/commerce/merchandising', trail: [{ label: 'Commerce', href: '/commerce' }, 'Merchandising'] },
  { pathPrefix: '/commerce/storefront-config', trail: [{ label: 'Commerce', href: '/commerce' }, 'Storefront config'] },
  { pathPrefix: '/commerce/orders', trail: [{ label: 'Commerce', href: '/commerce' }, 'Orders'] },
  { pathPrefix: '/commerce/carts', trail: [{ label: 'Commerce', href: '/commerce' }, 'Carts'] },
  { pathPrefix: '/commerce', trail: ['Commerce'] },
];

// ---------------------------------------------------------------------------
// Module export — workspace panels are deliberately out of scope (Spec 073 §7).
// ---------------------------------------------------------------------------
export const commerceModule: AdminModule = {
  id: 'commerce',
  name: 'Commerce',
  requires: ['commerce'],
  navigation,
  routes,
  panels: [],
  panelComponents: {},
  breadcrumbs,
};
