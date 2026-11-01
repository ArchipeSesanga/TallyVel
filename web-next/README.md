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

## Known gaps

- Registration reveals whether an email has an account (409); hiding it needs email verification.
- No replay header on idempotent replays.
- Idempotency keys are stored in memory in the API, so an API restart forgets them (from Assignment 6.4).
- All sign-ins now come from the Next.js server's IP, so the per-IP login rate limit is shared by everyone (from 8.1).
