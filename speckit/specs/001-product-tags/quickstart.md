# Quickstart: Validate Product Tags

Run from the repo root. Details: [contracts/tags-api.md](./contracts/tags-api.md), [data-model.md](./data-model.md).

## Prerequisites

- .NET 10 SDK
- `dotnet-ef` tool available (`dotnet tool install --global dotnet-ef`, matching EF Core 10) for creating migrations
- Delete any old local `src/Catalog.Api/catalog.db` created before migrations were added (see research R1)

## Build and test

```bash
dotnet build
dotnet test
```

Expected: build succeeds; all tests in `tests/Catalog.Tests` pass, including `ProductTagTests`.

## Manual end-to-end check

```bash
dotnet run --project src/Catalog.Api
```

Then, against the printed base URL (replace `$BASE`):

1. Create two products: `POST $BASE/products` with `{ "name": "Mug", "price": 12.5 }` and `{ "name": "Pan", "price": 30 }`.
2. `POST $BASE/products/1/tags` with `{ "tag": "  Gift " }`. Expect `200`, `tags: ["gift"]`.
3. Repeat step 2. Expect `200`, still one `gift`.
4. Add `kitchen` to product 1, then `GET $BASE/products?tag=GIFT`. Expect only product 1, with `tags: ["gift","kitchen"]`.
5. `GET $BASE/products?tag=nothing-here`. Expect `200` `[]`.
6. `GET $BASE/products?tag=bad%20tag`. Expect `400`.
7. Add `a`, `b`, `c` to product 1 (5 tags now), then add `d`. Expect `400`; re-add `a`. Expect `200`.
8. `DELETE $BASE/products/1/tags/a`. Expect `204`. Delete it again. Expect `404`. Add `d`. Expect `200`.
9. `POST $BASE/products/999/tags`. Expect `404`.

## Spec coverage

Each acceptance scenario in `spec.md` (US1 to US4) and each edge case has one named test in `tests/Catalog.Tests/ProductTagTests.cs`; `tasks.md` will list the mapping.
