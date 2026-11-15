using Voxa.Frames;

namespace Aonik.Voice.Frames;

internal sealed record ApprovalReviewFrame(Guid ApprovalRequestId, string? AdminUiBaseUrl) : DataFrame;
