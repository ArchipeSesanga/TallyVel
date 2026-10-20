// Compile-time checks on the generated API types. Nothing here runs: if the API's
// shape drifts (a role is added, a money field turns into a string, a nullable
// field stops being nullable), `npm run check` stops compiling.
import type { components } from "./api";

type Schemas = components["schemas"];
type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends (<T>() => T extends B ? 1 : 2) ? true : false;
function assertType<_T extends true>() {}

// Enums are unions of their names, not `string`.
assertType<Equal<Schemas["MemberRole"], "Member" | "Treasurer" | "Chairperson">>();
assertType<Equal<Schemas["ContributionStatus"], "Pending" | "Paid" | "Confirmed">>();
assertType<Equal<Schemas["CycleStatus"], "Open" | "Closed" | "PaidOut">>();
assertType<Equal<Schemas["ContributionResponse"]["status"], Schemas["ContributionStatus"]>>();
assertType<Equal<Schemas["HomeStokvelCard"]["role"], Schemas["MemberRole"]>>();

// Money is integer cents, which TypeScript sees as `number`.
assertType<Equal<Schemas["ContributionResponse"]["amountCents"], number>>();
assertType<Equal<Schemas["PayoutResponse"]["amountCents"], number>>();
assertType<Equal<Schemas["RecordContributionRequest"]["amountCents"], number>>();
assertType<Equal<Schemas["StokvelResponse"]["contributionAmountCents"], number>>();
assertType<Equal<Schemas["CreateStokvelRequest"]["contributionAmountCents"], number>>();
assertType<Equal<Schemas["HomeStokvelCard"]["paidThisCycleCents"], number>>();
assertType<Equal<Schemas["HomeStokvelCard"]["owedThisCycleCents"], number>>();

// Nullable fields include null, so the compiler makes the frontend handle it.
assertType<Equal<Schemas["HomeStokvelCard"]["currentCycle"], Schemas["HomeCurrentCycle"] | null>>();
assertType<Equal<Schemas["HomeStokvelCard"]["nextPayoutDate"], string | null>>();
assertType<Equal<Schemas["HomeCurrentCycle"]["status"], Schemas["CycleStatus"]>>();
