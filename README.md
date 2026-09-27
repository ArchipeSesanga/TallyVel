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