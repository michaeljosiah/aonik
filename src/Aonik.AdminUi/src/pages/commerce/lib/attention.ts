// The overview's attention rows (Spec 084 §2). Pure and tested, because this list is the
// page's whole claim: every row must be a REAL state read from the surface that owns it,
// with a link there — never a computed guess, and never a placeholder.
//
// FOUR states are deliberately distinct, and conflating any two is the failure mode:
//
//   NOTHING TO REPORT   → the row is OMITTED. A green "0 items need review" row is noise that
//                         trains operators to skim past the card.
//   SOMETHING TO REPORT → the row appears, toned by urgency.
//   SOURCE UNAVAILABLE  → the row appears saying so. Silence would read as "nothing to
//                         report", which is the one thing a failed read cannot establish.
//   CLEAN BUT PARTIAL   → the row appears saying THAT. A bounded scan that found nothing has
//                         established nothing beyond its bound: with 300 products and a
//                         200-product scan, a flagged product 201 would render identically to
//                         a genuinely clean store. This is the same error as the unavailable
//                         case wearing a zero, and omitting it was the bug this comment now
//                         exists to prevent recurring.

export type AttentionTone = 'warn' | 'info' | 'muted';

export interface AttentionRowModel {
  key: string;
  tone: AttentionTone;
  /** The finding, in one line. */
  statement: string;
  /** What it means or how it was counted — including any window the number describes. */
  subline: string;
  href: string;
}

/** A source is in flight, has failed, or has an answer. */
export type SourceState<T> =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ready'; value: T };

/** A count taken over a paged source, carrying whether the whole source was seen. */
export interface ScannedCount {
  count: number;
  /** How many records were inspected to produce it. */
  inspected: number;
  /** False when the source had more pages — the count is then a floor, and says so. */
  complete: boolean;
}

export interface AttentionSources {
  /** Products whose authored content needs a human look (Spec 075 requiresReview). */
  contentReview: SourceState<ScannedCount>;
  /** The live delivery promise, or null when the tenant publishes none. */
  deliveryPromise: SourceState<string | null>;
  /** Draft products staged inside collections, and whether every collection was inspected. */
  stagedDrafts: SourceState<{ count: number; collectionsInspected: number; complete: boolean }>;
  /** Active extras omitted from the rail for want of a price (Spec 078). */
  skippedExtras: SourceState<number>;
  /** Abandoned carts, whole-store from the pagination envelope. */
  abandonedCarts: SourceState<number>;
  /** Open carts that cannot check out, counted over a named window. */
  blockedCarts: SourceState<{ count: number; window: number }>;
}

function unavailable(key: string, what: string, href: string): AttentionRowModel {
  return {
    key,
    tone: 'muted',
    statement: `${what} could not be read`,
    subline: 'This check did not run — its state is unknown, not clear.',
    href,
  };
}

/** Found nothing, but could not see everything — reported rather than passed off as clean. */
function partiallyClean(
  key: string,
  what: string,
  scope: string,
  href: string,
): AttentionRowModel {
  return {
    key,
    tone: 'muted',
    statement: `${what} checked as far as ${scope}`,
    subline: 'Nothing found there, but the rest was not inspected — open the page for the full picture.',
    href,
  };
}

/**
 * The rows to render, in triage order: things that are wrong, then things that are merely
 * worth knowing. A `loading` source contributes nothing — the page fills in as each settles.
 */
export function buildAttentionRows(sources: AttentionSources): AttentionRowModel[] {
  const rows: AttentionRowModel[] = [];

  // ── Content awaiting review ──────────────────────────────────────────────
  if (sources.contentReview.kind === 'unavailable') {
    rows.push(unavailable('content', 'Product content', '/commerce/content'));
  } else if (sources.contentReview.kind === 'ready' && sources.contentReview.value.count === 0) {
    if (!sources.contentReview.value.complete) {
      rows.push(
        partiallyClean(
          'content',
          'Product content',
          `the first ${sources.contentReview.value.inspected} products`,
          '/commerce/content',
        ),
      );
    }
  } else if (sources.contentReview.kind === 'ready') {
    const { count, inspected, complete } = sources.contentReview.value;
    rows.push({
      key: 'content',
      // "At least" when the scan was partial — a bare count would present a floor as a total.
      statement: `${complete ? '' : 'At least '}${count} product${
        count === 1 ? '' : 's'
      } awaiting content review`,
      tone: 'warn',
      subline: complete
        ? 'The standard preparation changed underneath the authored block — declarations are withheld until it is confirmed.'
        : `Declarations stay withheld until confirmed. Counted across the first ${inspected} products only.`,
      href: '/commerce/content',
    });
  }

  // ── Delivery promise ─────────────────────────────────────────────────────
  if (sources.deliveryPromise.kind === 'unavailable') {
    rows.push(unavailable('delivery', 'The delivery promise', '/commerce/delivery'));
  } else if (sources.deliveryPromise.kind === 'ready') {
    const promise = sources.deliveryPromise.value;
    rows.push(
      promise
        ? {
            key: 'delivery',
            tone: 'info',
            statement: `Earliest delivery is ${promise}`,
            subline: 'Customers see this date on the storefront.',
            href: '/commerce/delivery',
          }
        : {
            // The one "good news" row that still earns its place: a missing promise is
            // invisible on the storefront, so nothing else would ever surface it.
            key: 'delivery',
            tone: 'warn',
            statement: 'No delivery promise — customers see no date',
            subline: 'No calendar is configured or active, so the storefront shows nothing.',
            href: '/commerce/delivery',
          },
    );
  }

  // ── Drafts staged in collections ─────────────────────────────────────────
  if (sources.stagedDrafts.kind === 'unavailable') {
    rows.push(unavailable('drafts', 'Collection membership', '/commerce/merchandising'));
  } else if (sources.stagedDrafts.kind === 'ready' && sources.stagedDrafts.value.count === 0) {
    if (!sources.stagedDrafts.value.complete) {
      rows.push(
        partiallyClean(
          'drafts',
          'Collection membership',
          `the first ${sources.stagedDrafts.value.collectionsInspected} collections`,
          '/commerce/merchandising',
        ),
      );
    }
  } else if (sources.stagedDrafts.kind === 'ready') {
    const { count, collectionsInspected, complete } = sources.stagedDrafts.value;
    rows.push({
      key: 'drafts',
      tone: 'muted',
      statement: `${count} draft product${count === 1 ? '' : 's'} staged in collections`,
      subline: complete
        ? 'Invisible to shoppers until activated, then they appear in place.'
        : `Invisible until activated. Counted across the first ${collectionsInspected} collections only.`,
      href: '/commerce/merchandising',
    });
  }

  // ── Unpriceable extras ───────────────────────────────────────────────────
  if (sources.skippedExtras.kind === 'unavailable') {
    rows.push(unavailable('extras', 'The extras rail', '/commerce/merchandising'));
  } else if (sources.skippedExtras.kind === 'ready' && sources.skippedExtras.value > 0) {
    const n = sources.skippedExtras.value;
    rows.push({
      key: 'extras',
      tone: 'warn',
      statement: `${n} extra${n === 1 ? '' : 's'} skipped for want of a price`,
      subline: 'Active collection members the rail cannot show, so customers never see them.',
      href: '/commerce/merchandising',
    });
  }

  // ── Carts ────────────────────────────────────────────────────────────────
  // Both cart reads get the same unavailable treatment as every other source. Without it a
  // carts outage was indistinguishable from having no blocked or abandoned carts — the exact
  // silence this module exists to refuse, and I had left these two branches out of it.
  if (sources.blockedCarts.kind === 'unavailable') {
    rows.push(unavailable('blocked-carts', 'Open carts', '/commerce/carts'));
  } else if (sources.blockedCarts.kind === 'ready' && sources.blockedCarts.value.count > 0) {
    const { count, window } = sources.blockedCarts.value;
    rows.push({
      key: 'blocked-carts',
      tone: 'warn',
      statement: `${count} open cart${count === 1 ? '' : 's'} cannot check out`,
      subline: `Drifted or not a full box. Counted over the ${window} most recent open carts.`,
      href: '/commerce/carts',
    });
  }

  if (sources.abandonedCarts.kind === 'unavailable') {
    rows.push(unavailable('abandoned-carts', 'Abandoned carts', '/commerce/carts'));
  } else if (sources.abandonedCarts.kind === 'ready' && sources.abandonedCarts.value > 0) {
    const n = sources.abandonedCarts.value;
    rows.push({
      key: 'abandoned-carts',
      tone: 'muted',
      statement: `${n} abandoned cart${n === 1 ? '' : 's'}`,
      subline: 'Sessions that went idle before checkout.',
      href: '/commerce/carts',
    });
  }

  return rows;
}

/** True once every source has settled — used to tell "all quiet" from "still looking". */
export function allSettled(sources: AttentionSources): boolean {
  return Object.values(sources).every((source) => source.kind !== 'loading');
}
