using System.Text.Json.Serialization;

namespace Aonik.Commerce.Contracts.Api.Loyalty;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record MarkLoyaltySeenRequest([property: JsonRequired] long Mark);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record AdjustLoyaltyRequest(Guid PartyId, Guid AdjustmentId, long Points, string Reason);
