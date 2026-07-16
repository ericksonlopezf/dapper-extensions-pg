# Contributing to EricksonLopez.DapperExtensions.PostgreSQL

Thank you for your interest in contributing! This document provides guidelines for contributing to this project.

## Getting Started

1. **Fork** the repository
2. **Clone** your fork locally
3. Create a **feature branch** from `develop`: `git checkout -b feature/your-feature develop`
4. Make your changes
5. **Run tests** to ensure nothing is broken
6. **Commit** with a clear message
7. **Push** to your fork and create a **Pull Request** against `develop`

## Development Setup

```bash
# Clone (with sibling shared-kernel for local development)
git clone https://github.com/<your-username>/dapper-extensions-pg.git
git clone https://github.com/<your-username>/dotnet-shared-kernel.git
cd dapper-extensions-pg

# Restore and build
dotnet restore
dotnet build --configuration Release

# Run unit tests only
dotnet test --configuration Release --filter "Category!=Integration"

# Run integration tests (requires Docker)
dotnet test --configuration Release --filter "Category=Integration"

# Run benchmarks
dotnet run --project benchmarks/EricksonLopez.DapperExtensions.PostgreSQL.Benchmarks --configuration Release
```

> **Note:** The project auto-detects `dotnet-shared-kernel` as a sibling directory. If it exists locally, it uses `ProjectReference`; otherwise, it falls back to the NuGet package.

## Code Standards

This project enforces strict code quality. Your PR must pass all of these:

- **TreatWarningsAsErrors** — zero warnings allowed
- **Nullable enabled** — all reference types must be annotated
- **WarningLevel 5** — maximum warning sensitivity
- **EnforceCodeStyleInBuild** — code style rules are build errors
- **EditorConfig** — formatting rules in `.editorconfig`

## Pull Request Guidelines

- **One concern per PR** — keep changes focused
- **Include tests** — unit tests for logic, integration tests for database operations
- **Update CHANGELOG.md** — add your changes under `[Unreleased]`
- **Follow SemVer** — breaking changes require a major version bump discussion
- **Tag integration tests** — use `[Trait("Category", "Integration")]`

## What to Contribute

- 🐛 Bug fixes with a failing test that proves the fix
- 📖 Documentation improvements
- ⚡ Performance improvements (with BenchmarkDotNet evidence)
- ✨ New PostgreSQL-specific extensions (open an issue first to discuss)

## What NOT to Contribute

- ❌ Support for databases other than PostgreSQL
- ❌ Breaking API changes without prior discussion
- ❌ Generated files or build artifacts

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
