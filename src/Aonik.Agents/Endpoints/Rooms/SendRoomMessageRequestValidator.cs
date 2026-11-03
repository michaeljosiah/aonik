using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class SendRoomMessageRequestValidator : Validator<SendRoomMessageRequest>
{
    public SendRoomMessageRequestValidator()
    {
        RuleFor(request => request.ClientRequestId).NotEmpty();
        RuleFor(request => request.Content).Must(value => value?.Trim().Length is >= 1 and <= 16000).WithMessage("Provide a message of 1–16,000 characters.");
        RuleFor(request => request.MentionKeys).Must(keys => keys is null || keys.Count <= 16 && keys.All(key => !string.IsNullOrWhiteSpace(key) && key.Length <= 128))
            .WithMessage("Use up to sixteen valid participant mentions.");
    }
}
