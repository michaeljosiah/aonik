# Contact enquiries

Issue #356 adds durable, tenant-scoped enquiries using the existing Platform settings, private file store, transactional outbox, email templates and admin UI. It does not create customer accounts, newsletter consent or an order association from a supplied order number.

## Public contract

`POST /v1/contact-enquiries` accepts multipart form data with `submission_id` (nonempty UUID), `name`, `email`, `topic`, `message`, optional `order_number`, optional empty honeypot `website`, and up to three repeated `images` parts. Topics are `order`, `new`, `dish`, `delivery`, `gift`, and `other`. Name/email/order-number/message limits are 200/254/64/5,000 characters; messages need at least 10 characters. Order numbers are retained only for the order topic.

JPEG, PNG and HEIC images are limited to 10 MiB each and 32 MiB for the whole request. The server measures actual bytes, checks the detected format against the extension and media type, scans the original bytes, bounds native decode resources, applies orientation and strips metadata, then stores a normalized JPEG. No original image is published or attached to email.

The browser must retain the submission ID while retrying unchanged content. A matching retry returns the original receipt without reprocessing images or queuing duplicate messages. Reusing the ID with different content returns 409. A 202 response with `id` and `receivedAtUtc` means the enquiry and two email jobs are saved; it does not promise that email has arrived. Validation errors return 422, oversized requests 413, throttling 429, and unavailable processing/routing/storage 503. Responses are not cached.

The existing Abby's Table contact adapter lives in a separate repository. Wire its enquiry path and submission identifier there, preserve draft fields and valid attachments after errors, and describe success as receipt rather than completed email delivery. This backend change does not deploy that storefront integration.

## Routing and email

Set tenant setting `ContactEnquiries.Configuration` using the existing tenant settings service. No recipient is inferred from a customer-supplied address or the public business profile:

```json
{
  "isEnabled": true,
  "topicRecipients": {
    "order": "orders@example.com",
    "new": "hello@example.com",
    "dish": "kitchen@example.com",
    "delivery": "delivery@example.com",
    "gift": "hello@example.com",
    "other": "hello@example.com"
  },
  "adminOrigin": "https://admin.example.com"
}
```

These are illustrative addresses, not production defaults. The origin must be an HTTPS origin with no path, credentials, query or fragment. Staff mail contains escaped enquiry text and a link to the authenticated detail page. Customer acknowledgement contains the receipt reference, without reflecting customer-supplied message content or links. Each audience has an independent existing outbox delivery, and existing outbox/inbox retry behaviour applies. Provider acknowledgement loss can still cause duplicate email; database submission deduplication does not claim exactly-once delivery by ACS.

## Private images and operational setup

`BlobStorage:ContactImages` defaults to the `contact-private` path/container. It must have no `PublicBaseUrl`, and must not overlap any publicly mounted storage directory, custom web root or public container. Local paths through symlinks/junctions are rejected. Azure access verifies that the container ACL is private before access; an existing public container is rejected rather than silently changing its ACL. Photos are served only through the permission-checked admin endpoint.

Configure `ContactEnquiries:ClamAv` with `Enabled`, a private/loopback `Host`, `Port` (default 3310), and bounded `TimeoutSeconds` (default 10). Deploy and maintain clamd signatures before accepting images. Clamd INSTREAM has no transport authentication or encryption, so isolate this endpoint on a private network. Missing or unreachable scanning fails closed. See the [official protocol](https://docs.clamav.net/manual/Usage/ClamdProtocol.html).

The contact decoder uses pinned [Magick.NET-Q8-AnyCPU 14.17.2](https://www.nuget.org/packages/Magick.NET-Q8-AnyCPU/14.17.2). Tests include actual HEIC bytes and metadata stripping. Other image-processing paths remain unchanged; ImageSharp 4's [build-time licence requirement](https://docs.sixlabors.com/articles/imagesharp/index.html) prevents treating that package upgrade as an automatic contact fix.

Deployment must provide the approved topic destinations (including allergen enquiries), admin origin, ACS sender/domain, running outbox worker, private storage permissions and retention/orphan-cleanup policy, scanner/signature updates, and appropriate ingress body caps. Compensation removes known failed uploads; unknown commit outcomes retain private objects so an accepted enquiry never loses its photos. Apply a retention/reconciliation policy rather than blindly deleting potentially referenced objects.

The API bounds active multipart parsing and native processing and rate-limits by tenant plus trusted connection address. Behind a storefront BFF, configure trusted forwarded headers or equivalent client throttling at trusted ingress; never accept an arbitrary public forwarded-IP header. Per-process rate limits are not a distributed abuse quota. No live ACS delivery, private Azure account or production scanner configuration is implied by merging this code.

## Staff access

`/contact-enquiries` and `/contact-enquiries/:id` reuse existing admin controls. API routes are `GET /v1/admin/contact-enquiries`, `GET /v1/admin/contact-enquiries/{id}` and `GET /v1/admin/contact-enquiries/{id}/images/{imageId}`. They require the existing admin read policy and `Customers.Read`, enforce tenant scope, and return no-store responses. Photos load through the authenticated API client into temporary browser object URLs, released when the view closes. The submitted order number remains clearly labelled as customer-supplied text.
