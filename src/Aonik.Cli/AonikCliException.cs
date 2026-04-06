namespace Aonik.Cli;

public sealed class AonikCliException : Exception
{
    public AonikCliException(string message)
        : base(message)
    {
    }
}
