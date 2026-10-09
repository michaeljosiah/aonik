import type { AdminModule, ModuleRouteConfig } from '../types';
import type { NavigationSection } from '@/types';
import {
  BoxPlansPage,
  CommerceCartsPage,
  CommerceOrdersPage,
  CommerceOrderPackingPage,
  CommerceOverviewPage,
  CommerceProductsPage,
  CommerceDiscountsPage,
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
// Admin navigation is composed from the selected JSON profile (Spec 098).
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
// Spec 098 screen metadata mirrors the Commerce read endpoints' AdminUserPolicy.
// Write operations retain their separate server policies; this does not grant write access.
// ---------------------------------------------------------------------------
const routes: ModuleRouteConfig[] = [
  { screen: { id: 'commerce.overview', label: 'Commerce', permissions: { authenticatedAdmin: true } }, path: '/commerce', element: CommerceOverviewPage },
  { screen: { id: 'commerce.products', label: 'Products', permissions: { authenticatedAdmin: true } }, path: '/commerce/products', element: CommerceProductsPage },
  { screen: { id: 'commerce.discounts', label: 'Discount codes', permissions: { authenticatedAdmin: true } }, path: '/commerce/discounts', element: CommerceDiscountsPage },
  { screen: { id: 'commerce.product-detail', label: 'Product', permissions: { authenticatedAdmin: true } }, path: '/commerce/products/:productId', element: CommerceProductsPage, isDynamic: true },
  { screen: { id: 'commerce.personalisation', label: 'Personalisation', permissions: { authenticatedAdmin: true } }, path: '/commerce/personalisation', element: PersonalisationPage },
  { screen: { id: 'commerce.content', label: 'Product content', permissions: { authenticatedAdmin: true } }, path: '/commerce/content', element: ProductContentPage },
  { screen: { id: 'commerce.box-plans', label: 'Box plans', permissions: { authenticatedAdmin: true } }, path: '/commerce/box-plans', element: BoxPlansPage },
  { screen: { id: 'commerce.delivery', label: 'Delivery', permissions: { authenticatedAdmin: true } }, path: '/commerce/delivery', element: DeliveryCalendarPage },
  { screen: { id: 'commerce.merchandising', label: 'Merchandising', permissions: { authenticatedAdmin: true } }, path: '/commerce/merchandising', element: MerchandisingPage },
  { screen: { id: 'commerce.storefront-config', label: 'Storefront config', permissions: { authenticatedAdmin: true } }, path: '/commerce/storefront-config', element: StorefrontConfigPage },
  { screen: { id: 'commerce.orders', label: 'Orders', permissions: { authenticatedAdmin: true } }, path: '/commerce/orders', element: CommerceOrdersPage },
  // Route-addressable order drawer (Spec 083 §2) — deep links, including Spec 084's
  // recent-orders rows, open it directly.
  { screen: { id: 'commerce.order-detail', label: 'Order', permissions: { authenticatedAdmin: true } }, path: '/commerce/orders/:orderId', element: CommerceOrdersPage, isDynamic: true },
  { screen: { id: 'commerce.order-packing', label: 'Packing slip', permissions: { authenticatedAdmin: true }, policy: 'AdminReadPolicy' }, path: '/commerce/orders/:orderId/packing', element: CommerceOrderPackingPage, isDynamic: true },
  { screen: { id: 'commerce.carts', label: 'Carts', permissions: { authenticatedAdmin: true } }, path: '/commerce/carts', element: CommerceCartsPage },
];

// ---------------------------------------------------------------------------
// Breadcrumbs — longest-prefix entries per route (resolution sorts by length).
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/commerce/products', trail: [{ label: 'Commerce', href: '/commerce' }, 'Products'] },
  { pathPrefix: '/commerce/discounts', trail: [{ label: 'Commerce', href: '/commerce' }, 'Discount codes'] },
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
