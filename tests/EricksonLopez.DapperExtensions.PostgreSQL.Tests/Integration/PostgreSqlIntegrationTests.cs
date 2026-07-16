using Dapper;
using EricksonLopez.DapperExtensions.PostgreSQL.Bulk;
using EricksonLopez.DapperExtensions.PostgreSQL.Pagination;
using EricksonLopez.DapperExtensions.PostgreSQL.Transactions;
using EricksonLopez.SharedKernel.Pagination;
using AwesomeAssertions;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;

namespace EricksonLopez.DapperExtensions.PostgreSQL.Tests.Integration;

/// <summary>
/// Integration tests that spin up a real PostgreSQL instance via Docker (Testcontainers).
/// Requires Docker Desktop or Docker Engine to be running.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PostgreSqlIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private NpgsqlConnection _connection = null!;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _connection = new NpgsqlConnection(_container.GetConnectionString());
        await _connection.OpenAsync();

        await _connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS products (
                id          UUID        NOT NULL PRIMARY KEY,
                name        TEXT        NOT NULL,
                price       NUMERIC     NOT NULL,
                is_active   BOOLEAN     NOT NULL DEFAULT true,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )
            """);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private sealed record ProductRow(Guid Id, string Name, decimal Price, bool IsActive);

    private static IEnumerable<ProductRow> GenerateProducts(int count)
        => Enumerable.Range(1, count).Select(i =>
            new ProductRow(Guid.NewGuid(), $"Product {i}", i * 9.99m, i % 2 == 0));

    // ─── BulkInsert tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task BulkInsertAsync_ShouldInsertAllRows()
    {
        var products = GenerateProducts(100).ToList();

        var parameters = BulkParameters.From(products)
            .Add("Ids",       p => p.Id,       NpgsqlDbType.Uuid)
            .Add("Names",     p => p.Name,     NpgsqlDbType.Text)
            .Add("Prices",    p => p.Price,    NpgsqlDbType.Numeric)
            .Add("IsActives", p => p.IsActive, NpgsqlDbType.Boolean)
            .Build();

        var rowsAffected = await _connection.BulkInsertAsync(
            """
            INSERT INTO products (id, name, price, is_active)
            SELECT * FROM UNNEST(@Ids, @Names, @Prices, @IsActives)
            """,
            parameters);

        rowsAffected.Should().Be(100);

        var count = await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products");
        count.Should().Be(100);

        await _connection.ExecuteAsync("DELETE FROM products");
    }

    [Fact]
    public async Task BulkUpsertAsync_ShouldUpdateOnConflict()
    {
        // Insert initial product
        var id = Guid.NewGuid();
        await _connection.ExecuteAsync(
            "INSERT INTO products (id, name, price) VALUES (@Id, @Name, @Price)",
            new { Id = id, Name = "Original", Price = 10m });
        var items = new[] { new { Id = 1, Tag = (string?)null }, new { Id = 2, Tag = (string?)"active" } };

        // Upsert with updated name/price
        var products = new[] { new ProductRow(id, "Updated", 99m, true) };
        var parameters = BulkParameters.From(products)
            .Add("Ids",    p => p.Id,    NpgsqlDbType.Uuid)
            .Add("Names",  p => p.Name,  NpgsqlDbType.Text)
            .Add("Prices", p => p.Price, NpgsqlDbType.Numeric)
            .Build();

        await _connection.BulkUpsertAsync(
            """
            INSERT INTO products (id, name, price)
            SELECT * FROM UNNEST(@Ids, @Names, @Prices)
            ON CONFLICT (id) DO UPDATE
                SET name  = EXCLUDED.name,
                    price = EXCLUDED.price
            """,
            parameters);

        var updated = await _connection.QuerySingleAsync<ProductRow>(
            "SELECT id, name, price, is_active AS IsActive FROM products WHERE id = @Id",
            new { Id = id });

        updated.Name.Should().Be("Updated");
        updated.Price.Should().Be(99m);

        await _connection.ExecuteAsync("DELETE FROM products");
    }

    // ─── Pagination tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task QueryPagedAsync_ShouldReturnCorrectPage()
    {
        // Insert 25 products
        var products = GenerateProducts(25).ToList();
        var parameters = BulkParameters.From(products)
            .Add("Ids",       p => p.Id,       NpgsqlDbType.Uuid)
            .Add("Names",     p => p.Name,     NpgsqlDbType.Text)
            .Add("Prices",    p => p.Price,    NpgsqlDbType.Numeric)
            .Add("IsActives", p => p.IsActive, NpgsqlDbType.Boolean)
            .Build();

        await _connection.BulkInsertAsync(
            "INSERT INTO products (id, name, price, is_active) SELECT * FROM UNNEST(@Ids, @Names, @Prices, @IsActives)",
            parameters);

        var pagination = PaginationParameters.Of(page: 2, pageSize: 10);

        var page = await _connection.QueryPagedAsync<ProductRow>(
            sql:        "SELECT id, name, price, is_active AS IsActive FROM products ORDER BY name",
            countSql:   "SELECT COUNT(*) FROM products",
            pagination: pagination);

        page.Page.Should().Be(2);
        page.PageSize.Should().Be(10);
        page.TotalCount.Should().Be(25);
        page.TotalPages.Should().Be(3);
        page.Items.Should().HaveCount(10);
        page.HasPreviousPage.Should().BeTrue();
        page.HasNextPage.Should().BeTrue();

        await _connection.ExecuteAsync("DELETE FROM products");
    }

    // ─── Transaction tests ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteInTransactionAsync_ShouldCommitOnSuccess()
    {
        var id = Guid.NewGuid();

        await _connection.ExecuteInTransactionAsync(async trx =>
        {
            await _connection.ExecuteAsync(
                "INSERT INTO products (id, name, price) VALUES (@Id, @Name, @Price)",
                new { Id = id, Name = "TxProduct", Price = 1m },
                trx);
        });

        var count = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM products WHERE id = @Id", new { Id = id });

        count.Should().Be(1);
        await _connection.ExecuteAsync("DELETE FROM products");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ShouldRollbackOnException()
    {
        var id = Guid.NewGuid();

        var act = async () => await _connection.ExecuteInTransactionAsync(async trx =>
        {
            await _connection.ExecuteAsync(
                "INSERT INTO products (id, name, price) VALUES (@Id, @Name, @Price)",
                new { Id = id, Name = "WillRollback", Price = 1m },
                trx);

            throw new InvalidOperationException("Simulated failure");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();

        var count = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM products WHERE id = @Id", new { Id = id });

        count.Should().Be(0); // Rolled back
    }
}
