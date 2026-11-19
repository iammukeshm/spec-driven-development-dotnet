# Implementation Plan: Product Tags

**Branch**: `001-product-tags` | **Date**: 2026-11-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-product-tags/spec.md`

## Summary

Add per-product tags to the existing Catalog API. A product holds 0 to 5 tags (1 to 30 chars of `a-z`, `0-9`, `-`; trimmed and lowercased first). Three Minimal API endpoints are added to the existing `/products` group: `POST /products/{id}/tags`, `DELETE /products/{id}/tags/{tag}`, and a `?tag=` filter on `GET /products`. Tags live in a new `ProductTag` table (composite key `ProductId + Tag`) so uniqueness per product is enforced by the database. `ProductResponse` gains a sorted `Tags` list. The schema change is shipped as an EF Core migration (see research R1: the app currently uses `EnsureCreated()`, which the constitution does not allow for schema changes).

## Technical Context

**Language/Version**: C# (current for .NET 10 SDK), `net10.0`

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite` 10.0.15). One new **dev-only** package: `Microsoft.EntityFrameworkCore.Design` (needed to create migrations; justified in Complexity Tracking).

**Storage**: SQLite via EF Core 10. New table `ProductTags`.

**Testing**: xUnit v3 + `WebApplicationFactory<Program>` in `tests/Catalog.Tests`, reusing the existing `CatalogApiFactory` (own temp SQLite file per factory instance).

**Target Platform**: Any (cross-platform ASP.NET Core service)

**Project Type**: web-service (single API project + one test project)

**Performance Goals**: None stated. Catalog is small; no caching or paging added.

**Constraints**: Max 5 tags per product; tag format fixed by FR-003; errors returned as `ProblemDetails`.

**Scale/Scope**: Sample-sized catalog. Filter by tag uses an index on `ProductTags.Tag`.

No NEEDS CLARIFICATION items remain.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. .NET 10 Minimal APIs | PASS | New routes added to the existing `MapGroup("/products")`. Return `TypedResults`; errors via `ProblemDetails` (400 validation, 404 not found). |
| II. EF Core 10 for Data Access | PASS (with work) | All access via `CatalogDbContext`, no raw SQL. Existing app uses `EnsureCreated()` with no migrations, so this plan adds a baseline migration + a tags migration and switches startup to `Migrate()` (R1). |
| III. Integration Tests for Every Rule | PASS | Every FR and acceptance scenario maps to a `WebApplicationFactory` test in `ProductTagTests.cs`; tests written first (red, green, refactor). See quickstart. |
| IV. Keep It Simple | PASS | No repository, mediator or service layer. Endpoint logic stays in `Products.cs` (or a sibling endpoint file). No global tag table, no extra features. |
| V. No New Libraries Unless Needed | PASS (justified) | Only `Microsoft.EntityFrameworkCore.Design` added, `PrivateAssets=all`, to create migrations. Validation uses built-in `[GeneratedRegex]`; no FluentValidation. |

**Post-design re-check**: unchanged. All five principles still pass; the only deviation is the Design package, tracked below.

## Project Structure

### Documentation (this feature)

```text
specs/001-product-tags/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── tags-api.md      # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
src/Catalog.Api/
├── Program.cs                 # EnsureCreated() -> Migrate()
├── Products.cs                # Product gets Tags; DbContext config; ProductResponse + Tags; tag endpoints + ?tag= filter
├── Tags.cs                    # NEW: ProductTag entity, TagRules (normalize + validate), AddTagRequest
├── Migrations/                # NEW: baseline (Products) + AddProductTags
└── Catalog.Api.csproj         # + Microsoft.EntityFrameworkCore.Design (dev-only)

tests/Catalog.Tests/
├── ProductTests.cs            # existing, unchanged
└── ProductTagTests.cs         # NEW: one test per rule / acceptance scenario
```

**Structure Decision**: Keep the existing two-project layout. Existing code puts entity, DbContext, DTOs and endpoints together in `Products.cs`; tag-specific types go in a new `Tags.cs` to keep that file readable, with the three endpoints mapped from the existing `MapProductEndpoints`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New package `Microsoft.EntityFrameworkCore.Design` (dev-only) | Constitution II requires migrations; `dotnet ef migrations add` needs this package in the startup project. | `EnsureCreated()` cannot alter an existing database and is not a migration. Hand-writing migration files without the tooling is error-prone. |
