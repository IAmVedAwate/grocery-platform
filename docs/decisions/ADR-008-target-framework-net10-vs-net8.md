# ADR-008: Target Framework — .NET 10 vs. .NET 8

**Status:** Accepted — Phase 1, decided at scaffolding time

## Problem

The PRD and earlier ADRs were originally written targeting .NET 8, matching the reference target-skill profile and the fact that .NET 8 is a widely-deployed enterprise LTS. At the point of scaffolding the actual solution (September 2026), the development machine has only the .NET 10 SDK and runtime installed, and .NET 8 reaches end-of-support around November 2026 — roughly two months away. A concrete target framework must be chosen before any project file is created.

## Options Considered

1. **.NET 8** — matches the original plan and the reference resume exactly; requires installing an additional SDK/runtime side-by-side with what's already present; would be within ~2 months of end-of-support by the time this project is actively used in interviews.
2. **.NET 10** — the current LTS (released Nov 2025, supported to ~Nov 2028), already installed, no extra setup required.

## Decision

**.NET 10.**

## Reasoning

- Building a new project, in September 2026, on a framework that goes out of support two months later is a poor engineering choice regardless of what's already documented — shipping on the current LTS is the professionally defensible default, and explaining *why* is itself a reasonable interview answer ("I target the current LTS unless a specific constraint says otherwise").
- No functional requirement in the PRD depends on a .NET 8-specific API; the language/runtime feature set relevant to this project (minimal APIs, EF Core, ASP.NET Core middleware, JWT handling) is present and current in .NET 10.
- Avoids installing and maintaining a second SDK/runtime side-by-side purely to match an earlier planning assumption.

## Trade-offs

- **Given up:** an exact keyword match to ".NET 8" on a resume/reference profile some job postings specifically screen for.
- **Gained:** a project built on actively-supported tooling for its realistic interview-usage lifetime, and a cleaner local dev environment (one SDK, not two).

## Consequences

- All prior references to ".NET 8" / `net8.0` in the PRD, README, and operations docs are updated to .NET 10 / `net10.0`.
- If a specific target company or role explicitly requires demonstrated .NET 8 experience, the gap is a one-line explanation ("I built on .NET 10, the current LTS; the same architecture and patterns apply directly to .NET 8") rather than a rebuild — the code uses no .NET 10-only feature that doesn't have a direct .NET 8 equivalent.

## Interview Questions This Creates

- "Why .NET 10 instead of .NET 8, given most enterprises are still on 8?"
- "What would change if you had to backport this to .NET 8?"
