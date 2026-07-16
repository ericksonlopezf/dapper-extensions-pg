using Npgsql;
using NpgsqlTypes;

namespace EricksonLopez.DapperExtensions.PostgreSQL.Bulk;

/// <summary>
/// Builds the array parameters required for PostgreSQL UNNEST bulk operations.
/// </summary>
/// <remarks>
/// UNNEST allows inserting thousands of rows in a single query by passing arrays of values,
/// avoiding the N+1 problem and parameter count limits of multi-value INSERT statements.
///
/// Usage:
/// <code>
/// var parameters = BulkParameters.From(products)
///     .Add("Ids",        p => p.Id,        NpgsqlDbType.Uuid)
///     .Add("Names",      p => p.Name,      NpgsqlDbType.Text)
///     .Add("Prices",     p => p.Price,     NpgsqlDbType.Numeric)
///     .Add("CreatedAts", p => p.CreatedAt, NpgsqlDbType.TimestampTz)
///     .Build();
///
/// var sql = """
///     INSERT INTO products (id, name, price, created_at)
///     SELECT * FROM UNNEST(@Ids, @Names, @Prices, @CreatedAts)
///     """;
///
/// await connection.ExecuteAsync(sql, parameters);
/// </code>
/// </remarks>
public sealed class BulkParameters<T>
{
    private readonly IReadOnlyList<T> _items;
    private readonly List<(string Name, Array Values, NpgsqlDbType DbType)> _columns = [];

    internal BulkParameters(IEnumerable<T> items)
    {
        _items = items.ToList();
    }

    /// <summary>
    /// Adds a column mapping: extracts a value from each item and stores it as a typed array.
    /// </summary>
    /// <param name="parameterName">The UNNEST parameter name (used in the SQL query as @Name).</param>
    /// <param name="selector">Extracts the column value from each item.</param>
    /// <param name="dbType">The PostgreSQL type of the column.</param>
    public BulkParameters<T> Add<TValue>(
        string parameterName,
        Func<T, TValue> selector,
        NpgsqlDbType dbType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        ArgumentNullException.ThrowIfNull(selector);

        var values = _items.Select(selector).ToArray();
        _columns.Add((parameterName, values, dbType));
        return this;
    }

    /// <summary>
    /// Builds the <see cref="NpgsqlParameter"/> collection ready to pass to Dapper.
    /// </summary>
    public NpgsqlParameter[] Build()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException(
                "At least one column must be added via Add() before calling Build().");

        return _columns
            .Select(col => new NpgsqlParameter(col.Name, col.DbType | NpgsqlDbType.Array)
            {
                Value = col.Values
            })
            .ToArray();
    }

    /// <summary>
    /// The number of items that will be inserted.
    /// </summary>
    public int Count => _items.Count;
}

/// <summary>
/// Entry point for creating <see cref="BulkParameters{T}"/> instances.
/// </summary>
public static class BulkParameters
{
    /// <summary>
    /// Begins building bulk parameters for the given collection.
    /// </summary>
    public static BulkParameters<T> From<T>(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new BulkParameters<T>(items);
    }
}
