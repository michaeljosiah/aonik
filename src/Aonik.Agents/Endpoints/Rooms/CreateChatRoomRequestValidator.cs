using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class CreateChatRoomRequestValidator : Validator<CreateChatRoomRequest>
{
    public CreateChatRoomRequestValidator()
    {
        RuleFor(request => request.Title).Must(value => value?.Trim().Length is >= 1 and <= 160).WithMessage("Room title must contain 1–160 characters.");
        RuleFor(request => request.Context).Must(context => context is null || context.Count <= 12 && context.All(item => item is not null
            && new[] { "Invoice", "Order", "PaymentIntent", "LedgerEntry", "Proposal", "Customer" }.Contains(item.Type)
            && !string.IsNullOrWhiteSpace(item.Reference) && item.Reference.Length <= 100 && !item.Reference.Any(char.IsControl)))
            .WithMessage("Use up to twelve typed record references, each at most 100 characters.");
    }
}
