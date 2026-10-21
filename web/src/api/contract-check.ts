// Compile-time checks on the generated API types. Nothing here runs: if the API's
// shape drifts (a role is added, a money field turns into a string, a nullable
// field stops being nullable), `npm run typecheck` stops compiling.
import type { components } from "./schema";

type Schemas = components["schemas"];
type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends (<T>() => T extends B ? 1 : 2) ? true : false;
type Expect<T extends true> = T;

export type ContractChecks = [
  // Enums are unions of their names, not `string`.
  Expect<Equal<Schemas["MemberRole"], "Member" | "Treasurer" | "Chairperson">>,
  Expect<Equal<Schemas["ContributionStatus"], "Pending" | "Paid" | "Confirmed">>,
  Expect<Equal<Schemas["CycleStatus"], "Open" | "Closed" | "PaidOut">>,
  Expect<Equal<Schemas["ContributionResponse"]["status"], Schemas["ContributionStatus"]>>,
  Expect<Equal<Schemas["HomeStokvelCard"]["role"], Schemas["MemberRole"]>>,

  // Money is integer cents, which TypeScript sees as `number`.
  Expect<Equal<Schemas["ContributionResponse"]["amountCents"], number>>,
  Expect<Equal<Schemas["PayoutResponse"]["amountCents"], number>>,
  Expect<Equal<Schemas["RecordContributionRequest"]["amountCents"], number>>,
  Expect<Equal<Schemas["StokvelResponse"]["contributionAmountCents"], number>>,
  Expect<Equal<Schemas["CreateStokvelRequest"]["contributionAmountCents"], number>>,
  Expect<Equal<Schemas["HomeStokvelCard"]["paidThisCycleCents"], number>>,
  Expect<Equal<Schemas["HomeStokvelCard"]["owedThisCycleCents"], number>>,

  // Nullable fields include null, so the compiler makes the frontend handle it.
  Expect<Equal<Schemas["HomeStokvelCard"]["currentCycle"], Schemas["HomeCurrentCycle"] | null>>,
  Expect<Equal<Schemas["HomeStokvelCard"]["nextPayoutDate"], string | null>>,
  Expect<Equal<Schemas["HomeCurrentCycle"]["status"], Schemas["CycleStatus"]>>,
];