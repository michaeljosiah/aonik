# Dish catalog data

Issue #359 extends the existing catalog and content pipeline. It does not add a separate dish, portion, nutrition, tag or asset model. Operators continue using the existing product, content, category, facet and collection editors.

## Authored facts

Product create, admin read/PATCH and public detail expose the following facts. Unknown values remain null; zero heat and false flags are explicit facts.

| Field | Meaning and limits | PATCH clear |
|---|---|---|
| `heat` | Integer 0–3: none, mild, medium, hot | `clearHeat: true` |
| `componentsLine` | Authored components text, maximum 500 characters | Empty string |
| `lowSugar` | Nullable authored flag | `clearLowSugar: true` |
| `freezable` | Nullable authored flag | `clearFreezable: true` |
| `shelfLife` | Authored text with storage conditions, maximum 1,000 characters | Empty string |
| `relatedCollectionId` | Same-tenant curated collection, admin authoring only | `clearRelatedCollection: true` |
| `isPlaceholder` | Explicit dish assurance marker, defaults to true | Set false only after operator review |

Omitted fields preserve the existing value. Text facts are trimmed. Existing `CategoryId` provides the category/protein source; operators assign real categories using the existing taxonomy. Generic `AttributesJson` remains available for unrelated facts but no longer overrides typed heat, low-sugar or nutrition facet sources.

`TagsJson` continues to store stable keys. The tenant's configured `Tag` facet options supply their vocabulary and labels. New keys require an active configured definition; with no definitions, new managed tags cannot be authored. Existing retired/legacy keys remain visible and removable in the editor, and unchanged keys do not prevent unrelated edits. No health claims or tenant-specific vocabulary are seeded. An under-500 badge derives from resolved kcal; it must not be authored as a tag.

## Browse, sorting and related dishes

Browse and collection rows share one summary mapper. They now include description, active category name/slug, the typed product facts, hero alt text, the placeholder flag, kcal, protein and fibre, plus serving label and content validity/version fields.

Standard-preparation content is resolved in a batch by the existing content service. It shares the detail resolver's exact-variant precedence and default/staleness rules. When an exact variant becomes the default, list cards and detail both use that variant. Stale default figures are withheld from numeric card fields, filters and nutrition sorts; absent figures remain null.

- `sort=protein-desc` orders highest protein first.
- `sort=calories-asc` orders lowest kcal first.
- Missing figures sort last, with deterministic name/slug/ID ties. Filtering and sorting precede pagination.
- Recommended uses the existing curated `collection=<slug>&sort=rank` contract. There is no inferred recommendation score.

Configured facets can read `nutrition.kcal`, `nutrition.proteinGrams` or `nutrition.fibreGrams` as `Range`, `heat` as `Range` or `Attribute`, and `lowSugar` as `Attribute` with `true`/`false` values. Range bounds retain the existing inclusive minimum/exclusive maximum rule; maximum 500 excludes exactly 500. Unknown nutrition does not match a numeric band.

Public detail publishes `relatedCollectionSlug` only when the referenced collection is active and belongs to the tenant. The storefront uses the existing public collection endpoint and its rank ordering, excluding the viewed dish. An inactive reference does not expose hidden collection metadata.

## Per-portion nutrition and heating

The existing content block and exact option-selection variants already support distinct portion figures and heating instructions. `saturatesGrams` is now the eighth nullable nutrition figure, alongside kcal, protein, fibre, carbs, fat, sugars and salt. It uses the same nonnegative bounds, decimal precision, full-replacement semantics and concurrency signatures as other figures.

An active variant must provide every figure published on the default block. Adding saturates to the block therefore refuses incomplete active variants; legacy blocks with no saturates remain valid. Variant figures are not mixed with default figures or scaled to invent another portion. Null heating remains unpublished; an explicit empty list remains an authored empty list. Existing method/body instructions can express microwave, hob and oven instructions without another table.

## Images and assurance

`POST /commerce/admin/products/{productId}/images` requires the existing admin write policy. Send multipart fields `file` and `altText`. Alt text is required for uploads, with 1–500 characters. The server checks ownership, actual bytes and format, accepts JPEG/PNG up to 10 MiB, caps dimensions at 10,000 pixels per side and 40 megapixels, and produces a single JPEG within 1,920 by 1,920 pixels. It applies orientation and strips embedded metadata. Unsupported containers are rejected before pixel decoding; filename and browser MIME are not trusted.

The result is `{ "url": "...", "altText": "..." }`, an uploaded draft asset. The existing ordered media replacement saves its attachment. Uploading does not append hidden server media, discard unsaved gallery edits or mark a dish approved. Abandoned upload drafts are not automatically attached or deleted by this slice.

`ProductMedia.AltText` is nullable for existing URL-authored assets and limited to 500 characters. Every media edit/reorder/save carries it through. The hero URL and alt text come from the same first image, skipping document entries.

The existing image-processing interface now lives in SharedKernel; Infrastructure supplies the same processor and a ProductImages-keyed instance of the existing `IFileStore`. The default attachment store is unchanged. Configure `BlobStorage:ProductImages` and a usable `PublicBaseUrl`/container access for deployed storage. Local development serves the configured product-image path under `/storage/`.

`isPlaceholder` defaults to true for new and existing unverified products. Uploads never flip it. Public reads expose it so the Abby's Table storefront can reject placeholder records in its production build. That separate storefront build gate is not implemented in this backend repository. Actual nutrition, weights, category assignments, tag vocabulary, shelf life, photography, alt text and curated memberships still require operator authoring; prototype data is not promoted to fact.

## Verification

Coverage includes exact-default variant parity across detail/list/collection, stale-value withholding, sorted pagination, typed facets, tag/reference tenant isolation, nullable PATCH clears, saturates completeness/signatures and SQL precision, image authorization/format/size checks, media alt preservation, and the existing admin save flows. Schema changes use the canonical CLI-generated `AonikDbContext` migration stream.
