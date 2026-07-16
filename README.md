# EricksonLopez.DapperExtensions.PostgreSQL

[![NuGet](https://img.shields.io/nuget/v/EricksonLopez.DapperExtensions.PostgreSQL?style=for-the-badge&logo=nuget&logoColor=white&color=512BD4)](https://www.nuget.org/packages/EricksonLopez.DapperExtensions.PostgreSQL)
[![CI](https://img.shields.io/github/actions/workflow/status/ericksonlopezf/dapper-extensions-pg/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=CI)](https://github.com/ericksonlopezf/dapper-extensions-pg/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)

High-performance Dapper extensions for **PostgreSQL**. The core feature is bulk insert and upsert via UNNEST — a PostgreSQL-native strategy that's **10-30x faster than row-by-row inserts** with a single round-trip to the database.

Also includes: paginated queries returning `PagedList<T>`, transaction helpers, and JSONB type handler.

---

## Why this exists

Dapper is fast, but inserting collections row-by-row (even with `ExecuteAsync`) is slow:
- **100 rows row-by-row:** ~15ms
- **100 rows UNNEST:** ~3ms
- **10,000 rows row-by-row:** ~1,300ms
- **10,000 rows UNNEST:** ~45ms (~29x faster)

Multi-value `INSERT ... VALUES (...),(...)` is limited by PostgreSQL's ~65,535 parameter count.  
UNNEST has no such limit. 1,000,000 rows? One round-trip.

---

## Installation

```bash
dotnet add package EricksonLopez.DapperExtensions.PostgreSQL
```

**Requires:** `EricksonLopez.SharedKernel` (automatically pulled as a dependency)

---

## Quick Start

### Bulk Insert (UNNEST)

```csharp
// Build the typed array parameters
var parameters = BulkParameters.From(products)
    .Add("Ids",       p => p.Id,        NpgsqlDbType.Uuid)
    .Add("Names",     p => p.Name,      NpgsqlDbType.Text)
    .Add("Prices",    p => p.Price,     NpgsqlDbType.Numeric)
    .Add("CreatedAts",p => p.CreatedAt, NpgsqlDbType.TimestampTz)
    .Build();

// Execute — one round-trip regardless of how many rows
var rowsInserted = await connection.BulkInsertAsync(
    """
    INSERT INTO products (id, name, price, created_at)
    SELECT * FROM UNNEST(@Ids, @Names, @Prices, @CreatedAts)
    """,
    parameters);
```

### Bulk Upsert (ON CONFLICT DO UPDATE)

```csharp
var parameters = BulkParameters.From(products)
    .Add("Ids",    p => p.Id,    NpgsqlDbType.Uuid)
    .Add("Names",  p => p.Name,  NpgsqlDbType.Text)
    .Add("Prices", p => p.Price, NpgsqlDbType.Numeric)
    .Build();

await connection.BulkUpsertAsync(
    """
    INSERT INTO products (id, name, price)
    SELECT * FROM UNNEST(@Ids, @Names, @Prices)
    ON CONFLICT (id) DO UPDATE
        SET name  = EXCLUDED.name,
            price = EXCLUDED.price
    """,
    parameters);
```

### Paginated Query

Returns `PagedList<T>` from `EricksonLopez.SharedKernel` — includes `TotalCount`, `TotalPages`, `HasNextPage`, etc.

```csharp
var page = await connection.QueryPagedAsync<ProductDto>(
    sql:      "SELECT id, name, price FROM products WHERE active = @Active ORDER BY name",
    countSql: "SELECT COUNT(*) FROM products WHERE active = @Active",
    pagination: PaginationParameters.Of(page: 1, pageSize: 20),
    param:    new { Active = true });

// page.Items        — IReadOnlyList<ProductDto> for this page
// page.TotalCount   — total matching rows
// page.TotalPages   — total pages
// page.HasNextPage  — true if not on last page
```

**Single round-trip variant** (query + count in one call):

```csharp
var page = await connection.QueryPagedMultipleAsync<ProductDto>(
    sql: """
        SELECT id, name FROM products WHERE active = @Active ORDER BY name
        LIMIT @Limit OFFSET @Offset;
        SELECT COUNT(*) FROM products WHERE active = @Active;
        """,
    pagination: PaginationParameters.Of(page: 1, pageSize: 20),
    param: new { Active = true, Limit = 20, Offset = 0 });
```

### Transactions (Unit of Work)

```csharp
// Void overload — commits on success, rolls back on exception
await connection.ExecuteInTransactionAsync(async trx =>
{
    await connection.ExecuteAsync(insertOrderSql,    orderParams,    trx);
    await connection.ExecuteAsync(insertLinesSql,    linesParams,    trx);
    await connection.ExecuteAsync(updateInventorySql,inventoryParams, trx);
});

// T-returning overload
var newId = await connection.ExecuteInTransactionAsync(async trx =>
{
    await connection.ExecuteAsync(insertSql, param, trx);
    return await connection.ExecuteScalarAsync<Guid>(selectIdSql, param, trx);
});
```

### JSONB Type Handler

```csharp
// Register once at startup (Program.cs or DI setup)
NpgsqlTypeHandlerRegistrar.RegisterJsonbHandler<AddressDto>();
NpgsqlTypeHandlerRegistrar.RegisterJsonbHandler<MetadataDto>();

// Then use normally — Dapper handles serialization/deserialization
var result = await connection.QueryAsync<Customer>(
    "SELECT id, name, address FROM customers");
// customer.Address is automatically deserialized from JSONB
```

---

## NpgsqlDbType Reference

Common mappings for `BulkParameters.Add()`:

| C# Type | NpgsqlDbType |
|---|---|
| `Guid` | `NpgsqlDbType.Uuid` |
| `string` | `NpgsqlDbType.Text` |
| `int` | `NpgsqlDbType.Integer` |
| `long` | `NpgsqlDbType.Bigint` |
| `decimal` | `NpgsqlDbType.Numeric` |
| `bool` | `NpgsqlDbType.Boolean` |
| `DateTime` (UTC) | `NpgsqlDbType.TimestampTz` |
| `DateOnly` | `NpgsqlDbType.Date` |
| `object` (JSONB) | `NpgsqlDbType.Jsonb` |
| `string[]` | `NpgsqlDbType.Array \| NpgsqlDbType.Text` |

---

## Architecture Decisions

- [ADR-002: UNNEST over Multi-Value INSERT for Bulk Operations](docs/decisions/ADR-002-unnest-bulk-strategy.md)

---

## License

MIT © [Erickson López](https://github.com/ericksonlopezf)
