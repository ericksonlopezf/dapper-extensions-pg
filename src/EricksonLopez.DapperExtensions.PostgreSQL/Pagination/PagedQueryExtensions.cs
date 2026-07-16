using System.Data;
using Dapper;
using EricksonLopez.SharedKernel.Pagination;

namespace EricksonLopez.DapperExtensions.PostgreSQL.Pagination;

/// <summary>
/// Extension methods for executing paginated queries with Dapper, returning
/// <see cref="PagedList{T}"/> from the SharedKernel.
/// </summary>
public static class PagedQueryExtensions
{
    /// <summary>
    /// Executes a paginated query and returns a <see cref="PagedList{T}"/> with metadata.
    /// Executes two SQL statements: one for the page data and one for the total count.
    /// Both queries run in the same connection. No transaction is required.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">
    /// The data query. Do NOT include LIMIT/OFFSET — they are appended automatically
    /// based on <paramref name="pagination"/>.
    /// </param>
    /// <param name="countSql">
    /// The count query. Should return a single <see cref="int"/> or <see cref="long"/>.
    /// Example: <c>SELECT COUNT(*) FROM products WHERE active = true</c>
    /// </param>
    /// <param name="pagination">Pagination parameters (page, page size).</param>
    /// <param name="param">Optional Dapper parameters shared between both queries.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <param name="commandTimeout">Optional command timeout in seconds.</param>
    /// <returns>A <see cref="PagedList{T}"/> with items and full pagination metadata.</returns>
    /// <remarks>
    /// Usage:
    /// <code>
    /// var page = await connection.QueryPagedAsync&lt;ProductDto&gt;(
    ///     sql:        "SELECT id, name, price FROM products WHERE active = @Active ORDER BY name",
    ///     countSql:   "SELECT COUNT(*) FROM products WHERE active = @Active",
    ///     pagination: PaginationParameters.Of(page: 1, pageSize: 20),
    ///     param:      new { Active = true });
    ///
    /// // page.TotalCount, page.TotalPages, page.HasNextPage, etc. are all set.
    /// </code>
    /// </remarks>
    public static async Task<PagedList<T>> QueryPagedAsync<T>(
        this IDbConnection connection,
        string sql,
        string countSql,
        PaginationParameters pagination,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(countSql);
        ArgumentNullException.ThrowIfNull(pagination);

        var pagedSql = $"{sql} LIMIT {pagination.PageSize} OFFSET {pagination.Skip}";

        var items = await connection.QueryAsync<T>(
            pagedSql, param, transaction, commandTimeout);

        var totalCount = await connection.ExecuteScalarAsync<int>(
            countSql, param, transaction, commandTimeout);

        return PagedList<T>.Create(
            items: items,
            totalCount: totalCount,
            parameters: pagination);
    }

    /// <summary>
    /// Executes a paginated query using a single SQL with multiple result sets (QueryMultipleAsync).
    /// The SQL must return two result sets: the page data first, then COUNT(*) second.
    /// Slightly more efficient as it uses one round-trip instead of two.
    /// </summary>
    /// <remarks>
    /// Expected SQL pattern:
    /// <code>
    /// SELECT id, name FROM products WHERE active = @Active ORDER BY name LIMIT @Limit OFFSET @Offset;
    /// SELECT COUNT(*) FROM products WHERE active = @Active;
    /// </code>
    /// </remarks>
    public static async Task<PagedList<T>> QueryPagedMultipleAsync<T>(
        this IDbConnection connection,
        string sql,
        PaginationParameters pagination,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(pagination);

        using var multi = await connection.QueryMultipleAsync(sql, param, transaction, commandTimeout);

        var items = await multi.ReadAsync<T>();
        var totalCount = await multi.ReadSingleAsync<int>();

        return PagedList<T>.Create(items, totalCount, pagination);
    }
}
