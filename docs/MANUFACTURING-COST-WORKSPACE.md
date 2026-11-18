# Manufacturing cost workspace — 18 November 2026

The production screen now records manufacturing costs for a job rather than requiring phase selection and incremental phase completion. The normal work tab has cutting, edge banding (Ashareet), assembly/accessory fitting and packaging subtabs. Pause/resume with reasons, material issues/returns, waste, quality inspection, history and the durable production outbox are retained.

## Cost capture and identity

### Wage-entry restoration

The wages/workers tab includes both worker registration and a separate wage form for any described work on the selected job. Assembly wage entry remains in the assembly subtab. Both use the same `production_work_costs` source ledger, event/retry handling, globally unique reference and finance wage-source link, so viewing a wage in both places creates no extra cost. The wages tab lists assembly and general wage entries together. Each form has its own worker selection and inputs. General wages require a work description; a flat amount can be entered as quantity 1 times the amount. These are earned wages; settlement remains in finance.

Apply **016_general_worker_wages.sql** once after 015, then rebuild/restart API and desktop. It extends work-cost checks to `general_labor` and preserves all existing entries. From the repository root:

```powershell
docker compose exec postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /docker-entrypoint-initdb.d/016_general_worker_wages.sql'
```

Restoration verification: API/desktop builds pass; 97 focused production/finance regressions and the extended runtime UI checks pass. The real PostgreSQL manufacturing suite is explicitly skipped without its disposable database URL; migration 016 has not been applied or verified against a live database here. The opt-in integration test now includes general wages, rejection of a reference reused across assembly/general entry, and inclusion of the general wage in finance/job totals.

- Cutting and banding: machine name/code, charging basis (unit, minute or hour), actual billable quantity, rate, unique work reference, optional description. The UI previews the amount; PostgreSQL calculates and stores `ROUND(quantity * rate, 4)`.
- Assembly/accessory fitting: select a registered worker by code/name, choose unit/minute/hour/day/week/month, and enter the billable quantity/share and rate attributable to this job. Daily/weekly/monthly entries are actual job allocations, not automatic full-period payroll. Do not enter the same full salary against several jobs.
- Packaging: retains one total for the selected job with an optional description. Its existing unique-per-job source remains authoritative.
- Each section has its own total and the job has a combined direct manufacturing total. Material/accessory issues minus returns are displayed separately. Legacy direct wages are included once; pooled wages remain separate.
- Workers have UUID identities and unique, case-insensitive trimmed business codes. Duplicate names are allowed and remain distinguishable. Creation is permission checked and repeatable with the same immutable ID/data after an uncertain response. There is no rename/edit/delete endpoint in this patch. Historical free-text workers are not guessed or merged into new identities.

Example: cutting 30 units × 3 = 90, banding 15, assembly 80, packaging 20 gives 205 manufacturing cost. If issued material cost is 500, it remains a separate component; the production cost ledger has 705 total direct cost, not 910.

## Integrity and finance

Each cost posts through the existing production transaction, job row lock, current accounting-period checks, audit event and immutable operation-ID/payload replay handling. One generated-amount source writes one unique direct production cost-ledger entry. Work references are globally unique, ignoring case/outer spaces, to reject a repeated session or wage allocation under a new operation ID. Use distinct references for genuinely different sessions/allocations. The source has a deferred foreign key to its official production event. A failed source, costing or event write rolls back everything.

New worker earnings are available to finance as `job_work` wage-payable sources. The source stores the worker UUID plus a stable code/name snapshot; the payable/payment counterparty uses that snapshot and retains the source link. A wage payable can consume a source only once. Posting its payable or settlement does not insert another production cost.

Finance reports include job, customer-order code, posting month, status, work costs, packaging, historical wages, direct manufacturing and separate pooled wages. These are source-derived cost reports. Incurred costs remain dated to their original month and are not copied or re-expensed when the job finishes. The existing production WIP calculation includes new direct costs once.

Quality acceptance in the new workflow is bounded by the job's planned quantity minus accepted/rejected quantities, with bounded reinspection. It does not require cutting/assembly/packaging progress counts. Full acceptance retains the existing `ready_for_costing` state. Finished-goods valuation, approved monthly allocation, WIP-to-finished-goods transfer and formal period close remain the later costing milestone; this patch does not claim to implement them or deduct WIP a second time from profit.

Verification: API and desktop builds pass (23 existing desktop warnings, zero errors). The complete Node run has 132 passing regressions and three explicitly skipped PostgreSQL suites. Production runtime checks pass for cost subtabs, selectors, independent forms, decimal preview/input, unique worker selection and separate per-section/job totals; the finance runtime/draft/PDF check also passes. These checks do not substitute for the real PostgreSQL acceptance below.

## Existing jobs

New starts use cost-focused mode automatically. An existing in-progress/on-hold job has an explicit conversion button. Historical progress and wage records are preserved. Conversion rejects unsettled historical wage agreements, including unearned unit commitments, for reconciliation rather than discarding them. Fixed-period historical accrual remains accessible in the collapsed historical-wage section. A stage with historical wage agreements/labor entries rejects a new work-cost entry until those sources are reviewed; no automatic guess is made about whether it represents the same work.

## Activation

### Missing workers endpoint / empty job picker fix

`Cannot POST /api/production/workers` means the connected API does not expose the new route. Applying a SQL migration does not update an already-running Node process. Stop the old API in its terminal with Ctrl+C, then from `apps/api` run `npm.cmd run build` followed by `npm.cmd run start`. Close and rebuild/reopen the desktop as well. Do not rerun 015 if it already committed successfully.

The desktop previously waited for workers before binding its successfully fetched job list, so a worker-endpoint failure left an empty picker. The job list now binds first, worker failures stay visible in a separate message, and a worker error does not discard job choices. The initial job must be explicitly selected. Missing-worker-route responses (including the older API's UUID-route validation response) show update/restart guidance instead of being hidden by cached workers. New manufacturing actions stay disabled until their worker/API dependency is available. A runtime regression simulates worker-endpoint failure/recovery while confirming that jobs remain searchable; an HTTP regression starts the built API and verifies both worker routes reach authentication rather than returning 404.

Back up the database and apply **015_job_manufacturing_costs.sql** once, after migrations through 014. This migration requires the finance source-link table from 013; the owner has already reported applying 013 and should not rerun it. Rebuild/restart API and desktop after applying 015. No live migration was applied by the agent.

```powershell
npm.cmd --prefix apps/api run build
dotnet build apps/desktop/WoodFactoryErp.Desktop/WoodFactoryErp.Desktop.csproj --no-restore -o apps/desktop/WoodFactoryErp.Desktop/bin/ManufacturingVerification
node --test tests/*.test.cjs
dotnet run --project tests/ProductionUiSmoke/ProductionUiSmoke.csproj --no-restore -p:DesktopVerificationOutput=ManufacturingVerification
dotnet run --project tests/FinanceUiSmoke/FinanceUiSmoke.csproj --no-restore -p:DesktopVerificationOutput=ManufacturingVerification
```

For real integration acceptance, set `MANUFACTURING_TEST_DATABASE_URL` to a disposable test database and run `node --test tests/manufacturing-postgres.test.cjs`. It applies migrations in its own random schema and tests exact generated amounts, worker codes, concurrent duplicate references, replay, phase-free QC acceptance, financial source consumption without duplicate costs and closed-period rejection, then drops that schema. Without that connection it is explicitly skipped. SQL/migration/concurrency execution against PostgreSQL remains unverified in this environment.
