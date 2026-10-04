using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Application.Services;

public class StokvelMembershipService
{
    private readonly IUserRepository _userRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly ILogger<StokvelMembershipService> _logger;

    public StokvelMembershipService(
        IUserRepository userRepository,
        IStokvelRepository stokvelRepository,
        ILogger<StokvelMembershipService> logger)
    {
        _userRepository = userRepository;
        _stokvelRepository = stokvelRepository;
        _logger = logger;
    }

    public Stokvel AddMember(Guid stokvelId, Guid userId, MemberRole role)
    {
        var stokvel = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException("stokvel", stokvelId);

        var user = _userRepository.GetById(userId)
            ?? throw new NotFoundException("user", userId);

        try
        {
            stokvel.AddMember(user.Id, role);
        }
        catch (InvalidOperationException ex)
        {
            // Stokvel only throws here for "already a member" — a
            // genuine duplicate, so 409.
            throw new AlreadyExistsException("member-already-exists", ex.Message);
        }

        _logger.LogInformation("Added user {UserId} to stokvel {StokvelId} as {Role}", user.Id, stokvelId, role);

        return stokvel;
    }

    public Stokvel RemoveMember(Guid stokvelId, Guid userId)
    {
        var stokvel = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException("stokvel", stokvelId);

        try
        {
            stokvel.RemoveMember(userId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is not a member"))
        {
            throw new NotFoundException("member", userId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // "Cannot remove the last remaining admin" — well-formed
            // request, state says no. 422.
            throw new BusinessRuleViolationException("last-admin", ex.Message);
        }

        _logger.LogInformation("Removed user {UserId} from stokvel {StokvelId}", userId, stokvelId);

        return stokvel;
    }

    public Stokvel ChangeRole(Guid stokvelId, Guid userId, MemberRole newRole)
    {
        var stokvel = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException("stokvel", stokvelId);

        try
        {
            stokvel.ChangeRole(userId, newRole);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is not a member"))
        {
            throw new NotFoundException("member", userId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleViolationException("invalid-role-change", ex.Message);
        }

        _logger.LogInformation("Changed role of user {UserId} in stokvel {StokvelId} to {Role}", userId, stokvelId, newRole);

        return stokvel;
    }
}