using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

using Moq;

namespace Aonik.Database.Tests.Commerce;

internal static class DiscountQuoteTestFactory
{
    public static CartDiscountQuotes Create(CommerceDbContext context, Guid tenantId, IClock clock)
        => new(context, new TestTenantProvider(tenantId), new DiscountService(context, new TestTenantProvider(tenantId), clock),
            new ZeroRateTaxCalculator(), Mock.Of<ITenantSettingStore>(), Mock.Of<ISettingProvider>(), Mock.Of<ITenantCurrencyProvider>());
}
