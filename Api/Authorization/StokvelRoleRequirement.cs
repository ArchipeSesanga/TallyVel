using Domain;
using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>The caller must hold one of these roles in the stokvel being accessed.</summary>
public sealed class StokvelRoleRequirement(params MemberRole[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<MemberRole> AllowedRoles { get; } = allowedRoles;
}
