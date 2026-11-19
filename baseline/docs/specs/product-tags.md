# Feature: Product tags

## Why
Shoppers filter the catalog by tags like "kitchen" or "gift". Today products have no tags.

## Rules
1. A product can have at most 5 tags.
2. A tag is 1 to 30 characters: lowercase letters, digits and hyphens only. Input is trimmed and lowercased before checking.
3. A tag is unique per product. Adding a tag the product already has returns 200 and changes nothing.
4. Removing a tag the product does not have returns 404.
5. Unknown product id returns 404.

## Endpoints
- `POST /products/{id}/tags` with `{ "tag": "Kitchen" }` -> 200 with the product and its tags, 400 on invalid tag or 6th tag.
- `DELETE /products/{id}/tags/{tag}` -> 204.
- `GET /products?tag=kitchen` -> products that have the tag. Without `tag`, all products (current behavior).
- `GET /products` responses include a `tags` array (sorted).

## Examples
| Existing tags | Request | Result |
|---|---|---|
| none | add " Kitchen " | 200, tags = [kitchen] |
| [kitchen] | add "kitchen" | 200, tags = [kitchen] |
| 5 tags | add "gift" | 400 |
| none | add "gift wrap" | 400 (space) |
| [gift] | delete "kitchen" | 404 |

## Out of scope
Tag renaming, tag descriptions, a global tag list endpoint.

## Done when
- Integration tests in tests/Catalog.Tests cover every rule and example above, using the existing WebApplicationFactory setup.
- `dotnet test` passes.
