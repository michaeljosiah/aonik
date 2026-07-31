// Buyer rendering shared by the orders list, carts list and both drawers (Spec 083).
//
// A party-bound buyer links to the unified customer detail (Spec 081); a guest renders AS a
// guest with NO link — a dead link to a customer record that does not exist is worse than
// plainly saying the checkout was anonymous.

import { Link } from 'react-router-dom';
import { User, UserCircle } from 'lucide-react';

const PARTY = 'party';

export function BuyerLabel({
  buyerKind,
  buyerPartyId,
  className,
  /**
   * Render the party as plain text instead of a link. Required wherever the label sits inside
   * another interactive element — an `<a>` nested in a `<button>` is invalid content with
   * ambiguous focus and activation for keyboard and assistive-technology users.
   */
  linkless = false,
}: {
  buyerKind: string;
  buyerPartyId: string | null;
  className?: string;
  linkless?: boolean;
}) {
  const isParty = buyerKind?.toLowerCase() === PARTY && !!buyerPartyId;
  const Icon = isParty ? UserCircle : User;

  return (
    <span className={`flex items-center gap-1.5 ${className ?? ''}`}>
      <Icon
        className={`h-3.5 w-3.5 shrink-0 ${
          isParty ? 'text-[var(--color-brand-primary)]' : 'text-[var(--color-text-tertiary)]'
        }`}
        aria-hidden
      />
      {isParty && linkless ? (
        <span className="truncate font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-secondary)]">
          {buyerPartyId!.slice(0, 8)}
        </span>
      ) : isParty ? (
        <Link
          to={`/customers/${buyerPartyId}`}
          onClick={(e) => e.stopPropagation()}
          className="truncate font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
        >
          {buyerPartyId!.slice(0, 8)}
        </Link>
      ) : (
        <span className="text-[12px] text-[var(--color-text-secondary)]">Guest</span>
      )}
    </span>
  );
}
