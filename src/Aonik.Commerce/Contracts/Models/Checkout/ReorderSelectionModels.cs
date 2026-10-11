namespace Aonik.Commerce.Contracts.Models.Checkout;

public sealed record ReorderSelectionRequest(IReadOnlyList<ReorderDishChoice> Selections);
public sealed record ReorderDishChoice(Guid SelectionId, int Quantity);
public sealed record ReorderDishPreview(Guid SelectionId, string Name, int Quantity,
    string? PersonalisationSummary, bool IsSignature, int MaxQuantity);
public sealed record ReorderPreviewDto(Guid OrderId, IReadOnlyList<ReorderDishPreview> Dishes);
