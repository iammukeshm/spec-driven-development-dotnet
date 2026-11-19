# Contract: Product Tags API

Base path: `/products`. All errors are `application/problem+json` (`ProblemDetails`). Tag input is always trimmed and lowercased first; rules in [data-model.md](../data-model.md).

## POST /products/{id}/tags

Add a tag to a product.

**Request body**

```json
{ "tag": "kitchen" }
```

| Status | When | Body |
|--------|------|------|
| 200 | Tag added, or product already had it (no change, also at 5 tags) | `ProductResponse` |
| 400 | Tag missing/empty/over 30 chars/bad characters, or product has 5 tags and tag is new | validation `ProblemDetails` (`errors.tag`) |
| 404 | Product `{id}` does not exist | `ProblemDetails` |

Order of checks: product exists (404), then format (400), then already-present (200), then limit (400). A missing product wins over a bad tag.

## DELETE /products/{id}/tags/{tag}

Remove a tag from a product. `{tag}` is normalized; URL-decoded value is trimmed and lowercased.

| Status | When | Body |
|--------|------|------|
| 204 | Tag removed | none |
| 404 | Product does not exist, product lacks the tag, or `{tag}` is malformed | `ProblemDetails` |

## GET /products?tag={tag}

List products, optionally filtered by one tag.

| Status | When | Body |
|--------|------|------|
| 200 | No `tag` param: all products | `ProductResponse[]` |
| 200 | Valid `tag`: products carrying it (possibly `[]`) | `ProductResponse[]` |
| 400 | `tag` present but malformed or empty after trimming | validation `ProblemDetails` (`errors.tag`) |

Each returned product includes all of its tags, not only the filtered one. Results stay ordered by `Id`.

## Shared type

```json
ProductResponse {
  "id": 1,
  "name": "Mug",
  "price": 12.50,
  "tags": ["apple", "mango", "zebra"]   // sorted ordinal, [] when none
}
```

`GET /products` (no filter) and `POST /products` also return this shape; `POST /products` always returns `"tags": []`.
