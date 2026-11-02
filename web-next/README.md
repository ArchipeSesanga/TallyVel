This is a [Next.js](https://nextjs.org) project bootstrapped with [`create-next-app`](https://nextjs.org/docs/app/api-reference/cli/create-next-app).

## Getting Started

First, run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

You can start editing the page by modifying `app/page.tsx`. The page auto-updates as you edit the file.

This project uses [`next/font`](https://nextjs.org/docs/app/building-your-application/optimizing/fonts) to automatically optimize and load [Geist](https://vercel.com/font), a new font family for Vercel.

## Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

You can check out [the Next.js GitHub repository](https://github.com/vercel/next.js) - your feedback and contributions are welcome!

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.

## API check

What the running API does, checked by hand with `curl` and two Node scripts in `scripts/`. Both sign in as `lerato@tallyvel.test`. Run them from `web-next/`:

```bash
DEMO_PASSWORD=... node scripts/refresh-check.mjs   # refresh and reuse (question 1)
DEMO_PASSWORD=... node scripts/clocks-check.mjs    # sliding or fixed refresh token (question 2)
```

### 1. What happens when a refresh token is used twice?

`POST /api/v1/auth/refresh` with `Cookie: tallyvel_rt=<A>` returns 200, a new access token in the body, and a new refresh token B in `Set-Cookie` (`path=/api/v1/auth`, HttpOnly, Secure, SameSite=Strict). Sending A a second time returns 401, and the API also revokes the whole token family, so B then gets 401 too.

**Why the frontend needs it:** two requests that refresh with the same token at the same time would sign the person out. The refresh in `proxy.ts` must never run twice with the same token (Part 6, The race).

### 2. How long does each of the three clocks last?

| Clock | Lifetime |
|---|---|
| Access token | 15 minutes |
| Refresh token | 7 days, sliding: each refresh issues a new token with a fresh 7 days |
| Session cookie | 7 days |

`clocks-check.mjs` measured it: refreshing 5 seconds after sign-in gave a token B that expires 5 seconds after A.

**Why the frontend needs it:** the refresh token decides when someone must sign in again, which is after 7 days without using RondiTrack. `proxy.ts` rewrites the session with a fresh 7 days on every refresh, so the browser never keeps a session the API would refuse. On a shared phone an active session can last indefinitely, so members must press Sign out, which revokes the refresh token at the API.

### 3. What does registration return, and what does it give away?

`POST /api/v1/auth/register` returns 201 with `{ id, email, fullName }`, never the password, and does not sign the person in. Bad input returns 400 with one message per field (`email`, `fullName`, `password`; passwords need at least 12 characters). An email that already has an account returns 409, which reveals that the email has an account (account enumeration).

**Why the frontend needs it:** the register page never shows the 409 detail. It says "We couldn't create an account with those details. If you already have one, sign in." Fully hiding it needs email verification (see Known gaps). The 409 `detail` currently reads "A user with this email already exists."

### 4. How do I get the latest contributions?

`GET /api/v1/stokvels/{id}/contributions?pageSize=5&sort=-recordedAt` returns at most 5 items, newest first (`-` means descending). `nextPageToken` is a token for the next page, and `""` on the last page. Checked on Family Stokvel as Lerato with `pageSize=2`: 2 items came back with a non-empty `nextPageToken`, and `pageSize=100` returned all 6 items with `nextPageToken: ""`. Paging works.

**Why the frontend needs it:** "Recent contributions" on the detail page makes exactly this one small request. Items include `memberUserId`, which the page never shows and never passes to a Client Component.

### 5. Who may record a contribution for whom?

A non-member gets 404, so the stokvel's existence isn't revealed. Checking this found a security bug: an ordinary Member could record a contribution for another member and got 201. Fixed in the API:

- Recording for yourself is allowed for any role.
- Recording for someone else needs this stokvel's Treasurer or Chairperson role, checked against the database. Otherwise the API returns 403 "Only this stokvel's Treasurer or Chairperson can record a contribution for another member."
- A member who isn't in the stokvel gets 422.
- The check runs before the idempotency key is used, so a refused request doesn't use up its key.

Tests (`Tests/Integration/ContributionAuthorizationTests.cs`):

- `A_member_recording_for_themselves_gets_201`
- `A_member_recording_for_another_member_gets_403_and_no_contribution_is_created`
- `A_treasurer_recording_for_another_member_gets_201`
- `A_non_member_of_the_stokvel_gets_404`
- `Recording_for_a_user_who_is_not_a_member_of_this_stokvel_gets_422`
- `A_refused_403_does_not_use_up_the_idempotency_key_so_a_treasurer_retry_with_it_succeeds`

**Why the frontend needs it:** the member picker in the page is a convenience. The API decides.

### 6. How does idempotency behave?

Unchanged since 7.4:

- The same key and body replays the original 201: same `id`, same `recordedAt`, nothing new recorded. There is no replay header, so the page can't say "already recorded".
- The same key with a different amount returns 422 `idempotency-key-reused`.
- A missing key returns 400 "An Idempotency-Key header is required."
- A new key for a member who already paid this cycle returns 409 `contribution-already-recorded`.

**Why the frontend needs it:** the Server Action keeps the key after an unknown outcome (no answer, a timeout), and makes a new key after a success or a changed amount.

## The race

The API rotates refresh tokens and treats a reused one as theft: it revokes the whole token family. When several requests arrive at the Next.js server together with a nearly expired access token, each one would call `/auth/refresh` with the SAME refresh token. One wins, the others are refused, and the person is signed out.

**The fix** is in `src/lib/sharedRefresh.ts`, the only place the app refreshes (`proxy.ts` calls `refreshSession` and nothing else does):

- A `Map` from refresh token to the in-flight refresh, so concurrent callers with the same token await ONE request.
- A finished result is kept for 30 seconds under the OLD token, because a request that arrives just after the refresh still carries the old cookie (the browser has not received the new one yet). Expired entries are removed on every call.
- A refused refresh is shared and remembered like a good one, so no caller retries it. An unreachable API is shared while the request is running but is not remembered, so the next request tries again.
- The map is only in this server's memory. Tokens are never logged or put in a response.

**The test** (`src/lib/sharedRefresh.test.ts`, no running API: `fetch` is stubbed with a 20 ms delay so the calls really overlap) checks that five concurrent calls make exactly one request and get the same new session, that a call 1 s later with the old token gets the stored result without a new request, that the result is forgotten after 30 s, and that a refused refresh is shared and not retried.

With the sharing in place:

```
RUN  v5.0.5 /Users/archipesesanga/Development/TallyVel/web-next


 Test Files  1 passed (1)
      Tests  6 passed (6)
   Start at  23:28:47
   Duration  388ms (tests 78%, transform 18%, import 3%, worker 1%)
```

With the sharing removed (`refreshSession` calling the API directly), the same test fails:

```
 RUN  v5.0.5 /Users/archipesesanga/Development/TallyVel/web-next
 ❯ src/lib/sharedRefresh.test.ts (6 tests | 4 failed) 249ms
     × five concurrent calls with the same token make exactly one request and share the new session 54ms
     × a call made 1 second after the first finished, with the old token, gets the stored result without a new request 42ms
     × a refused refresh is shared too, and no caller retries it 43ms
     × an unreachable API is shared while in flight but not remembered, so the next request tries again 43ms
⎯⎯⎯⎯⎯⎯⎯ Failed Tests 4 ⎯⎯⎯⎯⎯⎯⎯
AssertionError: expected "vi.fn()" to be called 1 times, but got 5 times
AssertionError: expected "vi.fn()" to be called 1 times, but got 2 times
AssertionError: expected "vi.fn()" to be called 1 times, but got 6 times
AssertionError: expected "vi.fn()" to be called 2 times, but got 4 times
 Test Files  1 failed (1)
      Tests  4 failed | 2 passed (6)
```

(The sharing is back in the code. The failing run was done once to capture this output.)

**What this fix handles**

- Several requests reaching ONE Next.js server at the same time with the same token: one refresh, everyone gets the same new session.
- A request that arrives up to 30 seconds after the refresh finished but still carries the old cookie.
- A refused refresh: it is not retried by every caller.

**What it does not handle**

- **Several Next.js servers.** Each has its own memory and its own map, so two servers can still refresh with the same token at once, and the API will see a reuse. That needs a grace window in the API (accept the previous token for a few seconds and answer with the same successor) or a store shared by the servers. The API's reuse detection is unchanged on purpose.
- **A request that left before another tab's refresh came back.** A tab can send a request with the old cookie, and the request can reach the server later than 30 seconds after the refresh (a slow network, a sleeping laptop). The shared result is gone by then, so it refreshes with a used token and the API revokes the family. A longer window would shrink this but makes the next point worse.
- **A lost answer.** If the API rotates the token but the response never reaches the server (a network error), nothing is remembered, and the next request retries with a token the API has already used.
- **What the window costs.** For 30 seconds after a refresh, anyone who sends the OLD session cookie to this server gets the NEW session back from memory instead of a refusal. A thief replaying an old copy of the cookie in that window would not set off the API's reuse detection and would receive working tokens. After the window the normal reuse detection applies again. 30 seconds is a trade between this and the lost-race case above.

## Record a contribution

On `/stokvels/[id]`, while the stokvel's current cycle is Open, a form records a contribution. It is a small client component (`src/components/ContributionForm.tsx`, `useActionState`) backed by the `recordContribution` Server Action in `src/app/actions.ts`. The action checks, in order: the session, then a Zod schema (`src/lib/contributionSchema.ts`), then the API (`POST /api/v1/stokvels/<built-in function id>/contributions` with the `Idempotency-Key` header).

- **Amount.** The person types rands ("150.50"). `parseRandsToCents` (`src/lib/moneyInput.ts`, with its tests) turns it into whole cents with string logic only, and refuses "150.505". The field is prefilled with what the member still owes, from `/me/home`.
- **Who it is for.** Everyone can record for themselves. Someone who is Treasurer or Chairperson in THIS stokvel (the role comes from this stokvel's entry in `/me/home`, never from the session) also gets a picker of the stokvel's members. The picker shows names, so the stokvel endpoint now returns each member's `fullName` (an API change with a test, `The_detail_lists_each_member_with_their_full_name_and_role`). The picker is a convenience: the API decides, and answers 403 to anyone else.
- **Errors are returned as state, never thrown.** 400: a message under each field. 409, 422 and 403: the API's `detail` at the top (a 403 never signs anyone out). 429: "Try again in N seconds." from `Retry-After`. No answer or a 5xx: "This payment may have gone through. Trying again is safe." 401: back to sign in. Every API error shows `Reference: <correlationId>`. The form keeps what was typed after any failure.
- **One click, one request.** The button is disabled while the request runs.

**The Idempotency-Key** is a hidden field the server writes. The page renders the first key, and the action's returned state carries the key for the next attempt:

| What happened | Key for the next attempt |
|---|---|
| Form opened, or the last attempt succeeded (a replayed 201 counts) | NEW key |
| No response, a 5xx, or a 429 | SAME key |
| The person changed the amount or the member | NEW key (the API answers 422 to a reused key with a different body) |
| 400, 409 or 422, then sent again unchanged | SAME key |

Why the same key after a 400, 409 or 422: the API releases the key whenever a request fails (`ContributionServices` releases it in its `catch`), and the refusal is about the data, not the key. Sending the same body again is the same request, so the same key is correct, and if the person fixes the amount the rule above gives a new key. The action remembers the body each key was last sent with (`keyBody` in the form state) to see whether it changed.

**After a success** the action calls `refresh()` from `next/cache`, and the form says who, how much and which cycle. I recorded a contribution once WITHOUT `refresh()` first: the confirmation appeared, but the "Current cycle" line on the same page stayed at "R 0,00 collected of R 500,00" until the page was reloaded (then "R 150,50 collected"). With `refresh()` the line changes straight away, with no reload.

Checked in a browser against the running API: "150.505" shows one message under the amount and keeps what was typed; a successful record shows the confirmation and a new key; recording again for the same member and cycle shows the API's 409 with a reference and keeps the key; a double click on the button records one contribution.

## Known gaps

- Registration reveals whether an email has an account (409); hiding it needs email verification.
- No replay header on idempotent replays.
- Idempotency keys are stored in memory in the API, so an API restart forgets them (from Assignment 6.4).
- All sign-ins now come from the Next.js server's IP, so the per-IP login rate limit is shared by everyone (from 8.1).
