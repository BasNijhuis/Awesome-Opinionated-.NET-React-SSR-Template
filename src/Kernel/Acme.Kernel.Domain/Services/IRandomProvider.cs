namespace Acme.Kernel.Domain.Services;

public interface IRandomProvider
{
    int Next(int maxExclusive);

    int Next(int minInclusive, int maxExclusive);
}
