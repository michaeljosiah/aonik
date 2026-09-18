using Aonik.SharedKernel.Abstractions.Groups;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Groups.Endpoints;

internal sealed class CreateGroupRequestValidator : Validator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Kind)
            .NotEmpty()
            .Must(kind => kind is not null && (kind.Trim().ToLowerInvariant() is GroupKinds.Family or GroupKinds.Household))
            .WithMessage("kind must be family or household.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

internal sealed class AddGroupMemberRequestValidator : Validator<AddGroupMemberRequest>
{
    public AddGroupMemberRequestValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().MaximumLength(32);
    }
}
