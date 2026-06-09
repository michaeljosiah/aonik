using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Services.Partners.Connectors;

/// <summary>
/// Materialises a runtime connector <strong>bound to a persisted <see cref="Connector"/> row</strong>
/// (Spec 042 §7.1). The connector kinds, their typed <c>HttpClient</c>s and auth handlers stay registered in
/// DI exactly as before; the factory resolves those and binds the row's <c>CredentialsRef</c> + <c>ConfigJson</c>
/// so the returned object authenticates with that account's credentials and stamps its own <c>ConnectorId</c>
/// downstream. The fail-closed precedence (§7.2) is enforced by the config provider at call time: a row with
/// no bound bundle that is not the legacy default throws rather than borrowing the global account.
/// </summary>
internal interface IPartnerConnectorFactory
{
    IPartnerPayoutConnector CreatePayout(Connector row);
    IPartnerBillPaymentConnector CreateBillPayment(Connector row);
}

internal sealed class PartnerConnectorFactory : IPartnerConnectorFactory
{
    private readonly IServiceProvider _services;

    public PartnerConnectorFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IPartnerPayoutConnector CreatePayout(Connector row)
    {
        var (descriptor, binding) = Resolve(row, PartnerServiceCategory.Payout);
        return descriptor.Kind switch
        {
            ConnectorRegistry.FlutterwavePayoutV4 => new FlutterwavePayoutConnector(
                    _services.GetRequiredService<FlutterwaveClient>(),
                    _services.GetRequiredService<IFlutterwaveConfigProvider>())
                .Bind(binding),
            _ => throw new InvalidOperationException(
                $"No payout connector is registered for kind '{descriptor.Kind}'."),
        };
    }

    public IPartnerBillPaymentConnector CreateBillPayment(Connector row)
    {
        var (descriptor, binding) = Resolve(row, PartnerServiceCategory.BillPayment);
        return descriptor.Kind switch
        {
            ConnectorRegistry.FlutterwaveBillsV3 => new FlutterwaveBillPaymentConnector(
                    _services.GetRequiredService<FlutterwaveBillsClient>(),
                    _services.GetRequiredService<IFlutterwaveBillsConfigProvider>())
                .Bind(binding),
            _ => throw new InvalidOperationException(
                $"No bill-payment connector is registered for kind '{descriptor.Kind}'."),
        };
    }

    private static (ConnectorKindDescriptor Descriptor, ConnectorBinding Binding) Resolve(
        Connector row, PartnerServiceCategory expectedPort)
    {
        // ConnectorType normally stores the connector kind. Tolerate rows persisted before the Spec 042 lift
        // that still hold the bare provider code (e.g. "Flutterwave"): map provider code + port → the kind.
        var descriptor = ConnectorRegistry.Get(row.ConnectorType)
            ?? ConnectorRegistry.ForProvider(row.ConnectorType).FirstOrDefault(k => k.Port == expectedPort)
            ?? throw new InvalidOperationException(
                $"Connector {row.Id} has an unregistered kind '{row.ConnectorType}'.");

        if (descriptor.Port != expectedPort)
        {
            throw new InvalidOperationException(
                $"Connector {row.Id} kind '{descriptor.Kind}' is a {descriptor.Port} connector, not {expectedPort}.");
        }

        var binding = new ConnectorBinding(
            ConnectorId: row.Id,
            ConnectorKind: descriptor.Kind,
            ProviderCode: descriptor.ProviderCode,
            CredentialsRef: row.CredentialsRef,
            ConfigJson: row.ConfigJson,
            AllowLegacyFallback: row.IsLegacyDefault);

        return (descriptor, binding);
    }
}
