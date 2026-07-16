using System.Data;
using Npgsql;

namespace EricksonLopez.DapperExtensions.PostgreSQL.Transactions;

/// <summary>
/// Extension methods for executing operations within a PostgreSQL transaction,
/// supporting the Unit of Work pattern without requiring a dedicated UoW class.
/// </summary>
public static class TransactionExtensions
{
    /// <summary>
    /// Executes an async operation within a new transaction.
    /// Commits on success. Rolls back on any exception (including cancellation).
    /// </summary>
    /// <remarks>
    /// Usage:
    /// <code>
    /// await connection.ExecuteInTransactionAsync(async trx =>
    /// {
    ///     await connection.ExecuteAsync(insertSql, insertParams, trx);
    ///     await connection.ExecuteAsync(updateSql, updateParams, trx);
    /// });
    /// </code>
    /// </remarks>
    /// <param name="connection">The database connection.</param>
    /// <param name="operation">The async operation to execute. Receives the active transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ExecuteInTransactionAsync(
        this NpgsqlConnection connection,
        Func<NpgsqlTransaction, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(operation);

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await operation(transaction);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Executes an async operation within a new transaction and returns a result.
    /// Commits on success. Rolls back on any exception.
    /// </summary>
    /// <typeparam name="TResult">The return type of the operation.</typeparam>
    public static async Task<TResult> ExecuteInTransactionAsync<TResult>(
        this NpgsqlConnection connection,
        Func<NpgsqlTransaction, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(operation);

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
