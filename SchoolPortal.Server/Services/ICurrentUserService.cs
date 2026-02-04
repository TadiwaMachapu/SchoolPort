namespace SchoolPortal.Server.Services;

public interface ICurrentUserService
{
    Guid SchoolId { get; }
    Guid UserId { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
}
