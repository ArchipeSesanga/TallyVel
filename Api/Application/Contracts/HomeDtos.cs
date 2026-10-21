using Domain;

namespace Api.Application.Contracts;

/// <summary>
/// Exactly what the member home screen shows: one card per stokvel the caller
/// belongs to. Only the caller's own amounts; no member lists.
/// </summary>
public sealed record HomeResponse(IReadOnlyList<HomeStokvelCard> Stokvels);

/// <summary>
/// Money is integer cents (exact in JavaScript); dates are South African
/// calendar dates ("2026-10-31"), not UTC dates.
/// </summary>
public sealed record HomeStokvelCard(
    Guid StokvelId,
    string Name,
    MemberRole Role,
    HomeCurrentCycle? CurrentCycle,
    long PaidThisCycleCents,
    long OwedThisCycleCents,
    DateOnly? NextPayoutDate);

/// <summary>
/// The stokvel's open cycle. EndsOn is the due date. StartsOn and EndsOn are
/// null only for a cycle created before cycles had dates.
/// </summary>
public sealed record HomeCurrentCycle(
    Guid Id,
    string Label,
    CycleStatus Status,
    DateOnly? StartsOn,
    DateOnly? EndsOn);
