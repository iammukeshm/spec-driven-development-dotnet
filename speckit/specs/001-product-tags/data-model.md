# Data Model: Product Tags

## Entities

### Product (existing, extended)

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK (existing) |
| Name | string (max 200) | existing |
| Price | decimal(18,2) | existing |
| Tags | `List<ProductTag>` | NEW navigation, 0 to 5 items |

### ProductTag (new)

| Field | Type | Notes |
|-------|------|-------|
| ProductId | int | Part of composite PK; FK to `Products.Id`, cascade delete |
| Tag | string (max 30) | Part of composite PK; stored normalized (trimmed, lowercase) |

- **Table**: `ProductTags`
- **Key**: `(ProductId, Tag)`, so a tag is unique per product (FR-005) and the same tag can sit on many products.
- **Index**: on `Tag`, for the filter query (FR-008).
- No identity or data beyond the text; no global tag table (spec: out of scope).

## Validation rules

| Rule | Source | Enforced in |
|------|--------|-------------|
| Trim + lowercase before use | FR-002 | `TagRules.Normalize`, on add, remove and filter |
| Matches `\A[a-z0-9-]{1,30}\z` after normalizing | FR-003 | `TagRules.IsValid` (add: 400; filter: 400; remove: 404) |
| At most 5 tags per product | FR-004 | Endpoint check before insert; skipped when tag already present (FR-005) |
| Unique per product | FR-005 | Endpoint check + composite PK as backstop |
| Persisted between sessions | FR-011 | SQLite table |

## State transitions (per product)

```text
tags = {}  --add valid new tag (count < 5)-->  tags + {t}
tags       --add tag already present------->  unchanged (200)
tags (5)   --add new tag------------------->  rejected (400), unchanged
tags       --remove tag present----------->  tags - {t} (204)
tags       --remove tag absent / malformed->  unchanged (404)
```

## Response shape change

`ProductResponse(int Id, string Name, decimal Price, IReadOnlyList<string> Tags)`: `Tags` is sorted with ordinal comparison, empty when none (FR-010).

## Migrations

1. `InitialCreate`: baseline for the existing `Products` table.
2. `AddProductTags`: creates `ProductTags` with the composite key and `Tag` index.
