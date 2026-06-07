using System.Net;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// A typed failure from the Flutterwave API. Carries the vendor error <see cref="ErrorType"/> /
/// <see cref="ErrorCode"/> and a <see cref="Retryable"/> classification (Spec 037 §5.10, §8): 5xx /
/// 429 are retryable; 4xx / 409 are not. The caller (and the standard resilience handler) act on the
/// flag. Transport timeouts surface as <see cref="TimeoutException"/>, not this type.
/// </summary>
internal sealed class FlutterwaveException : Exception
{
    public FlutterwaveException(
        string message,
        string? errorType,
        string? errorCode,
        HttpStatusCode? statusCode,
        bool retryable,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorType = errorType;
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Retryable = retryable;
    }

    public string? ErrorType { get; }

    public string? ErrorCode { get; }

    public HttpStatusCode? StatusCode { get; }

    public bool Retryable { get; }

    /// <summary>
    /// Retryable vs non-retryable per Spec 037 §5.10: 5xx + 429 retryable; 4xx (incl. 409 conflict,
    /// 401 auth, 422 validation) non-retryable.
    /// </summary>
    public static bool IsRetryableStatus(HttpStatusCode statusCode)
        => (int)statusCode >= 500 || statusCode == HttpStatusCode.TooManyRequests;
}
