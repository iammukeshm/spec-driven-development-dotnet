# Spec-Driven Development in .NET: Spec Kit vs a one-page spec

Companion code for the article [Spec-Driven Development in .NET](https://codewithmukesh.com/blog/spec-driven-development-dotnet/).

One small feature (product tags on a .NET 10 Minimal API with EF Core 10 and SQLite), built three ways by Claude Code (Sonnet) in headless mode:

| Folder | How it was built |
|---|---|
| `baseline/` | The starting API, plus the one-page spec in `docs/specs/product-tags.md` |
| `onepage/` | One prompt: "Implement the feature in docs/specs/product-tags.md" |
| `speckit/` | GitHub Spec Kit 1.4.3: constitution, specify, plan, tasks, implement |
| `onepage-v2/` | The one-page spec plus one rule about existing databases (28 more words) |
| `acceptance/` | 15 acceptance tests written by hand from the spec, run against every build |
| `runs/` | Raw `claude -p --output-format json` results and the git history of each run |

## Results

| | One-page | Spec Kit | One-page v2 |
|---|---|---|---|
| Spec written by hand | 241 words | prompts only | 269 words |
| Docs generated | 0 | 706 lines, 6,491 words | 0 |
| Agent time | 94 s | 371 s (5 steps) | 85 s |
| Cost (Sonnet) | $0.42 | $1.86 | $0.38 |
| Agent turns | 19 | 97 | 12 |
| Agent's own tests | 18/18 | 38/38 | 21/21 |
| Independent acceptance tests | 15/15 | 15/15 | 15/15 |
| Existing database from the old version | 500: no such table | crash on startup: table "Products" already exists | works, data kept |

Spec Kit's first implement attempt was blocked by my tool allowlist ($0.32, no code written). It is kept in `runs/speckit-implement-blocked.json` and is not counted above.

## Run it

```bash
cd onepage   # or speckit, onepage-v2
dotnet test
```

Requires the .NET 10 SDK.
