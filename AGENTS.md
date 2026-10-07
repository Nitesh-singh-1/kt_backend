# AGENTS.md — KTransport Backend

**For any AI assistant or human** working inside this repository (`kt_backend/`). Stack: .NET 8, EF Core 8, PostgreSQL. Multi-tenant via `ITenantScopedEntity` + EF `HasQueryFilter`.

If you have the parent workspace open (`G:\SourceCodeNitesh\`), the full coordination layer is at `../.agent/`. If you only have this repo open, follow the condensed rules here.

---

## The five database rules — hard constraints

Any time you add a column, create a migration, or shape a response DTO, these apply. Full version at `../.agent/RULES/DATABASE.md` and `../.agent/specs/decisions/database-table-hierarchy.md` (ADR, Accepted 2026-10-07).

1. **Aggregate root ≤ 20 columns.** `Shipment`, `Trip`, `Invoice`, `Vehicle`, `Party` are aggregate roots. If you are about to add a 21st scalar property, stop — the field belongs on an extension table.
2. **Separate concerns into separate tables.** Settlement, delivery, financial totals, e-way bill, routing — each is its own table (1:1 extension OR 1:N event, depending on cardinality). Do NOT keep piling columns onto the aggregate.
3. **No FK + full snapshot** unless the snapshot is legally immutable. If a row already carries `vehicle_id`, do not also carry `vehicle_no`, `vehicle_type`, etc. — read via join. The ONE exception: fields that form part of a printed legal document (e.g. consignor name/GST/address on a Bilty) are frozen at creation and stay on the row.
4. **History is append-only.** Settlement, status change, payment, reversal → one event-table row per occurrence (`trip_settlements`, `shipment_status_history`). Never overwrite columns on the parent to represent a new event.
5. **Nullable columns are a smell.** More than three related nullables on a table = extract into an extension table whose row existence IS the signal.

**Working example**: [TASK-037](../.agent/tasks/TASK-037-trip-settlement-backend.md) — `trip_settlements` extracted from `trips`. 23 columns on the new event table, exceeds the 20-cap but event tables are explicitly exempt per the ADR (the cap is for aggregate roots).

---

## Checklist before you write a migration

If you reach for `dotnet ef migrations add`, work through this first:

1. **Which rule does this column help satisfy?** If the answer is "none — it just felt right on this table," stop and reconsider table placement.
2. **Does the parent entity already carry an FK to the source of truth?** If yes, do you need the snapshot column at all? If yes, is the snapshot legally immutable per Rule 3? If not, delete the column from your plan.
3. **Will this field change more than once in a row's lifetime?** If yes, Rule 4 → event table row, not a column.
4. **How many related nullables does this bring the parent to?** If > 3, Rule 5 → extension table.

Then run `dotnet ef migrations add <Name>` and **read the generated SQL** before committing. The migration file name becomes part of git history; treat it like code.

---

## EF Core conventions on this repo

- Every new entity implements `ITenantScopedEntity`:
  ```csharp
  public class YourEntity : ITenantScopedEntity
  {
      public long Id { get; set; }
      public Guid TenantId { get; set; }
      public virtual Tenant? Tenant { get; set; }
      // …
  }
  ```
- Fluent API in `Data/KTransportDbContext.cs`:
  - `entity.HasKey(e => e.Id).HasName("<table>_pkey")`.
  - Snake-case `.HasColumnName("snake_case")` for every property.
  - `entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId)`.
  - Default-tenant via `.HasDefaultValue(TenantConstants.DefaultTenantId)` on `TenantId`.
  - FKs: `OnDelete(DeleteBehavior.Cascade)` for extension/event children, `SetNull` for soft references.
- Precision on decimals: money is `(14, 2)`, weight is `(12, 3)`, odometer is `(12, 2)`.
- Timestamps: `timestamp without time zone` + `.HasDefaultValueSql("CURRENT_TIMESTAMP")` for `CreatedAt`.

---

## HTTP contract rules

- Public HTTP response shape is **additive only** inside a task unless the contract YAML says otherwise. Never rename or remove a field. Never change a status code.
- When extending a DTO, mark the new fields nullable so clients consuming the previous shape don't break.
- Controllers go under `[Authorize]` at minimum. For paid features, add `[RequireFeature(FeatureConstants.X)]` at class level. For per-page gating, add `[RequirePermission("page.key")]` on the specific endpoint.
- See `TASK-039` for the per-page attribute pattern — `kt_backend/KTransport.API/Authorization/RequirePermissionAttribute.cs`.

---

## Testing

- Tests live in `KTransport.API.Tests/`. xUnit + Microsoft.EntityFrameworkCore.InMemory.
- Mirror the fixture pattern from `TripSettlementServiceTests.cs` or `InvoiceServiceTests.cs`.
- Required coverage for a new feature: at least one happy-path test per public method you add, plus one negative test for each permission/validation guard.
- Run with: `dotnet test kt_backend/KTransport.API.Tests`. Green on main.

---

## When the parent workspace is NOT open

If you only have this repo mounted (common with Cursor, Codex, Gemini CLI), these local sources are enough to work correctly:

- This file (`AGENTS.md`).
- `KTransport.API/Models/*.cs` for entity shape.
- `KTransport.API/Data/KTransportDbContext.cs` for Fluent API examples.
- `KTransport.API/Migrations/*.cs` as examples of correct migration shape.
- `KTransport.API.Tests/*.cs` for test patterns.

If you need to see the ADR or the per-task contracts and don't have access to the parent folder, ask the user to open it or paste the relevant section.
