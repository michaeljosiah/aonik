namespace Aonik.SharedKernel.GuardClauses;

public static class Guard
{
    public static void Against<TException>(bool condition, string message)
        where TException : Exception
    {
        if (condition)
        {
            throw (TException)Activator.CreateInstance(typeof(TException), message)!;
        }
    }

    public static void AgainstNull<T>(T? value, string paramName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    public static void AgainstNullOrEmpty(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or empty", paramName);
        }
    }

    public static void AgainstNegativeOrZero(decimal value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Value must be greater than zero", paramName);
        }
    }
}
