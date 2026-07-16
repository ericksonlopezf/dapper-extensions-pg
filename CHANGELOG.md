# Changelog

All notable changes to this project will be documented in this file.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) · Versioning: [SemVer](https://semver.org/)

## [Unreleased]

## [1.0.0] — 2026-10-01

### Added
- `BulkParameters<T>` — fluent builder for PostgreSQL UNNEST array parameters
- `BulkExtensions.BulkInsertAsync` — single round-trip bulk INSERT via UNNEST
- `BulkExtensions.BulkUpsertAsync` — bulk INSERT ... ON CONFLICT DO UPDATE via UNNEST
- `PagedQueryExtensions.QueryPagedAsync` — paginated query with parallel count, returns `PagedList<T>`
- `PagedQueryExtensions.QueryPagedMultipleAsync` — paginated query in a single round-trip via QueryMultiple
- `TransactionExtensions.ExecuteInTransactionAsync` — void and T-returning overloads with auto commit/rollback
- `JsonbTypeHandler<T>` — Dapper type handler for JSONB columns using System.Text.Json
- `NpgsqlTypeHandlerRegistrar` — startup helper for registering JSONB handlers
- Unit tests (8) — BulkParameters builder, guard clauses, array extraction
- Integration tests (5) — BulkInsert 100 rows, BulkUpsert ON CONFLICT, paginated query, transaction commit/rollback
- BenchmarkDotNet benchmarks — row-by-row vs UNNEST at 100/1K/10K rows
- ADR-002: UNNEST bulk strategy decision with benchmark data
- GitHub Actions: ci.yml (unit + integration jobs) + publish.yml (NuGet on tag)

[Unreleased]: https://github.com/ericksonlopezf/dapper-extensions-pg/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/ericksonlopezf/dapper-extensions-pg/releases/tag/v1.0.0
