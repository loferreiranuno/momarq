using Testcontainers.PostgreSql;

namespace VisualSearch.Api.Tests.Fixtures;

/// <summary>
/// Shared PostgreSQL container fixture using Testcontainers.
/// Uses pgvector/pgvector:pg16 image for vector similarity search support.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("visualsearch_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithCleanUp(true)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Collection definition for sharing the PostgreSQL container across test classes.
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
}
