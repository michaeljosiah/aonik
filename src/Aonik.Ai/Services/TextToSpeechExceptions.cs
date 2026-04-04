namespace Aonik.Ai.Services;

internal sealed class TextToSpeechPolicyViolationException : InvalidOperationException
{
    public TextToSpeechPolicyViolationException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

internal sealed class TextToSpeechNotFoundException : InvalidOperationException
{
    public TextToSpeechNotFoundException(string message)
        : base(message)
    {
    }
}
