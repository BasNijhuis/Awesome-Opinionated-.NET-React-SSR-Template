namespace Acme.DomainAbstractions;

/// <summary>Classifies an <see cref="Error"/> so the API can map it to an HTTP status.</summary>
public enum ErrorCategory
{
    NotFound,
    Conflict,
    Validation,
}
