namespace Aonik.Commerce.Services.Fulfilment;

public sealed class OrderFulfilmentConflictException() : Exception("Fulfilment changed. Reload the order before updating it.");
