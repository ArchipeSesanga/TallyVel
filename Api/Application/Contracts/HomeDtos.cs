using Domain;

namespace Api.Application.Contracts;

/// <summary>
/// Exactly what the member home screen shows: one card per stokvel the caller
/// belongs to. Only the caller's own amounts; no member lists.
/// </summary>
public sealed record HomeResponse(IReadOnlyList<HomeStokvelCard> Stokvels);

/// <summary>
/// Money is integer cents (exact in JavaScript); dates are UTC calendar dates.
/// </summary>
public sealed record HomeStokvelCard(
    Guid StokvelId,
    string Name,
    MemberRole Role,
    string? CurrentCycle,
    long PaidThisCycleCents,
    long OwedThisCycleCents,
    DateOnly? NextPayoutDate);
