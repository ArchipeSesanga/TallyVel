import type { components } from './schema'

type Schemas = components['schemas']

// One entry in GET /api/v1/me/home: one stokvel, from the signed-in user's point of view
export type MeHomeEntry = Schemas['HomeStokvelCard']

// The open cycle inside a MeHomeEntry (or null when none is open)
export type HomeCycle = Schemas['HomeCurrentCycle']

// One item from GET /api/v1/stokvels/{id}/contributions
export type ContributionItem = Schemas['PagedResponseOfContributionResponse']['items'][number]

// The fixed lists of words the API allows
export type MemberRole = Schemas['MemberRole']
export type CycleStatus = Schemas['CycleStatus']
export type ContributionStatus = Schemas['ContributionStatus']

export type AccessTokenResponse = Schemas['AccessTokenResponse']

export type RecordContributionRequest = Schemas['RecordContributionRequest']
export type ValidationProblemDetails = Schemas['ValidationProblemDetails']

// GET /api/v1/stokvels/{id}: the stokvel with its member count and current cycle already worked out by the API
export type StokvelDetail = Schemas['StokvelDetailResponse']

// One page of GET /api/v1/stokvels/{id}/contributions. nextPageToken is "" on the last page.
export type ContributionPage = Schemas['PagedResponseOfContributionResponse']

// GET /api/v1/me/activity: the member's feed across their own stokvels, newest first
export type ActivityItem = Schemas['ActivityItem']
export type ActivityKind = Schemas['ActivityKind']
export type ActivityPage = Schemas['PagedResponseOfActivityItem']

// 9.1: the invite preview (what a non-member may see), proofs and the safe close
export type InvitePreview = Schemas['InvitePreviewResponse']
export type ContributionFrequency = Schemas['ContributionFrequency']
export type ClosePreview = Schemas['ClosePreviewResponse']
export type CloseResult = Schemas['CloseCycleResponse']
export type ArrearItem = Schemas['ArrearItem']
export type StokvelCurrentCycle = NonNullable<StokvelDetail['currentCycle']>
