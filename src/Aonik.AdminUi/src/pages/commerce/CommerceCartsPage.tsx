import { CommercePlaceholder } from './CommercePlaceholder';

export function CommerceCartsPage() {
  return (
    <CommercePlaceholder
      title="Carts"
      subtitle="Open, converted and abandoned carts — with read-only drift flags"
      spec="083"
      summary="Cart list with box fill state, and the cart detail whose availability and price-change flags are computed at load time, never persisted."
    />
  );
}
