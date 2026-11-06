# Proposed DB-level hardening (NOT the EF schema)

`001_phase2_schema.sql` is a hand-written PostgreSQL schema I drafted while the implemented EF model was not visible to me.
It does **not** match the EF Core model (different table/column names, precision, and extra constraints), so do not apply it to a database that EF migrations manage.

Its constraint tests (`001_phase2_constraints_test.sql`) did pass on PostgreSQL 16 against that hand-written schema.
Value of keeping it: the constraints below are NOT in the current EF model and could be added to a migration (via `HasCheckConstraint` / filtered indexes):

- `SiteWallets.Balance >= 0`, `SiteWalletTransactions.Amount > 0`, `BalanceAfter >= 0`
- primary site must be active
- site-level SMS override requires an organization
- one platform-default SMS setting per provider
- platform role must not belong to an organization

The authoritative schema is the EF model plus migrations (generate with `dotnet ef migrations add Initial`).
