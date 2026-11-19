# Feature Specification: Product Tags

**Feature Branch**: `001-product-tags`

**Created**: 2026-11-20

**Status**: Draft

**Input**: User description: "Product tags. Shoppers filter the catalog by tags like kitchen or gift. A product can have at most 5 tags. A tag is 1 to 30 characters: lowercase letters, digits and hyphens only; input is trimmed and lowercased first. A tag is unique per product; adding an existing tag succeeds and changes nothing. Removing a tag the product does not have is not found. Unknown product is not found. Users can add a tag to a product, remove a tag, list products by tag, and see each product's tags (sorted) in the product list. Out of scope: renaming tags, tag descriptions, a global tag list."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tag a product (Priority: P1)

A user adds a tag such as `kitchen` or `gift` to a product so the product can be grouped and found later.

**Why this priority**: Without the ability to add tags, nothing else in this feature has data to work with. It also carries most of the business rules (format, limit, uniqueness).

**Independent Test**: Add a tag to an existing product, then view the product list and confirm the tag appears on that product.

**Acceptance Scenarios**:

1. **Given** a product with no tags, **When** the user adds `kitchen`, **Then** the add succeeds and the product has the tag `kitchen`.
2. **Given** a product with no tags, **When** the user adds `  Gift  `, **Then** the tag is trimmed and lowercased, and the product has the tag `gift`.
3. **Given** a product that already has `kitchen`, **When** the user adds `kitchen` again (in any letter case), **Then** the add succeeds and the product still has exactly one `kitchen` tag.
4. **Given** a product with 5 tags, **When** the user adds a sixth, different tag, **Then** the add is rejected with a clear error and the product still has its 5 tags.
5. **Given** a product with 5 tags, **When** the user adds one of those 5 tags again, **Then** the add succeeds and nothing changes.
6. **Given** any product, **When** the user adds a tag that is empty after trimming, longer than 30 characters, or contains characters other than lowercase letters, digits and hyphens (after trimming and lowercasing), **Then** the add is rejected with a clear error and the product is unchanged.
7. **Given** a product that does not exist, **When** the user adds a tag, **Then** the result is "not found".

---

### User Story 2 - Find products by tag (Priority: P1)

A shopper lists the catalog filtered by a tag, such as `gift`, to see only the products that carry it.

**Why this priority**: This is the reason the feature exists: shoppers filtering the catalog.

**Independent Test**: Tag two of three products with `gift`, filter by `gift`, and confirm only those two are returned.

**Acceptance Scenarios**:

1. **Given** products A and B tagged `gift` and product C without it, **When** the user lists products by `gift`, **Then** A and B are returned and C is not.
2. **Given** a valid tag that no product carries, **When** the user lists products by that tag, **Then** an empty list is returned (not an error).
3. **Given** the filter input `  GIFT `, **When** the user lists products by tag, **Then** it is trimmed and lowercased and behaves as `gift`.
4. **Given** a malformed filter tag (empty after trimming, longer than 30 characters, or with disallowed characters), **When** the user lists products by tag, **Then** the request is rejected with a clear error.

---

### User Story 3 - See tags in the product list (Priority: P2)

Everyone viewing the product list sees each product's tags, sorted alphabetically.

**Why this priority**: Makes tags visible and lets shoppers discover tags to filter by, but the feature works (add, filter) without it.

**Independent Test**: Add `zebra`, `apple` and `mango` to a product and confirm the product list shows them in the order `apple`, `mango`, `zebra`.

**Acceptance Scenarios**:

1. **Given** a product tagged `zebra`, `apple`, `mango`, **When** the user views the product list, **Then** that product shows its tags as `apple`, `mango`, `zebra`.
2. **Given** a product with no tags, **When** the user views the product list, **Then** it shows an empty tag list.
3. **Given** results of a list-by-tag request, **When** the user views them, **Then** each product also shows all of its tags (sorted), not only the filtered one.

---

### User Story 4 - Remove a tag (Priority: P2)

A user removes a tag from a product, for example after tagging it by mistake.

**Why this priority**: Needed to correct mistakes and to free a slot once the 5-tag limit is reached.

**Independent Test**: Add a tag, remove it, and confirm it no longer appears and the product no longer shows up when filtering by it.

**Acceptance Scenarios**:

1. **Given** a product tagged `kitchen`, **When** the user removes `kitchen`, **Then** the removal succeeds and the product no longer has the tag.
2. **Given** a product that does not have `sale`, **When** the user removes `sale`, **Then** the result is "not found" and the product is unchanged.
3. **Given** a product that does not exist, **When** the user removes any tag, **Then** the result is "not found".
4. **Given** a product tagged `kitchen`, **When** the user removes `  KITCHEN `, **Then** the input is trimmed and lowercased and the tag is removed.
5. **Given** a product with 5 tags, **When** the user removes one and then adds a new valid tag, **Then** the new tag is accepted.

---

### Edge Cases

- A tag that is exactly 1 character or exactly 30 characters is valid; 0 or 31 characters is not.
- A tag consisting only of hyphens (for example `-`) is valid under the stated character rule.
- Tags with spaces inside (for example `gift set`), underscores, accented letters, or other symbols are rejected.
- Input that is only whitespace is empty after trimming and is rejected.
- Length is measured after trimming and lowercasing.
- The same tag may be on many different products; uniqueness applies only within one product.
- Adding a duplicate tag to a product already at 5 tags succeeds (no change), because it does not exceed the limit.
- Removing a tag that is malformed (for example `Bad Tag!`) cannot match any stored tag, so it is "not found".
- Two products with the same tag in different letter cases of input end up with the same stored tag, since input is lowercased.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to add a tag to an existing product.
- **FR-002**: Before any add, remove or filter, the system MUST trim leading and trailing whitespace from the tag input and convert it to lowercase.
- **FR-003**: A tag MUST be 1 to 30 characters long and contain only lowercase letters, digits and hyphens, checked after trimming and lowercasing. Adding a tag that breaks this rule MUST be rejected with a clear error and MUST NOT change the product.
- **FR-004**: A product MUST NOT have more than 5 tags. Adding a new tag to a product that already has 5 MUST be rejected with a clear error and MUST NOT change the product.
- **FR-005**: A tag MUST be unique per product. Adding a tag the product already has MUST succeed and MUST change nothing (including when the product already has 5 tags).
- **FR-006**: Users MUST be able to remove a tag from a product. Removing a tag the product does not have MUST return "not found".
- **FR-007**: Any add or remove request for a product that does not exist MUST return "not found".
- **FR-008**: Users MUST be able to list products by a tag; only products carrying that tag are returned. A valid tag that no product has MUST return an empty list.
- **FR-009**: A list-by-tag request with a malformed tag MUST be rejected with a clear error.
- **FR-010**: The product list MUST show each product's tags in ascending alphabetical order, and an empty list for products without tags. This also applies to the products returned by list-by-tag.
- **FR-011**: Tags MUST be remembered between sessions; a tag added to a product stays until it is removed.

### Key Entities

- **Product**: An item in the catalog (existing). Gains a set of 0 to 5 tags.
- **Tag**: A short label attached to a product, 1 to 30 characters of lowercase letters, digits and hyphens. It has no identity or data beyond its text, and exists only as part of a product. There is no global tag list.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can tag a product and see the tag in the product list in a single action, with the change visible immediately on the next view.
- **SC-002**: 100% of products returned when filtering by a tag actually carry that tag, and 100% of products carrying it are returned.
- **SC-003**: No product ever shows more than 5 tags or a repeated tag, whatever sequence of add and remove actions is used.
- **SC-004**: Every rejected or not-found request leaves the product's tags unchanged and gives the user a clear reason.
- **SC-005**: Tags in every product list view appear in the same sorted order every time.

## Assumptions

- A product catalog with a product list already exists; this feature extends it and does not create or change products themselves.
- Everyone who can use the catalog can add and remove tags. No new roles or permissions are introduced.
- "Not found" and "rejected" are distinct outcomes: unknown product or missing tag on remove is "not found"; bad tag format or exceeding the limit is a validation error.
- Sorting is plain ascending character order (the allowed characters are only lowercase letters, digits and hyphens, so no language-specific rules apply).
- Filtering takes one tag at a time; combining several tags in one filter is not included.
- Out of scope: renaming tags, tag descriptions, and a global tag list.
