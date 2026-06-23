namespace Acme.CQRS.Abstractions;

public interface IRequestValidator<in TRequest>
{
    ValidationResult Validate(TRequest request);
}
