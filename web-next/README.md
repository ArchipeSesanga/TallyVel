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

## API check (8.3)

What the running API does for this assignment, checked as Lerato against the rebuilt API on :8080 (the seeded Family Stokvel and Work Stokvel). The 8.2 API check above is unchanged.

### 1. What can the contribution list be filtered by, and how does paging work?

`GET /api/v1/stokvels/{id}/contributions` takes `memberUserId`, `from`, `to`, `cycle`, `minAmountCents`, `maxAmountCents` and `sort` (`recordedAt`, `-recordedAt`, `amount`, `-amount`; the `-` means descending). Filtering, sorting and paging all run in the database. `pageSize` defaults to 20 and is capped at 100. A response is `{ items, nextPageToken }`; `nextPageToken` is an opaque string, and it is **`""` (an empty string, not `null`) on the last page**. `pageSize=2&sort=-recordedAt` returned 2 items, newest first, with a 268-character token. `pageSize=100` returned everything with `nextPageToken: ""`.

### 2. What exactly do `from` and `to` take, and how are they compared?

They take UTC instants in ISO 8601 that **end in `Z`** (`2026-10-31T22:00:00Z`, fractional seconds allowed). An offset (`+02:00`), a bare date or a local time is refused, because the API would have to guess the time zone. They are compared with `recordedAt` in the database as a **half-open range: `from` is included, `to` is excluded**, so one range ends exactly where the next begins. Checked on the seeded "midnight" contribution, which was recorded at exactly `2026-10-31T22:30:00Z`:

| Request | Result |
|---|---|
| `from=2026-10-31T00:00:00Z&to=2026-10-31T22:30:00Z` (`to` is that instant) | 0 items: excluded |
| `from=2026-10-31T22:30:00Z&to=2026-11-02T00:00:00Z` (`from` is that instant) | 2 items, the first at `2026-10-31T22:30:00Z`: included |

Tests: `ContributionRangeTests.From_is_included_and_to_is_excluded_so_a_contribution_exactly_at_to_is_left_out` (a row one second before `to` is in, a row exactly at `to` is out, a row exactly at `from` is in).

### 3. What does the activity feed return?

`GET /api/v1/me/activity?pageSize=&pageToken=` returns what happened in **only the stokvels the caller belongs to**, newest first: contributions recorded, payouts made and members joined, as one list that the database orders and pages. Each item has `id` (unique, opaque), `kind` (`ContributionRecorded`, `PayoutMade` or `MemberJoined`), `stokvelId`, `stokvelName`, `occurredAt` (UTC, ends in `Z`), `amountCents` (an integer, or `null` when no money is involved) and `personName` (the member the line is about; `null` once they are no longer in that stokvel). The last page's token is **`""`**, the same as the contribution list. With `pageSize=4` Lerato's 19 events came back in 5 pages with no row repeated and the last token `""`; only Family Stokvel and Work Stokvel appeared. Tests: `ActivityFeedTests` (only my stokvels, newest first, every kind, a payout, paging that neither repeats nor skips, a malformed token, no token).

### 4. What happens when a page token is used with a different filter?

A token remembers the filter and sort it was made under. Used under another one, the API answers **400 problem+json with `code: "page-token-filter-mismatch"`** and tells the client to start again from the first page. A token that is not a token at all gets a different code, `invalid-pagetoken`.

```
GET /api/v1/stokvels/b12b…/contributions?pageSize=1&sort=-recordedAt&from=2026-10-01T00:00:00Z&pageToken=<token made WITHOUT from>

HTTP 400  {"type":"tag:tallyvel,2026:problem/page-token-filter-mismatch","title":"Invalid Query","status":400,
           "detail":"This page token was issued for a different filter or sort. Start again from the first page.",
           "code":"page-token-filter-mismatch","correlationId":"052a194e…"}
```

Tests: `A_page_token_sent_with_a_different_filter_gets_400_with_the_mismatch_code` and `A_malformed_page_token_gets_400_with_its_own_code_not_the_mismatch_one`.

### 5. What happens with a member who is not in the stokvel, or a malformed date?

Both are **400 problem+json**, the same status as other bad input:

```
GET …/contributions?memberUserId=00000000-0000-0000-0000-000000000001
HTTP 400  {"code":"invalid-memberuserid","detail":"That member does not belong to this stokvel.", …}

GET …/contributions?from=2026-02-30
HTTP 400  {"code":"invalid-from","detail":"'from' must be a UTC time in ISO 8601 ending in Z, for example 2026-10-31T22:00:00Z.", …}
```

The member message is the same whether that person exists in another stokvel or nowhere, so it never says anything about other stokvels' members (the test checks the other stokvel's member id does not appear in the answer). `from` equal to or after `to` is also a 400. Tests: `A_member_who_is_not_in_this_stokvel_gets_400_even_if_they_exist_in_another_one`, `A_malformed_instant_gets_400`, `From_that_is_not_earlier_than_to_gets_400`.

## Where each piece of state lives

| State | Lives in | Why there |
|---|---|---|
| The filters: member, from, to, order | **The URL** (`?member=&from=&to=&sort=`, nuqs) | A reload, a shared link and the Back button must show the same list, and the server needs the filter to render page one. |
| Page one of the history and of the activity feed | **The server** (a Server Component renders it into the HTML) | It is API data and must be fresh on every visit; it is rendered where the session and the token already are. |
| Pages after page one | **TanStack Query's cache**, in the browser | It is server data that the browser asks for later, one page at a time, and it needs loading, error and cache handling. The key holds every filter value and the token page one ended with. |
| "Has the person asked for more yet?" | Component state (`useState`) | It only matters to this one list while it is on screen. |
| "The server is rendering the new list" | Component state (`useTransition`) | A short-lived flag for one control. |
| The contribution form: what was typed, the errors, the Idempotency-Key, the confirmation | Component state from `useActionState` | It belongs to one form. The key is written by the server action and carried back in its state. |
| Pinned stokvels (an id and a name each) | **Zustand**, saved in `localStorage` | It is a small preference of this browser that the API does not know about, shared by the sidebar and the stokvel page, and it must work without a request. |
| Which stokvels the member is in (used to hide a pin for one they left) | **The server**, passed to the shell as a prop for one render | It is API data. It is compared with the pins and never stored. |
| Balances, roles, member lists | **The server / the API**, never copied | They change; a copy would go out of date and could be wrong. |
| The session and the tokens | An encrypted **HttpOnly cookie**, read only on the server | The browser's JavaScript must never be able to read a token. |
| The sidebar drawer and the profile menu | Component state | Pure interface state. |

## Filters

The filters are written **once**, in `src/lib/historyParams.ts` (built from `nuqs/server`), and used by three places: the page (`loadHistoryParams` on the server), the Route Handler under `/bff/`, and the controls (`useQueryStates` in the browser). One file means one meaning.

- `member` is a strict UUID. On the server it is also checked against **this stokvel's** members; an unknown one means "everyone" with no error.
- `from` and `to` are strict `YYYY-MM-DD` and must be a real day (`isRealDay` from `saRange.ts`). nuqs's `parseAsIsoDate` is not used: it gives midnight UTC.
- `sort` is an allow-list of the four sorts the API has; the default is `-recordedAt` (newest first).

Anything else falls back to the default, with no error page and no 400 shown. Checked in the browser on the history page (all answered HTTP 200 and showed the list):

| Address | What the person sees |
|---|---|
| `?member=banana&from=2026-02-30&to=yesterday` | the whole list |
| `?member=00000000-0000-4000-8000-000000000001` (a UUID that is not in this stokvel) | the whole list |
| `?from=2026-10-20&to=2026-10-10` | the whole list, and the line "“To” is before “from”, so the dates were ignored." |
| `?sort=;drop` | the default order |
| `?from=2026-11-03&from=2026-11-04` (twice) | the list, no error |

`clearOnDefault` is on, so the unfiltered page has the clean address `/stokvels/{id}/contributions`.

**Push or replace?** The controls use `history: 'push'`, because they apply on a deliberate change (a pick or a date, not every keystroke), so each filter the person chose is a step Back can return to.

A filter that matches nothing is not an error: the page says "No contributions match these filters." with a "Show everything" link.

## Dates

`src/lib/saRange.ts` is the **only** place a South African calendar day becomes a UTC instant for filtering. `southAfricanDaysToUtcRange(from?, to?)` returns the half-open range the API takes:

- `from` = 00:00 South African time on the `from` day;
- `to` = 00:00 South African time on the day **after** the `to` day.

Africa/Johannesburg is UTC+2 all year, with no daylight saving since 1944, so a South African midnight is always 22:00 UTC the evening before. The code relies on that and uses only UTC arithmetic (`Date.UTC`), never the machine's time zone. `2026-02-30` is refused; it does not roll over to 2 March.

The rules I chose:

| Input | Result |
|---|---|
| `from` only | everything from the start of that day, no upper end |
| `to` only | everything up to the end of that day, no lower end |
| the same day twice | that day, exactly 24 hours |
| `to` before `from` | refused (`to-before-from`); the page shows everything and says so. The days are not swapped, because that would answer a question the person did not ask. |
| not a real day | refused (`invalid-date`) |

1–31 October is `from = 2026-09-30T22:00:00Z` (included) to `to = 2026-10-31T22:00:00Z` (excluded), so a contribution at 23:30 on 31 October (`21:30Z`) is inside and the first hours of November are not. Lists show South African time again with the existing formatters in `dates.ts` (a 23:30 contribution on 31 October reads as 31 October; this is tested).

`npm run test:tz` runs the date tests under `TZ=UTC` and under `TZ=Africa/Johannesburg`. Both runs (the "Start at" times differ by two hours, which shows the zone really changed):

```
> TZ=UTC vitest run src/lib/saRange.test.ts src/lib/dates.test.ts
 Test Files  2 passed (2)
      Tests  14 passed (14)
   Start at  23:35:02

> TZ=Africa/Johannesburg vitest run src/lib/saRange.test.ts src/lib/dates.test.ts
 Test Files  2 passed (2)
      Tests  14 passed (14)
   Start at  01:35:03
```

## Paging

**Page one is the server's. The rest is the browser's, through our own site.** Page one is rendered by a Server Component. Later pages are asked for by `useInfiniteQuery` from `GET /bff/stokvels/{id}/contributions` and `GET /bff/activity`: Route Handlers that read the session cookie (no session means a JSON 401, never an HTML redirect), validate the query with the **same parsers** as the page, call the API with the token, and return JSON. The browser never calls the API's port and never sees a token. `proxy.ts` also answers `/bff/` requests without a session with a JSON 401.

- `initialPageParam` is the token page one ended with. `getNextPageParam` returns `undefined` for an empty (`""`) or `null` token, which means "no more".
- **Every filter value is in the query key**, and so is the token page one ended with. A different filter, or a page one that changed (a new contribution), is a different key, so it starts again and a page from one list can never show under another. The history list also has a `key` of its filter, so it remounts for each filter.
- An `IntersectionObserver` on the end of the list loads the next page, **and** a real "Load more" button does the same for keyboards (checked: focus the button, press Enter). Nothing extra is fetched until the end is on screen or the button is pressed. On a short list the end is already on screen, so the next page loads straight away.
- A `role="status"` line says "Showing 20 contributions. There are more." and says "That's everything: 24 contributions." **only after** the API's token was empty. While a page loads it says "Loading more…", and the button stays (disabled) instead of disappearing. After an error the observer stops by itself and the button says "Try again".

**Two caches, and how each one learns about a new contribution.** The server's page one is not kept in the browser: after the contribution Server Action succeeds it calls `refresh()` (from `next/cache`), so the page the person is on is rendered again. TanStack's later pages live in the browser, so the form also clears them there (`removeQueries` for the history and the activity feed) when its confirmation appears. The next time a list is shown it starts from the fresh page one, with no stale page behind it.

**Proof that the app never sends a token under the wrong filter.** The request that *would* be wrong is the one in "API check (8.3), question 4": a token made under one filter, sent with another. The API refuses it with a 400 and `page-token-filter-mismatch`. The app cannot make that request because (1) the token in a request always comes from the page one (or the later page) of **the same filter**: the list gets its filter and its first token from the same server render and builds every URL from that one filter; (2) the query key holds the filter and the first token, so another filter is another cache entry; and (3) the list remounts when the filter changes. I checked it in the browser: 3 `/bff/` requests that carried a page token were decoded, and in every one the sort and the "from" in the token's filter matched the request (0 mismatches), including after changing the order with a list already loaded.

## Pinned stokvels

A pin is a stokvel's id and name, and nothing else (no balance, role or member list). The store is in `src/lib/pinsStore.ts`:

- It follows Zustand's rules for Next.js: a **store factory** (`createStore` from `zustand/vanilla`), **one store per provider** (`useState(() => createPinsStore(userId))` in `PinsProvider`), a hook that reads it from context, and no store at the top of a module. Server Components never read or write it.
- It is saved with `persist` and `skipHydration: true`, and read with `rehydrate()` in an effect after the first render. The server's HTML and the first client render both have no pins, so they match (no hydration warning); the saved pins appear a moment later. The saved data is checked on the way in (a hand-edited value is dropped), and every access to `localStorage` is wrapped so a private window cannot crash the app.
- Pins are saved **per person** (`ronditrack-pins:<user id>`), so the next person on the same browser never finds them.
- A "Pin" / "Unpin" button is on `/stokvels/[id]`; pinned stokvels are listed under "Pinned" in the sidebar on every signed-in page.

**Sign-out and expired sessions.** The sign-out Server Action runs on the server and cannot touch `localStorage`, so the **Sign out buttons (the sidebar's and the profile menu's) clear the store and its storage in the browser first, then submit the action.** A session that **expired or was refused** never presses that button, but it always ends on the sign-in page, so **the sign-in page clears every saved pin when it opens** (`ClearPinsOnSignIn`). Together they cover both ways a session ends. Checked in the browser: after Sign out the keys were gone, and pins saved by hand were removed as soon as `/login` opened.

**A pin for a stokvel the member has left disappears.** The signed-in layout reads which stokvels the member is in now (`/me/home`) and passes only their ids to the sidebar; pins for any other stokvel are hidden and removed from the saved pins. I chose this over leaving a link that opens a "not found" page, because a dead link in the sidebar on every page is worse than one missing pin, and the member can pin it again if they rejoin. Checked: a chairperson removed a member, and on the next page load the pin was gone from the sidebar and from storage. If the API cannot be read, nothing is hidden. A pinned stokvel that is renamed gets its new name when its page is opened.

## Known gaps (8.3)

- `npm audit` does not report 0. There are 23 advisories (2 low, 21 moderate, 0 high), all in **development tooling** that is not shipped: a `minimatch` denial-of-service advisory reached through ESLint, `shadcn` and `openapi-typescript`, and a `deepmerge` prototype-pollution advisory in the `shadcn` CLI. The `minimatch` fix (11.0.1 or later) was released less than two weeks ago, and `deepmerge` has no patched release yet. npm's suggested "fixes" are downgrades to ESLint 4 and `eslint-config-next` 12, which I did not make. The one high advisory (`zod`) is fixed by updating it to 4.6.7.
- Pins are per browser. They do not follow the person to another device.
- If someone signs out in one browser tab, another tab that is already open still has the pins in memory until it reloads, and pinning there would save them again. The sign-in page clears them the next time it opens.
- The signed-in layout asks the API for the member's stokvels once per full page load, to hide pins for stokvels they left. The cost is one small request; a change of membership shows after the next full load.
- The lists keep every loaded item in the page (no windowing), so a very long feed makes a long page.
- The activity feed shows a person's name only while they are still in that stokvel; after that the line says "A member".
- `ContributionRangeTests` and `ActivityFeedTests` run against a real database, but `dotnet test` still fails to start on its own since 1 November 2026: the development seeder gives one member two contributions in the 2026-11 cycle. (Not changed here; I ran the suite with a temporary seeder patch that I reverted.)
- The two `Seed` tests that check the "midnight" row fail while that patch is applied; they are the only failures.
- Screenshots and a browser recording: **TODO (yours):** see the checklist in my report. The run described above was done with a scripted Chrome, not by hand.

## API check (9.1)

What the running API does for the join flow, proof of payment and the safe close. Checked against the rebuilt API on :8080 with the seeded accounts, and with throwaway accounts and stokvels for anything that changes data, so the demo stays as seeded. Unless it says otherwise every error is `application/problem+json` with a stable `code` and a `correlationId`.

### 1. Invites: what do create, preview and join answer, including for an unknown code, an expired code and someone who is already a member?

| Call | Answer |
|---|---|
| `POST /stokvels/{id}/invites` by the stokvel's Treasurer or Chairperson | **201** `{ "code": "-7R8qvcwyFW3lqWL8Cc2Ow", "expiresAt": "2026-11-16T08:17:03Z" }` (7 days). The code is 22 URL-safe characters (128 random bits from the operating system's CSPRNG) and is the table's primary key, so it is unique. |
| ...by an ordinary Member (Nandi, Work Stokvel) | **403** |
| ...by someone who is not in the stokvel (Thandi) | **404**, the same as a stokvel that does not exist |
| `GET /invites/{code}` with a code that does not exist | **404** `invite-not-found`: "No invite was found with that code." |
| `GET /invites/{code}` or `POST …/join` with an **expired** (or revoked) code | **410** `invite-expired`: "This invite has expired or was withdrawn. Ask for a new one." |
| `POST /invites/{code}/join` by someone who is **already a member** | **409** `already-member` with the stokvel's id in the problem: `{"code":"already-member","stokvelId":"b12b7944-…", …}` (the caller is a member, so they may know it) |
| `POST /invites/{code}/join` without an `Idempotency-Key` | **400** "An Idempotency-Key header is required." |
| Joining for the first time | **201** `{ "stokvelId": "7219ce83-…" }`; the caller is now a Member |
| The same `Idempotency-Key` again | **201**, the original answer, and still one membership. Two requests with the same key at the same moment also both get 201 and make one membership. |
| A new key after joining | **409** `already-member` |
| Two joins at once with different keys | one **201** and one **409**: the database's one-membership-per-person key lets only one in |
| More than 10 preview or join requests a minute from one user | **429** with `Retry-After: 60` |

Tests: `InviteTests` (20 tests, e.g. `A_treasurer_creates_an_invite_that_expires_in_seven_days`, `An_expired_code_gets_410_with_the_invite_expired_code`, `Joining_when_already_a_member_gets_409_with_the_stokvel_id_in_the_problem`, `A_double_click_with_the_same_key_at_the_same_time_makes_one_membership_and_two_201s`, `Two_joins_at_once_with_different_keys_make_one_membership_and_the_database_decides_the_loser`, `Guessing_codes_too_fast_gets_429_with_Retry_After`).

### 2. What exactly is in the invite preview, and is there any member data?

`GET /invites/{code}` (signed in) returns exactly these nine fields and nothing else:

```
code, stokvelName, rules, contributionAmountCents, frequency, cycleLengthDays, memberCount, expiresAt, alreadyMember
```

For the seeded Family Stokvel: `stokvelName: "Family Stokvel"`, `contributionAmountCents: 50000`, `frequency: "Monthly"`, `cycleLengthDays: null` (a calendar month is not a fixed number of days; weekly is 7 and bi-weekly 14), `memberCount: 4`, and the stokvel's `rules` text. **There are no member names, no user ids, no balances, no contributions and no payout order.** The caller is not a member yet, so none of that is theirs to see. The query selects only these fields, and `The_preview_has_only_the_allowed_fields_and_nothing_about_members` asserts the exact set of fields and that no member id or name appears anywhere in the body.

### 3. What can be uploaded as proof of payment, how big, and who decides the type?

`POST /stokvels/{id}/contributions/{contributionId}/proof` takes `multipart/form-data` with one file field named `file`. **JPEG, PNG and PDF only, at most 5 MB (5 × 1024 × 1024 bytes). The type is decided by the file's first bytes, never by its name or the client's Content-Type.** Run against the real server:

| Upload | Answer |
|---|---|
| A text file named `receipt.pdf`, sent with Content-Type `application/pdf` | **415** `unsupported-file-type` |
| A PNG named `../../x<y>.txt`, sent as `text/plain` | **200**, stored as `image/png`, named `x_y_.png` (no path, odd characters replaced, extension of the real type) |
| Exactly 5,242,880 bytes | **201** |
| 5,242,881 bytes | **413** `file-too-large` (problem+json) |
| 12 MB | **413** `file-too-large` (problem+json), not a connection reset |
| No `file` field | **400** |
| Uploading again while the cycle is open | **200**, the proof is replaced (still one per contribution) |
| Upload, replace or delete once the cycle is closed or paid out | **409** `proof-locked` |

The limit is enforced in four agreeing places: the endpoint's `RequestSizeLimit` and `RequestFormLimits`, a filter that refuses a declared length over the limit before the form is read, Kestrel's own `MaxRequestBodySize` (5 MB plus 64 KB of multipart wrapping), and a check on the file itself. The proof is stored in PostgreSQL (`bytea`), which is fine for files of at most 5 MB in a project this size; a larger system would use object storage. Tests: `ProofTests` (19 tests).

### 4. Who may download a proof?

`GET …/proof`: only **the contribution's member (the uploader)** and **this stokvel's Treasurer or Chairperson**. Everyone else gets the **same 404** as a contribution with no proof, so nobody can tell whether a proof exists.

| Caller | Answer |
|---|---|
| The uploader | **200**: `Content-Type: image/png`, `Content-Disposition: attachment; filename=…`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store` |
| This stokvel's Chairperson or Treasurer | **200** |
| An ordinary member of the same stokvel | **404** `proof-not-found` |
| A treasurer of a different stokvel | **404** `proof-not-found` (identical to the one above and to a contribution with no proof) |

Seeded demo, checked: in Work Stokvel Lerato's contribution has a proof; Sipho (its Treasurer) gets 200, Nandi (an ordinary member) gets 404 and Thabo (Family's Chairperson, a stranger to Work) gets 404. In Family Stokvel Thabo's contribution has a proof; Lerato (its Treasurer) gets 200 and Sipho (an ordinary member there) gets 404. Contributions carry `hasProof` and `canDownloadProof` (computed by the same rule as the download): the ordinary member above sees `hasProof: true, canDownloadProof: false`. Tests: `An_ordinary_member_and_another_stokvels_treasurer_get_the_same_404_as_a_contribution_with_no_proof`, `Contributions_say_whether_they_have_a_proof_and_whether_the_caller_may_download_it`.

### 5. What does the close preview contain, and is it the same code as the real close?

`GET /stokvels/{id}/cycles/{cycleId}/close-preview` (Treasurer or Chairperson of **this** stokvel; an ordinary member gets **403**, a non-member **404**):

```json
{ "cycleId": "ac791e19-…", "cycleLabel": "2026-11", "asOfDate": "2026-11-09",
  "arrears": [ { "memberUserId": "702fa832-…", "fullName": "Live payer", "shortfallCents": 10000 } ],
  "totalArrearsCents": 10000, "version": "dad98def…" }
```

`asOfDate` is the South African calendar date the close counts up to: the cycle's last day, or today if it is still running. An empty `arrears` list is valid (nobody owes anything, and the close still works). **It is the same code as the real close**: `CycleClosing.CalculateArrearsAsync` is the one function that works out the arrears, and the nightly job (6.4), the preview and the treasurer's close all call it (`The_nightly_job_records_exactly_the_arrears_the_preview_showed` moves a fake clock past the cycle's end and compares).

`version` is the SHA-256 (hex) of the calculation's inputs, in a fixed order: `v1 | cycleId | label | asOfDate | contributionAmountCents`, then for every member counted (ordered by user id) `| userId:paidCents`. It changes whenever anything that affects the result changes: a counted contribution added, removed, edited or changing status, a member joining or leaving the counted group, the amount owed, or the as-of date. A Pending contribution that still does not count does not change it, because it cannot change the result.

### 6. What happens with a stale preview, and with a repeated close?

`POST …/close` takes `{ "version": "…" }` and an `Idempotency-Key`. Same roles as the preview.

| Situation | Answer |
|---|---|
| A payment came in after the treasurer looked (old `version`) | **409** `close-preview-stale`: "Something changed since you looked… Nothing was closed." No arrears are written and the cycle stays open. |
| The same key retried with the **new** version after a stale refusal | **200**: a refused request does not use up its key |
| A good close | **200** `{ cycleId, closedAt, arrears, totalArrearsCents }`, the very list the preview showed; the arrears rows and the next cycle are written in the same transaction |
| The same `Idempotency-Key` again | **200**, the original answer (identical body, same `closedAt`); the arrears are recorded once |
| Two closes at the same moment with the **same** key | one close; both get the same 200 |
| A **different** key with the same version, after it closed (or at the same moment) | **409** `cycle-already-closed`, and the arrears exist once |
| Already closed by the nightly job or another treasurer | **409** `cycle-already-closed` (also for the preview) |
| No `Idempotency-Key`, or no `version` | **400** |

"Exactly once" is the 6.4 mechanism: the close is claimed with `UPDATE … SET ClosedAt = now WHERE Id = … AND ClosedAt IS NULL` and the row count says who won; a second close waits for the first to commit, then matches no row. The version is re-checked inside that transaction, so what was checked is what was written, and a stale version rolls the claim back. Tests: `CycleCloseTests` (17 tests).

### What changed in the data

One migration (`AddInvitesProofsAndRules`): the `StokvelInvites` table (the code is the primary key), the `ContributionProofs` table (one row per contribution, `bytea` content, a size check constraint) and a nullable `Stokvels.Rules` column. The demo seed adds Thandi (thandi@tallyvel.test, in no stokvel), one valid invite to Family Stokvel and one expired invite to Work Stokvel (both codes are written to the API log at startup), Sipho as Treasurer of Work Stokvel and an ordinary Member of Family Stokvel, a proof on one other member's contribution in each, and rules text on both stokvels. It runs on every Development start and only adds what is missing, so a database seeded earlier gains the new data.

## Join flow

Joining a stokvel with an invite takes three steps. The step is in the address: `/join?code=<code>&step=<code|rules|confirm>`. **Why the address:** it is the only place that survives a reload, works with the Back button and is what an invite link already is (`/join?code=XYZ`); React state would be lost on reload and a cookie would break the invite link.

| Step | Address | What it shows | What moves it on |
|---|---|---|---|
| 1. Enter the code | `/join?step=code` or `/join?code=…&step=code` | A labelled input, filled from the address. A notice if the API refused the code (below). | "Continue": a GET form to `?code=…&step=rules`. |
| 2. Check the rules | `/join?code=…&step=rules` | Stokvel name, contribution (`formatCents`), schedule, member count, when the invite expires, and the rules text. Drawn on the server. | "Continue" (a link to step 3) or "Back" (a link to step 1). |
| 3. Confirm | `/join?code=…&step=confirm` | The name, amount and schedule from step 2 again (never asked for again) and a "Join <name>" button. | The button: a Server Action (`joinStokvel`), then `/stokvels/{id}?joined=1`. |

**One function decides the step.** `decideStep(requested, hasCode, previewKind)` in [src/lib/joinFlow.ts](src/lib/joinFlow.ts) is the only place that says which step may be shown; the page redirects to `stepUrl(step, code)` whenever the answer differs from the step in the address. So steps 2 and 3 need a code the API accepted, an address with a code and no step (the invite link itself) lands on step 2, and `/join?step=confirm` goes to `/join?step=code`. The page asks the API about the code on every view (the code is the only thing it knows), so a code that expires between steps is caught at once. Tests: `joinFlow.test.ts` (every transition, including "confirm with no valid code → code"), `safeNext.test.ts` (a `next` with a query string).

**The URL parsers** ([src/lib/joinParams.ts](src/lib/joinParams.ts), nuqs): `code` must match `[A-Za-z0-9_-]{1,64}` (a real code is 22 characters; the API decides whether it exists) and `step` must be one of the three words. Anything else counts as "not set". A code with odd characters (`bad code!`) is kept in the box on step 1 with "Check the code and try again", not thrown away.

**What the API can answer, and what the person sees** (always a screen, never an error page):

| API | Screen |
|---|---|
| 404 `invite-not-found` | Step 1, code kept: "Check the code and try again." |
| 410 `invite-expired` | Step 1: "This invite has expired. Ask the treasurer for a new one." |
| 200 with `alreadyMember: true`, or 409 `already-member` on joining | "You're already a member" with a link to `/stokvels/{stokvelId}` (the 409 carries the id; the preview does not, so for the preview case the link is found among the person's own stokvels by name, and goes to `/stokvels` if two share the name) |
| 429 | "Try again in N seconds." (from `Retry-After`) |
| 5xx, or no answer | On step 1 a message with the reference; on step 3 "We couldn't confirm that you joined. Press the button again", with the same key (trying again is safe) |

**Never asked twice (WCAG 2.2 SC 3.3.7).** Step 1 rewrites its own history entry to `?code=<typed>&step=code` just before leaving ([CodeForm.tsx](src/components/CodeForm.tsx)), because that entry was loaded with an empty box and Back would otherwise return to it. I found this by running the flow: the address was right but the box was empty. The box reads the code from the address (`useSearchParams`) for the same reason.

**What the server sends to the browser.** The preview has nine fields (`code, stokvelName, rules, contributionAmountCents, frequency, cycleLengthDays, memberCount, expiresAt, alreadyMember`) and nothing about members. The only Client Components on the join page are `CodeForm` (the code) and `JoinForm` (the code, the stokvel's name for the button, and the first Idempotency-Key); the rest is drawn on the server. I fetched the page's HTML and its server-component data as a non-member for all three steps and looked for member names: none.

**Joining** is the Server Action `joinStokvel` ([src/app/inviteActions.ts](src/app/inviteActions.ts)): the person's own token, an `Idempotency-Key` the server wrote into a hidden field (it stays the same for a retry), the button disabled while pending. Pressing it twice quickly made one membership (checked). **Focus:** each step's heading ([StepHeading.tsx](src/components/StepHeading.tsx), `tabIndex={-1}`, `key={step}`) takes focus on arrival and the step list marks the current step with `aria-current="step"`. **Signed out:** the proxy sends `/join?code=…&step=…` to `/login?next=<that address>` and `safeNextPath` keeps the query string, so sign-in comes back to the same step and code (checked).

**"Create invite link"** on `/stokvels/[id]`, shown when the person runs THIS stokvel's cycles (`canRunCycle`: Treasurer or Chairperson, from `/me/home`), the same rule the API uses. It shows the full link and a copy button. (An earlier version showed these controls to Treasurers only; that left the person who creates a stokvel, who is its Chairperson, with no way to use them.) The dashboard at `/dashboard/treasurer` still lists only stokvels where the person is Treasurer.

## Uploads

A proof is a JPEG, PNG or PDF of at most 5 MB (5 × 1024 × 1024 bytes). Every hop agrees, so a valid file is never refused on the way and the API has the last word:

| Hop | Limit | Where it is set | What the person sees when a file is over it |
|---|---|---|---|
| Browser | 5 MiB; type from the file's first bytes (`%PDF-`, PNG and JPEG signatures), never the name or `Content-Type` | [src/lib/proofFile.ts](src/lib/proofFile.ts) | A message **under the input**, before anything is sent; the Upload button stays disabled |
| Next.js proxy | buffers request bodies up to 10 MB (Next's default `proxyClientMaxBodySize`) | not changed | Not reachable first: the next hop is smaller |
| Next.js Server Action | **6 MB** (`bodySizeLimit: '6mb'` = 6 MiB), above the API's 5 MiB plus multipart wrapping | [next.config.ts](next.config.ts), with a comment | Only reachable by getting past the browser check: a toast that stays, with a reference. The message is "The request didn't go through" because Next refuses the body before our code runs |
| API (Kestrel, the endpoint, a filter, the file itself) | 5 MiB of file (Kestrel 5 MiB + 64 KB); type judged by bytes | the API | 413 `file-too-large` and 415 `unsupported-file-type` are shown **under the input**, in the API's words |

Checked in the real app, as the treasurer of a throwaway stokvel: 6 MiB file → "This file is bigger than 5 MB…" under the input; a text file renamed `.pdf` → "This isn't a JPEG, PNG or PDF…"; a file of exactly 5 MiB and one of 5 MiB − 1 byte → uploaded ("Proof uploaded." toast, "Proof attached" and a download link); a text file `.pdf` slipped past the browser check → the API's 415 text under the input; a 6 MiB file slipped past → Next's limit, the persistent toast. (The browser check is a courtesy; the API checks again.)

**Who may download what.** `GET /bff/stokvels/{id}/contributions/{contributionId}/proof` ([route.ts](src/app/bff/stokvels/%5Bid%5D/contributions/%5BcontributionId%5D/proof/route.ts)) checks the session (JSON 401 if signed out), adds the person's token, and **streams** the API's body back with `Content-Disposition: attachment`, `X-Content-Type-Options: nosniff` and `Cache-Control: private, no-store`. It is never a public link, and the file is never held in memory here. The API decides: the contribution's own member and this stokvel's Treasurer or Chairperson get 200; everyone else gets one identical 404 (a 404 from the API is passed on as a 404). The page shows the download link only where `canDownloadProof` is true, but the 404 is what protects the file. Checked as Sipho: Work Stokvel (Treasurer) → 200 with the three headers; Family Stokvel (Member) → 404, and the page shows no link there. After the cycle is closed the page offers neither replace nor remove; the API's 409 `proof-locked` is shown as a message if it is ever sent.

## Closing a cycle

On `/stokvels/[id]`, for THIS stokvel's Treasurer while the cycle is open, "Close <label>…" opens a shadcn AlertDialog ([CloseCycleDialog.tsx](src/components/CloseCycleDialog.tsx)).

- **The preview** comes from `GET /bff/stokvels/{id}/cycles/{cycleId}/close-preview`, which passes on the API's `{ cycleLabel, asOfDate, arrears[], totalArrearsCents, version }`. While it loads the dialog shows skeleton rows and the confirm button is disabled. The browser never calculates arrears; it shows each member's shortfall, the total and the South African date the close counts up to, or "Nobody goes into arrears."
- **One implementation.** In the API, `CycleClosing.CalculateArrearsAsync` is the single function behind the nightly job, the preview and the close, so what the dialog shows is what is recorded.
- **The button says what it does:** "Close 2026-11 and record R 600,00 in arrears" or "Close 2026-11 with no arrears", never "OK". Focus starts on Cancel, Esc closes (not while a close is running) and focus returns to the button that opened it. The dialog stays open while the close runs.
- **A stale preview is refused.** The Server Action `closeCycle` ([src/app/closeActions.ts](src/app/closeActions.ts)) sends the preview's `version` and an `Idempotency-Key`. If anything changed since (a payment, a member), the API answers 409 `close-preview-stale` and writes nothing; the dialog then fetches and shows the NEW preview ("Something changed since you looked…") and the next confirmation uses a new key, because it is a different request. Checked: a payment recorded behind the open dialog gave R 500,00 → R 300,00, and only the second confirmation closed.
- **Exactly once.** The key is created by the server, stays the same for a retry after no answer or a 5xx, and changes after any finished answer or when the version changes. A double click on the confirm button closed once, and the database has one arrears row for that cycle.
- **Other answers:** 409 `cycle-already-closed` → a message in the dialog and the page refreshed; 403 → the API's message in the dialog, nobody is signed out. **After success** the dialog closes, `refresh()` re-renders the page and a toast says what was recorded. The API opens no next cycle, so the page then shows "No open cycle" rather than the word "Closed".

## Creating and leaving a stokvel

- **Create** (`/stokvels/new`, [CreateStokvelForm.tsx](src/components/CreateStokvelForm.tsx), Server Action `createStokvel`): name, contribution in rands (typed as text and turned into whole cents by `parseRandsToCents`, never a fractional number), how often, and optional rules. Zod explains problems under each field first; the API's own 400 messages go under the same fields. The API only accepts the caller's own id as `creatorId` (anything else is 403), so the action takes it from the session, never from the form. The creator becomes the stokvel's **Chairperson**, so the Treasurer-only controls (invite link, close, proof download for others) are not shown to them until someone makes them Treasurer; there is no screen for changing roles yet. The button is disabled while the request runs; the create endpoint takes no Idempotency-Key, so that is the only protection against a double click.
- **Leave** ([LeaveStokvelDialog.tsx](src/components/LeaveStokvelDialog.tsx), Server Action `leaveStokvel`): a confirmation dialog (focus on Cancel, Esc closes) that says what leaving does. The action removes the **signed-in person only**: the user id comes from the session. The only Chairperson is not offered the button; the page says why, and the API would answer 422 anyway. A 404 on a retry counts as success (they are out, which is what they wanted).
- **API change this needed.** `DELETE /stokvels/{id}/members/{userId}` used to be Chairperson-only, so an ordinary member could not leave. It now lets anyone remove themselves (the Member policy when `userId` is the caller) and keeps Chairperson-only for removing someone else (`LeaveStokvelTests`, 7 tests). Past contributions stay.
- **Not built: deleting a stokvel.** The API has no endpoint for it, and a stokvel holds money history; closing/archiving it needs its own rules.

## Feedback

| Where | Used for | Why |
|---|---|---|
| Inline message, under the input | A wrong file (browser check, API 413/415) | It belongs to that input and must stay until the person fixes it; a toast would vanish and be far from the field. |
| Inline message, next to a form or in a dialog | A refusal about the situation: join answers, `proof-locked`, 403, `close-preview-stale`, `cycle-already-closed`, "try again in N seconds" | The person must read it and act on it where they are. |
| Toast, short | Success: "Proof uploaded.", "You joined <name>.", "Closed 2026-11. Recorded R 300,00 in arrears for 1 member." | Confirms something that is already visible; nothing to do. |
| Toast, stays until closed, with "Reference: …" | An unexpected failure of an upload or removal (5xx, no answer, body refused by Next) | There is nothing the person can fix; they may need to quote the reference. It has a close button and no timeout. A failure that never reached the API gets a `web-…` reference that is also written to the Next server log. |
| Dialog | Closing a cycle | It cannot be undone, and the person should see the consequence first. |

## Known gaps (9.1)

- Leaving is allowed even with unpaid arrears, and the confirmation does not mention them; there is no way yet to change a member's role from the UI, and a stokvel cannot be deleted.
- The close's version check runs at read-committed isolation, so a payment landing in the last instant before the commit could still slip in.
- Idempotency keys are held in memory in the API, so a restart forgets them (the database's own rules still stop a second join or a second close).
- Creating a stokvel has no Idempotency-Key in the API; the button is disabled while the request runs, but a retry after a lost answer could make a second stokvel with the same name.
- Leaving: the API keeps the person's past contributions and arrears records but the person can no longer see them; an unpaid arrear does not block leaving. The only Chairperson cannot leave (the page says so); there is no screen yet to make another member Chairperson.
- Deleting a stokvel is not offered: the API has no delete endpoint. Archiving it (read-only, only with no open cycle or arrears) would be an API change first.
- Proof controls appear on the five latest contributions of `/stokvels/[id]`, not yet on the full history page.
- The invite preview and the join share one rate limit in the API (10 a minute). Each step views the preview once, so a person clicking through quickly uses a few of them.
- Pressing Back from step 2 after signing in through an invite link goes to the sign-in page (it is in the history); the three-step Back test is done from a signed-in `/join`.
- The API's own `dotnet test` run: 228 passed. `npm audit` reports 13 findings (2 low, 11 moderate), the same 13 as before this work, all inside the `shadcn` command-line tool and `eslint-config-next`; npm's only fix is a breaking downgrade of both, so I left them.
- **TODO (yours):** screenshots and the 3-minute recording (see the checklist in my report). The checks above were done with a scripted Chrome, not by hand.

## Create and leave a stokvel

- **Create** (`/stokvels/new`, linked from "My stokvels"): name, contribution in rands (text in, whole cents out through `parseRandsToCents`), frequency, optional rules. The Server Action `createStokvel` ([src/app/stokvelActions.ts](src/app/stokvelActions.ts)) sends `creatorId` from the session, never from the form (the API refuses any other id with 403). The person becomes the stokvel's Chairperson, so the Treasurer-only controls stay hidden for them until someone makes them Treasurer. The API's field errors (400) are shown under the matching input.
- **Leave** ("Leave <name>…" on the stokvel page, for every member): an AlertDialog with focus on Cancel that says what leaving does. The action removes the signed-in person only (`DELETE /stokvels/{id}/members/{their own id}`; the id comes from the session). **This needed one API change**: until now only a Chairperson could remove a member, so an ordinary member got 403. Now anyone may remove themselves and removing someone else is still Chairperson-only (`LeaveStokvelTests`, 7 tests). The API refuses the last Chairperson (422); the page then shows a note instead of the button.

## Members and roles

The stokvel page lists its members and their roles. A **Chairperson** sees a role picker and "Change role" on each row (`changeMemberRole` in [src/app/stokvelActions.ts](src/app/stokvelActions.ts) → `PUT /stokvels/{id}/members/{userId}/role`); everyone else sees the role as text. This is how a Treasurer is chosen: the creator of a stokvel is its Chairperson, invites join people as Members, and the Chairperson promotes. The API decides: only a Chairperson of THIS stokvel may change roles, and it refuses a change that would leave no Chairperson (422), which the row shows with its reference. Checked in the browser: promote a member to Treasurer (toast, and that member then sees the invite and close controls), and the only Chairperson trying to demote themselves is refused.

## Dashboard group overview

`/dashboard/member` shows a payout banner (only when a group's next payout is today, in South African time) above a list of the person's groups ([GroupOverview.tsx](src/components/GroupOverview.tsx); its rules are plain functions in [groupOverview.ts](src/lib/groupOverview.ts), with tests). `/demo/group-overview` shows every state with fixed data. Where it differs from the design spec, on purpose:
- **Tokens** are in `globals.css` under `@theme` (Tailwind 4 has no `tailwind.config`). The spec's `muted` is `ink-muted`, because shadcn already owns `--color-muted`.
- **Money is whole cents and dates are `"YYYY-MM-DD"` text**, not fractional rands and `Date` objects, like everywhere else here. Amounts go through `formatCents`, which gives "R 1 500,00" (comma decimals, no-break spaces).
- **The banner's sentence** "Everyone has paid in…" is shown only when the group is paid up; otherwise it says some members still owe.
- **"Record payout"** calls `onRecordPayout(groupId)`; on the dashboard that opens the group's page, because the payout screen is not built yet. **"Set a date"** also opens the group's page; there is no screen to set one yet.
- Every colour pair meets 4.5:1 (lowest: white on the calendar tab, 5.02).

## "My stokvels" header

[StokvelsHeader.tsx](src/components/StokvelsHeader.tsx): the title, a one-line summary, a box for an invite code and "Create a stokvel". Where it differs from the design spec, on purpose:
- **"Join" checks the code, then opens step 2 of `/join`; it does not join by itself.** Joining stays in the three steps, so the person always sees the rules and confirms. The box takes the code (or a whole pasted link) through the Server Action `checkInviteCode`; 404 and 410, already a member, a wait (429) and no connection each show their own message under the field. `/join?code=…` still works for links opened from WhatsApp or SMS.
- **`autoCapitalize="none"`, not `characters`:** invite codes are case-sensitive, so a phone keyboard capitalising them would break them.
- **The summary reuses `summaryOf`** (the same words as the dashboard's list: "1 of 3 groups has money due"), so the wording agrees across pages.
- **The page keeps its existing cards** below the header; the payout banner lives on the dashboard.
- On desktop the error sits under the field without moving the buttons beside it. Tokens `label` and `field-border` are new; the spec's `muted` is `ink-muted` (see the dashboard section).

## Stokvel page redesign and the TallyVel style

The theme in `globals.css` now carries the TallyVel palette on the shadcn variables (`--background` #EEF1F4, `--primary` forest #17322A, `--ring` #0E6B53 and so on, plus `--divider`, `--success`, `--success-soft`, `--warning`, `--avatar-alt-*`), Figtree is the only sans family, radii are fixed (10 / 12 / 16 px) and buttons are 44px (40px compact) with the primary, outline and secondary looks. This applies to the whole app, not just this page. The stokvel page ([page.tsx](src/app/(app)/stokvels/%5Bid%5D/page.tsx), [StokvelParts.tsx](src/components/StokvelParts.tsx)) keeps every action, form field and hidden input; what changed is layout and copy.
- **Setup card** (no open cycle, or one member): "Invite members" reuses the invite-link action. **"Open a cycle" is a disabled button with a TODO**: the API has `POST /stokvels/{id}/cycles`, but the app has no action for it yet, and the card says so.
- **The facts strip shows the payout date** from `/me/home` (`nextPayoutDate`), with "collected of" under it; if no date is set it shows the cycle label.
- **Kept although the spec did not draw them:** "Record a contribution" and "Close the cycle" (as cards), and the proof upload/download controls under each recent contribution. Contribution rows also show the status and cycle next to the date.
- Treasurer controls show for Treasurer or Chairperson; role pickers only for a Chairperson. The only Chairperson's role box and "Leave stokvel" are disabled, each pointing at its reason with `aria-describedby`.
- Checked at 1280px and 375px with no horizontal scroll.
- The layout lives in [StokvelDetail.tsx](src/components/StokvelDetail.tsx), which only arranges things: the page passes in the data and the existing forms and actions (record, close, invite, roles, leave, pin). Stacking happens below 768px. Measured against the spec on the real page: sizes, radii, paddings, gaps, colours and icon sizes all match.

**One typeface.** Figtree is the only font: `--font-sans` and `--font-mono` both point at it (Geist and Geist Mono are no longer loaded), the Toaster is told to use it, and no component sets its own family. Checked in the browser: every text element on every page, an open dialog and a toast all report Figtree. Codes and references (`font-mono`) are therefore in Figtree too.

## Plan

Assignment 9.2, part A: tests, CI and the money rules. These are the seven answers that decided how it is built.

**1. Which rules are pure functions, and which were buried.** Pure and tested directly (Vitest, in `src/lib/`): typed rands → whole cents and back ([moneyInput.ts](src/lib/moneyInput.ts), [money.ts](src/lib/money.ts)); South African days → UTC ranges ([saRange.ts](src/lib/saRange.ts), [dates.ts](src/lib/dates.ts)); the form's validation ([contributionSchema.ts](src/lib/contributionSchema.ts), [stokvelSchema.ts](src/lib/stokvelSchema.ts), [proofFile.ts](src/lib/proofFile.ts)); the join steps ([joinFlow.ts](src/lib/joinFlow.ts)); `next=` safety ([safeNext.ts](src/lib/safeNext.ts)); and the dashboard rules ([groupOverview.ts](src/lib/groupOverview.ts)). **Buried, and moved out first** (each with tests):
- *The contribution rules* were inside the Server Action: validation, the Idempotency-Key choice and what each API answer means. They are now [recordContribution.ts](src/lib/recordContribution.ts), which takes a small "port" (send the request, make a key, refresh the page); the Server Action only supplies the real ones ([recordContributionApi.ts](src/lib/recordContributionApi.ts) is the real "send").
- *The close key and the close wording* ("Close 2026-11 and record R 600,00 in arrears", the toast) were inside the close action and the dialog: now [closeRules.ts](src/lib/closeRules.ts).
- *What the stokvel page offers each role* (treasurer actions, "only Chairperson", who may change a proof) was written inline in the page: now [stokvelView.ts](src/lib/stokvelView.ts), a function of the API's own types.
- *The money parser* itself used `Number(...)` and a division; it is now BigInt and string logic only, and a guard test ([noFloatMoney.test.ts](src/lib/noFloatMoney.test.ts)) fails if `parseFloat`, `toFixed`, `Math.round` or `* 100` appear in the money files.

**2. Which tool for which part, and why.**

| Part | Tool | Why |
|---|---|---|
| Money, dates, validation, keys, wording | Vitest (plain) | Pure functions: tables of cases run in milliseconds, in any time zone. |
| The contribution form (messages under fields, one click = one request, the retry) | Vitest + Testing Library | It is about what a person sees and does; no browser or server needed, and the clock is under the test's control. |
| What a Treasurer and a Member are shown | Vitest + Testing Library, typed fixtures | The page is plain components; the fixtures are built from the generated API types so an API rename stops compiling. |
| The exact request the API receives (token, Idempotency-Key, cents) | Vitest + MSW | Real HTTP to a fake API; checks headers and bodies that a component test can't see. |
| Sign-in redirect with `?next=`, the JSON 401 on `/bff/`, a forged request refused by the API, the record-a-contribution journey | Playwright | They need the real proxy, real cookies, the real API and a real browser. |
| Close dialog focus, proof download headers, the join flow | Manual and scripted checks (9.1 README), unchanged | Already checked in a real browser in 9.1; not repeated here. |
| Accessibility, Lighthouse, metadata | Part B | Not in this part. |

**3. The edges of the contribution form, and what stands in for each in a test.** The form has one real edge: the **Server Action** (it sends the payment). `ContributionForm` takes it as a prop (`action`); the page passes the real `recordContribution`, the tests pass the real rules ([recordContributionCore](src/lib/recordContribution.ts)) over a **fake API port** that answers only when the test says so. The **router**: the form never navigates; after a recorded payment the action asks for the page to refresh (`port.onRecorded`, a no-op in tests), and the end-to-end journey checks the total changes without a reload. The **URL**: the form neither reads nor writes the address (the stokvel id and cycle are hidden inputs the server wrote), so there is nothing to fake. The **query cache**: the form clears TanStack Query's cached pages after a success, so the tests wrap it in a real `QueryClientProvider`. The **session**: read inside the Server Action, not in the form; Playwright covers it.

**4. The login rate limit, and how the tests get past it.** The API allows **5 sign-ins a minute per IP**. The end-to-end suite signs in once per person (3 people) plus 3 API sign-ins to prepare data, so even a single run would be refused at 5. The limit was already a setting (`RateLimiting:Login:PermitLimit`); it is now passed through `compose.yaml` as `LOGIN_RATE_LIMIT`, **default 5 (today's value)**, and the test and CI environments set 1000. It must never be raised in production: that limit is what slows password guessing. CI gets a fresh database by starting Compose on a new runner after `docker compose down --volumes`, and a fresh set of secrets made in the run with `openssl rand` (`POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, `SEED_DEMO_PASSWORD`, `SESSION_SECRET`); they are masked in the log and never written to a file. The API seeds the demo data itself, because the job runs it in Development.

**5. Every route, and which a search engine should index.** Public (no session): `/login` and `/register`: these are the only pages that should be indexed (and `/` is only a redirect to the dashboard, which sends a signed-out person to `/login`). Signed in, **not** to be indexed: `/dashboard/member`, `/dashboard/treasurer`, `/stokvels`, `/stokvels/new`, `/stokvels/[id]`, `/stokvels/[id]/contributions`, `/join` (an invite link is a secret), `/activity`, `/contributions`, and the demo page `/demo/group-overview`. Never indexed: everything under `/bff/` (JSON and downloads) and the error pages. (Part B adds the metadata and `robots` rules.)

**6. The LCP element on the home screen, on a phone (best estimate; to be confirmed with Lighthouse).** The signed-in home is `/dashboard/member`. On a 390px phone the largest thing above the fold is the **payout banner's heading** ("<stokvel> pays out today", 26px, wrapping over two or three lines) when a payout is due today, otherwise the **first group's name and status** in the list. Both are drawn from the streamed part of the page, so the LCP waits for: the session cookie, then the API's `/me/home` answer, then the stream arriving. The static shell (the sidebar and the page title) is already there, so it is not the LCP. The Figtree font is loaded with `next/font` (self-hosted and preloaded), so a font swap should not move it much. For a signed-out visitor the LCP is the sign-in page's headline paragraph, which has no data to wait for.

**7. Dragging and typing from memory.** *Nothing needs dragging, and no payout rotation screen exists:* the sidebar has "Payouts", "Members" and "Settings" items but no pages behind them, and the API has no rotation order to edit. (The sign-in page's marketing text mentions a payout rotation; that is wording, not a feature.) So there is no drag-and-drop to make keyboard-accessible, and I did not invent one. *Typing from memory:* the **amount** is prefilled with what the member still owes, so it is rarely typed from scratch; an **invite code** (22 random characters) is never meant to be typed: people open the link, or paste it into the box. Passwords use the browser's password manager (`autocomplete="current-password"`).

## Running the tests

**Fast tests (no server, no Docker):**

```bash
cd web-next
npm test                 # Vitest: unit + component tests, once
npm run test:tz          # the whole suite twice: TZ=UTC, then TZ=Africa/Johannesburg
npm run test:repeat      # the whole suite 10 times in a row; stops at the first failure
npm run typecheck        # next typegen, then tsc --noEmit
npm run lint
```

**End-to-end tests (Playwright, against the whole stack):** these need, in this order:

1. The API and database with the demo seed: `LOGIN_RATE_LIMIT=1000 docker compose up -d` from the repo root (the compose file needs `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY` and `SEED_DEMO_PASSWORD` in your `.env`). Without `LOGIN_RATE_LIMIT` the API stays at 5 sign-ins a minute and the suite is refused.
2. The production build of the app: `npm run build` (the tests run `npm run start`, never `next dev`).
3. The demo password in the environment, never in a file: `E2E_DEMO_PASSWORD="$SEED_DEMO_PASSWORD" npm run e2e`.
4. Once per machine: `npx playwright install chromium`. (If that download is not possible, `E2E_BROWSER_CHANNEL=chrome` uses the Google Chrome that is already installed.)

The app reads `API_BASE_URL` and `SESSION_SECRET` from `.env.local` as usual; the tests start it on port 3100 (`E2E_PORT` changes that) and set `REFRESH_MARGIN_SECONDS=60` for it, so a developer's test setting of a huge margin can't make every request rotate the refresh token. Each run signs in again and re-saves the sessions under `e2e/.auth/` (git-ignored: it holds cookies and tokens), because refresh tokens rotate. Each test that needs data makes its own stokvel with a unique name, so tests don't depend on each other or on what an earlier run left.

## What's checked where

| Behaviour | Where | Tool |
|---|---|---|
| Typed rands → cents: accepts `1500`, `1 500`, `1 500,50`, `R1500`, `R 1 500,50`, no-break spaces, `,5`; refuses 3 decimals, `1,500.50`, letters, empty, negatives, above the API's maximum | [moneyInput.test.ts](src/lib/moneyInput.test.ts) | Vitest |
| Format then parse gives the same cents (0, 1, 99, 100, 150050, the maximum) | [moneyInput.test.ts](src/lib/moneyInput.test.ts) | Vitest |
| No floating-point shortcuts in the money files | [noFloatMoney.test.ts](src/lib/noFloatMoney.test.ts) | Vitest |
| Dates: ranges, display, and a payment at 23:30 SAST on a month's last day belongs to that month; whole suite in two time zones | [saRange.test.ts](src/lib/saRange.test.ts), [dates.test.ts](src/lib/dates.test.ts), [monthBoundaries.test.ts](src/lib/monthBoundaries.test.ts) | Vitest, `npm run test:tz` and CI |
| The idempotency key: same on a retry (dropped connection, 5xx, refused-for-what-was-asked), different for the next payment or a changed amount or member | [recordContribution.test.ts](src/lib/recordContribution.test.ts), [recordContributionApi.test.ts](src/lib/recordContributionApi.test.ts) (the API sees it in the header) | Vitest, Vitest + MSW |
| What each API answer means (400 per field, 409, 422, 403, 429, 5xx, 401) | [recordContribution.test.ts](src/lib/recordContribution.test.ts) | Vitest |
| The form: a message under the right field, the reference at the top, the success line, what was typed is kept, one click = one request, the retry key | [ContributionForm.test.tsx](src/components/ContributionForm.test.tsx) | Vitest + Testing Library |
| A Treasurer is offered record-for-others, close, invite and proof actions; a Member is offered none of them and nothing hints they exist | [StokvelDetail.roles.test.tsx](src/components/StokvelDetail.roles.test.tsx) | Vitest + Testing Library, typed fixtures |
| The close key and the close wording | [closeRules.test.ts](src/lib/closeRules.test.ts) | Vitest |
| Join steps, proof file checks, `next=` safety, group overview rules | `joinFlow`, `proofFile`, `safeNext`, `groupOverview` tests in `src/lib/` | Vitest |
| A forged request from a Member is refused by the API, shown as a message, and nobody is signed out; the same for a `/bff/` route | [the-ui-is-not-the-rule.spec.ts](e2e/the-ui-is-not-the-rule.spec.ts) | Playwright |
| Signed out: `/login?next=` (query string kept), JSON 401 from `/bff/` | [signed-out.spec.ts](e2e/signed-out.spec.ts) | Playwright |
| Record R 150,50: the total rises by exactly 15050 cents with no reload | [journey.spec.ts](e2e/journey.spec.ts) | Playwright |
| Close dialog behaviour, proof upload and download, the join journey | checked by hand and with a scripted Chrome in 9.1 (see the 9.1 sections above) | manual |
| Hydration warnings, Lighthouse, accessibility, metadata | Part B | not yet |

**`data-testid`:** none is used. Every element the tests need has a role, a label or visible text. (The one hidden input the forged-request test changes is found by its `name`, on purpose: it is the thing an attacker would change, and no person can see it.)

## CI

[.github/workflows/ci.yml](../.github/workflows/ci.yml) runs on every pull request (and on pushes to `main`), with a read-only token (`permissions: contents: read`):

1. **Backend tests**, **Frontend checks** and, after the backend, **API image builds**. These are the fast jobs. *Frontend checks*: `npm ci`, lint, `next typegen` then `tsc --noEmit`, Vitest **twice** (`TZ=UTC` and `TZ=Africa/Johannesburg`) and `npm audit --omit=dev`.
2. **End-to-end (Playwright)** has `needs: [build-test, frontend]`, so it never starts while a fast check is red. It makes throwaway secrets with `openssl rand` (masked, in the job's environment only), starts Compose on a fresh database with `LOGIN_RATE_LIMIT=1000`, installs Chromium (`npx playwright install --with-deps chromium`), builds the app, runs Playwright, and **uploads the HTML report as an artifact only when the run fails**. No real secret is in the workflow or the repository.

**The deliberate red PR (to prove the gate works).** Make a one-line change that a component test notices, in a throwaway branch:

```bash
git switch -c deliberate-red
# web-next/src/components/ContributionForm.tsx, line 130: rename the button
sed -i '' "s/'Record contribution'/'Save contribution'/" web-next/src/components/ContributionForm.tsx
git commit -am "Deliberately break the Record contribution button (do not merge)"
git push -u origin deliberate-red      # then open a pull request
```

The *Frontend checks* job fails (the form tests look for a button called "Record contribution"), and **End-to-end stays skipped** because it needs the fast jobs. Copy that run's link into the table below. To go green again, revert the change and push:

```bash
git revert --no-edit HEAD && git push
```

| Run | Link |
|---|---|
| Red (the deliberate break) | **TODO (yours):** paste the run link |
| Green (after the revert) | **TODO (yours):** paste the run link |

## Known gaps (9.2)

- The end-to-end job has **not run on GitHub yet**: I could not run Actions from here. Everything it does was run locally (the commands in `ci.yml`, a fresh-secrets Compose start was *not* repeated because Docker could not pull images from this machine, so the API was started with `dotnet run` against the Compose database with the same settings). **TODO (yours):** push and watch the first run.
- The end-to-end suite is small on purpose (three behaviours that need the whole stack). Close, proof upload and the join journey are not in Playwright yet.
- Component tests run without the React Compiler (Vitest compiles JSX itself); the compiler is only on in the real build, which the end-to-end tests do exercise.
- The seeded stokvels are not safe to depend on: people change them (for example closing Family Stokvel's cycle), so every test that needs a stokvel creates its own.
- ~~The stokvel cards on "My stokvels" are one big link around an `article`, so the link has no accessible name.~~ Fixed in Part B (the link now has an `aria-label`: "Work Stokvel, Treasurer"; see the Accessibility table).
- The 40-member performance stokvel is added by the Development seed on every start if it is missing, so a long-lived local database gains it; it is made of invented people and touches no demo account.
