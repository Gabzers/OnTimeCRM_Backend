using OnTimeCRM.Application.DTOs.Users;

namespace OnTimeCRM.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(UserDetailRow user);
    DateTimeOffset GetExpiry();
}
