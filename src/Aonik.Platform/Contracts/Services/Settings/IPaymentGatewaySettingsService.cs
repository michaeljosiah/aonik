using Aonik.Platform.Contracts.Models.Settings;

namespace Aonik.Platform.Contracts.Services.Settings;

public interface IPaymentGatewaySettingsService
{
    Task<PaymentGatewaySettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);

    Task<PaymentGatewaySettingsSnapshot> UpdateAsync(
        PaymentGatewaySettingsUpdate update,
        CancellationToken cancellationToken = default);

    Task<PaymentGatewayTestResult> TestAsync(
        string providerCode,
        CancellationToken cancellationToken = default);
}
