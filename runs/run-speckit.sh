#!/bin/sh
# Runs the full Spec Kit flow headless with Claude Code (Sonnet) and saves each step's JSON result.
cd "$(dirname "$0")/speckit" || exit 1
TOOLS='Read Edit Write Glob Grep Skill Bash(dotnet build:*) Bash(dotnet test:*) Bash(git:*) Bash(bash .specify/*) Bash(.specify/*) Bash(sh .specify/*) Bash(python3 .specify/*) Bash(python .specify/*)'
run() {
  step=$1; shift
  echo "== $step $(date +%T)"
  # shellcheck disable=SC2086
  claude -p "$*" --model sonnet --output-format json --permission-mode acceptEdits --allowedTools $TOOLS > "../speckit-$step.json" 2> "../speckit-$step.err"
  git add -A > /dev/null 2>&1 && git commit -qm "speckit: $step" > /dev/null 2>&1
}
run constitution "/speckit-constitution Principles: .NET 10 Minimal APIs, EF Core 10, integration tests with xUnit v3 and WebApplicationFactory for every rule, keep it simple, no new libraries unless needed."
run specify "/speckit-specify Product tags. Shoppers filter the catalog by tags like kitchen or gift. A product can have at most 5 tags. A tag is 1 to 30 characters: lowercase letters, digits and hyphens only; input is trimmed and lowercased first. A tag is unique per product; adding an existing tag succeeds and changes nothing. Removing a tag the product does not have is not found. Unknown product is not found. Users can add a tag to a product, remove a tag, list products by tag, and see each product's tags (sorted) in the product list. Out of scope: renaming tags, tag descriptions, a global tag list."
run plan "/speckit-plan Use the existing ASP.NET Core Minimal API in src/Catalog.Api with EF Core 10 and SQLite. Endpoints: POST /products/{id}/tags with body { tag }, DELETE /products/{id}/tags/{tag}, GET /products?tag=. Tests in tests/Catalog.Tests with the existing WebApplicationFactory setup."
run tasks "/speckit-tasks"
run implement "/speckit-implement Run dotnet test at the end and make sure it passes."
echo "== done $(date +%T)"
