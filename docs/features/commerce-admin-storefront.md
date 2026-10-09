# Commerce storefront administration

The existing Commerce routes now provide the Delivery, Merchandising and Storefront config screens from Specs 077–079. They use the current Commerce APIs and shared Admin UI controls; there are no new database tables or settings contracts.

## Delivery

`/commerce/delivery` edits the fulfilment calendar: timezone, delivery weekdays, cutoff, lead days, blackout dates and active state. The promise shown to customers comes from the server. An unsaved calendar edit marks that promise stale and removes its month-grid ring while the grid previews the edited weekdays and closures. Calendar dates are formatted without applying the viewer's timezone.

Capacity is authored explicitly for individual dates in boxes. Missing capacity is unconfigured, not an available date or an unlimited budget. Existing rows require their observed version when saving; a conflict requires the operator to reload and review. Occupied capacity includes active holds, payment-pending claims and committed orders. The public availability response remains authoritative for whether a customer can choose a date.

A missing public promise is shown honestly. Network or permission failures are shown as failures rather than being converted into a promise or a no-delivery result. Saving capacity refreshes the committed promise without discarding an unsaved calendar draft. Configure actual operating capacity, delivery weekdays and cutoffs for each tenant before accepting orders.

## Merchandising

`/commerce/merchandising` manages collections and facet groups. Collection membership is a local draft until Save replaces the ranked list. Up/down actions produce contiguous ranks; removing every member deliberately sends an empty list. Draft products stay visible to staff but do not appear in the storefront preview. Inactive collections show an inactive preview.

The extras collection is identified by the configured slug. Its saved preview comes from the public extras API, including the count of members skipped for unavailable pricing. The admin membership list retains those members so operators can fix or remove them. The page does not advertise an unsaved reorder as the published extras rail.

Facet options are edited as rows and serialized through the existing contract. Range bounds are half-open: the minimum is included and the maximum is excluded. A group's key and match kind cannot be changed after creation; retire it and create a replacement when those semantics change. Category-tree authoring is outside Specs 077–079's page acceptance scope.

## Storefront config

`/commerce/storefront-config` edits the recommended-choice label, results page size, back-to-top trigger, delivery list and charged amounts, default box, and extras collection. Currency comes from the tenant. The page does not seed a delivery fee or override the tenant's current values.

Only touched fields are sent. Clearing an override is supported for the label, trigger and default box. The API does not support clearing numeric fields or the extras slug, so the form does not offer those actions. Trigger input must be a JSON object; amounts must be nonnegative, representable whole-penny values.

Previews use saved public values and refresh after a successful update. They display the actual recommended label, delivery charge and configured box presets, including authored savings. A missing box plan remains an empty state. The existing settings writer commits keys individually, so a failed save must be followed by a reload to establish which values persisted; the UI does not promise atomic settings updates.

## Access and integration

Write controls follow the existing admin policy, and server authorization remains authoritative. Tenant changes discard drafts and invalidate outstanding page requests so a previous tenant's response cannot populate the new tenant's screen.

These screens configure the AONIK backend used by the storefront. Publishing the separate Abby's Table frontend, choosing operational capacity, configuring live delivery prices, and supplying approved business/legal settings remain deployment responsibilities. Greeting-card, Terms and Signature settings are not added to the Spec 079 write contract by this change.
