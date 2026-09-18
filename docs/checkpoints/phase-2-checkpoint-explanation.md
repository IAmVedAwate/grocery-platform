# Phase 2 Checkpoint — Core Business Workflows

This explains what Phase 2 actually built, what you need to understand about it, and how to talk about it in an interview — with exact file/line references. See [ROADMAP.md](../ROADMAP.md#phase-2--core-business-workflows-p0) for the original checkpoint definition this expands on. It assumes you've already internalized [Phase 1's checkpoint](./phase-1-checkpoint-explanation.md) — multi-tenancy and permission-based auth apply to every module below without being re-explained.

---

## 1. Engineering Checkpoint — what was actually built

**Inventory (the concurrency-safe core everything else depends on):**
- `InventoryItem.RowVersion` is a SQL Server `ROWVERSION` column, configured at `backend/src/Infrastructure/Persistence/GroceryDbContext.cs:174` (`IsRowVersion()`). `Increase`/`Decrease` live on the entity itself — `backend/src/Domain/Inventory/InventoryItem.cs:35` (`Increase`), `:51` (`Decrease`, with the insufficient-stock guard at `:56`).
- Every quantity change produces an immutable `StockMovement` row — `backend/src/Domain/Inventory/StockMovement.cs`.
- `InventoryApplicationService` owns all three ways stock changes — `backend/src/Application/Inventory/InventoryApplicationService.cs:33` (`ReceiveAsync`), `:50` (`SellAsync`), `:69` (`AdjustAsync`). Receive/Sell are composable (they don't call `SaveChangesAsync` — the caller commits once, atomically, alongside whatever else it's doing); Adjust is standalone and self-contained since it's a direct, auditable action on its own.
- Audit plumbing: `IAuditWriter.Record(...)` stages an entry without saving — `backend/src/Infrastructure/Audit/AuditWriter.cs:11,25` — so it commits in the same transaction as the business action that triggered it.

**Purchasing (the state machine):**
- `PurchaseOrder`: Draft → Submitted → Approved → PartiallyReceived/Received → Cancelled, enforced inside the aggregate itself — `backend/src/Domain/Purchasing/PurchaseOrder.cs:50` (`Submit`), `:56` (`Approve`), `:75` (`ReceiveItems`, which decides Received vs PartiallyReceived at `:89`), `:92` (the shared `EnsureStatus` guard every transition uses).
- Receiving commits the PO-side state and the inventory-side state (a `StockMovement` + `InventoryItem` increase per line) in one `SaveChangesAsync` call — `backend/src/Application/Purchasing/PurchasingApplicationService.cs` (`ReceiveAsync`, which loops calling `InventoryApplicationService.ReceiveAsync` before the single final save).
- Approval is a universal gate (every PO needs it) rather than a value-based threshold — a deliberate scope decision, not an oversight (see §2).

**Sales/Checkout (the highest-value workflow in the project):**
- The transaction: validate lines → check + decrement stock → capture payment → issue invoice → audit → **one** `SaveChangesAsync` — `backend/src/Application/Sales/SalesApplicationService.cs:27` (`CheckoutAsync`), `:57` (server-side price/tax resolution — a client-submitted price is never trusted), `:63` (the stock decrement, where a concurrency conflict can surface), `:65-66` (payment + invoice), `:76` (the single commit).
- Idempotency: a required `Idempotency-Key` header — `backend/src/Api/Controllers/SalesController.cs:39,41-42` — checked against a unique `(StoreId, IdempotencyKey)` index (`GroceryDbContext.cs:235`) via `GetByIdempotencyKeyAsync` (`SalesApplicationService.cs:32`), so a retried request returns the original sale instead of creating a duplicate.
- **The concurrency guarantee**: `GroceryDbContext.SaveChangesAsync` (override) catches `DbUpdateConcurrencyException` and rethrows as `ConflictAppException` — `GroceryDbContext.cs:42-56` — so a checkout that loses the race for stock gets a clean 409, not a raw EF exception leaking to a 500.
- Refunds: `SalesOrder.RefundLines` (`backend/src/Domain/Sales/SalesOrder.cs:88`) rejects refunding more than was sold (minus what's already refunded), and `SalesApplicationService.RefundAsync` (`:80`) restocks via `InventoryApplicationService.ReceiveAsync` in the same transaction as the refund.

**The bug-translation layer** (a real gap found and fixed this phase): `Application.Common.DomainRuleGuard` — `backend/src/Application/Common/DomainRuleGuard.cs:19,25` — wraps every Domain state-transition call (`Submit`/`Approve`/`ReceiveItems`, `CapturePayment`/`IssueInvoice`/`RefundLines`, negative `AdjustAsync`) and translates `InvalidOperationException` into `ConflictAppException` (409). It's used at each specific call site, not as a blanket exception-handler rule, because `InvalidOperationException` is also thrown for a genuine server bug elsewhere (`HttpTenantContext`'s missing-claim guard) that really should stay a 500.

**Web UI:** Inventory (search + low-stock filter + inline adjust), Purchasing (supplier quick-create, PO creation with dynamic lines, a detail page driving the whole state machine), Checkout (barcode/keyboard-first entry — an exact single search match adds straight to the cart), Sales history + refund, Customers.

---

## 2. Learning Checkpoint — what you need to actually understand

- **Optimistic concurrency, concretely.** `RowVersion` isn't manually incremented anywhere in application code — SQL Server bumps it automatically on every `UPDATE`. EF Core includes the *originally-read* value in the `UPDATE ... WHERE Id = @id AND RowVersion = @original` statement it generates. If another transaction already changed the row, that `WHERE` matches zero rows, and EF Core raises `DbUpdateConcurrencyException` — no explicit lock is ever held for the request's duration, which is what makes this scale under real concurrent load rather than serializing every checkout through a lock.
- **Why this specific mechanism, not a `SELECT ... FOR UPDATE`-style pessimistic lock.** A pessimistic lock would hold a row lock for the whole checkout request — including the network round-trip and payment/invoice steps — which under real concurrent load would serialize checkouts unnecessarily. Optimistic concurrency only pays a cost when there's an *actual* conflict, which for "two cashiers, one product, one instant" is rare.
- **Why the checkout transaction is one `SaveChangesAsync` call, not several.** If stock decremented successfully but the payment/invoice write failed separately, you'd have sold inventory with no record of payment — the exact "half-completed sale" failure mode called out in the PRD. One call means EF Core wraps everything in one database transaction: all of it commits, or none of it does.
- **Idempotency vs concurrency — they solve different problems.** Concurrency control (RowVersion) stops two *different* sale attempts from both succeeding on the same unit of stock. Idempotency (the `Idempotency-Key` header) stops the *same* sale attempt, retried after a dropped response, from being recorded twice. `SalesCheckoutTests.cs` tests both, separately, on purpose.
- **Why a blanket `InvalidOperationException → 409` mapping in the exception handler would have been wrong**, even though it would have "fixed" the bug faster. It's a real trade-off worth being able to explain: precision at each call site versus a global rule that's easier to write but silently wrong in a different, real case elsewhere in the same codebase.

---

## 3. Interview Checkpoint — questions and how to answer them

> Answers are written in first person. Each one names the file/line to have open or mention if asked to go deeper.

**Q: How do you prevent overselling inventory under concurrent requests? Walk me through it concretely.**

> "`InventoryItem` has a `RowVersion` column backed by SQL Server's native `ROWVERSION` type, configured at `GroceryDbContext.cs:174`. When checkout decrements stock, EF Core reads the current row — RowVersion included — and when it saves, it generates an `UPDATE` with `WHERE Id = @id AND RowVersion = @originalValue`. If two checkouts read the same row at nearly the same time and both try to sell the last unit, both compute a valid-looking new quantity in memory, but only the *first* one to actually commit changes the RowVersion in the database. The second one's `WHERE` clause no longer matches anything, so EF Core throws `DbUpdateConcurrencyException`. I catch that once, centrally, in `GroceryDbContext.SaveChangesAsync` (`GroceryDbContext.cs:50-56`) and translate it into a `ConflictAppException`, which the API returns as 409. I don't just assert this works — `SalesCheckoutTests.Checkout_TwoSimultaneousSalesForTheLastUnit_ExactlyOneSucceeds` (`SalesCheckoutTests.cs:154`) fires two real concurrent HTTP requests at one unit of stock using `Task.WhenAll` (`:173`) and asserts exactly one gets `201 Created` and the other gets `409 Conflict` (`:175-179`), with final stock landing at exactly zero, never negative."

**Q: Why RowVersion instead of a database-level lock?**

> "A lock held for the whole checkout — stock check, payment capture, invoice creation, all of it — would serialize every checkout that happens to touch a popular product, even when there's no actual conflict. Optimistic concurrency only costs anything when there's a *real* race, which for a single unit of stock at the same instant is the exception, not the norm. It also means I'm not holding a database resource open across a chain of operations that includes application-layer work, which is generally something you want to avoid."

**Q: Walk me through your checkout transaction. What happens if it fails halfway?**

> "It can't fail 'halfway' in a way that leaves inconsistent state, because everything — the stock decrements for every line, the `SalesOrder` and its items, the `Payment`, the `Invoice`, the audit entry — gets staged in memory against the same DbContext and committed with exactly one `SaveChangesAsync` call, at `SalesApplicationService.cs:76`. That's one database transaction. If anything in that set fails — insufficient stock on line 2 of 3, a concurrency conflict, whatever — nothing before it gets committed either. There's no code path where stock decreases but no sale record exists."

**Q: What is idempotency, and why does your checkout endpoint specifically need it?**

> "If a client's checkout request succeeds on the server but the response gets lost — a network blip, a timeout — the client doesn't actually know whether the sale happened. Without idempotency, the natural thing for a client to do is retry, which risks charging the customer twice and selling the same stock twice. My checkout endpoint requires an `Idempotency-Key` header (`SalesController.cs:39`, rejected with 400 if missing at `:41-42`), and `SalesOrder` has a unique `(StoreId, IdempotencyKey)` index (`GroceryDbContext.cs:235`). `CheckoutAsync` checks for an existing order with that key first (`SalesApplicationService.cs:32`) and returns it unchanged if found, instead of creating a second sale. I have a specific test for this — `Checkout_RetriedWithSameIdempotencyKey_ReturnsOriginalSale_DoesNotDoubleDecrementStock` — that checks stock only decremented once across two identical requests."

**Q: Explain your purchase order state machine, and why you designed approval the way you did.**

> "Draft → Submitted → Approved → PartiallyReceived or Received, plus Cancelled from any non-terminal state. Every transition is a method on the `PurchaseOrder` aggregate itself — `Submit`, `Approve`, `ReceiveItems` — each guarded by `EnsureStatus` (`PurchaseOrder.cs:92`), so an invalid transition, like trying to receive against a Draft order, throws before anything happens. The original design considered a value-based approval threshold — auto-approve small orders, require explicit approval above some amount — but I deliberately simplified that to 'every order requires approval,' because a threshold needs a configurable settings entity that has no other purpose in this phase. I documented that as a real scope trade-off rather than quietly cutting a corner — it's a clearly-labeled P1 refinement, not a gap I'm hoping nobody asks about."

**Q: How do refunds work, and what stops someone from over-refunding?**

> "`SalesOrder.RefundLines` (`SalesOrder.cs:88`) tracks `QuantityRefunded` per line and refuses to refund more than `Quantity - QuantityRefunded` — that check lives on `SalesOrderItem` itself. `SalesApplicationService.RefundAsync` (`:80`) then restocks the refunded quantity through `InventoryApplicationService.ReceiveAsync`, committed in the same transaction as the refund. There's a test that specifically tries to over-refund and asserts it gets rejected with 409, then successfully partially refunds and checks the stock number afterward."

**Q: Tell me about a bug you found in this phase.**

> "A few, actually, and they're a good illustration of how a design decision that looks fine in isolation can be wrong for a reason you only discover by actually exercising it. First: I had Domain throw `InvalidOperationException` for invalid state transitions — the same pattern used correctly elsewhere in the codebase — but I'd forgotten that my central exception handler only maps a specific set of custom exception types, so these fell through to a generic 500 instead of a 409. I fixed it by wrapping exactly those Domain calls with a small `DomainRuleGuard.Run(...)` helper (`DomainRuleGuard.cs:19`) that translates the exception at the call site — deliberately *not* a blanket mapping in the handler itself, because that same exception type is also thrown for a genuine server bug elsewhere, and I didn't want to accidentally turn a real 500 into a fake 409. Second, an EF Core one: my inventory overview query does a LEFT JOIN and projects straight into a custom record type, then tried to `.OrderBy()` on that projected shape — EF Core couldn't translate it and threw at runtime, not at compile time. The fix was ordering and filtering on the *raw* joined shape and only projecting into the final DTO as the very last step, right before returning. Third, after I'd fixed the backend and had 68 tests passing, I ran the actual UI in a real browser and found a CORS failure that none of my backend tests would ever catch: `docker-compose.yml` had `ALLOWED_WEB_ORIGIN` hardcoded to just `:3000` from before I'd added multi-origin support, silently overriding the smarter default in the application code. All three are written up with root cause and fix in `docs/operations/troubleshooting.md` — I think that's a more honest and more useful artifact than pretending it all worked first try."

**Q: How would this handle 10x traffic? What would you change first?**

> "Checkout itself already scales horizontally — it's stateless, and the concurrency guarantee doesn't depend on holding any server-side lock, so more API instances just work. The place I'd look first is the background processing model — right now low-stock notifications and anything similar run in-process via `IHostedService`, which is fine at this scale but ties background work to the same process as request handling. If that started to genuinely compete for resources, that's the point where a real message queue earns its complexity, not before."

---

## 4. Evidence Checkpoint — how to actually show this works

```
cd backend
dotnet test tests/Unit           # PurchaseOrder and SalesOrder state-machine rules, InventoryItem invariants
dotnet test tests/Integration    # real SQL Server — full purchasing cycle, checkout, refunds, the concurrency race
```

- `SalesCheckoutTests.cs:154-179` — the concurrency test. This is the single strongest piece of evidence in the whole project: it doesn't assert against a mock or simulate a race with sleeps, it fires two real HTTP requests concurrently and checks the actual outcome.
- `PurchaseOrderTests.cs` and `SalesOrderTests.cs` (unit) — every state-machine transition, valid and invalid.
- `docs/operations/troubleshooting.md` — three more real bugs, root cause and fix, from this phase alone.
- A live demo: create a supplier, raise and approve a purchase order, receive it (watch inventory increase), sell some of it through Checkout (watch inventory decrease and an invoice appear), then refund a line and watch stock come back — all through the actual web UI, verified end-to-end in a real browser during this phase (see the Phase 2 completion summary for the exact walkthrough and stock cross-check: 0 → 50 → 70 → 69).
