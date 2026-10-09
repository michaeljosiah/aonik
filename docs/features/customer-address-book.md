# Customer address book

Issue [#361](https://github.com/michaeljosiah/aonik/issues/361) adds self-service address CRUD and a shipping default to the existing Platform party/address records. It does not introduce a separate address store or change placed-order delivery snapshots.

## API and ownership

The authenticated customer's address book lives at `/profiles/customers/me/addresses`:

| Method | Route suffix | Action |
| --- | --- | --- |
| GET | — | Read addresses, effective default and book version |
| POST | — | Add an address |
| PUT | `/{addressId}` | Replace one address |
| DELETE | `/{addressId}` | Remove one address |
| PUT | `/{addressId}/default` | Choose the default |

Reads and successful writes return `addresses`, `defaultAddressId` and `version`. Each address includes its ID, type, three address lines, city, state, postcode, country and derived `isDefault`. Create returns 201; other successful actions return 200.

POST and PUT address bodies contain the full address fields plus `expectedVersion`. DELETE and the default-selection PUT take a JSON body containing only `expectedVersion`. Unknown body properties, including owner selectors, are rejected.

The service resolves the current local user, tenant and linked live party, and checks the existing `UserInfo.Read` or `UserInfo.Update` permission. Personal users can use the existing profile policy. Callers cannot select an owner or tenant in a request body. Unknown, deleted and other customers' addresses share the same not-found response. Address responses, including early authentication/binding errors, carry `Cache-Control: no-store` and `Referrer-Policy: no-referrer`.

## Versions and defaults

Every write supplies the last observed `expectedVersion`, including the first address creation. The version belongs to the parent party, so concurrent edits, additions, deletions and default changes all participate in one native concurrency check. A missing, malformed or stale version returns `409 concurrency_conflict`; read the book again before retrying. Changes to other party information can also invalidate an observed version.

The first address becomes default. Removing the default chooses the earliest remaining live owned address by creation time and ID; removing the final address leaves no default. Selecting a default requires a live address in this same book. The address change and parent default/version update save atomically.

Older records may have no saved default, or a pointer to an unavailable address. Reads derive the same earliest-address fallback without writing data. The next successful mutation persists a valid default. The nullable `Party.DefaultShippingAddressId` is the only new database column; existing audited address rows retain their soft-delete behavior.

## Checkout integration

The separate Abby's Table storefront reads this book and offers the default when the checkout address form is empty. A saved checkout draft takes precedence over the book. Selecting an address copies its values into the existing versioned checkout-draft API; changing the book later does not silently update a draft, held date or placed order.

Address storage supports manual international addresses and does not call the postcode coverage provider. Checkout retains its own country, coverage and delivery validation. Map address `state` to checkout `region` and `country` to `countryCode`. Stored addresses can contain a third line and longer legacy values than checkout accepts. Show those values without truncation, and require the customer to resolve an incompatible address before saving the checkout form; never silently discard the third line.

Customer export/import includes a valid owned default and remaps it through the existing import ID map. Missing, deleted, foreign or unexported targets are not carried over as usable default references. The default does not change the existing registered-country/admin address semantics.

## Verification and deployment

The canonical EF migration is generated against `AonikDbContext`. Tests cover self-service permissions and isolation, CRUD/default behavior, import ID remapping, stale writes and native SQL concurrency. Existing Commerce checkout tests continue to protect copied delivery snapshots. Connect the separate storefront address form and checkout picker to these endpoints; this AONIK change does not deploy that frontend.
