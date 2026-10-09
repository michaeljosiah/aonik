import type { AdminCollectionItemDto, ProductSummaryDto } from '@/types/commerce';

export function membershipLines(items: readonly AdminCollectionItemDto[]) {
  return items.map((item, index) => ({ productId: item.productId, rank: index + 1 }));
}

export function sameMembership(left: readonly AdminCollectionItemDto[], right: readonly AdminCollectionItemDto[]) {
  return left.length === right.length && left.every((item, index) => item.productId === right[index].productId);
}

export function moveMember(items: readonly AdminCollectionItemDto[], index: number, direction: -1 | 1) {
  const next = [...items];
  const target = index + direction;
  if (index < 0 || target < 0 || index >= next.length || target >= next.length) return next;
  [next[index], next[target]] = [next[target], next[index]];
  return next.map((item, position) => ({ ...item, rank: position + 1 }));
}

export function addMember(items: readonly AdminCollectionItemDto[], product: Pick<ProductSummaryDto, 'id' | 'slug' | 'name' | 'status'>) {
  if (items.some((item) => item.productId === product.id)) return [...items];
  return [...items, { productId: product.id, slug: product.slug, name: product.name, status: product.status,
    rank: items.length + 1, unitPrice: null, currency: null, isPriceable: null }];
}

export function memberState(item: AdminCollectionItemDto, isExtras: boolean | null, collectionActive = true) {
  if (item.status === 'Draft') return 'Draft — staged';
  if (item.status !== 'Active') return `${item.status} — hidden`;
  if (!collectionActive) return 'Collection inactive';
  if (isExtras === null) return 'Active — preview unavailable';
  if (isExtras && item.isPriceable === false) return 'Unpriceable — skipped publicly';
  if (isExtras && item.isPriceable === null) return 'Price not evaluated';
  return isExtras ? 'Priceable' : 'Active';
}

export function merchandisingError(error: unknown, fallback: string) {
  if (error && typeof error === 'object' && 'userMessage' in error && error.userMessage) return String(error.userMessage);
  return error instanceof Error ? error.message : fallback;
}

export function sortOrderFrom(value: string) {
  if (!/^-?\d+$/.test(value.trim())) throw new Error('Sort order must be a whole number.');
  const number = Number(value);
  if (!Number.isInteger(number) || number < -2_147_483_648 || number > 2_147_483_647) throw new Error('Sort order is outside the supported range.');
  return number;
}
