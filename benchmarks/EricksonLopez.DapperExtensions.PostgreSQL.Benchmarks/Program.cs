using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Dapper;
using EricksonLopez.DapperExtensions.PostgreSQL.Bulk;
using Npgsql;
using NpgsqlTypes;

// NOTE: Benchmarks require a running PostgreSQL instance.
// Set the connection string via environment variable: BENCHMARK_PG_CONN
// Example: "Host=localhost;Database=benchdb;Username=postgres;Password=postgres"
//
// The table must exist:
// CREATE TABLE IF NOT EXISTS bench_products (
//     id uuid PRIMARY KEY, name text NOT NULL, price numeric NOT NULL
// );

var connStr = Environment.GetEnvironmentVariable("BENCHMARK_PG_CONN")
    ?? "Host=localhost;Database=benchdb;Username=postgres;Password=postgres";

BenchmarkRunner.Run<BulkInsertBenchmarks>(args: args);

[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
public class BulkInsertBenchmarks
{
    private NpgsqlConnection _connection = null!;
    private List<BenchProduct> _products = null!;

    [Params(100, 1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var connStr = Environment.GetEnvironmentVariable("BENCHMARK_PG_CONN")
            ?? "Host=localhost;Database=benchdb;Username=postgres;Password=postgres";

        _connection = new NpgsqlConnection(connStr);
        await _connection.OpenAsync();

        await _connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS bench_products (
                id    UUID    NOT NULL PRIMARY KEY,
                name  TEXT    NOT NULL,
                price NUMERIC NOT NULL
            )
            """);

        _products = Enumerable.Range(1, RowCount)
            .Select(i => new BenchProduct(Guid.NewGuid(), $"Product {i}", i * 9.99m))
            .ToList();
    }

    [IterationSetup]
    public async Task CleanTable()
        => await _connection.ExecuteAsync("TRUNCATE TABLE bench_products");

    [Benchmark(Baseline = true, Description = "Row-by-row INSERT")]
    public async Task RowByRowInsert()
    {
        foreach (var p in _products)
        {
            await _connection.ExecuteAsync(
                "INSERT INTO bench_products (id, name, price) VALUES (@Id, @Name, @Price)",
                new { p.Id, p.Name, p.Price });
        }
    }

    [Benchmark(Description = "UNNEST bulk INSERT")]
    public async Task UnnestBulkInsert()
    {
        var parameters = BulkParameters.From(_products)
            .Add("Ids", p => p.Id, NpgsqlDbType.Uuid)
            .Add("Names", p => p.Name, NpgsqlDbType.Text)
            .Add("Prices", p => p.Price, NpgsqlDbType.Numeric)
            .Build();

        await _connection.BulkInsertAsync(
            "INSERT INTO bench_products (id, name, price) SELECT * FROM UNNEST(@Ids, @Names, @Prices)",
            parameters);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _connection.ExecuteAsync("DROP TABLE IF EXISTS bench_products");
        await _connection.DisposeAsync();
    }
}

internal sealed record BenchProduct(Guid Id, string Name, decimal Price);
