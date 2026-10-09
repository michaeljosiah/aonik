using Aonik.Platform.Contracts.Services.Authentication;

using Microsoft.AspNetCore.Http;

namespace Aonik.Infrastructure.Authentication;

internal sealed class AccountAccessIdentityProofAccessor(IHttpContextAccessor httpContext) : IAccountAccessIdentityProofAccessor
{
    public AccountAccessIdentityProof? GetCurrent()
        => httpContext.HttpContext?.Items[typeof(AccountAccessIdentityProof)] as AccountAccessIdentityProof;
}
