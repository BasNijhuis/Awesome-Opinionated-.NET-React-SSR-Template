namespace Acme.CQRS.Abstractions;

/// <summary>A state-changing request. The generic is the success value; handlers return <c>Result&lt;TResult&gt;</c>.</summary>
public interface ICommand<out TResult>;
