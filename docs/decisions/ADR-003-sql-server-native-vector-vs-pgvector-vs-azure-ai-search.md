# ADR-003: Vector Store for RAG

**Status:** Accepted — decided in Phase 1 planning, implemented in Phase 5

## Problem

The RAG subsystem (§16 of the PRD) needs to store and query embedding vectors for document chunks, filtered by tenant, to support grounded document Q&A. A vector storage/retrieval mechanism must be chosen.

## Options Considered

1. **PostgreSQL + pgvector** — matches the reference target-skill resume exactly; requires standing up and operating a second database engine alongside SQL Server.
2. **Azure AI Search** — managed, purpose-built vector + hybrid search with strong Azure-resume alignment; adds cost and another managed service to configure, secure, and explain.
3. **SQL Server 2025 native `VECTOR` type** — the developer's primary, actively-practiced database engine now supports vector storage and similarity search directly; no second engine, no extra managed service.

## Decision

**SQL Server 2025 native `VECTOR` type (option 3).**

## Reasoning

- The project's own stated anti-pattern list explicitly warns against adding infrastructure "because a resume lists it." Introducing Postgres solely for pgvector, or Azure AI Search solely for vector search, when the platform's primary database already supports the capability, is exactly that anti-pattern.
- The developer is already actively strengthening SQL Server skills (AdventureWorks practice). Extending that same engine to cover vector search compounds that investment instead of fragmenting attention across two database technologies.
- Tenant-scoped retrieval (§16: a similarity query must filter by `StoreId`) is simpler to reason about and test when the vector data lives in the same database, transaction boundary, and backup regime as the tenant data it belongs to.
- Embeddings and similarity search are still fully demonstrated conceptually and practically — the interview story ("what is an embedding, how does similarity search work, why did you filter by tenant before/alongside the vector search") does not depend on which product implements the vector index.

## Trade-offs

- **Given up:** Azure AI Search's built-in hybrid (lexical + vector) search tooling and managed scaling; pgvector's ecosystem maturity and the exact resume-keyword match with the reference profile.
- **Gained:** zero additional infrastructure to provision, secure, back up, or explain; a single consistent transactional boundary between business data and the vectors derived from it; direct reinforcement of the SQL Server track already underway.

## Consequences

- If retrieval quality or scale ever genuinely requires hybrid search or a purpose-built ANN index at a scale SQL Server's vector support doesn't comfortably handle, that becomes a future ADR with a *measured* reason — not a default choice.
- The RAG ingestion pipeline (chunk → embed → store) writes to a normal SQL Server table with a `VECTOR` column and standard tenant-scoping conventions, not a separate data-access pattern.

## Interview Questions This Creates

- "What is an embedding and how does similarity search actually work?"
- "Why SQL Server for vectors instead of a dedicated vector database?"
- "How do you keep a RAG answer from leaking another tenant's document?"
- "What would push you toward Azure AI Search or pgvector later?"
