# SQL Performance Pass (Phase 3)

Fulfils the ROADMAP.md Phase 3 checkpoint: "identify at least one genuinely
slow query against a realistically sized seeded dataset, fix it, measure
before/after with execution plans and `STATISTICS IO/TIME`." See
[Indexing Principles](./data-architecture.md#indexing-principles) for the
design rule this pass verifies rather than assumes.

## Target query

`Infrastructure/Persistence/ReportingRepository.cs` `GetSalesByProductAsync`
— the sales-by-product report (`GET /api/v1/reports/sales-by-product`,
[ReportsController.cs:22](../../backend/src/Api/Controllers/ReportsController.cs#L22)).
Chosen because before this pass, `SalesOrder` had no index at all on
`CreatedAtUtc` (only a unique index on `(StoreId, IdempotencyKey)`), and
`SalesOrderItem` had no application-defined index whatsoever — EF Core's
convention auto-creates an index for a configured foreign key, and
`SalesOrderId` got one that way (via `SalesOrder.HasMany(o => o.Items)`),
but `ProductId` was never configured as a navigation/FK relationship at
all, so it had no index of any kind.

## Method

1. Seeded 200,000 synthetic `SalesOrder`/`SalesOrderItem` rows for one real
   store directly via T-SQL (bypassing the API — this is pure data volume,
   not a correctness test), spread pseudo-randomly across the last 365 days.
2. Ran the exact SQL EF Core generates for `GetSalesByProductAsync` — including
   the `StoreId` predicate that `SalesOrder`'s global query filter appends to
   *every* query automatically (`GroceryDbContext.cs`), which a hand-simplified
   version of the query would have missed — with
   `SET STATISTICS IO, TIME, PROFILE ON`, filtering the last 30 days, page 1
   of 20, ordered by revenue descending.
3. Added the indexes below, re-ran the identical query, compared.
4. Deleted the synthetic rows afterward; the indexes themselves were kept —
   encoded into `GroceryDbContext.cs` and migration
   `20260918103725_Phase3SalesReportingIndexes`, applied like any other
   migration.

## Indexes added

```csharp
// SalesOrder
b.HasIndex(o => new { o.StoreId, o.CreatedAtUtc });

// SalesOrderItem — covering indexes: every column the report's aggregate
// reads (Quantity, UnitPrice, TaxAmount, LineDiscount) plus the other join
// key are INCLUDEd, so SQL Server never needs a key lookup back into the
// clustered index for a matching row.
b.HasIndex(i => i.SalesOrderId)
    .IncludeProperties(i => new { i.ProductId, i.Quantity, i.UnitPrice, i.TaxAmount, i.LineDiscount });
b.HasIndex(i => i.ProductId)
    .IncludeProperties(i => new { i.SalesOrderId, i.Quantity, i.UnitPrice, i.TaxAmount, i.LineDiscount });
```

`(StoreId, CreatedAtUtc)` leads with `StoreId` because every query against
`SalesOrders` is implicitly scoped to one tenant first (the query filter),
then the explicit date range — same principle as every other tenant-scoped
index in this schema. `SalesOrderItem.SalesOrderId` already had a plain,
non-covering index from EF Core's own FK convention; it was replaced with a
covering version rather than left alongside a second, narrower one.

## Results

| Metric | Before | After |
|---|---|---|
| `SalesOrders` access | **Clustered Index Scan**, 6,628 logical reads | **Index Seek** on `IX_SalesOrders_StoreId_CreatedAtUtc`, 99 logical reads |
| `SalesOrderItems` access | **Clustered Index Scan**, 2,793 logical reads | Covering **Index Scan**/**Index Seek** (adaptive join between the two new indexes), 2,119 logical reads |
| `Products` access | Clustered Index Seek, 2 logical reads (already fine) | unchanged, 2 logical reads |
| Total logical reads | **9,423** | **2,220** (−76%) |
| CPU time | 887 ms | 103 ms (−88%) |
| Elapsed time | 157 ms | 104 ms |

Raw `SET STATISTICS IO/TIME` output and the `STATISTICS PROFILE` text plan
for both runs are reproduced below.

### Before (no supporting index)

```
Table 'SalesOrderItems'. Scan count 17, logical reads 2793.
Table 'SalesOrders'. Scan count 17, logical reads 6628.
Table 'Products'. Scan count 0, logical reads 2.

|--Clustered Index Scan(OBJECT:([SalesOrders].[PK_SalesOrders] AS [o]),
     WHERE:([o].[CreatedAtUtc]>=[@FromUtc] AND [o].[CreatedAtUtc]<=[@ToUtc]))
|--Clustered Index Scan(OBJECT:([SalesOrderItems].[PK_SalesOrderItems] AS [item]))

 SQL Server Execution Times: CPU time = 887 ms, elapsed time = 157 ms.
```

The `StoreId` predicate didn't even show up as a residual filter in the
`SalesOrders` scan step above — the optimizer pushed it into a `Filter`
elsewhere in the plan, but the underlying access was still a full clustered
index scan either way: with no index leading with `StoreId` or
`CreatedAtUtc`, every row of the table has to be read and checked.

### After (with the new indexes)

```
Table 'SalesOrderItems'. Scan count 1, logical reads 2119, read-ahead reads 4.
Table 'SalesOrders'. Scan count 1, logical reads 99.
Table 'Products'. Scan count 0, logical reads 2.

|--Index Seek(OBJECT:([SalesOrders].[IX_SalesOrders_StoreId_CreatedAtUtc] AS [o]),
     SEEK:([o].[StoreId]=[@StoreId] AND [o].[CreatedAtUtc] >= [@FromUtc]
           AND [o].[CreatedAtUtc] <= [@ToUtc]) ORDERED FORWARD)
|--Index Scan(OBJECT:([SalesOrderItems].[IX_SalesOrderItems_ProductId] AS [item]))
|--Index Seek(OBJECT:([SalesOrderItems].[IX_SalesOrderItems_SalesOrderId] AS [item]),
     SEEK:([item].[SalesOrderId]=[SalesOrders].[Id] as [o].[Id]) ORDERED FORWARD)

 SQL Server Execution Times: CPU time = 103 ms, elapsed time = 104 ms.
```

`SalesOrders` went from a **scan** to a genuine **seek** — the checkpoint's
literal bar ("a report endpoint measurably uses an index seek"). The
optimizer's adaptive join chose to *scan* the new `IX_SalesOrderItems_ProductId`
covering index rather than seek it — with a 30-day window matching most of
the table's date range in this synthetic dataset, scanning the narrow
covering index outright was cheaper than 16,000+ individual seeks; either
way it's reading a narrow nonclustered index instead of the wide clustered
index, which is what drove `SalesOrderItems`' logical reads down from 2,793
to 2,119 and — more importantly — eliminated the *scan count 17* (repeated
scans from the old hash-join strategy) down to a *single* pass.

## Why this is called "SARGable" / what a covering index buys here

A predicate is SARGable ("Search ARGument-able") when the optimizer can
translate it directly into an index seek's start/end range instead of
having to evaluate it row-by-row after reading every row. `CreatedAtUtc >=
@FromUtc AND CreatedAtUtc <= @ToUtc` was always SARGable *in principle* —
the problem before this pass was that there was no index for the optimizer
to seek *into*, so it fell back to a scan and applied the predicate as a
residual filter over every row instead.

A **covering index** takes this further: even once a matching row is found
via seek, satisfying the rest of the query (the aggregate's `SUM(Quantity *
UnitPrice + TaxAmount - LineDiscount)`) normally requires a **key lookup**
back into the clustered index for every matching row to fetch those
non-indexed columns — for ~16,600 matching rows in this dataset, that's
16,600 extra random-access reads. `INCLUDE`ing `Quantity`, `UnitPrice`,
`TaxAmount`, and `LineDiscount` directly in the nonclustered index means the
aggregate can be computed entirely from the narrower index, with zero key
lookups — the single biggest reason the logical read count didn't just drop
a little, but dropped by three-quarters.
