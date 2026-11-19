<!--
Sync Impact Report
- Version change: (template, unversioned) → 1.0.0
- Modified principles: none (initial adoption; all five principles are new)
- Added sections: Core Principles (I–V), Technology Constraints, Development Workflow, Governance
- Removed sections: none
- Follow-up TODOs: none
-->
# Speckit .NET Sample Constitution

## Core Principles

### I. .NET 10 Minimal APIs
All HTTP endpoints MUST be built with ASP.NET Core Minimal APIs on .NET 10. Controllers and
MVC-style endpoint stacks MUST NOT be used. Endpoints SHOULD be grouped with `MapGroup` and return
`TypedResults`. Errors MUST be returned as `ProblemDetails`.
Rationale: one consistent, low-ceremony endpoint style keeps the sample small and readable.

### II. EF Core 10 for Data Access
All persistence MUST go through EF Core 10 via a `DbContext`. Schema changes MUST be made with EF
Core migrations. Raw SQL MUST NOT be used unless EF Core cannot express the query, and the reason
MUST be recorded in the plan.
Rationale: a single data access path keeps behavior predictable and testable.

### III. Integration Tests for Every Rule (NON-NEGOTIABLE)
Every business rule and every endpoint behavior in a spec MUST be covered by at least one
integration test using xUnit v3 and `WebApplicationFactory`. Tests MUST be written before the
implementation and MUST fail first (red, green, refactor). A rule without a test is not done.
Rationale: tests running through the real HTTP pipeline prove the rules, not just the code.

### IV. Keep It Simple
Start with the simplest design that meets the spec. Abstractions (repositories, mediators,
generic layers) MUST NOT be added without a concrete need stated in the plan. Features not in the
spec MUST NOT be built (YAGNI).
Rationale: spec-driven work stays reviewable only when the code stays small.

### V. No New Libraries Unless Needed
The built-in .NET 10 and EF Core 10 features MUST be preferred. A new NuGet package MUST NOT be
added unless the plan states what need it covers and why the built-in option is not enough.
Test-only packages (xUnit v3, `Microsoft.AspNetCore.Mvc.Testing`) are pre-approved.
Rationale: fewer dependencies mean less upgrade and security burden.

## Technology Constraints

- Runtime and language: .NET 10, current C# version for that SDK.
- Web: ASP.NET Core Minimal APIs. API docs: built-in OpenAPI support.
- Data: EF Core 10. Tests MAY use SQLite in-memory or the EF Core in-memory provider unless a rule
  depends on provider-specific behavior; then the real provider MUST be used.
- Testing: xUnit v3 with `WebApplicationFactory<Program>`. Each test MUST be isolated (own
  database per test run or class).

## Development Workflow

- Work follows Spec Kit order: specify, plan, tasks, implement.
- Each spec rule MUST map to a task that names its integration test.
- `dotnet build` and `dotnet test` MUST pass before a change is considered complete.
- Every plan MUST include a Constitution Check against the principles above.

## Governance

This constitution supersedes other practices in this repository. Amendments MUST be made through
`/speckit-constitution`, with the reason recorded in the commit message.

Versioning follows semantic versioning: MAJOR for removed or redefined principles, MINOR for new
principles or materially expanded guidance, PATCH for wording clarifications.

Compliance: every plan MUST pass the Constitution Check, and reviews MUST verify that each rule has
an integration test. Any deviation MUST be justified in the plan's complexity section.

**Version**: 1.0.0 | **Ratified**: 2026-11-20 | **Last Amended**: 2026-11-20
