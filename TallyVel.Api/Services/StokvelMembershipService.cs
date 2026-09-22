using TallyVel.Api.Common;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Services;

public class StokvelMembershipService
{
    private readonly IUserRepository _userRepository;
    private readonly IStokvelRepository _stokvelRepository;

    public StokvelMembershipService(IUserRepository userRepository, IStokvelRepository stokvelRepository)
    {
        _userRepository = userRepository;
        _stokvelRepository = stokvelRepository;
    }

    public Stokvel AddMember(Guid stokvelId, Guid userId, MemberRole role)
    {
        var stokvel = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException($"No stokvel found with id {stokvelId}.");

        var user = _userRepository.GetById(userId)
            ?? throw new NotFoundException($"No user found with id {userId}.");

        try
        {
            stokvel.AddMember(user.Id, role);
        }
        catch (InvalidOperationException ex)
        {
            // Stokvel only throws here for "already a member" — a
            // genuine duplicate, so 409.
            throw new ConflictException(ex.Message);
        }

        return stokvel;
    }

    public Stokvel RemoveMember(Guid stokvelId, Guid userId)
    {
        var stokvel = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException($"No stokvel found with id {stokvelId}.");

        try
        {
            stokvel.RemoveMember(userId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is not a member"))
        {
            throw new NotFoundException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // "Cannot remove the last remaining admin" — well-formed
            // request, state says no. 422.
            throw new BusinessRuleViolationException(ex.Message);
        }

        return stokvel;
    }

    public Stokvel ChangeRole(Guid stokvelId, Guid userId, MemberRole newRole)
    {
        var stokvel = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException($"No stokvel found with id {stokvelId}.");

        try
        {
            stokvel.ChangeRole(userId, newRole);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is not a member"))
        {
            throw new NotFoundException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }

        return stokvel;
    }
}