# TallyVel

> Keep your stokvel on track, together.

## Overview

TallyVel is a simple, no-fuss way to keep track of your stokvel — no more spreadsheets, WhatsApp screenshots, or arguments over who paid what.

Log contributions as they come in, see who's up to date and who's behind, and keep a clear record of your group's payout rotation — all in one place that every member can check anytime.

TallyVel doesn't move or hold your money. It's purely a tracking and record-keeping tool, built to give your stokvel transparency and peace of mind, without the risk or complexity of a payments platform.

## Key Features

- 📋 Track contributions per member, per cycle
- ✅ See at a glance who's paid, pending, or missed
- 🔄 Manage and view your payout schedule/rotation
- 🧾 Keep a clear, shared history everyone can trust
- 👥 Built for how real stokvels actually run

## Who It's For

Whether it's a small savings circle with friends or a larger community stokvel, TallyVel keeps everyone on the same page.

## Running the Project

The API lives in `TallyVel.Api` and targets .NET 10.

### From the terminal

```bash
cd TallyVel.Api
dotnet run
```

### From VS Code

Open the **Run and Debug** panel (⇧⌘D on Mac / Ctrl+Shift+D on Windows/Linux), select **TallyVel.Api** from the dropdown, and press F5. This builds the project and starts it with the debugger attached.

### Viewing the Scalar UI

Once the app is running in development, open:

```
http://localhost:5203/scalar/v1
```

This gives you an interactive reference for every API endpoint — no separate setup required.

## Error Handling

Domain failures (a stokvel that doesn't exist, a member who's already contributed for a cycle, and so on) are represented as typed exceptions rather than status codes scattered through the controllers. Every one of them derives from `TallyVelException` (`TallyVel.Api/Domain/Common/Exceptions.cs`):

| Exception | Meaning |
|---|---|
| `NotFoundException` | The stokvel, user, member, or contribution referenced doesn't exist |
| `BusinessRuleViolationException` | The request is well-formed but breaks a domain rule (e.g. removing the last admin) |
| `AlreadyExistsException` | The thing being created already exists (e.g. already a member, already contributed for this cycle) |
| `IdempotencyKeyReusedException` | An `Idempotency-Key` was reused with a different request payload |
| `IdempotencyKeyInProgressException` | A request with that `Idempotency-Key` is still being processed |

Each carries a stable `Code` (e.g. `stokvel-not-found`) that identifies the failure independently of its HTTP status or message text.

### How a failure becomes an HTTP response

Controllers and services just `throw` (or `?? throw`) the exception that matches the failure — they don't catch it or build a `ProblemDetails` response themselves:

```csharp
var stokvel = _stokvelRepository.GetById(id)
    ?? throw new NotFoundException("stokvel", id);
```

`TallyVelExceptionHandler` (`TallyVel.Api/Domain/Common/TallyVelExceptionHandler.cs`), registered globally in `Program.cs` via `AddExceptionHandler` + `UseExceptionHandler`, is the **only** place that maps a failure type to a status code:

| Exception | Status |
|---|---|
| `NotFoundException` | 404 Not Found |
| `BusinessRuleViolationException`, `IdempotencyKeyReusedException` | 422 Unprocessable Entity |
| `AlreadyExistsException`, `IdempotencyKeyInProgressException` | 409 Conflict |

It turns the exception into a [`ProblemDetails`](https://datatracker.ietf.org/doc/html/rfc7807) response with the exception's `Code` echoed back in a `code` extension field and a `tag:` URI in `Type`, so clients can branch on the specific failure rather than parsing the message. Any exception that isn't a `TallyVelException` (e.g. `ArgumentException` for malformed input) is left to whatever local `try/catch` a controller still has, or falls through to a generic 500.

Adding a new failure means adding a new `TallyVelException` subclass **and** a case in `TallyVelExceptionHandler`'s mapping — the handler throws on purpose if a case is missing, so a new exception can't silently turn into a 500.



Assignment 5.1

## Getting Started (Clean Machine)

Follow these steps in order. They assume **nothing** is installed yet. At no point does a password get written into a file that git tracks.

### Prerequisites

| Tool | Why | How to check |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Builds and runs the API | `dotnet --version` → `10.x` |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Runs PostgreSQL in a container | `docker --version` and `docker compose version` |
| `dotnet-ef` CLI tool | Creates and applies EF Core migrations | `dotnet ef --version` |
| A PostgreSQL client (TablePlus, DBeaver, pgAdmin, or `psql`) | Proves the database is reachable independently of the API | — |

Install the EF Core CLI tool once per machine:

```bash
dotnet tool install --global dotnet-ef
```

> **Windows:** install Docker Desktop with the **WSL 2** backend and restart when asked.
> **macOS:** open Docker Desktop once after installing and wait for **"Engine running"** before using `docker` in the terminal.

### Step 1 — Clone the repository

```bash
git clone https://github.com/ArchipeSesanga/TallyVel.git
cd TallyVel
```

### Step 2 — Create your local `.env` file

The Postgres superuser password is read from a `.env` file that is **git-ignored**. A committed template, `.env.example`, shows what it needs.

```bash
cp .env.example .env
```

Open `.env` and set a real password:

```
POSTGRES_PASSWORD=your-strong-superuser-password
```

**Save the file**, then check that Docker Compose can actually see the value:

```bash
docker compose config | grep POSTGRES_PASSWORD
```

If the value shows up empty, `.env` wasn't saved or is in the wrong folder (see [Troubleshooting](#troubleshooting)).

### Step 3 — Start PostgreSQL

```bash
docker compose up -d
docker ps
docker logs tallyvel-pg
```

- `docker ps` should show `tallyvel-pg` with status **Up**. **Restarting** means something is wrong.
- The logs should end with `database system is ready to accept connections`.

`docker-compose.yml` pins **PostgreSQL 17**, creates a dedicated database called `tallyvel`, and stores data in a named volume (`tallyvel-pgdata`), so the data survives container restarts.

### Step 4 — Create the application user

The API does **not** connect as the `postgres` superuser. It uses a dedicated, least-privilege account called `tallyvel_app`.

Open a SQL shell inside the container:

```bash
docker exec -it tallyvel-pg psql -U postgres -d tallyvel
```

At the `tallyvel=#` prompt, run:

```sql
CREATE USER tallyvel_app WITH PASSWORD 'your-app-password';
ALTER DATABASE tallyvel OWNER TO tallyvel_app;
GRANT ALL ON SCHEMA public TO tallyvel_app;
```

Verify, then exit:

```sql
\du            -- tallyvel_app should be listed with no special attributes
\l tallyvel    -- Owner should be tallyvel_app
\q
```

> Use a **different** password from the superuser one in `.env`.

### Step 5 — Prove connectivity *from outside* the container

This step matters. The official Postgres image **trusts connections made inside the container without checking the password**, so a `docker exec … psql` test alone proves nothing about credentials. The API connects from the host over TCP, which **does** require the password, so test that exact path.

**Option A: GUI client** (TablePlus, DBeaver, or pgAdmin)

| Field | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| Database | `tallyvel` |
| User | `tallyvel_app` |
| Password | your app password |

**Option B: `psql` on the host**

```bash
# macOS: brew install libpq && brew link --force libpq
psql -h localhost -p 5432 -U tallyvel_app -d tallyvel
```

Then run:

```sql
SELECT current_database(), current_user, version();
```

Expected result: `tallyvel | tallyvel_app | PostgreSQL 17.x …`

<!-- TODO: add screenshot of successful client connection, e.g. docs/images/db-connection.png -->

### Step 6 — Give the API its connection string via User Secrets

The connection string (including the password) is stored with **.NET User Secrets**. It lives in your user profile folder, outside the repository, so it can never be committed by accident. `appsettings.json` contains **no** credentials.

```bash
cd TallyVel.Api
dotnet user-secrets set "ConnectionStrings:TallyVel" "Host=localhost;Port=5432;Database=tallyvel;Username=tallyvel_app;Password=postgres;Maximum Pool Size=20"
dotnet user-secrets list
```

Where the secrets are stored:
- **macOS/Linux:** `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`

> User Secrets only load when `ASPNETCORE_ENVIRONMENT=Development`, which is the default for `dotnet run` and the VS Code launch profile.

### Step 7 — Apply the database migrations

```bash
dotnet ef database update
```

Confirm the tables exist:

```bash
docker exec -it tallyvel-pg psql -U tallyvel_app -d tallyvel -c "\dt"
```

### Step 8 — Run the API

See [Running the Project](#running-the-project) below.

---

## Everyday Database Commands

| Task | Command |
|---|---|
| Start Postgres | `docker compose up -d` |
| Stop Postgres (keeps data) | `docker compose stop` |
| Check status | `docker ps` |
| View logs | `docker logs tallyvel-pg` |
| Open a SQL shell | `docker exec -it tallyvel-pg psql -U tallyvel_app -d tallyvel` |
| **Wipe everything and start fresh** | `docker compose down -v`, then repeat Steps 3–4 and 7 |

> ⚠️ The `POSTGRES_*` values in `.env` are only applied the **first time** the volume is initialised. Changing the password in `.env` later does nothing to an existing database unless you wipe it with `docker compose down -v`.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `WARN The "POSTGRES_PASSWORD" variable is not set` and the container shows **Restarting** | `.env` is missing, empty (0 bytes), or not in the same folder as `docker-compose.yml` | `ls -la` to check the file size, then `echo "POSTGRES_PASSWORD=…" > .env`, `docker compose down -v`, `docker compose up -d` |
| `port is already allocated` / `address already in use` on 5432 | A native PostgreSQL install is already using the port | Stop the native service, or change the mapping to `"5433:5432"` and use port `5433` in your connection string |
| `password authentication failed for user "tallyvel_app"` | Wrong password in User Secrets, or the user was never created | Re-run Step 4 or `dotnet user-secrets set …` with the correct password |
| `Cannot connect to the Docker daemon` | Docker Desktop isn't running | Open Docker Desktop and wait for "Engine running" |
| `dotnet ef` not found | EF CLI tool not installed / not on PATH | `dotnet tool install --global dotnet-ef`, then open a new terminal |

## Why PostgreSQL Runs in Docker

- **Reproducible:** `docker-compose.yml` is committed, so every developer gets the same PostgreSQL version and configuration from one command, whatever their OS.
- **Isolated:** nothing is installed system-wide, and a broken database can be reset in seconds with `docker compose down -v`.
- **Secret-safe:** the only credential Compose needs comes from a git-ignored `.env`. The committed `.env.example` documents it without exposing it.
- **Forward-looking:** later work uses Testcontainers for integration tests, which also relies on Docker.

---

## Running the Project

The API lives in `TallyVel.Api` and targets .NET 10. **PostgreSQL must be running first** (`docker compose up -d`).

### From the terminal

```bash
cd TallyVel.Api
dotnet run
```

### From VS Code

Open the **Run and Debug** panel (⇧⌘D on Mac / Ctrl+Shift+D on Windows/Linux), select **TallyVel.Api** from the dropdown, and press F5. This builds the project and starts it with the debugger attached.

### Viewing the Scalar UI

Once the app is running in development, open:

```
http://localhost:5203/scalar/v1
```

This gives you an interactive reference for every API endpoint — no separate setup required.

## Error Handling

Domain failures (a stokvel that doesn't exist, a member who's already contributed for a cycle, and so on) are represented as typed exceptions rather than status codes scattered through the controllers. Every one of them derives from `TallyVelException` (`TallyVel.Api/Domain/Common/Exceptions.cs`):

| Exception | Meaning |
|---|---|
| `NotFoundException` | The stokvel, user, member, or contribution referenced doesn't exist |
| `BusinessRuleViolationException` | The request is well-formed but breaks a domain rule (e.g. removing the last admin) |
| `AlreadyExistsException` | The thing being created already exists (e.g. already a member, already contributed for this cycle) |
| `IdempotencyKeyReusedException` | An `Idempotency-Key` was reused with a different request payload |
| `IdempotencyKeyInProgressException` | A request with that `Idempotency-Key` is still being processed |

Each carries a stable `Code` (e.g. `stokvel-not-found`) that identifies the failure independently of its HTTP status or message text.

### How a failure becomes an HTTP response

Controllers and services just `throw` (or `?? throw`) the exception that matches the failure — they don't catch it or build a `ProblemDetails` response themselves:

```csharp
var stokvel = _stokvelRepository.GetById(id)
    ?? throw new NotFoundException("stokvel", id);
```

`TallyVelExceptionHandler` (`TallyVel.Api/Domain/Common/TallyVelExceptionHandler.cs`), registered globally in `Program.cs` via `AddExceptionHandler` + `UseExceptionHandler`, is the **only** place that maps a failure type to a status code:

| Exception | Status |
|---|---|
| `NotFoundException` | 404 Not Found |
| `BusinessRuleViolationException`, `IdempotencyKeyReusedException` | 422 Unprocessable Entity |
| `AlreadyExistsException`, `IdempotencyKeyInProgressException` | 409 Conflict |

It turns the exception into a [`ProblemDetails`](https://datatracker.ietf.org/doc/html/rfc7807) response with the exception's `Code` echoed back in a `code` extension field and a `tag:` URI in `Type`, so clients can branch on the specific failure rather than parsing the message. Any exception that isn't a `TallyVelException` (e.g. `ArgumentException` for malformed input) is left to whatever local `try/catch` a controller still has, or falls through to a generic 500.

Adding a new failure means adding a new `TallyVelException` subclass **and** a case in `TallyVelExceptionHandler`'s mapping — the handler throws on purpose if a case is missing, so a new exception can't silently turn into a 500.

---

## Persistence with EF Core & PostgreSQL (Assignment 5.1)

> Sections marked **TODO** get filled in as each part of the work is completed. Nothing here is claimed until it's actually done and verified.

### Secret management

- The Postgres superuser password lives only in `.env` (git-ignored). `.env.example` is the committed template.
- The API connection string lives only in **User Secrets** (Step 6), outside the repo.
- `appsettings.json` and `appsettings.Development.json` contain no credentials.
- Checked with `git check-ignore .env` and by searching history: `git log -p | grep -i password`.

### Schema and the property that didn't map cleanly

**TODO:** which entity/property EF Core rejected, the exact error, why it happened, and the decision (ignore with reason vs. fix the shape).

### First migration: what I checked

**TODO:** column types (money as `numeric(18,2)`), nullability, foreign keys and `onDelete` behaviour, unique indexes backing business rules, enum storage, and how a rename can be generated as drop + add (data loss) and how I'd catch and fix it.

### Npgsql pooling and retry

**TODO:** chosen `maxRetryCount` and `maxRetryDelay` and the reasoning behind them, one failure it should retry, one it deliberately shouldn't.

### Repository swapped to EF Core

**TODO:** which repository and why; the Singleton → Scoped lifetime change and what would break with a Singleton holding a `DbContext`; confirmation that no controller, service, or DTO changed.

### Test suite before and after the swap

**TODO:** test output before and after, confirmation it ran against the real PostgreSQL instance, and anything that went red and what it exposed. Note: until Testcontainers is introduced, the integration tests depend on this same local database.

### Payout processing and the explicit transaction

**TODO:** rotation rule, scope decisions, the writes the transaction protects, the endpoint, and the rollback test result (verified by re-querying).

### Definition of Done (extended)

**TODO:** existing table plus **Persisted via EF Core** (yes/no) and **Explicit transaction tested** (yes/no/N-A).

### Known gaps not closed yet

**TODO**

Assignement 5.2
================

Why a composite key on StokvelMember?
A membership is the pair (user, stokvel) — a user can't belong to the same stokvel twice, so that pair is already unique and meaningful. A surrogate Guid Id would be a second identity nobody uses, and it wouldn't stop duplicate memberships unless I also added a unique index on (UserId, StokvelId) — at which point the pair is the real key anyway. The composite PK enforces "one membership per user per stokvel" at the database level for free.

How Contribution and Payout reference a membership
I used a composite foreign key (UserId, StokvelId) → StokvelMember. This makes the database reject a contribution from someone who isn't a member of that stokvel.
I didn't reference UserId alone, because that moves the "must be a member" rule out of the database and into code that could be bypassed.
I didn't add a surrogate alternate key, because it brings back the synthetic identity the composite key was meant to avoid.
Trade-off: Contribution now carries StokvelId alongside CycleId, which could in theory disagree with the cycle's stokvel. I validate that in the service for now; enforcing it in the schema is a gap I've noted.

---

## Assignment 5.3 — Paging and query plans

### Query plan measurement

**What I measured:** the keyset-paged contributions listing (`GET /api/stokvels/{stokvelId}/contributions?cycle=<label>&pageSize=20`), before and after adding one composite index.

#### Seeded volume and the separate database

I measured on a separate database, `tallyvel_perf`, so the dev database `tallyvel` was never loaded with test data. The seeder (`VolumeSeeder`, run only with `--seed-volume`, never on normal startup) creates **5 stokvels × 50 members × 48 cycles = 12,000 contributions** (plus 250 users, 250 memberships and 240 cycles). It uses a fixed random seed (42) so it is repeatable, spreads `RecordedAt` across each cycle's own month, respects "one contribution per member per cycle", and ends with `ANALYZE "Contributions"`. I checked with SQL that the count is exactly 12,000, that every contributor is a member of the stokvel, and that no `RecordedAt` falls outside its cycle's month.

`tallyvel_app` can't create databases, so the perf database was created by the Postgres superuser and then handed over:

```bash
docker exec tallyvel-pg psql -U postgres -c 'ALTER DATABASE tallyvel_perf OWNER TO tallyvel_app;'
docker exec tallyvel-pg psql -U postgres -d tallyvel_perf -c 'ALTER SCHEMA public OWNER TO tallyvel_app;'
```

Then, with the perf connection string (`Host=localhost;Port=5433;Database=tallyvel_perf;Username=tallyvel_app;Password=***;Maximum Pool Size=20`):

```bash
dotnet ef database update --connection "<perf connection string>"
dotnet run -- --seed-volume --ConnectionStrings:TallyVel="<perf connection string>"
```

(I actually passed the string as the `ConnectionStrings__TallyVel` environment variable so the password never reached the shell history or the screen; it is equivalent.)

**Safeguards.** `VolumeSeeder` refuses to run unless the database name ends in `_perf`; I tested that it refuses against `tallyvel`. For every `ef` and `run` command against perf I used a shell check that the connection string contains `Database=tallyvel_perf`, and aborts otherwise. I added it after a mistyped substitution once pointed an `ef database update` at the dev database (it was a no-op because dev was already up to date, but it showed the risk).

#### The SQL measured (as logged by EF Core)

Page 1 (`@cycle = '2025-06'`, `@p = 21`, i.e. page size 20 + 1 to detect another page):

```sql
SELECT c."Id", c."Amount", c."Cycle", c."MemberUserId", c."RecordedAt", c."StokvelId"
FROM "Contributions" AS c
WHERE c."StokvelId" = @q_StokvelId AND c."Cycle" = @cycle
ORDER BY c."RecordedAt", c."Id"
LIMIT @p
```

Page 2 adds the keyset condition from the previous page's last row:

```sql
SELECT c."Id", c."Amount", c."Cycle", c."MemberUserId", c."RecordedAt", c."StokvelId"
FROM "Contributions" AS c
WHERE c."StokvelId" = @q_StokvelId AND c."Cycle" = @cycle
  AND (c."RecordedAt", c."Id") > (@t_LastRecordedAt_Value, @t_LastId)
ORDER BY c."RecordedAt", c."Id"
LIMIT @p
```

To run it by hand I substituted only the parameter values. Plans were captured with `EXPLAIN (ANALYZE, BUFFERS)`, three runs each; I report the third (warm) run. Full output is in `docs/explain/`.

#### Before the index (`docs/explain/before.txt`)

```
 Limit  (cost=109.60..109.65 rows=21 width=69) (actual time=0.074..0.077 rows=21 loops=1)
   Buffers: shared hit=33
   ->  Sort  (cost=109.60..109.72 rows=50 width=69) (actual time=0.073..0.075 rows=21 loops=1)
         Sort Key: "RecordedAt", "Id"
         Sort Method: top-N heapsort  Memory: 29kB
         Buffers: shared hit=33
         ->  Bitmap Heap Scan on "Contributions" c  (cost=4.80..108.25 rows=50 width=69) (actual time=0.023..0.053 rows=50 loops=1)
               Recheck Cond: (("StokvelId" = 'a76414e6-754c-4655-b5db-93d39e0a1c25'::uuid) AND (("Cycle")::text = '2025-06'::text))
               Heap Blocks: exact=25
               Buffers: shared hit=27
               ->  Bitmap Index Scan on "IX_Contributions_StokvelId_Cycle"  (cost=0.00..4.79 rows=50 width=0) (actual time=0.015..0.015 rows=50 loops=1)
                     Index Cond: (("StokvelId" = 'a76414e6-754c-4655-b5db-93d39e0a1c25'::uuid) AND (("Cycle")::text = '2025-06'::text))
                     Buffers: shared hit=2
 Planning:
   Buffers: shared hit=178
 Planning Time: 0.253 ms
 Execution Time: 0.098 ms
(17 rows)
```

| | |
|---|---|
| Node types | Limit → Sort (top-N heapsort) → Bitmap Heap Scan → Bitmap Index Scan |
| Index used | `IX_Contributions_StokvelId_Cycle` (the helper index EF created for the cycle foreign key) |
| Execution time | 0.098 ms (planning 0.253 ms) |
| Estimated vs actual rows | scan: 50 est / 50 actual; Limit: 21 / 21 |
| Rows Removed by Filter | none |
| Buffers | 33 |

**This was not a Seq Scan.** The planner used the existing `(StokvelId, Cycle)` index to find the 50 rows of the cycle and then sorted them. So the index found the rows well. What was missing was ordering.

#### After the index (`docs/explain/after.txt`)

```
 Limit  (cost=0.29..58.77 rows=21 width=69) (actual time=0.036..0.072 rows=21 loops=1)
   Buffers: shared hit=23
   ->  Index Scan using "IX_Contributions_StokvelId_Cycle_RecordedAt_Id" on "Contributions" c  (cost=0.29..139.54 rows=50 width=69) (actual time=0.035..0.069 rows=21 loops=1)
         Index Cond: (("StokvelId" = 'a76414e6-754c-4655-b5db-93d39e0a1c25'::uuid) AND (("Cycle")::text = '2025-06'::text))
         Buffers: shared hit=23
 Planning:
   Buffers: shared hit=190
 Planning Time: 0.450 ms
 Execution Time: 0.084 ms
(9 rows)
```

| | |
|---|---|
| Node types | Limit → Index Scan |
| Index used | `IX_Contributions_StokvelId_Cycle_RecordedAt_Id` |
| Execution time | 0.084 ms (planning 0.450 ms) |
| Estimated vs actual rows | scan: 50 est / 21 actual (it stopped early); Limit: 21 / 21 |
| Rows Removed by Filter | none |
| Buffers | 23 |

#### Comparison

The Sort node is gone and `LIMIT` now stops the scan after 21 rows, so it reads 23 buffers instead of 33. But at only 50 rows per cycle the time saved is tiny (0.098 ms → 0.084 ms, within run-to-run noise). The benefit scales with the number of rows *in one cycle*, not the total number of rows in the table.

**Page 2.** Before the index, the keyset condition was applied as a filter after the bitmap scan (20 rows removed by filter) and then sorted: 0.096 ms, 33 buffers (`docs/explain/before-page2.txt`). After the index the condition becomes part of the index condition (nothing removed by filter), but the planner **still chose a Bitmap Heap Scan plus a Sort** of about 30 rows: 0.088 ms, 28 buffers (`docs/explain/after-page2.txt`). To check that this was the planner's choice and not a limit of the index, I ran a diagnostic with `SET enable_bitmapscan = off` for that session only: it then used a plain ordered Index Scan with no Sort, in 0.052 ms and 22 buffers (`docs/explain/after-page2-diagnostic.txt`). The two plans were close in estimated cost (58.6 vs 61.4), and the row estimate was 19 against an actual 30. So I can't claim that every page avoids the Sort at this size.

#### Why this column order

`(StokvelId, Cycle, RecordedAt, Id)`:

- **`StokvelId`, `Cycle` first:** these are equality filters, so the scan jumps straight to one stokvel's one cycle.
- **`RecordedAt`, `Id` next:** inside that one cycle the entries are already stored in `ORDER BY "RecordedAt", "Id"` order. That's why the Sort disappears and `LIMIT` can stop early.
- **`Id` last:** it is the tiebreaker that makes the order deterministic, and it matches the keyset row comparison `("RecordedAt", "Id") > (...)`.

The existing unique index `(StokvelId, MemberUserId, Cycle)` can't serve this query: `MemberUserId` sits between `StokvelId` and `Cycle`, and the query doesn't filter on the member, so Postgres can't use `Cycle` (or any later column) to narrow the scan. It also can't give ordering by `RecordedAt`.

#### Migration review (`AddContributionPagingIndex`)

EF generated a `DropIndex` of `IX_Contributions_StokvelId_Cycle` as well as the `CreateIndex`. EF does this because the new composite index has the same leading columns `(StokvelId, Cycle)`, so the old helper index became redundant (the foreign-key lookups it served are covered by the new index's prefix). Keeping both would only add write cost. I checked that the migration drops no columns or tables, so no data is lost, and that `Down` recreates the old index (I proved it by rolling the perf database back and forward). A `CREATE INDEX` blocks writes to the table while it builds. That's fine at this size, but on a large production table I would use `CREATE INDEX CONCURRENTLY`, which EF doesn't generate by default. It was applied to `tallyvel_perf` first and then to `tallyvel`.

#### Small data (`docs/explain/small-data.txt`)

On the dev database (6 contributions), with ids that exist there:

```
 Limit  (cost=1.10..1.10 rows=1 width=134) (actual time=0.027..0.028 rows=3 loops=1)
   Buffers: shared hit=7
   ->  Sort  (cost=1.10..1.10 rows=1 width=134) (actual time=0.026..0.027 rows=3 loops=1)
         Sort Key: "RecordedAt", "Id"
         Sort Method: quicksort  Memory: 25kB
         Buffers: shared hit=7
         ->  Seq Scan on "Contributions" c  (cost=0.00..1.09 rows=1 width=134) (actual time=0.005..0.006 rows=3 loops=1)
               Filter: (("StokvelId" = '30d8a9e8-727e-41d2-b828-5d2daf5cb2c1'::uuid) AND (("Cycle")::text = '2026-09'::text))
               Rows Removed by Filter: 3
               Buffers: shared hit=1
 Planning:
   Buffers: shared hit=154
 Planning Time: 0.259 ms
 Execution Time: 0.046 ms
(14 rows)
```

PostgreSQL chose a **Seq Scan** even though the new index exists: it read one page and discarded 3 rows (Rows Removed by Filter: 3), then sorted the 3 matches in 0.046 ms. That is the right choice. The whole table fits in a page or two, so reading it directly costs less than going through the index and then fetching the same page. (The row estimate was 1 against an actual 3, because dev statistics were never gathered; it doesn't change the decision.) Indexes pay off as tables grow, not on tiny ones.

#### Measured gap: the stokvel-wide listing

Without a `cycle` filter (`?pageSize=20`) the new index can't help, because `Cycle` sits between `StokvelId` and `RecordedAt`, so entries for a stokvel are not in `RecordedAt` order. I measured it before and after (`docs/explain/stokvel-wide.txt`): both plans are a Bitmap scan feeding a top-N Sort over 2,400 rows, 0.633 ms before and 0.672 ms after, 40 and 58 buffers. It is no faster, and marginally more expensive because the wider index is bigger to scan. I did not add another index for it. A `(StokvelId, RecordedAt, Id)` index would serve it.

#### Gaps and caveats

- The assignment's `/cycles/{cycleId}/contributions` route does not exist; the cycle filter is the `?cycle=<label>` query parameter on the stokvel endpoint.
- These are single-machine, local-Docker timings on a small table. Differences under about 0.05 ms are noise; the plan shapes (Sort gone, early stop, buffers) are the reliable result.
