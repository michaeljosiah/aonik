# Controlled food allergens

Issue #351 extends the existing Spec 067 product-content service and Spec 075 editor.
It does not introduce another catalogue or label-authoring store.

## Authoring

Default blocks and exact option variants accept `allergensPresent` and
`precautionaryStatement` through their existing admin PUT/POST endpoints.

- `allergensPresent: null` means unreviewed or unpublished.
- `allergensPresent: []` explicitly records a reviewed list with none of the 14 groups declared.
  It is not an allergen-free claim.
- Values are `Celery`, `CerealsContainingGluten`, `Crustaceans`, `Eggs`, `Fish`,
  `Lupin`, `Milk`, `Molluscs`, `Mustard`, `Peanuts`, `Sesame`, `Soybeans`,
  `SulphurDioxideAndSulphites`, and `TreeNuts`. Each entry must name one group;
  unknown values, numbers and combined names such as `"Milk, Fish"` are rejected.
  Repeated values are deduplicated and stored in enum order.
- The optional precautionary statement is authored from the kitchen's actual
  cross-contamination risk assessment, never generated from the selected groups.
  It is trimmed and limited to 2,000 characters.

The full ingredients declaration remains necessary, including the specific cereal
or nut ingredients where applicable. These group values alone do not establish
compliance with packaging-specific labelling requirements.

## Reading and production labels

Public product content and `GET /commerce/admin/products/{productId}/label-content`
return the same resolved DTO. The label endpoint requires `AdminUserPolicy` and
accepts the same URL-encoded `selection` JSON as the public content endpoint.
Label consumers should use the exact production selection and must handle
`declarationsWithheld`, `heatingWithheld`, and `isStale`; withheld declarations
are not printable allergen assurances. This is a data feed, not a label printer.

An active exact variant supplies its own declarations. A missing variant never
inherits default allergens for another preparation. A stale default block or
invalid stored allergen JSON withholds the controlled list and precautionary
statement. The existing content-version cache and write preconditions cover
these new fields. Label responses use `Cache-Control: no-store`.

## Existing content

The migration adds nullable fields without guessing allergen values from old text.
Legacy `allergens` text remains visible to admins as a reference for review.
Until an operator authors a controlled list, public declarations are marked
withheld and the public `allergens` display is null. Once authored, that display
is derived from the controlled list, so it cannot conflict with another stored
free-text declaration. Operators must review existing product blocks and variants
before relying on their allergen publication after deployment.

See [FSA allergen guidance](https://www.gov.uk/government/publications/allergen-guidance-for-food-businesses/allergen-guidance-for-food-businesses)
for the regulated groups and distance-selling requirements.
