namespace Acme.Modules.Greetings.Domain.Contracts;

/// <summary>Inputs for <c>Greeting.Create</c>. Implemented by the Application command model.</summary>
public interface ICreateGreetingSpec
{
    string Message { get; }
}
