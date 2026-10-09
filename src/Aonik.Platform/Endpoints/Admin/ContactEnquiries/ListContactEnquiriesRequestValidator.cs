using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.SharedKernel.Validation;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.Admin.ContactEnquiries;

public sealed class ListContactEnquiriesRequestValidator : Validator<ListContactEnquiriesRequest>
{
    public ListContactEnquiriesRequestValidator()
    {
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize();
        RuleFor(x => x.Topic).Must(topic => topic is null || ContactEnquiryTopics.IsKnown(topic))
            .WithMessage("Choose a known enquiry topic.");
    }
}
