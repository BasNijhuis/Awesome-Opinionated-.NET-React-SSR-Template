namespace Acme.Kernel.Domain.Services;

public sealed record RandomProvider : IRandomProvider
{
    private readonly Random _random = new();

    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
