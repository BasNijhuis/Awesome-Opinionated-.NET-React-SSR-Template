using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Modules.Greetings.Application.Contracts;

namespace Acme.Modules.Greetings.Application.Features.Suggest;

/// <summary>
/// Returns a greeting phrase localized to the request's active locale. The locale handling lives
/// behind <see cref="IGreetingSuggestionProvider"/> (implemented in Infrastructure over the kernel
/// <c>ILocaleProvider</c>), so the handler stays locale-agnostic.
/// </summary>
internal sealed class SuggestGreetingHandler(IGreetingSuggestionProvider provider)
    : IQueryHandler<SuggestGreetingQuery, GreetingSuggestionDto>
{
    public Task<Result<GreetingSuggestionDto>> HandleAsync(
        SuggestGreetingQuery query,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult<Result<GreetingSuggestionDto>>(
            new GreetingSuggestionDto { Suggestion = provider.Suggest() }
        );
}
