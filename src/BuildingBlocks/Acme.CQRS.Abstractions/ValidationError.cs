namespace Acme.CQRS.Abstractions;

public sealed record ValidationError
{
    public required string PropertyName { get; init; }
    public required string Message { get; init; }
}
