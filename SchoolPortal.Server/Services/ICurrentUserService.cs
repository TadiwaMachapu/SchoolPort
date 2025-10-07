namespace SchoolPortal.Server.Services;

public interface ICurrentUserService
{
    int SchoolId { get; }
    int UserId { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
}
