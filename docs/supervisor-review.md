# TallyVel: work to review

Each point names the problem, the fix, and links to the code that supports it. Line numbers are as of the latest commits. Links are relative to this file.

## 1. Security bug: a Member could record a contribution for another member

- **The fix:** [ContributionsController.cs:109-127](../Api/Presentation/Controllers/ContributionsController.cs#L109-L127).
  - The caller must be a member of the stokvel, otherwise 404 ([line 103](../Api/Presentation/Controllers/ContributionsController.cs#L103), unchanged).
  - If the target isn't the caller ([line 111](../Api/Presentation/Controllers/ContributionsController.cs#L111)), the caller needs the Treasurer or Chairperson role. This reuses the same policy as the cycle and payout endpoints, read from the database ([line 115](../Api/Presentation/Controllers/ContributionsController.cs#L115)). Otherwise the API returns 403 ([line 119](../Api/Presentation/Controllers/ContributionsController.cs#L119)).
  - A target who isn't in the stokvel gets 422 ([line 124](../Api/Presentation/Controllers/ContributionsController.cs#L124)).
- **Why a refused request keeps its key:** these checks run before the service reserves the Idempotency-Key ([ContributionServices.cs:54](../Api/Application/Services/ContributionServices.cs#L54)).
- **The policy it reuses:** [StokvelPolicies.cs:20](../Api/Authorization/StokvelPolicies.cs#L20).
- **OpenAPI:** the 403 response is declared at [line 96](../Api/Presentation/Controllers/ContributionsController.cs#L96), and the 422 at [line 99](../Api/Presentation/Controllers/ContributionsController.cs#L99).
- **Tests**, in [ContributionAuthorizationTests.cs](../Tests/Integration/ContributionAuthorizationTests.cs):
  - [Member for themselves gets 201](../Tests/Integration/ContributionAuthorizationTests.cs#L47)
  - [Member for another gets 403 and no row is created](../Tests/Integration/ContributionAuthorizationTests.cs#L58)
  - [Treasurer for another gets 201](../Tests/Integration/ContributionAuthorizationTests.cs#L75)
  - [Non-member gets 404](../Tests/Integration/ContributionAuthorizationTests.cs#L87)
  - [Target not in the stokvel gets 422](../Tests/Integration/ContributionAuthorizationTests.cs#L98)
  - [A 403 doesn't use up the key, so a Treasurer retry succeeds](../Tests/Integration/ContributionAuthorizationTests.cs#L113)

## 2. Error messages fit for people

- **Duplicate contribution (409):** the detail is now "A contribution has already been recorded for this member in cycle 2026-11." with no ids. It is set in two places: [ContributionServices.cs:96](../Api/Application/Services/ContributionServices.cs#L96) for the normal check, and [EfContributionRepository.cs:114](../Infrastructure/Persistence/EfContributionRepository.cs#L114) for the database race.
- **Missing Idempotency-Key (400):** [ContributionServices.cs:46](../Api/Application/Services/ContributionServices.cs#L46) now throws without a parameter name, so .NET adds no "(Parameter ...)" suffix.
- **Tests:**
  - [The 409 text, with no ids](../Tests/Integration/ContributionErrorMessageTests.cs#L23)
  - [The exact 400 text](../Tests/Integration/ContributionErrorMessageTests.cs#L42)
  - [Unit test for the missing key](../Tests/Unit/ContributionServiceTests.cs#L82)
  - [Unit test for the duplicate cycle](../Tests/Unit/ContributionServiceTests.cs#L117)

## 3. Next.js dashboard now matches the React app

- **App shell:** [AppShell.tsx:106](../web-next/src/components/AppShell.tsx#L106) is the green sidebar ([line 147](../web-next/src/components/AppShell.tsx#L147)) and sticky header ([line 203](../web-next/src/components/AppShell.tsx#L203)). Sign out is a form that calls the `signOut` Server Action ([line 260](../web-next/src/components/AppShell.tsx#L260)). It is mounted by [layout.tsx:10](../web-next/src/app/%28app%29/layout.tsx#L10), which redirects to login if there is no session ([line 7](../web-next/src/app/%28app%29/layout.tsx#L7)).
- **Member dashboard:** [page.tsx](../web-next/src/app/%28app%29/dashboard/member/page.tsx) shows `StokvelCard`s ([line 41](../web-next/src/app/%28app%29/dashboard/member/page.tsx#L41)). The contributions are fetched on the server ([line 28](../web-next/src/app/%28app%29/dashboard/member/page.tsx#L28)), and the picker is at [line 47](../web-next/src/app/%28app%29/dashboard/member/page.tsx#L47).
- **Picker:** [StokvelPicker.tsx:19](../web-next/src/components/StokvelPicker.tsx#L19) keeps the choice in the URL.
- **Treasurer dashboard:** [page.tsx:37](../web-next/src/app/%28app%29/dashboard/treasurer/page.tsx#L37) uses `StokvelCard`.
- **Contribution card:** [ContributionCard.tsx](../web-next/src/components/ContributionCard.tsx).

## 4. API check write-up for Assignment 8.2

- The six questions are in [web-next/README.md:38](../web-next/README.md#L38), one subsection each: [1](../web-next/README.md#L47), [2](../web-next/README.md#L53), [3](../web-next/README.md#L65), [4](../web-next/README.md#L71), [5](../web-next/README.md#L77), [6](../web-next/README.md#L97). Known gaps are at [line 108](../web-next/README.md#L108).
- **Scripts:** [refresh-check.mjs](../web-next/scripts/refresh-check.mjs) covers refresh and reuse. [clocks-check.mjs](../web-next/scripts/clocks-check.mjs) covers sliding versus fixed refresh tokens.

## Open items

- **Contributions on the Next dashboard:** reported as not working, and the cause hasn't been found.
- **Test suite:** a plain `dotnet test` fails today because of a seeder bug that predates this work (Sipho gets two rows in the 2026-11 cycle). The new tests passed only with a temporary seeder patch that was reverted.
- **"emailalready" typo:** it isn't in the repo, so there was nothing to fix, and the README doesn't claim a fix.
- **Pages the README describes but that don't exist yet:** `proxy.ts`, the register page, and "Recent contributions" on the stokvel detail page.
