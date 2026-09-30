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