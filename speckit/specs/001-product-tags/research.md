# Research: Product Tags

All Technical Context unknowns were resolved from the repo and the user's plan input. No external research was needed.

## R1: Schema management (EnsureCreated vs migrations)

- **Finding**: `Program.cs` calls `Database.EnsureCreated()` and there is no `Migrations/` folder. `EnsureCreated()` only creates a database that does not exist; it will not add the `ProductTags` table to an existing `catalog.db`. Constitution II says schema changes MUST use migrations.
- **Decision**: Add a baseline migration for the current `Products` schema and a second migration `AddProductTags`. Replace `EnsureCreated()` with `Database.Migrate()` at startup.
- **Rationale**: Meets the constitution; works on fresh and on test databases (each test factory uses a new temp file, so `Migrate()` runs both migrations).
- **Risk**: A developer's existing local `catalog.db` made by `EnsureCreated()` has no migration history, so `Migrate()` fails with "table Products already exists". Fix: delete the local `catalog.db` (sample data only). Documented in quickstart.
- **Alternatives considered**: Keep `EnsureCreated()` (violates II, breaks existing DBs silently); one combined migration (loses the clean baseline).

## R2: Storing tags

- **Decision**: Separate `ProductTag` entity, table `ProductTags`, composite primary key `(ProductId, Tag)`, FK to `Products` with cascade delete, index on `Tag`.
- **Rationale**: The composite key enforces "unique per product" in the database. The `Tag` index serves the filter. Plain relational query, works the same on every provider.
- **Alternatives considered**:
  - JSON/primitive-collection column on `Product`: fewer tables, but filtering by element depends on provider JSON translation and gives no uniqueness guarantee.
  - Comma-separated string: cannot be queried or constrained cleanly.
  - Global `Tag` table with many-to-many: out of scope (spec says no global tag list).

## R3: Normalization and validation

- **Decision**: One static `TagRules` helper: `Normalize(string?)` = `Trim().ToLowerInvariant()`, and `IsValid` using a source-generated regex `\A[a-z0-9-]{1,30}\z`. Used by add, remove and filter, so rules live in one place (FR-002, FR-003).
- **Rationale**: Built-in `[GeneratedRegex]`, no new package. `\A...\z` avoids the `$` trailing-newline quirk. Length is checked after normalization, as the spec requires.
- **Alternatives considered**: Data annotations or FluentValidation (extra moving parts, and FluentValidation is a new package).

## R4: HTTP status mapping

- **Decision** (matches the spec's "not found" vs "rejected" split):

  | Case | Result |
  |------|--------|
  | Add: valid, new or already present | `200 OK` with updated `ProductResponse` |
  | Add: bad format, or new tag when product has 5 | `400` validation `ProblemDetails` |
  | Add / remove: unknown product | `404` `ProblemDetails` |
  | Remove: success | `204 No Content` |
  | Remove: product lacks tag (including malformed tag) | `404` `ProblemDetails` |
  | List: no `tag` param | all products |
  | List: valid tag, no matches | `200` with `[]` |
  | List: malformed tag (including `?tag=` empty) | `400` validation `ProblemDetails` |

- **Rationale**: Same `Results.ValidationProblem` style the existing `POST /products` uses. `200` for add (not `201`) because an add can be a no-op and the caller does not need to know which. `ProblemDetails` bodies for 404 follow constitution I (`TypedResults.NotFound()` has no body, so use `TypedResults.Problem(statusCode: 404, ...)`).
- **Alternatives considered**: `409` for the 5-tag limit (spec calls it a validation error, so 400); `201`/`200` split on add (leaks a detail the spec says should not matter); `204` on add.

## R5: Sorted tags in responses

- **Decision**: Load each product's tags and sort in memory with `StringComparer.Ordinal`, then build `ProductResponse(Id, Name, Price, Tags)`. `ProductResponse` gets a new last parameter `IReadOnlyList<string> Tags`. `POST /products` returns an empty list.
- **Rationale**: Ordinal order is exactly the spec's "plain ascending character order" and does not depend on SQLite collation. Catalog is small; in-memory sort is simplest. Existing test only reads `Name`, so the added field does not break it.
- **Alternatives considered**: `ORDER BY` in a translated subquery (relies on SQLite BINARY collation matching ordinal; harder to read).

## R6: Concurrency

- **Decision**: Do the 5-tag count check and insert in one `SaveChanges` call; rely on the composite key as the final guard against duplicate inserts. No extra locking.
- **Rationale**: SQLite serializes writers. Two concurrent adds of the same new tag: the loser gets a key violation, which the endpoint can treat as "already present" (success). Two concurrent adds of different tags to a product with 4 tags could exceed 5 in theory; accepted for a sample (YAGNI, constitution IV) and noted as a known limit.
- **Alternatives considered**: Explicit transactions or a trigger/check constraint (more code than this feature warrants).

## R7: Test isolation

- **Decision**: Reuse `CatalogApiFactory` (temp SQLite file per factory instance) in a new `ProductTagTests : IClassFixture<CatalogApiFactory>`. Each test creates its own product via `POST /products`, so tests do not depend on each other. Use real SQLite (already the case), since the 5-tag and uniqueness rules touch the database.
- **Alternatives considered**: In-memory provider (does not enforce the composite key the same way).
