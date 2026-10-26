import type { components } from './schema'

type Schemas = components['schemas']

// One entry in GET /api/v1/me/home: one stokvel, from the signed-in user's point of view
export type MeHomeEntry = Schemas['HomeStokvelCard']

// The open cycle inside a MeHomeEntry (or null when none is open)
export type HomeCycle = Schemas['HomeCurrentCycle']

// One item from GET /api/v1/stokvels/{id}/contributions
export type ContributionItem = Schemas['PagedResponseOfContributionResponse']['items'][number]

// The body of POST /api/v1/stokvels/{id}/contributions
export type RecordContributionRequest = Schemas['RecordContributionRequest']

// The 400 error body: field name -> messages, plus the correlationId
export type ValidationProblemDetails = Schemas['ValidationProblemDetails']

// The fixed lists of words the API allows
export type MemberRole = Schemas['MemberRole']
export type CycleStatus = Schemas['CycleStatus']
export type ContributionStatus = Schemas['ContributionStatus']

export type AccessTokenResponse = Schemas['AccessTokenResponse']