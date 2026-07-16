using System.Data;
using Dapper;
using Npgsql;

namespace EricksonLopez.DapperExtensions.PostgreSQL.Bulk;

/// <summary>
/// Extension methods for bulk INSERT and UPSERT operations on PostgreSQL connections.
/// All methods leverage UNNEST for significantly higher throughput than row-by-row inserts.
/// </summary>
public static class BulkExtensions
{
    /// <summary>
    /// Executes a bulk INSERT using UNNEST. Suitable for inserting large collections of rows
    /// in a single round-trip to the database.
    /// </summary>
    /// <remarks>
    /// Performance characteristics vs. alternatives:
    /// <list type="bullet">
    ///   <item>Row-by-row INSERT: O(n) round-trips</item>
    ///   <item>Multi-value INSERT (...),(...): Limited by parameter count (~65,535 max)</item>
    ///   <item>UNNEST: 1 round-trip regardless of row count. No parameter limit.</item>
    /// </list>
    ///
    /// Expected SQL pattern:
    /// <code>
    /// INSERT INTO table_name (col1, col2, col3)
    /// SELECT * FROM UNNEST(@Param1, @Param2, @Param3)
    /// </code>
    ///
    /// Or with conflict handling (upsert):
    /// <code>
    /// INSERT INTO table_name (id, name, price)
    /// SELECT * FROM UNNEST(@Ids, @Names, @Prices)
    /// ON CONFLICT (id) DO UPDATE
    ///   SET name = EXCLUDED.name, price = EXCLUDED.price
    /// </code>
    /// </remarks>
    /// <param name="connection">The database connection. Must be a <see cref="NpgsqlConnection"/>.</param>
    /// <param name="sql">The SQL INSERT...SELECT FROM UNNEST(...) statement.</param>
    /// <param name="parameters">Parameters built with <see cref="BulkParameters{T}"/>.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <param name="commandTimeout">Optional command timeout in seconds.</param>
    /// <returns>Number of rows affected.</returns>
    public static async Task<int> BulkInsertAsync(
        this IDbConnection connection,
        string sql,
        NpgsqlParameter[] parameters,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.Length == 0)
            return 0;

        // Ensure connection is open — Dapper normally handles this, but
        // NpgsqlParameter arrays require an NpgsqlConnection
        var npgsqlConnection = connection as NpgsqlConnection
            ?? throw new ArgumentException(
                $"BulkInsertAsync requires an {nameof(NpgsqlConnection)}. " +
                $"Received: {connection.GetType().Name}", nameof(connection));

        await using var command = npgsqlConnection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = commandTimeout ?? 30;

        if (transaction is not null)
            command.Transaction = (NpgsqlTransaction)transaction;

        command.Parameters.AddRange(parameters);

        if (npgsqlConnection.State != ConnectionState.Open)
            await npgsqlConnection.OpenAsync();

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes a bulk INSERT ... ON CONFLICT DO UPDATE (upsert) using UNNEST.
    /// </summary>
    /// <remarks>
    /// Convenience overload that wraps <see cref="BulkInsertAsync"/> with the same semantics.
    /// The conflict resolution is defined in the <paramref name="sql"/> itself.
    /// </remarks>
    public static Task<int> BulkUpsertAsync(
        this IDbConnection connection,
        string sql,
        NpgsqlParameter[] parameters,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        => connection.BulkInsertAsync(sql, parameters, transaction, commandTimeout);
}
