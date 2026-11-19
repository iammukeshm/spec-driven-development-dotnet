# Tasks: Product Tags

**Input**: Design documents from `/specs/001-product-tags/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/tags-api.md, quickstart.md

**Tests**: INCLUDED. Constitution III requires an integration test for every rule, written first (red, green, refactor). All tests live in `tests/Catalog.Tests/ProductTagTests.cs` and reuse `CatalogApiFactory`. Each test creates its own product via `POST /products`.

**Organization**: Grouped by user story. Test tasks in one story share one file, so they are not marked [P].

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1 to US4, mapped to spec.md user stories
- Paths are relative to the repo root

---

## Phase 1: Setup

**Purpose**: Tooling needed to create migrations

- [X] T001 Add `Microsoft.EntityFrameworkCore.Design` (same 10.0.x line as the existing EF Core packages, `PrivateAssets=all`) to `src/Catalog.Api/Catalog.Api.csproj`; confirm `dotnet build` passes and `dotnet ef` is available (install the `dotnet-ef` tool if missing)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema, entity, rules and response shape that every story needs

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Generate the baseline migration `InitialCreate` for the CURRENT `Products` schema (before any model change) into `src/Catalog.Api/Migrations/` with `dotnet ef migrations add InitialCreate --project src/Catalog.Api` (research R1)
- [X] T003 [P] Create `src/Catalog.Api/Tags.cs` with: `ProductTag` entity (`ProductId`, `Tag`; fields per data-model.md `ProductTag.ProductId` and `ProductTag.Tag`), static `TagRules` (`Normalize(string?)` = trim + `ToLowerInvariant`, `IsValid(string)` via `[GeneratedRegex]` implementing the FR-003 pattern from data-model.md "Validation rules"), and `AddTagRequest(string? Tag)` record
- [X] T004 Extend `src/Catalog.Api/Products.cs`: add `Tags` navigation (`List<ProductTag>`, data-model.md `Product.Tags`) to `Product`; in `CatalogDbContext` configure `ProductTags` table, composite key `(ProductId, Tag)`, `Tag` max length per data-model.md `ProductTag.Tag`, cascade-delete FK to `Products`, and an index on `Tag` (depends on T003)
- [X] T005 Update `ProductResponse` in `src/Catalog.Api/Products.cs` to `(int Id, string Name, decimal Price, IReadOnlyList<string> Tags)` with a mapping helper that sorts tags using `StringComparer.Ordinal`; update the existing list/create endpoints to use it (`POST /products` returns `"tags": []`); keep existing `tests/Catalog.Tests/ProductTests.cs` passing (depends on T004)
- [X] T006 Generate migration `AddProductTags` into `src/Catalog.Api/Migrations/` with `dotnet ef migrations add AddProductTags --project src/Catalog.Api`; verify it creates `ProductTags` with the composite key and the `Tag` index (depends on T004)
- [X] T007 Replace `Database.EnsureCreated()` with `Database.Migrate()` in `src/Catalog.Api/Program.cs`; run `dotnet test` to confirm existing tests pass on a fresh temp DB (depends on T002, T006)
- [X] T008 Create `tests/Catalog.Tests/ProductTagTests.cs` skeleton: `ProductTagTests : IClassFixture<CatalogApiFactory>` with helpers `CreateProductAsync`, `AddTagAsync(productId, tag)`, `GetProductsAsync(tag?)` (depends on T005)

**Checkpoint**: Schema migrates, response shape carries `Tags`, test scaffold ready

---

## Phase 3: User Story 1 - Tag a product (Priority: P1) 🎯 MVP

**Goal**: `POST /products/{id}/tags` with normalization, format check, 5-tag limit, idempotent duplicates, 404 for unknown product (FR-001 to FR-005, FR-007, FR-011)

**Independent Test**: Add a tag to an existing product, list products, confirm the tag appears on that product.

### Tests for User Story 1 (write FIRST, confirm they FAIL)

- [X] T009 [US1] In `tests/Catalog.Tests/ProductTagTests.cs` add tests for scenarios 1 and 2: add `kitchen` returns 200 with that tag; add `  Gift  ` stores `gift`
- [X] T010 [US1] In `tests/Catalog.Tests/ProductTagTests.cs` add tests for scenarios 3 and 5: re-adding an existing tag (also in different case) returns 200 with exactly one copy; re-adding at 5 tags returns 200, unchanged
- [X] T011 [US1] In `tests/Catalog.Tests/ProductTagTests.cs` add test for scenario 4: sixth distinct tag returns 400 (`errors.tag`) and the product still has 5 tags
- [X] T012 [US1] In `tests/Catalog.Tests/ProductTagTests.cs` add theory for scenario 6 and edge cases: empty, whitespace-only, 31 chars, `gift set`, `under_score`, `café`, `Bad!` each return 400 and leave product unchanged; 1-char, 30-char and `-` return 200
- [X] T013 [US1] In `tests/Catalog.Tests/ProductTagTests.cs` add tests for scenario 7 and check order: unknown product with a valid tag returns 404 `ProblemDetails`; unknown product with a bad tag also returns 404 (contract "order of checks")
- [X] T014 [US1] In `tests/Catalog.Tests/ProductTagTests.cs` add test for FR-011 and uniqueness scope: tags persist across requests, and the same tag can be added to two different products

### Implementation for User Story 1

- [X] T015 [US1] Implement `POST /products/{id}/tags` in `src/Catalog.Api/Products.cs` (mapped from `MapProductEndpoints`), checks in the order from contracts/tags-api.md: product exists (404 via `TypedResults.Problem(statusCode: 404, ...)`), normalize + `TagRules.IsValid` (400 via `Results.ValidationProblem` with `errors.tag`), already present (200, no change), count of 5 (400), else insert and `SaveChanges`; return 200 `ProductResponse`
- [X] T016 [US1] In the same endpoint in `src/Catalog.Api/Products.cs`, catch the composite-key violation from a concurrent duplicate insert and treat it as "already present" (200), per research R6
- [X] T017 [US1] Run `dotnet test`; T009 to T014 must pass

**Checkpoint**: Tags can be added and appear in the product list (MVP)

---

## Phase 4: User Story 2 - Find products by tag (Priority: P1)

**Goal**: `GET /products?tag=` filters the list (FR-008, FR-009, FR-002 on filter)

**Independent Test**: Tag two of three products with `gift`, filter by `gift`, only those two are returned.

### Tests for User Story 2 (write FIRST, confirm they FAIL)

- [X] T018 [US2] In `tests/Catalog.Tests/ProductTagTests.cs` add test for scenario 1: products A, B tagged `gift`, C untagged; `?tag=gift` returns A and B only, ordered by `Id`
- [X] T019 [US2] In `tests/Catalog.Tests/ProductTagTests.cs` add tests for scenarios 2 and 3: valid unused tag returns 200 `[]`; `?tag=%20%20GIFT%20` behaves as `gift`; no `tag` param still returns all products
- [X] T020 [US2] In `tests/Catalog.Tests/ProductTagTests.cs` add theory for scenario 4: `?tag=` (empty), whitespace-only, 31 chars, `bad tag`, `Bad!` each return 400 with `errors.tag`

### Implementation for User Story 2

- [X] T021 [US2] Add optional `tag` query parameter to `GET /products` in `src/Catalog.Api/Products.cs`: when present, normalize, return 400 `ValidationProblem` (`errors.tag`) if invalid (including empty after trim), otherwise filter with `Any(t => t.Tag == tag)`; keep ordering by `Id`; use the T005 mapping helper
- [X] T022 [US2] Run `dotnet test`; T018 to T020 must pass

**Checkpoint**: US1 and US2 both work (core feature complete)

---

## Phase 5: User Story 3 - See tags in the product list (Priority: P2)

**Goal**: Every product in every list view shows its tags sorted ordinally, `[]` when none (FR-010)

**Independent Test**: Add `zebra`, `apple`, `mango`; list shows `apple`, `mango`, `zebra`.

### Tests for User Story 3 (write FIRST)

- [X] T023 [US3] In `tests/Catalog.Tests/ProductTagTests.cs` add tests for scenarios 1 and 2: tags added as `zebra`, `apple`, `mango` come back `apple`, `mango`, `zebra`; untagged product shows `[]`; include a digit/hyphen case (`1-a`, `a-b`, `a1`) to pin ordinal order
- [X] T024 [US3] In `tests/Catalog.Tests/ProductTagTests.cs` add test for scenario 3: a product with `gift` and `kitchen` returned by `?tag=gift` shows both tags, sorted

### Implementation for User Story 3

- [X] T025 [US3] Confirm the list and filter endpoints in `src/Catalog.Api/Products.cs` load tags (e.g. `Include` or projection) and use the T005 ordinal-sorting helper on both paths; fix any gap, then run `dotnet test` so T023 and T024 pass

**Checkpoint**: Tags visible and stably sorted everywhere

---

## Phase 6: User Story 4 - Remove a tag (Priority: P2)

**Goal**: `DELETE /products/{id}/tags/{tag}` (FR-006, FR-007)

**Independent Test**: Add a tag, remove it, confirm it is gone and the product no longer appears under that filter.

### Tests for User Story 4 (write FIRST, confirm they FAIL)

- [X] T026 [US4] In `tests/Catalog.Tests/ProductTagTests.cs` add tests for scenarios 1 and 4: removing `kitchen` returns 204 and the tag is gone from the list and from `?tag=kitchen`; removing `  KITCHEN ` (URL-encoded) also works
- [X] T027 [US4] In `tests/Catalog.Tests/ProductTagTests.cs` add tests for scenarios 2 and 3 and the malformed edge case: absent tag returns 404 and product unchanged; unknown product returns 404; malformed tag (`Bad Tag!`) returns 404
- [X] T028 [US4] In `tests/Catalog.Tests/ProductTagTests.cs` add test for scenario 5: at 5 tags, remove one, then add a new valid tag returns 200

### Implementation for User Story 4

- [X] T029 [US4] Implement `DELETE /products/{id}/tags/{tag}` in `src/Catalog.Api/Products.cs`: normalize the route value; 404 `ProblemDetails` if product missing, tag malformed, or tag absent; otherwise remove the row, `SaveChanges`, return 204
- [X] T030 [US4] Run `dotnet test`; T026 to T028 must pass

**Checkpoint**: All four stories work independently

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T031 [P] Add a test in `tests/Catalog.Tests/ProductTagTests.cs` that deleting a product removes its `ProductTags` rows (cascade), if a product delete endpoint exists; otherwise skip and note it (SKIPPED: no product delete endpoint exists)
- [ ] T032 Walk through `specs/001-product-tags/quickstart.md` manually against the running API (delete any local `catalog.db` first, per research R1) and fix any mismatch
- [X] T033 Run the full `dotnet build` and `dotnet test` with no warnings introduced; confirm no new packages beyond `Microsoft.EntityFrameworkCore.Design` (constitution V)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → all user stories → **Polish (7)**
- Inside Phase 2: T002 MUST run before T004 (baseline must capture the schema without tags). T003 → T004 → T005/T006 → T007 → T008.

### User Story Dependencies

- **US1 (P1)**: after Phase 2. No story dependencies.
- **US2 (P1)**: after Phase 2. Tests seed data through the add endpoint, so US1's T015 should land first (or seed via `DbContext` in tests).
- **US3 (P2)**: after Phase 2; most useful after US1 (needs tags to exist).
- **US4 (P2)**: after Phase 2; tests use add endpoint, so after US1.

Recommended order: US1 → US2 → US3 → US4.

### Within Each Story

Tests first and failing, then implementation, then run tests (red, green, refactor).

### Parallel Opportunities

- T003 (`Tags.cs`) is the only clear [P] task: it touches a different file than T002.
- Almost everything else edits `Products.cs` or `ProductTagTests.cs`, so work is sequential by design. With two developers, one can write US2 tests while the other implements US1, since they only share the test file in different regions (coordinate on merges).

```text
# Parallel example (Phase 2 start):
Task: "T002 Generate baseline migration InitialCreate"
Task: "T003 Create Tags.cs with ProductTag, TagRules, AddTagRequest"
```

---

## Implementation Strategy

### MVP First

1. Phase 1 and Phase 2 (including both migrations)
2. Phase 3: US1 (add a tag)
3. **Stop and validate**: add a tag, see it in the list
4. Add US2 for the real shopper value (filtering); US1 + US2 is the useful minimum, as both are P1

### Incremental Delivery

US1 → US2 → US3 → US4, running `dotnet test` after each story.

---

## Notes

- Known accepted limit (research R6): two concurrent adds of different tags at 4 tags could exceed 5. Not covered by tasks (Constitution IV).
- Do not commit until the user asks.
