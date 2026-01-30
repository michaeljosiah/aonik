# Autonumbering

## Overview

Autonumbering provides configurable reference generation for key entities (for example, invoices and orders) with support for prefixes, suffixes, and sequence strategies. The reference is generated in the application layer and stored on the target entity when it is created.

## Performance considerations

### Primary risks

- **Hot row contention on the profile counter**: A single sequence row can become a bottleneck under high concurrency if every insert attempts to increment the same counter.
- **Extra round-trips to enforce uniqueness**: If the system relies on a pre-check read to ensure a generated reference is unique, read amplification can add latency and lock contention.
- **Overly chatty reservation validation**: Re-checking a reservation on every save adds overhead when the reservation is already guaranteed by transaction boundaries or constraints.
- **Lock escalation under bursty load**: Large batches that hold locks too long can cause lock escalation and block other workloads.
- **Inefficient formatting lookups**: Repeated format token lookups or tenant metadata reads in the hot path can add avoidable latency.

### Mitigations

- **Use a single-row counter update with row-level locking**: Update the profile’s `LastIssuedValue` in a short transaction (SQL Server `UPDLOCK`/`ROWLOCK`), then construct the reference in the same transaction.
- **Avoid save-time reservation lookups**: For standard issuance, generate the reference directly and write it to the entity without querying a reservation table.
- **Rely on unique constraints for conflict detection**: Enforce uniqueness on the final reference (`Invoice.Reference`, `Order.Reference`) or on `ProfileId + SequenceValue` in the reservation table to avoid pre-check reads.
- **Use reservations only when pre-allocation is required**: If the product needs to pre-reserve numbers (e.g., offline issuance), use a reservation table with an expiration policy and background cleanup instead of checking it during each save.
- **Partition profiles by scope**: Keep counters per-tenant and per-entity to reduce contention and allow independent scaling.
- **Batch allocation for high-throughput issuers**: Allocate ranges (for example, 100 at a time) per service instance to reduce counter contention; store the high-water mark and consume locally until exhausted.
- **Cache static format metadata**: Cache tenant and profile formatting tokens (prefix/suffix templates) in memory to avoid repeated reads in the issuance hot path.

## Reservation table usage guidance

The reservation table should not be queried during the normal save flow for invoices or orders unless a feature explicitly requires pre-allocation. The default issuance path should:

1. Increment the profile counter in a transaction.
2. Build the formatted reference.
3. Persist the entity with the generated reference.

Reservations are an **optional** optimization for workflows that need to allocate numbers ahead of time. In those cases, consume the reservation by ID and avoid re-querying the reservation table during entity save unless the reservation is invalid or expired.

### Recommended indexes

- `AutonumberProfiles`: unique index on `(TenantId, EntityType)` to keep lookups tight.
- `AutonumberReservations`: unique index on `(ProfileId, SequenceValue)` to enforce uniqueness without pre-check reads.
- Target entity references (for example, `Invoice.Reference`, `Order.Reference`) should be unique per tenant to prevent duplicate references on the final records.
