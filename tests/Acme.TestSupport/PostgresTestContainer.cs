using Testcontainers.PostgreSql;

namespace Acme.TestSupport;

/// <summary>
/// Centralized Testcontainers Postgres setup for the persistence/infrastructure test projects, so the
/// image and container configuration live in one place rather than being duplicated per test.
/// </summary>
public static class PostgresTestContainer
{
    /// <summary>
    /// Matches the image Aspire's <c>AddPostgres</c> resolves — <c>Aspire.Hosting.PostgreSQL</c>
    /// 13.4.6's <c>PostgresContainerImageTags</c> default (<c>docker.io/library/postgres:18.3</c>) — so
    /// tests run against the same Postgres version as production. That type is internal in the package's
    /// reference assembly, so the tag is pinned here; bump it when the Aspire package's default changes.
    /// </summary>
    public const string Image = "docker.io/library/postgres:18.3";

    public static PostgreSqlContainer Create() => new PostgreSqlBuilder(Image).Build();
}
