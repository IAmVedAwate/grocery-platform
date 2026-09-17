# Original Planning Brief (Reference)

> This is the source brief the developer supplied at project inception, preserved here verbatim for traceability — several documents in this repository reference it by section number (e.g., "per the original brief §65"). The PRD ([../PRD.md](../PRD.md)) and roadmap ([../ROADMAP.md](../ROADMAP.md)) are the authoritative, current planning documents; this file is historical input, not a living spec. Where this brief and the PRD disagree (e.g., this brief assumes a single-store system and plain React — the PRD extends it to multi-tenant SaaS and Next.js per the developer's later direction), **the PRD wins.**

---

# ENTERPRISE GROCERY MANAGEMENT PLATFORM
## Master Project Brief for Claude Sonnet Max — PRD Planning, Architecture and Execution

> **Purpose of this file:** This is the backbone/context file to give Claude Sonnet Max before asking it to plan this project and generate the first professional PRD.
>
> Claude must treat this file as the user's real context, constraints, current skill level, career objective, and project intent.
>
> **Important:** This is not yet the final PRD. Claude's first responsibility is to turn this brief into a rigorous, realistic, implementation-ready PRD and phased technical plan.

---

# 1. WHY THIS PROJECT EXISTS

This project is being built primarily as a **career acceleration and engineering demonstration project**, not as a hobby CRUD application.

The developer has approximately **2 years of total experience at the point of leaving the current company**, including internships, and wants to compete for strong .NET roles around **₹13 LPA**.

The desired professional identity is:

> **Senior-leaning .NET Backend / Full-Stack Engineer with modern AI Engineering capability.**

*(Full original text preserved in the developer's conversation history with the planning assistant — this reference file retains the section structure and key constraints below for quick lookup. See the PRD for the fully worked-through, current version of every requirement.)*

## Key constraints carried into the PRD

- **Target compensation:** ~₹13 LPA, .NET Backend/Full-Stack role with AI/GenAI exposure.
- **Experience level:** ~2 years total (internships + ITUS Sports and Safety Pvt. Ltd. + Urban Web Host), full-stack + ownership experience (planning, architecture, mentoring), not pure ticket implementation.
- **Skill baseline (self-assessed /10):** .NET Core API 7, C# 6, EF Core 5, SQL Server 7, React 8, TypeScript 8, Azure 0, Docker 3, Kubernetes 1, Testing 0, System Design 2, Auth/Security 5.
- **Available time:** ~4 focused hours/day (two ~2-hour sessions).
- **AI-assisted development is expected**, but generated code must be understood well enough to defend in an interview — not merely typed faster.
- **Original client scope:** React web app, Avalonia desktop app, ASP.NET Core/.NET 8 backend, SQL Server database — later extended by the developer to Next.js (web) and a multi-tenant SaaS framing (see [PRD.md](../PRD.md)).
- **Original architecture principle:** modular monolith first, evolve toward distributed services only where justified — no microservices/Kubernetes/multi-agent systems "because they look senior."
- **Database:** Microsoft SQL Server, explicitly used to reinforce professional SQL skill (indexes, execution plans, SARGability, covering indexes, transactions, concurrency) alongside active AdventureWorks2025 practice.
- **AI requirement:** must be a genuine part of the business system (tool/function calling over real backend data, plus RAG over uploaded business documents with citations) — never a decorative chatbot, never given direct SQL access, never trusted as the authority for business permissions.
- **Testing, Docker, Azure, and system design** were named as the largest capability gaps to close, alongside EF Core depth and applied security.
- **Explicit anti-patterns to avoid:** adding Kubernetes/microservices/Redis/Kafka/multi-agent systems without a proven need; claiming professional experience the project didn't actually create; building a huge feature-count system instead of a few deeply-demonstrated capabilities.
- **Definition of success (from the brief):** the application works, the architecture is defensible, critical workflows are tested, SQL is understood, security is credible, deployment works, AI is meaningful, documentation is professional, the developer understands the system, and the system can be demonstrated in an interview — explicitly **not** measured by table count, endpoint count, or number of technologies used.

## Prioritization model carried into the PRD

`P0` Must Have / `P1` Strong Showcase / `P2` Advanced Showcase / `P3` Stretch — applied throughout [PRD.md](../PRD.md).

## Full original document

The complete original brief (89 sections) was supplied directly by the developer as project context and is preserved in the planning conversation that produced this repository. Every requirement from it that survived into the actual project scope is captured, current, and traceable in [PRD.md](../PRD.md), [ROADMAP.md](../ROADMAP.md), and [decisions/](../decisions/) — those are the documents to build from.
