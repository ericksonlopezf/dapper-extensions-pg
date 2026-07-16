using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace EricksonLopez.DapperExtensions.PostgreSQL.TypeHandlers;

/// <summary>
/// Dapper type handler for PostgreSQL JSONB columns.
/// Serializes/deserializes .NET objects to/from JSONB using System.Text.Json.
/// </summary>
/// <remarks>
/// Register once at application startup:
/// <code>
/// SqlMapper.AddTypeHandler(new JsonbTypeHandler&lt;MyDto&gt;());
/// </code>
///
/// Then use normally in Dapper queries:
/// <code>
/// var result = await connection.QueryAsync&lt;MyEntity&gt;("SELECT * FROM ...");
/// // The JSONB column will be deserialized automatically into MyDto.
/// </code>
/// </remarks>
/// <typeparam name="T">The .NET type to serialize/deserialize.</typeparam>
public sealed class JsonbTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = value is null
            ? DBNull.Value
            : System.Text.Json.JsonSerializer.Serialize(value, Options);

        if (parameter is NpgsqlParameter npgsqlParameter)
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
    }

    public override T? Parse(object value)
    {
        if (value is DBNull or null)
            return default;

        var json = value.ToString()!;
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, Options);
    }
}

/// <summary>
/// Convenience methods for registering JSONB type handlers.
/// </summary>
public static class NpgsqlTypeHandlerRegistrar
{
    /// <summary>
    /// Registers a JSONB type handler for the specified type.
    /// Call once per type at application startup (e.g., in Program.cs or DI setup).
    /// </summary>
    public static void RegisterJsonbHandler<T>()
        => SqlMapper.AddTypeHandler(new JsonbTypeHandler<T>());

    /// <summary>
    /// Registers JSONB handlers for multiple types at once.
    /// </summary>
    public static void RegisterJsonbHandlers(params Action[] registrations)
    {
        foreach (var registration in registrations)
            registration();
    }
}
