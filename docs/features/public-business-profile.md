# Public business profile

`GET /v1/business-profile` returns the current tenant's explicitly published contact,
legal and opening-hours information. It uses the normal tenant resolution rules and
allows anonymous visitors. No profile, unpublished profiles and invalid documents
return `404` with `Cache-Control: no-store`.

Public data is separate from the tenant's administrative name, email, phone and
address. Nothing is copied or inferred from those fields, and there is no global
fallback. Existing tenants therefore publish nothing until an administrator opts in.

## Configuration

Use the existing tenant settings API, with its existing admin and `Settings.Write`
permissions: `PUT /tenant/settings/values/Business.PublicProfile`. Its `value` is a
JSON-encoded string containing a document like this illustrative example:

```json
{
  "isPublished": true,
  "profile": {
    "displayName": "Example business",
    "website": "https://example.com",
    "contact": {
      "email": "hello@example.com",
      "phone": null,
      "whatsApp": null
    },
    "legal": {
      "companyName": null,
      "companyNumber": null,
      "registeredOffice": null,
      "icoRegistrationNumber": null,
      "isVatRegistered": null,
      "vatNumber": null
    },
    "openingHours": {
      "timezone": "Europe/London",
      "weeklyHours": [
        { "dayOfWeek": 1, "opensAt": "09:00:00", "closesAt": "17:00:00" }
      ],
      "bankHolidays": [],
      "exceptionalClosures": []
    }
  }
}
```

The existing settings column permits up to 4,000 characters. `displayName` is required
when publishing; all other public sections are optional. Website and logo URLs, if
supplied, must be absolute HTTP(S) URLs. Legal facts and contact channels must be
authored explicitly; missing facts remain null. Unknown document properties are not
included in the public DTO. The raw setting is not client-visible through the generic
public-settings endpoint.

Set `isPublished` to false or clear the tenant setting to withdraw publication. Changes
use the settings store's existing cache invalidation. Previously cached public responses
can remain visible for up to five minutes.

## Opening-hours interpretation

`openingHours: null` means unknown/unconfigured; an empty `weeklyHours` array explicitly
means closed all week. Configured hours require an IANA timezone and all three lists.
Weekdays use ISO numbers (Monday = 1, Sunday = 7). Periods open inclusively and close
exclusively, within one local day; multiple periods on a day may touch but cannot overlap.
An omitted day is closed. Opening and closing times must both be supplied.

`bankHolidays` and `exceptionalClosures` contain local `YYYY-MM-DD` dates and override
all weekly periods for that date. Administrators maintain the actual closure dates for
their business and region; this endpoint does not fetch or invent a holiday calendar.

Consumers compute “Open now” from the current instant in the supplied timezone, first
checking closures, then the weekday's periods. Recompute as time changes. The API
returns the schedule rather than a time-sensitive boolean that could become stale in
an HTTP cache. Actual delivery availability continues to use the fulfilment calendar.

## Caching

Responses vary on `X-Tenant-Id`. Successful reads use `public, max-age=300` only when
that header identifies the resolved tenant. Headerless authenticated reads, invalid
profiles and unpublished profiles use `no-store`. The profile has no separate cache
or database table.
