using System.Net;
using System.Net.Http.Json;

using Aonik.Commerce.Contracts.Models.Checkout;

using FluentAssertions;

namespace Aonik.Api.Tests;

public partial class CommerceBoxCartEndpointTests
{
    [Fact]
    public async Task LoyaltyDraft_Should_RestoreIntentAsLong_ExplainDisabledRedemption_AndRequireCurrentCartVersion()
    {
        var tenant = Guid.NewGuid();
        var (bundle, variant) = await SeedBoxWorldAsync(tenant);
        using var client = Client(tenant);
        var box = await CreateBoxAsync(client, bundle, variant, 2);
        client.DefaultRequestHeaders.Add("X-Cart-Token", box.CartToken);
        UseCartVersion(client, box.CartVersion);
        var path = DraftPath(box.Box.CartId);

        using var saved = await client.PutAsJsonAsync(path, new { requestedPoints = 3000000000L, notes = "Keep the draft" });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(saved);
        var draft = (await saved.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        draft.Draft!.RequestedPoints.Should().Be(3000000000L);
        var restored = (await client.GetFromJsonAsync<BoxCartDto>($"/commerce/carts/{box.Box.CartId}"))!;
        restored.CheckoutDraft!.RequestedPoints.Should().Be(3000000000L);
        restored.Quote.Loyalty!.ReasonCode.Should().Be(LoyaltyQuoteReasons.Disabled);
        restored.Quote.Loyalty.AppliedPoints.Should().Be(0);
        restored.Quote.Total.Should().Be(box.Quote.Total);

        UseCartVersion(client, "CAcGBQQDAgE=");
        using var stale = await client.PutAsJsonAsync(path, new { requestedPoints = 0 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        AssertNoStore(stale);
        UseCartVersion(client, draft.CartVersion);
        using var invalid = await client.PutAsJsonAsync(path, new { requestedPoints = -1 });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertNoStore(invalid);
        using var cleared = await client.PutAsJsonAsync(path, draft.Draft with { RequestedPoints = 0 });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        var clearDraft = (await cleared.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!.Draft!;
        clearDraft.RequestedPoints.Should().Be(0);
        clearDraft.Notes.Should().Be("Keep the draft");
    }
}
