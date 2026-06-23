namespace Acme.CQRS.Abstractions;

/// <summary>A read request. The generic is the success value; handlers return <c>Result&lt;TResult&gt;</c>.</summary>
public interface IQuery<out TResult>;
