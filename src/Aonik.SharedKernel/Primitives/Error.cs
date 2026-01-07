namespace Aonik.SharedKernel.Primitives;

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string entity, string identifier)
        => new("NotFound", $"{entity} with identifier '{identifier}' was not found");

    public static Error Validation(string message)
        => new("Validation", message);

    public static Error Conflict(string message)
        => new("Conflict", message);

    public static Error Failure(string message)
        => new("Failure", message);
}
