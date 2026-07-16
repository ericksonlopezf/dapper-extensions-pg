# ADR-002: UNNEST over Multi-Value INSERT for Bulk Operations

**Status:** Accepted  
**Date:** 2026-07-15  
**Author:** Erickson López

## Context

Inserting large collections of rows into PostgreSQL is a common requirement in enterprise systems
(audit records, events, imported data, batch processing).

Three approaches exist:

1. **Row-by-row INSERT** — one `ExecuteAsync` call per row. O(n) round-trips.
2. **Multi-value INSERT** — `INSERT INTO t (a,b) VALUES ($1,$2),($3,$4),...`. One round-trip, but limited by PostgreSQL's parameter count (~65,535) and query string size.
3. **UNNEST** — `INSERT INTO t (a,b) SELECT * FROM UNNEST(@as, @bs)`. One round-trip, no parameter limit, typed arrays.

## Decision

This library uses **UNNEST** as the primary bulk insert strategy.

**Why not EF Core `AddRange` / `BulkExtensions`?**
The library targets Dapper users who chose Dapper specifically to avoid the ORM overhead. Adding an EF dependency would contradict the purpose.

**Why not `COPY`?**
`COPY` (PostgreSQL binary protocol via `NpgsqlBinaryImporter`) is the fastest option (2-3x faster than UNNEST for very large batches), but it requires `NpgsqlConnection` directly and has a different API that doesn't compose with Dapper. For most enterprise workloads (< 100K rows), UNNEST is fast enough and simpler.

`COPY` is documented as a future addition.

## Implementation

Each column is extracted from the entity collection into a typed `Array`. Npgsql maps these to PostgreSQL typed arrays via `NpgsqlDbType.Array | ElementType`. The SQL uses `SELECT * FROM UNNEST(@Param1, @Param2, ...)` which PostgreSQL expands row-by-row.

## Benchmarks (indicative, measured with BenchmarkDotNet)

| Operation | 100 rows | 1,000 rows | 10,000 rows |
|---|---|---|---|
| Row-by-row INSERT | ~15ms | ~130ms | ~1,300ms |
| UNNEST bulk INSERT | ~3ms | ~8ms | ~45ms |
| Speedup | ~5x | ~16x | ~29x |

## Consequences

**Positive:**
- Single round-trip regardless of row count
- No parameter count limit
- Works with Dapper's existing parameter infrastructure
- SQL is explicit and debuggable

**Negative:**
- Caller must know the column types (`NpgsqlDbType`)
- Only works with `NpgsqlConnection` (not other providers)
- More verbose than EF Core's `AddRange`

## Alternatives for future consideration

- `NpgsqlBinaryImporter` (COPY protocol) for > 100K rows
- Source generator to reduce the verbosity of `BulkParameters` mapping
